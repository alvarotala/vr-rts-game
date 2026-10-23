using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public enum TutorialStep
    {
        Welcome, SelectWorker, MoveWorker, GatherMinerals, SelectHeadquarters, TrainWorker,
        OpenMenu, ResumeMenu, OpenWheel, BuildSupply, BuildBarracks, BuildExtractor, GatherGas,
        TrainArmy, SelectArmy, SaveGroup, RecallGroup, AttackMove, DestroyEnemy, Complete
    }

    // This director observes the ordinary simulation. The runtime freezes time for
    // briefings and routes commands through Allows before submitting them normally.
    public sealed class GuidedTutorial
    {
        public const int ArmySize = 3;
        private readonly Simulation match;
        private readonly bool touch;
        private readonly string faction;
        private readonly int headquarters, enemyHeadquarters, mineral, gas;
        private readonly Point movePoint = new Point(18.5f, 12.5f);
        private readonly Point armyPoint = new Point(24.5f, 15.5f);
        private readonly Point supplyOrigin = new Point(18, 14), barracksOrigin = new Point(18, 8);
        private readonly Point extractorOrigin;
        private int worker, acceptedOrders, targetUnitCount, building;
        private bool sawCargo, groupNotified, savedGroup;
        private Point orderedPoint;
        private int[] orderedUnits = Array.Empty<int>();

        public TutorialStep Step { get; private set; } = TutorialStep.Welcome;
        public bool Briefing { get; private set; } = true;
        public bool Complete => Step == TutorialStep.Complete;
        public bool WheelExplored { get; private set; }
        public int AcceptedOrderCount => acceptedOrders;
        public int StepCount => Enum.GetValues(typeof(TutorialStep)).Length - (touch ? 0 : 1);
        public int StepNumber => (int)Step + 1 - (!touch && Step > TutorialStep.OpenWheel ? 1 : 0);
        public float Progress => (StepNumber - 1f) / (StepCount - 1);
        // Keep controls usable while the lesson waits for an action, without
        // draining finite deposits or letting idle soldiers act ahead of it.
        public bool AdvanceSimulation => !Briefing && !Complete && (acceptedOrders > 0 || NeedsIncome);
        private bool NeedsIncome
        {
            get
            {
                string definition = ExpectedDefinition;
                if (definition == null) return false;
                var type = match.Catalog[definition];
                return !match.CanAfford(0, type.minerals, type.catalyst);
            }
        }

        public GuidedTutorial(Simulation match, bool touch)
        {
            this.match = match ?? throw new ArgumentNullException(nameof(match));
            if (match.Map.Definition.id != "training") throw new ArgumentException("The guided tutorial uses the training map.", nameof(match));
            this.touch = touch;
            faction = match.Players[0].faction;
            headquarters = OwnRole("hq").First().id;
            enemyHeadquarters = match.Entities.Values.First(e => e.owner == 1 && match.Type(e).role == "hq").id;
            worker = OwnRole("worker").First().id;
            mineral = match.Resources.First(r => r.definition == "camp0.mineral0").id;
            gas = match.Resources.First(r => r.definition == "camp0.catalyst").id;
            var deposit = Resource(gas).position;
            float half = match.Catalog.Role(faction, "extractor").footprint * .5f;
            extractorOrigin = new Point((float)Math.Floor(deposit.x - half), (float)Math.Floor(deposit.y - half));
        }

        public string ExpectedDefinition
        {
            get
            {
                string role = Step == TutorialStep.TrainWorker ? "worker"
                    : Step == TutorialStep.BuildSupply ? "supply"
                    : Step == TutorialStep.BuildBarracks ? "barracks"
                    : Step == TutorialStep.BuildExtractor ? "extractor"
                    : Step == TutorialStep.TrainArmy ? "ranged" : null;
                return role == null ? null : match.Catalog.Role(faction, role).id;
            }
        }

        public int FocusEntity
        {
            get
            {
                switch (Step)
                {
                    case TutorialStep.SelectWorker: case TutorialStep.MoveWorker: case TutorialStep.OpenWheel:
                    case TutorialStep.GatherMinerals: case TutorialStep.GatherGas: return worker;
                    case TutorialStep.SelectHeadquarters: case TutorialStep.TrainWorker: return headquarters;
                    case TutorialStep.BuildSupply: case TutorialStep.BuildBarracks: case TutorialStep.BuildExtractor:
                        return building > 0 ? building : worker;
                    case TutorialStep.TrainArmy: return OwnRole("barracks").FirstOrDefault()?.id ?? 0;
                    case TutorialStep.SelectArmy: case TutorialStep.SaveGroup: case TutorialStep.RecallGroup:
                        return Army().FirstOrDefault()?.id ?? 0;
                    case TutorialStep.DestroyEnemy: return enemyHeadquarters;
                    default: return 0;
                }
            }
        }

        public int FocusResource => Step == TutorialStep.GatherMinerals ? mineral
            : Step == TutorialStep.GatherGas || Step == TutorialStep.BuildExtractor ? gas : 0;

        public Point? FocusPoint
        {
            get
            {
                switch (Step)
                {
                    case TutorialStep.MoveWorker: return movePoint;
                    case TutorialStep.GatherMinerals: return Resource(mineral).position;
                    case TutorialStep.GatherGas: case TutorialStep.BuildExtractor: return Resource(gas).position;
                    // BuildPlacement floors the pointer to the foundation origin.
                    case TutorialStep.BuildSupply: return new Point(supplyOrigin.x + .5f, supplyOrigin.y + .5f);
                    case TutorialStep.BuildBarracks: return new Point(barracksOrigin.x + .5f, barracksOrigin.y + .5f);
                    case TutorialStep.AttackMove: return armyPoint;
                    default:
                        return match.Entities.TryGetValue(FocusEntity, out var entity) ? match.Center(entity) : (Point?)null;
                }
            }
        }

        public void Continue()
        {
            if (!Briefing || Complete) return;
            if (Step == TutorialStep.Welcome) Advance();
            else Briefing = false;
        }

        public void Observe(IEnumerable<int> selection)
        {
            if (Complete) return;
            if (match.Winner == 0 && Step == TutorialStep.DestroyEnemy) { Enter(TutorialStep.Complete); return; }
            if (Briefing) return;
            var selected = (selection ?? Enumerable.Empty<int>()).Distinct().ToArray();
            switch (Step)
            {
                case TutorialStep.SelectWorker:
                    if (OneRole(selected, "worker")) { worker = selected[0]; Advance(); }
                    break;
                case TutorialStep.MoveWorker:
                    if (acceptedOrders > 0 && match.Entities.TryGetValue(worker, out var moved)
                        && moved.position.Distance(orderedPoint) < 1.25f && moved.order == CommandKind.Stop) Advance();
                    break;
                case TutorialStep.GatherMinerals: case TutorialStep.GatherGas:
                    if (acceptedOrders == 0 || !match.Entities.TryGetValue(worker, out var gatherer)) break;
                    string kind = Step == TutorialStep.GatherGas ? "catalyst" : "mineral";
                    if (gatherer.cargo > 0 && gatherer.cargoKind == kind) sawCargo = true;
                    // Cargo can only become empty by depositing: every unrelated command
                    // is gated while this worker completes the demonstrated trip.
                    if (sawCargo && gatherer.cargo == 0) Advance();
                    break;
                case TutorialStep.SelectHeadquarters:
                    if (selected.Length == 1 && selected[0] == headquarters) Advance();
                    break;
                case TutorialStep.TrainWorker:
                    if (acceptedOrders == 1 && OwnRole("worker").Count() >= targetUnitCount) Advance();
                    break;
                case TutorialStep.BuildSupply: case TutorialStep.BuildBarracks: case TutorialStep.BuildExtractor:
                    if (building > 0 && match.Entities.TryGetValue(building, out var foundation) && foundation.complete) Advance();
                    break;
                case TutorialStep.TrainArmy:
                    if (acceptedOrders == ArmySize && Army().Count() >= targetUnitCount) Advance();
                    break;
                case TutorialStep.SelectArmy:
                    if (ArmySelected(selected)) Advance();
                    break;
                case TutorialStep.SaveGroup: case TutorialStep.RecallGroup:
                    if (groupNotified)
                    {
                        // A failed save/recall must be tried again. Selecting the
                        // right units later cannot retroactively repair that event.
                        groupNotified = false;
                        if (ArmySelected(selected)) { savedGroup = true; Advance(); }
                    }
                    break;
                case TutorialStep.AttackMove:
                    if (acceptedOrders > 0 && orderedUnits.All(id => match.Entities.TryGetValue(id, out var soldier)
                        && soldier.position.Distance(orderedPoint) < 3f && soldier.order == CommandKind.Stop)) Advance();
                    break;
            }
        }

        public bool Allows(GameCommand command)
        {
            if (Briefing || Complete || command == null || command.player != 0 || command.append
                || command.replacePendingSequence != 0 || command.unitOrderSequence != 0 || command.queueOrderId != 0
                || command.entities == null || command.entities.Length == 0
                || command.entities.Distinct().Count() != command.entities.Length
                || command.entities.Any(id => !match.Entities.TryGetValue(id, out var e) || e.owner != 0 || !e.complete || e.health <= 0)) return false;
            switch (Step)
            {
                case TutorialStep.MoveWorker:
                    return acceptedOrders == 0 && command.kind == CommandKind.Move && OneRole(command.entities, "worker") && Near(command.position, movePoint, 1.25f);
                case TutorialStep.GatherMinerals: case TutorialStep.GatherGas:
                    return acceptedOrders == 0 && command.kind == CommandKind.Gather && OneRole(command.entities, "worker")
                        && command.target == (Step == TutorialStep.GatherGas ? gas : mineral);
                case TutorialStep.TrainWorker:
                    return acceptedOrders == 0 && command.kind == CommandKind.Train && command.definition == ExpectedDefinition
                        && command.entities.Length == 1 && command.entities[0] == headquarters;
                case TutorialStep.BuildSupply: case TutorialStep.BuildBarracks: case TutorialStep.BuildExtractor:
                    Point origin = Step == TutorialStep.BuildSupply ? supplyOrigin : Step == TutorialStep.BuildBarracks ? barracksOrigin : extractorOrigin;
                    return acceptedOrders == 0 && command.kind == CommandKind.Build && command.definition == ExpectedDefinition
                        && OneRole(command.entities, "worker") && Math.Floor(command.position.x) == origin.x && Math.Floor(command.position.y) == origin.y;
                case TutorialStep.TrainArmy:
                    return acceptedOrders < ArmySize && command.kind == CommandKind.Train && command.definition == ExpectedDefinition && OneRole(command.entities, "barracks");
                case TutorialStep.AttackMove:
                    return acceptedOrders == 0 && command.kind == CommandKind.AttackMove && ArmySelected(command.entities) && Near(command.position, armyPoint, 1.25f);
                case TutorialStep.DestroyEnemy:
                    if (!ArmySelected(command.entities)) return false;
                    if (command.kind == CommandKind.Attack)
                        return match.Entities.TryGetValue(command.target, out var target) && target.owner == 1;
                    return command.kind == CommandKind.AttackMove && match.Entities.TryGetValue(enemyHeadquarters, out var enemy)
                        && Near(command.position, match.Center(enemy), 4f);
                default: return false;
            }
        }

        public void OnAccepted(GameCommand command)
        {
            // Call only after Simulation.Submit accepted. Failed placement/production
            // remains retryable and never advances the teaching sequence.
            if (!Allows(command)) return;
            if (acceptedOrders == 0)
            {
                if (Step == TutorialStep.TrainWorker) targetUnitCount = OwnRole("worker").Count() + 1;
                if (Step == TutorialStep.TrainArmy) targetUnitCount = Army().Count() + ArmySize;
            }
            acceptedOrders++;
            orderedPoint = command.position;
            orderedUnits = (int[])command.entities.Clone();
            if (OneRole(command.entities, "worker")) worker = command.entities[0];
            if (command.kind == CommandKind.Gather)
            {
                var gatherer = match.Entities[worker];
                sawCargo = gatherer.cargo > 0 && gatherer.cargoKind == (Step == TutorialStep.GatherGas ? "catalyst" : "mineral");
            }
            if (command.kind == CommandKind.Build)
                building = OwnRole(match.Catalog[command.definition].role).Single(e => !e.complete && e.position.Distance(command.position) < 1.5f).id;
        }

        public void NotifyMenu(bool open)
        {
            if (Briefing) return;
            if (Step == TutorialStep.OpenMenu && open || Step == TutorialStep.ResumeMenu && !open) Advance();
        }

        public void NotifyWheel(bool open = true)
        {
            if (Briefing || Step != TutorialStep.OpenWheel) return;
            if (open) WheelExplored = true;
            else if (WheelExplored) Advance();
        }

        public void NotifyGroup(int index, bool save)
        {
            if (Briefing || index != 0) return;
            if (Step == TutorialStep.SaveGroup && save || Step == TutorialStep.RecallGroup && !save && savedGroup) groupNotified = true;
        }

        private IEnumerable<EntityState> OwnRole(string role) => match.Entities.Values.Where(e => e.owner == 0 && e.health > 0 && match.Type(e).role == role).OrderBy(e => e.id);
        private IEnumerable<EntityState> Army() => OwnRole("ranged").Where(e => e.complete);
        private ResourceState Resource(int id) => match.Resources.First(r => r.id == id);
        private bool OneRole(int[] ids, string role) => ids.Length == 1 && match.Entities.TryGetValue(ids[0], out var e)
            && e.owner == 0 && e.complete && e.health > 0 && match.Type(e).role == role;
        private bool ArmySelected(int[] ids) => ids.Length == ArmySize && ids.Distinct().Count() == ArmySize
            && ids.All(id => match.Entities.TryGetValue(id, out var e) && e.owner == 0 && e.complete && e.health > 0 && match.Type(e).role == "ranged");
        private static bool Near(Point value, Point target, float distance) => value.Distance(target) <= distance;
        private void Advance()
        {
            var next = (TutorialStep)((int)Step + 1);
            if (!touch && next == TutorialStep.OpenWheel) next = TutorialStep.BuildSupply;
            Enter(next);
        }
        private void Enter(TutorialStep step)
        {
            Step = step; Briefing = true; acceptedOrders = 0; building = 0; sawCargo = false; groupNotified = false; WheelExplored = false;
            orderedUnits = Array.Empty<int>();
        }
    }
}
