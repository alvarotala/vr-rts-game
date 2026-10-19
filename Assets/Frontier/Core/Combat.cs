using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Simulation
    {
        private bool ValidEnemy(EntityState unit, EntityState enemy) => enemy.owner != unit.owner
            && enemy.health > 0 && Visible(unit.owner, Center(enemy));

        private float WeaponRange(EntityState unit, EntityState enemy) => Type(unit).range
            + (Type(enemy).building ? Type(enemy).footprint * .4f : 0);

        private bool CanFire(EntityState unit, EntityState enemy) => Center(unit).Distance(Center(enemy)) <= WeaponRange(unit,enemy)
            && Map.HasLineOfSight(Center(unit),Center(enemy));

        private int ThreatPriority(EntityState enemy)
        {
            var type = Type(enemy);
            // Workers carrying out economic orders are not equivalent to soldiers.
            // Armed workers become threats once they fire, without inspecting orders
            // that the opposing player cannot observe.
            if (enemy.complete && type.damage > 0 && (type.role != "worker"
                || Tick - enemy.lastShotTick <= TickRate * 2)) return 0;
            return type.building ? 2 : 1;
        }

        private void Attack(EntityState unit)
        {
            if (!Entities.TryGetValue(unit.target,out var enemy) || enemy.health <= 0 || enemy.owner == unit.owner)
            { FinishAttack(unit); return; }
            if (!Visible(unit.owner,Center(enemy)))
            {
                // Keep only the last observed point, never the hidden entity's new position.
                if (Tick - unit.targetSeenTick >= TickRate * 3) { FinishAttack(unit); return; }
                if (!Fight(unit,true) && Travel(unit,unit.destination,.5f,false)) FinishAttack(unit);
                return;
            }
            unit.destination = Center(enemy); unit.targetSeenTick = Tick;
            if (!Strike(unit,enemy,true)) Fight(unit,true);
        }

        private void FinishAttack(EntityState unit)
        {
            unit.order = CommandKind.Stop; unit.target = 0; unit.combatTarget = 0; unit.approachTarget = 0; unit.path = null;
            // A queued instruction has precedence over automatic acquisition.
            if (unit.pendingOrders.Count > 0) return;
            // The front of a selection can kill the focus before the rear even sees
            // the fight. Keep the last observed battlefield as an attack-move goal,
            // otherwise those distant soldiers enter Stop with nothing to acquire.
            if (unit.position.Distance(unit.destination) > Type(unit).range)
            {
                var command = new GameCommand { player = unit.owner, kind = CommandKind.AttackMove,
                    position = unit.destination, sequence = unit.activeOrderSequence };
                var plan = PlanMovement(command,new List<EntityState> { unit },false);
                if (plan != null && plan.Count > 0)
                {
                    var item = plan[0];
                    ActivateMovement(unit,item.order,true,item.path);
                }
            }
            Fight(unit,true);
        }

        private bool Fight(EntityState unit, bool pursue)
        {
            var type = Type(unit); if (type.damage <= 0) return false;
            var candidates = Entities.Values.Where(enemy => ValidEnemy(unit,enemy)
                    && Center(unit).Distance(Center(enemy)) <= (pursue ? type.vision : type.range)
                        + (Type(enemy).building ? Type(enemy).footprint * .4f : 0)
                    && (pursue || CanFire(unit,enemy)))
                .OrderBy(ThreatPriority)
                // A shot now beats chasing another enemy of the same threat class.
                .ThenBy(enemy => CanFire(unit,enemy) ? 0 : 1)
                .ThenBy(enemy => enemy.id == unit.combatTarget ? 0 : 1)
                .ThenBy(enemy => Center(unit).Distance(Center(enemy)))
                .ThenBy(enemy => enemy.id);
            foreach (var enemy in candidates)
            {
                if (!Strike(unit,enemy,pursue)) continue;
                unit.combatTarget = enemy.id;
                // Combat must not spring a previously parked unit back into formation.
                unit.moveGoal = false; unit.settled = false;
                return true;
            }
            unit.combatTarget = 0;
            return false;
        }

        private void EndCombatYield(EntityState unit)
        {
            if (unit.yieldUntil <= Tick) return;
            unit.yieldUntil = 0; unit.yieldFor = 0; unit.path = null; unit.approachTarget = 0;
        }

        private bool ApproachEnemy(EntityState unit, EntityState enemy)
        {
            if (unit.approachVersion != Map.Version) unit.unreachableTargets.Clear();
            unit.approachVersion = Map.Version;
            if (unit.unreachableTargets.TryGetValue(enemy.id,out int retry) && Tick < retry) return false;
            Point position = Center(enemy);
            bool changed = unit.approachTarget != enemy.id || unit.approachPosition.Distance(position) > .5f;
            if (changed || unit.path == null || unit.pathVersion != Map.Version || unit.blockedTicks >= TickRate * 2)
            {
                // Range alone is insufficient: terrain can hide the target from this
                // shooter even when an ally reveals it. Find an actual firing position.
                var route = paths.Find(unit.position,position,WeaponRange(unit,enemy)*.85f,
                    c => TrafficCost(unit,c),Type(unit).collisionRadius,
                    // Do not repeatedly accept the current arrival cell if rounding
                    // near a terrain edge still blocks the shot from the actual position.
                    point => point.Distance(unit.position) > .15f && Map.CanStand(point,Type(unit).collisionRadius)
                        && Map.HasLineOfSight(point,position));
                if (route == null || (!changed && unit.blockedTicks >= TickRate * 2))
                {
                    unit.unreachableTargets[enemy.id] = Tick + TickRate;
                    unit.approachTarget = 0; unit.path = null; unit.blockedTicks = 0;
                    return false;
                }
                unit.approachTarget = enemy.id; unit.approachPosition = position;
                unit.approachGoal = Map.Center(route.Count > 0 ? route[route.Count-1] : Map.Cell(unit.position));
                SetPath(unit,route,unit.approachGoal,.1f);
            }
            Travel(unit,unit.approachGoal,.1f,false);
            return true;
        }

        private bool Strike(EntityState unit, EntityState target, bool pursue)
        {
            var type = Type(unit); if (type.damage <= 0) return false;
            if (!CanFire(unit,target)) return pursue && !type.building && ApproachEnemy(unit,target);
            EndCombatYield(unit);
            // Preserve momentum and cooldown while acquiring or changing targets.
            if (!type.building && unit.order != CommandKind.Hold && unit.velocity.Distance(default) > .001f)
            {
                Brake(unit);
                if (!CanFire(unit,target)) return true;
            }
            if (Tick < unit.attackReadyTick) return true;
            float damage = Math.Max(1,type.damage*(1+Players[unit.owner].attackLevel*.2f)-Players[target.owner].armorLevel*2);
            target.health -= damage; target.lastDamageTick = Tick;
            unit.lastShotTick = Tick; unit.attackReadyTick = Tick + type.attackPeriod;
            RecordAttack(unit,target);
            if (type.role == "siege") foreach (var other in Entities.Values)
                if (other.id != target.id && other.owner != unit.owner && other.health > 0 && Center(other).Distance(Center(target)) < 2)
                { other.health -= damage * .35f; other.lastDamageTick = Tick; }
            return true;
        }
    }
}
