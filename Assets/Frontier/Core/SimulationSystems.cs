using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Simulation
    {
        private readonly List<(AttackEvent shot, int observers)> recentAttacks = new List<(AttackEvent, int)>();
        private int nextAttackId;
        public const int DeathHistoryTicks = TickRate * 6, MaxDeathHistory = 256;
        private readonly List<(DeathEvent death, int observers)> recentDeaths = new List<(DeathEvent, int)>();
        private void RecordDeath(EntityState unit)
        {
            RecordLoss(unit);
            if (Type(unit).building) return;
            int observers = 1 << unit.owner;
            for (int player = 0; player < 2; player++)
                if (Visible(player, unit.position)) observers |= 1 << player;
            recentDeaths.Add((new DeathEvent { unit = unit.id, tick = Tick, owner = unit.owner,
                definition = unit.definition, position = unit.position, direction = unit.velocity }, observers));
            if (recentDeaths.Count > MaxDeathHistory) recentDeaths.RemoveAt(0);
        }
        private void RecordAttack(EntityState source, EntityState target)
        {
            Point origin=Center(source),destination=Center(target);int observers=0;
            for(int player=0;player<2;player++)
                if(Visible(player,origin)&&Visible(player,destination))observers|=1<<player;
            recentAttacks.Add((new AttackEvent {id=++nextAttackId,tick=Tick,attacker=source.id,target=target.id,owner=source.owner,
                definition=source.definition,targetDefinition=target.definition,origin=origin,destination=destination},observers));
        }
        private void AdvanceConstruction(EntityState building)
        {
            if (building.awaitingClearance) return;
            var type = Type(building);
            bool active = building.remaining < type.duration && Catalog.Faction(type.faction).grows;
            if (!active) active = Entities.Values.Any(w => w.owner == building.owner && w.health > 0 && w.complete && w.clearingSite == 0 && Type(w).role == "worker"
                && w.order == CommandKind.Build && w.target == building.id && w.position.Distance(Center(building)) <= type.footprint * .75f + 1.3f);
            if (!active) return;
            building.remaining--; building.health = Math.Min(type.health, building.health + type.health * .85f / type.duration);
            if (building.remaining <= 0) { building.complete = true; RecordCompletion(building); FinishBuildOrder(building.owner,building.id,true,BuildOrderStatus.Completed); Notice?.Invoke(building.owner, type.name + " ready."); }
        }
        private void Construct(EntityState worker)
        {
            if (!Entities.TryGetValue(worker.target, out var building) || building.complete) { ResumeGather(worker); return; }
            if (building.awaitingClearance) { Brake(worker); return; }
            if (Travel(worker, Center(building), Type(building).footprint * .75f + 1.2f) && Catalog.Faction(Players[worker.owner].faction).grows)
            { if (building.remaining == Type(building).duration) building.remaining--; ResumeGather(worker); }
        }
        private void AdvanceProduction(EntityState producer)
        {
            if (producer.queue.Count == 0) return;
            var order = producer.queue[0]; if (order.remaining > 0) order.remaining--;
            if (order.remaining > 0) return;
            if (order.research)
            {
                if (order.definition == "attack") Players[producer.owner].attackLevel++; else Players[producer.owner].armorLevel++;
                producer.queue.RemoveAt(0); FinishBuildOrder(producer.owner,order.id,false,BuildOrderStatus.Completed); Notice?.Invoke(producer.owner, "Research complete."); return;
            }
            if (Units(producer.owner) >= Capacity(producer.owner)) return;
            float radius = Catalog[order.definition].collisionRadius;
            Point? exit = producer.hasRallyPoint ? FreeRallyExit(producer, radius) : FreeExit(producer, radius);
            if (!exit.HasValue) return;
            var spawned = Spawn(producer.owner, order.definition, exit.Value); producer.queue.RemoveAt(0);
            RecordCompletion(spawned); FinishBuildOrder(producer.owner,order.id,false,BuildOrderStatus.Completed);
            if (producer.hasRallyPoint) RallyProducedUnit(producer, spawned);
            else if (Type(spawned).role == "worker") ResumeGather(spawned);
        }
        private Point? FreeExit(EntityState building,float radius=.45f,bool reverse=false)
        {
            var t = Type(building); int size = t.footprint, x0 = (int)building.position.x, y0 = (int)building.position.y;
            for (int dy = -1; dy <= size; dy++) for (int dx = -1; dx <= size; dx++)
            {
                int x = x0 + (reverse ? size - 1 - dx : dx), y = y0 + (reverse ? size - 1 - dy : dy);
                if (x != x0 - 1 && x != x0 + size && y != y0 - 1 && y != y0 + size) continue;
                if (!Map.Inside(x, y)) continue; int cell = Map.Index(x, y);
                if (Map.Traversable(Map.Cell(Center(building)), cell) && ClearForSpawn(Map.Center(cell),radius)) return Map.Center(cell);
            }
            return null;
        }
        private void Gather(EntityState worker)
        {
            var resource = Resources.FirstOrDefault(r => r.id == worker.resourceId);
            if (worker.depositing || worker.cargo >= 8 || (resource != null && KnownRemaining(worker.owner, resource) == 0 && worker.cargo > 0))
            {
                worker.depositing = true;
                var hq = Entities.Values.Where(e => e.owner == worker.owner && e.complete && Type(e).role == "hq").OrderBy(e => Center(e).Distance(worker.position)).FirstOrDefault();
                if (hq == null) { worker.order = CommandKind.Stop; return; }
                if (Travel(worker, Center(hq), Type(hq).footprint * .75f + 1))
                {
                    if (worker.cargoKind == "catalyst") Players[worker.owner].catalyst += worker.cargo; else Players[worker.owner].minerals += worker.cargo;
                    var counters=matchCounters[worker.owner];
                    if(worker.cargoKind=="catalyst")counters.catalystCollected+=worker.cargo;else counters.mineralsCollected+=worker.cargo;
                    worker.cargo = 0; worker.depositing = false; worker.path = null;
                    if (worker.pendingOrders.Count > 0 && AdvanceUnitOrders(worker)) return;
                    if (resource != null) RebalanceGather(worker, resource);
                }
                return;
            }
            if (resource == null || KnownRemaining(worker.owner, resource) == 0) { ResumeGather(worker); return; }
            if (resource.kind == "catalyst" && !Extractor(worker.owner, resource)) { ResumeGather(worker); return; }
            if (Travel(worker, resource.position, GatherRange(resource)) && resource.remaining > 0 && Tick % 8 == worker.id % 8)
            { resource.remaining--; worker.cargo++; worker.cargoKind = resource.kind; }
        }
        private void Repair(EntityState worker)
        {
            if (!Entities.TryGetValue(worker.target, out var target) || !target.complete || target.health >= Type(target).health) { ResumeGather(worker); return; }
            if (Travel(worker, Center(target), Type(target).footprint * .75f + 1.2f) && Tick % 4 == 0 && Players[worker.owner].minerals > 0)
            { Pay(worker.owner,1,0); target.health = Math.Min(Type(target).health, target.health + 4); }
        }
        private void UpdateVision()
        {
            for (int player = 0; player < 2; player++)
            {
                byte[] cells = vision[player];
                for (int i = 0; i < cells.Length; i++) if (cells[i] == 2) cells[i] = 1;
                foreach (var entity in Entities.Values)
                {
                    if (entity.owner != player) continue;
                    Point center = Center(entity); int radius = (int)Type(entity).vision;
                    for (int y = Math.Max(0, (int)center.y - radius); y <= Math.Min(Map.Height - 1, (int)center.y + radius); y++)
                        for (int x = Math.Max(0, (int)center.x - radius); x <= Math.Min(Map.Width - 1, (int)center.x + radius); x++)
                        { Point point = new Point(x + .5f, y + .5f); if (center.Distance(point) <= radius && Map.HasLineOfSight(center, point)) cells[Map.Index(x, y)] = 2; }
                }
                foreach (var entity in Entities.Values) if (entity.owner != player && Type(entity).building && Visible(player, Center(entity))) memories[player][entity.id] = Observe(entity, true);
                foreach (var resource in Resources) if (Visible(player, resource.position)) resourceMemories[player][resource.id] = resource.remaining;
                foreach (int id in memories[player].Keys.ToArray())
                {
                    var memory = memories[player][id]; var t = Catalog[memory.definition];
                    var center = new Point(memory.position.x + t.footprint * .5f, memory.position.y + t.footprint * .5f);
                    if (Visible(player, center) && !Entities.ContainsKey(id)) memories[player].Remove(id);
                }
            }
        }
        private VisibleEntity Observe(EntityState entity, bool remembered = false)
        {
            var type = Type(entity);
            return new VisibleEntity { id = entity.id, owner = entity.owner, definition = entity.definition, position = entity.position, health = entity.health,
                attackTick = entity.attackReadyTick > 0 ? entity.attackReadyTick - type.attackPeriod : -1,
                damageTick = remembered ? -1 : entity.lastDamageTick,
                complete = entity.complete, progress = entity.complete ? 1 : 1 - entity.remaining / (float)type.duration, remembered = remembered, order = entity.order };
        }
        public PlayerView View(int player)
        {
            var visible = new List<VisibleEntity>();
            foreach (var entity in Entities.Values)
            {
                if (entity.owner != player && !Visible(player, Center(entity))) continue;
                var item = Observe(entity);
                if (entity.owner == player) { item.target = entity.target; item.destination = entity.destination;
                    item.awaitingClearance = entity.awaitingClearance;
                    item.energy = entity.energy;
                    item.abilityCooldowns = entity.abilityCooldowns.Select(c => new AbilityCooldown { ability = c.ability, readyTick = c.readyTick }).ToArray();
                    if (Type(entity).role == "worker")
                    {
                        item.cargo = entity.cargo; item.cargoKind = entity.cargoKind;
                        item.depositing = entity.order == CommandKind.Gather && (entity.depositing || entity.cargo >= 8);
                        item.working = WorkerIsWorking(entity);
                    }
                    item.hasRallyPoint = entity.hasRallyPoint; item.rallyPoint = entity.rallyPoint;
                    item.activeOrderSequence = entity.activeOrderSequence; item.pendingOrders = entity.pendingOrders.Select(CopyOrder).ToArray(); item.queue = entity.queue.Select(q => new ProductionOrder { id = q.id, definition = q.definition, research = q.research, remaining = q.remaining, total = q.total }).ToArray(); }
                else item.order = CommandKind.Stop;
                visible.Add(item);
            }
            foreach (var entry in memories[player]) if (!visible.Any(e => e.id == entry.Key))
            { var e = entry.Value; visible.Add(new VisibleEntity { id = e.id, owner = e.owner, definition = e.definition, position = e.position, health = e.health, complete = e.complete, progress = e.progress, remembered = true }); }
            var assignments = GatherAssignments(player, Array.Empty<int>());
            var knownResources = Resources.Where(r => Explored(player, r.position)).Select(r => ObserveResource(player, r, assignments)).ToArray();
            var upgrades = new List<string>(); if (Players[player].attackLevel > 0) upgrades.Add("attack"); if (Players[player].armorLevel > 0) upgrades.Add("armor");
            return new PlayerView { player = player, tick = Tick, winner = Winner, faction = Players[player].faction, minerals = Players[player].minerals, catalyst = Players[player].catalyst,
                supply = Units(player) + Reserved(player), capacity = Capacity(player), entities = visible.ToArray(), resources = knownResources, visibility = (byte[])vision[player].Clone(), researched = upgrades.ToArray(),
                abilityEvents = recentAbilities.Where(a => (a.observers & (1<<player)) != 0 && Visible(player,a.origin) && Visible(player,a.effect.position)).Select(a=>ObservedAbility(a.effect,a.ids[player])).ToArray(),
                deaths = recentDeaths.Where(d => (d.observers & (1<<player)) != 0 && (d.death.owner == player || Visible(player,d.death.position))).Select(d=>d.death).ToArray(),
                // Never reveal a previously unseen shot when an observer arrives later, or keep a trail in lost vision.
                attacks = recentAttacks.Where(a => (a.observers & (1<<player)) != 0 && Visible(player,a.shot.origin) && Visible(player,a.shot.destination)).Select(a=>a.shot).ToArray() };
        }
        public MatchSnapshot Snapshot()
        {
            // Authoritative diagnostic snapshot. Network code must send PlayerView, never this full state.
            return new MatchSnapshot { mapId = Map.Definition.id, tick = Tick, winner = Winner, entities = Entities.Values.OrderBy(e => e.id).ToArray(), players = Players, resources = Resources.ToArray() };
        }
    }
}
