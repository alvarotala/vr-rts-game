using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Simulation
    {
        private readonly List<(AbilityEvent effect, Point origin, int observers, int[] ids)> recentAbilities = new List<(AbilityEvent, Point, int, int[])>();
        // Event IDs must not disclose intervening spells outside the observer's vision.
        private readonly int[] nextAbilityIds = { 1, 1 };
        private static AbilityEvent ObservedAbility(AbilityEvent effect, int id) { effect.id=id; return effect; }

        private bool HasAbility(EntityState caster, AbilityDefinition ability) => caster.health > 0 && caster.complete
            && !Type(caster).building && ability != null && ability.faction == Type(caster).faction
            && Array.IndexOf(Type(caster).abilities, ability.id) >= 0;

        private bool AbilityReady(EntityState caster, AbilityDefinition ability) => caster.energy >= ability.energyCost
            && !caster.abilityCooldowns.Any(c => c.ability == ability.id && Tick < c.readyTick);

        private bool InCastRange(EntityState caster, AbilityDefinition ability, Point point) => Center(caster).Distance(point) <= ability.range
            && Map.HasLineOfSight(Center(caster), point);

        private List<int> CastRoute(EntityState caster, AbilityDefinition ability, Point point) => paths.Find(caster.position, point,
            ability.range * .85f, c => TrafficCost(caster,c), Type(caster).collisionRadius,
            p => p.Distance(caster.position) > .15f && Map.CanStand(p,Type(caster).collisionRadius) && Map.HasLineOfSight(p,point));

        private CommandResult BeginCast(GameCommand command, List<EntityState> selected)
        {
            if (command.append || command.replacePendingSequence != 0) return Reject("Abilities cannot be queued.");
            var ability = Catalog.Ability(command.definition);
            if (ability == null || ability.faction != Players[command.player].faction) return Reject("Unknown ability.");
            if (!Finite(command.position) || Map.Cell(command.position) < 0 || !Visible(command.player,command.position))
                return Reject("Choose a visible point on the battlefield.");
            var candidates = selected.Where(e => HasAbility(e,ability) && AbilityReady(e,ability))
                .OrderBy(e => e.order == CommandKind.Cast ? 1 : 0)
                .ThenBy(e => InCastRange(e,ability,command.position) ? 0 : 1)
                .ThenBy(e => Center(e).Distance(command.position)).ThenBy(e => e.id);
            foreach (var caster in candidates)
            {
                bool inRange = InCastRange(caster,ability,command.position);
                var path = inRange ? null : CastRoute(caster,ability,command.position);
                if (!inRange && path == null) continue;
                var resume = caster.castResume ?? new UnitOrder { kind = caster.order, target = caster.target,
                    position = caster.destination, sourceSequence = caster.activeOrderSequence,
                    trafficPriority = caster.trafficPriority, trafficMass = caster.trafficMass };
                int seenTick = caster.castResume != null ? caster.castResumeSeenTick : caster.targetSeenTick;
                MarkUnitOrder(caster,command.sequence,true); ResetTravel(caster);
                caster.castResume = resume; caster.castResumeSeenTick = seenTick;
                caster.castingAbility = ability.id; caster.castPoint = command.position;
                caster.destination = command.position; caster.target = 0; caster.order = CommandKind.Cast;
                // Bound congestion waits without preventing long but reachable approaches.
                caster.castDeadline = Tick + TickRate * 8 + (int)Math.Ceiling((path?.Count ?? 0) / Math.Max(.1f,Type(caster).speed) * TickRate);
                if (path != null)
                {
                    caster.castGoal = Map.Center(path.Count > 0 ? path[path.Count-1] : Map.Cell(caster.position));
                    SetPath(caster,path,caster.castGoal,.1f);
                }
                return Accept();
            }
            return Reject("No ready caster can reach that point.");
        }

        private void AdvanceCast(EntityState caster)
        {
            var ability = Catalog.Ability(caster.castingAbility);
            if (!HasAbility(caster,ability) || !AbilityReady(caster,ability) || !Visible(caster.owner,caster.castPoint)
                || Tick > caster.castDeadline)
            { FinishCast(caster); return; }
            if (!InCastRange(caster,ability,caster.castPoint))
            {
                if (caster.path == null || caster.pathVersion != Map.Version || caster.blockedTicks >= TickRate * 2)
                {
                    var route = CastRoute(caster,ability,caster.castPoint);
                    if (route == null || caster.blockedTicks >= TickRate * 2) { FinishCast(caster); return; }
                    caster.castGoal = Map.Center(route.Count > 0 ? route[route.Count-1] : Map.Cell(caster.position));
                    SetPath(caster,route,caster.castGoal,.1f);
                }
                Travel(caster,caster.castGoal,.1f,false);
                return;
            }
            EndCombatYield(caster);
            if (caster.velocity.Distance(default) > .001f)
            {
                Brake(caster);
                if (!InCastRange(caster,ability,caster.castPoint)) return;
            }
            caster.energy -= ability.energyCost;
            var cooldown = caster.abilityCooldowns.FirstOrDefault(c => c.ability == ability.id);
            if (cooldown == null) { cooldown = new AbilityCooldown { ability = ability.id }; caster.abilityCooldowns.Add(cooldown); }
            cooldown.readyTick = Tick + ability.cooldown;
            foreach (var target in Entities.Values)
            {
                if (target.health <= 0 || Center(target).Distance(caster.castPoint) > ability.radius) continue;
                if (ability.effect == "damage" && target.owner != caster.owner && Visible(caster.owner,Center(target)))
                { target.health -= ability.amount; target.lastDamageTick = Tick; }
                else if (ability.effect == "heal" && target.owner == caster.owner && !Type(target).building)
                    target.health = Math.Min(Type(target).health,target.health + ability.amount);
            }
            int observers = 1 << caster.owner; Point origin = Center(caster);
            for (int player=0; player<2; player++) if (Visible(player,origin) && Visible(player,caster.castPoint)) observers |= 1 << player;
            var ids = new int[2];
            for(int player=0;player<2;player++)if((observers & (1<<player))!=0)ids[player]=nextAbilityIds[player]++;
            recentAbilities.Add((new AbilityEvent { tick=Tick,caster=caster.id,owner=caster.owner,
                ability=ability.id,position=caster.castPoint },origin,observers,ids));
            FinishCast(caster);
        }

        private void FinishCast(EntityState caster)
        {
            var resume = caster.castResume;
            int seenTick = caster.castResumeSeenTick;
            caster.castingAbility = null; caster.castResume = null;
            ResetTravel(caster); caster.combatTarget = 0; caster.approachTarget = 0;
            caster.order = resume?.kind ?? CommandKind.Stop; caster.target = resume?.target ?? 0;
            caster.destination = resume?.position ?? caster.position;
            caster.activeOrderSequence = resume?.sourceSequence ?? 0; caster.targetSeenTick = seenTick;
            caster.trafficPriority = resume?.trafficPriority ?? 0; caster.trafficMass = resume?.trafficMass ?? 0;
            caster.moveGoal = caster.order == CommandKind.Move;
            // Resume by intent, recomputing paths from the casting position. Do not
            // restore stale routes or spend/execute any queued command a second time.
            // Appending from Hold normally activates the next action immediately.
            // A cast temporarily defers that transition; it must not strand orders
            // appended while approaching when the remembered Hold is restored.
            if (caster.order == CommandKind.Hold && caster.pendingOrders.Count > 0) AdvanceUnitOrders(caster);
        }
    }
}
