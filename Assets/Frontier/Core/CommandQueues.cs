using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Simulation
    {
        public const int UnitOrderLimit = 32;

        private static bool IsUnitAction(CommandKind kind) => kind == CommandKind.Move || kind == CommandKind.AttackMove
            || kind == CommandKind.Attack || kind == CommandKind.Gather || kind == CommandKind.Repair || kind == CommandKind.Build;

        private static UnitOrder CopyOrder(UnitOrder order) => new UnitOrder { sourceSequence = order.sourceSequence,
            kind = order.kind, target = order.target, position = order.position, definition = order.definition,
            trafficPriority = order.trafficPriority, trafficMass = order.trafficMass };

        private void MarkUnitOrder(EntityState unit, int sequence, bool preserveQueue)
        {
            unit.castingAbility = null; unit.castResume = null;
            if (!preserveQueue) unit.pendingOrders.Clear();
            unit.activeOrderSequence = sequence;
            unit.combatTarget = 0; unit.approachTarget = 0; unit.unreachableTargets.Clear();
        }

        private Point PlanningOrigin(EntityState unit, int replacing)
        {
            if (replacing > 0 && unit.activeOrderSequence == replacing) return unit.position;
            Point origin = unit.order == CommandKind.Move || unit.order == CommandKind.AttackMove ? unit.destination : unit.position;
            foreach (var pending in unit.pendingOrders)
            {
                if (replacing > 0 && pending.sourceSequence == replacing) break;
                if (pending.kind == CommandKind.Move || pending.kind == CommandKind.AttackMove) origin = pending.position;
            }
            return origin;
        }

        private List<(EntityState unit, UnitOrder order, List<int> path)> PlanMovement(GameCommand command, List<EntityState> selected, bool queued)
        {
            if (!Finite(command.position) || Map.Cell(command.position) < 0) return null;
            var plan = new List<(EntityState unit, UnitOrder order, List<int> path)>();
            long priority = ((long)selected.Min(e => e.id) << 32) | ++nextMovementOrder;
            float mass = selected.Max(e => Type(e).collisionMass);
            int formationRadius = Math.Max(12, (int)Math.Ceiling(Math.Sqrt(selected.Count)) * 2);
            var reserved = new HashSet<int>();
            var selectedIds = new HashSet<int>(selected.Select(e => e.id));
            foreach (var other in Entities.Values)
                if (!Type(other).building && !selectedIds.Contains(other.id) && (other.owner == command.player || Visible(command.player, other.position)))
                {
                    reserved.Add(Map.Cell(other.position));
                    if (other.owner != command.player) continue;
                    if (other.order == CommandKind.Move || other.order == CommandKind.AttackMove) reserved.Add(Map.Cell(other.destination));
                    if (queued) foreach (var pending in other.pendingOrders)
                        if (pending.kind == CommandKind.Move || pending.kind == CommandKind.AttackMove) reserved.Add(Map.Cell(pending.position));
                }
            foreach (var unit in selected.OrderBy(e => (queued ? PlanningOrigin(e, command.replacePendingSequence) : e.position).Distance(command.position)).ThenBy(e => e.id))
            {
                Point origin = queued ? PlanningOrigin(unit, command.replacePendingSequence) : unit.position;
                foreach (var destination in DestinationSlots(command.position, formationRadius, reserved, Type(unit).collisionRadius))
                {
                    var path = paths.Find(origin, destination, .1f, null, Type(unit).collisionRadius);
                    if (path == null) continue;
                    plan.Add((unit, new UnitOrder { sourceSequence = command.sequence, kind = command.kind,
                        position = destination, trafficPriority = priority, trafficMass = mass }, path));
                    // The same formation spacing applies to immediate and queued group commands.
                    int gap = selected.Count > 1 ? 1 : 0;
                    for (int y = (int)destination.y - gap; y <= (int)destination.y + gap; y++)
                        for (int x = (int)destination.x - gap; x <= (int)destination.x + gap; x++)
                            if (Map.Inside(x, y)) reserved.Add(Map.Index(x, y));
                    break;
                }
            }
            return plan;
        }

        private CommandResult ActivateMovement(EntityState unit, UnitOrder order, bool preserveQueue, List<int> plannedPath = null)
        {
            if (!Map.CanStand(order.position, Type(unit).collisionRadius)) return Reject("Destination no longer available.");
            var path = plannedPath ?? paths.Find(unit.position, order.position, .1f, null, Type(unit).collisionRadius);
            if (path == null) return Reject("No path to that destination.");
            MarkUnitOrder(unit, order.sourceSequence, preserveQueue);
            ResetTravel(unit);
            unit.trafficPriority = order.trafficPriority;
            unit.trafficMass = order.trafficMass;
            unit.order = order.kind; unit.destination = order.position; unit.target = 0;
            unit.moveGoal = order.kind == CommandKind.Move;
            SetPath(unit, path, order.position, .1f);
            return Accept();
        }

        private CommandResult ValidateQueuedAction(GameCommand command, List<EntityState> selected)
        {
            if (selected.Any(e => !e.complete || Type(e).building || e.health <= 0)) return Reject("Select completed units.");
            if (command.kind == CommandKind.Build) return ValidateBuildingOrder(command, selected[0], true);
            if (command.kind == CommandKind.Attack || command.kind == CommandKind.Repair)
            {
                Entities.TryGetValue(command.target, out var target);
                if (command.kind == CommandKind.Attack && (target == null || target.health <= 0 || target.owner == command.player || !Visible(command.player, Center(target))))
                    return Reject("No visible enemy at that location.");
                if (target == null || target.health <= 0) return Reject("Target is no longer available.");
                if (command.kind == CommandKind.Repair && (target.owner != command.player || Catalog.Faction(Players[command.player].faction).regenerates || selected.Any(e => Type(e).role != "worker")))
                    return Reject("These workers cannot repair that target.");
            }
            if (command.kind == CommandKind.Gather)
            {
                var resource = Resources.FirstOrDefault(r => r.id == command.target);
                if (resource == null || !Explored(command.player, resource.position) || KnownRemaining(command.player, resource) == 0 || selected.Any(e => Type(e).role != "worker"))
                    return Reject("Select workers and an explored resource deposit.");
                if (resource.kind == "catalyst" && !Extractor(command.player, resource)) return Reject("Build an extractor over the catalyst deposit.");
            }
            return Accept();
        }

        private CommandResult QueueCommand(GameCommand command, List<EntityState> selected)
        {
            var validation = ValidateQueuedAction(command, selected);
            if (!validation.accepted) return validation;
            // Construction uses one builder, like the immediate Build command.
            if (command.kind == CommandKind.Build) selected = new List<EntityState> { selected[0] };
            if (command.append && selected.Any(e => e.pendingOrders.Count >= UnitOrderLimit
                && !(command.replacePendingSequence > 0 && (e.activeOrderSequence == command.replacePendingSequence || e.pendingOrders.Any(q => q.sourceSequence == command.replacePendingSequence)))))
                return Reject("Unit action queue full (32).");
            List<(EntityState unit, UnitOrder order, List<int> path)> plan;
            if (command.kind == CommandKind.Move || command.kind == CommandKind.AttackMove)
            {
                plan = PlanMovement(command, selected, true);
                if (plan == null) return Reject("Destination outside the map.");
                if (plan.Count == 0) return Reject("No path to that destination.");
            }
            else plan = selected.Select(e => (e, new UnitOrder { sourceSequence = command.sequence, kind = command.kind,
                target = command.target, position = command.position, definition = command.definition }, (List<int>)null)).ToList();
            int accepted = 0;
            foreach (var item in plan)
            {
                var unit = item.unit;
                int replace = command.replacePendingSequence > 0
                    ? unit.pendingOrders.FindIndex(q => q.sourceSequence == command.replacePendingSequence) : -1;
                if (replace >= 0) { unit.pendingOrders[replace] = item.order; accepted++; continue; }
                if (command.replacePendingSequence > 0 && unit.activeOrderSequence == command.replacePendingSequence)
                {
                    if (ActivateUnitOrder(unit, item.order).accepted) accepted++;
                    continue;
                }
                if (!command.append)
                {
                    var replacement = new GameCommand { player = unit.owner, sequence = command.sequence, kind = item.order.kind,
                        entities = new[] { unit.id }, target = item.order.target, position = item.order.position, definition = item.order.definition };
                    var result = item.order.kind == CommandKind.Move || item.order.kind == CommandKind.AttackMove
                        ? ActivateMovement(unit, item.order, false) : ExecuteCommand(replacement, new List<EntityState> { unit }, false);
                    if (result.accepted) accepted++;
                    continue;
                }
                if ((unit.order == CommandKind.Stop || unit.order == CommandKind.Hold) && unit.pendingOrders.Count == 0)
                {
                    if (ActivateUnitOrder(unit, item.order).accepted) accepted++;
                }
                else { unit.pendingOrders.Add(item.order); accepted++; }
            }
            return accepted > 0 ? Accept() : Reject("The action is no longer available.");
        }

        private CommandResult ActivateUnitOrder(EntityState unit, UnitOrder order)
        {
            if (order.kind == CommandKind.Move || order.kind == CommandKind.AttackMove) return ActivateMovement(unit, order, true);
            var command = new GameCommand { player = unit.owner, sequence = order.sourceSequence, kind = order.kind,
                entities = new[] { unit.id }, target = order.target, position = order.position, definition = order.definition };
            var selected = new List<EntityState> { unit };
            var validation = ValidateQueuedAction(command, selected);
            return validation.accepted ? ExecuteCommand(command, selected, true) : validation;
        }

        private bool AdvanceUnitOrders(EntityState unit)
        {
            unit.activeOrderSequence = 0;
            while (unit.pendingOrders.Count > 0)
            {
                var next = unit.pendingOrders[0]; unit.pendingOrders.RemoveAt(0);
                var result = ActivateUnitOrder(unit, next);
                if (result.accepted) return true;
                // This message describes only the issuing player's previously known action.
                Notice?.Invoke(unit.owner, "Queued " + next.kind + " skipped: " + result.message);
            }
            return false;
        }
    }
}
