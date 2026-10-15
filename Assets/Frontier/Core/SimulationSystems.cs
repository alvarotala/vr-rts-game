using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public sealed partial class Simulation
    {
        private void AdvanceConstruction(EntityState building)
        {
            var type = Type(building);
            bool active = building.remaining < type.duration && Catalog.Faction(type.faction).grows;
            if (!active) active = Entities.Values.Any(w => w.owner == building.owner && w.order == CommandKind.Build && w.target == building.id && w.position.Distance(Center(building)) <= type.footprint * .75f + 1.3f);
            if (!active) return;
            building.remaining--; building.health = Math.Min(type.health, building.health + type.health * .85f / type.duration);
            if (building.remaining <= 0) { building.complete = true; Notice?.Invoke(building.owner, type.name + " listo."); }
        }
        private void Construct(EntityState worker)
        {
            if (!Entities.TryGetValue(worker.target, out var building) || building.complete) { ResumeGather(worker); return; }
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
                producer.queue.RemoveAt(0); Notice?.Invoke(producer.owner, "Investigación completada."); return;
            }
            if (Units(producer.owner) >= Capacity(producer.owner)) return;
            Point? exit = FreeExit(producer,Catalog[order.definition].collisionRadius); if (!exit.HasValue) return;
            var spawned = Spawn(producer.owner, order.definition, exit.Value); producer.queue.RemoveAt(0);
            if (Type(spawned).role == "worker") ResumeGather(spawned);
        }
        private Point? FreeExit(EntityState building,float radius=.45f)
        {
            var t = Type(building); int size = t.footprint, x0 = (int)building.position.x, y0 = (int)building.position.y;
            for (int y = y0 - 1; y <= y0 + size; y++) for (int x = x0 - 1; x <= x0 + size; x++)
            {
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
                    worker.cargo = 0; worker.depositing = false; worker.path = null;
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
            { Players[worker.owner].minerals--; target.health = Math.Min(Type(target).health, target.health + 4); }
        }
        private void Attack(EntityState unit)
        {
            if (!Entities.TryGetValue(unit.target, out var enemy) || enemy.health <= 0 || !Visible(unit.owner, Center(enemy))) { unit.order = CommandKind.Stop; unit.target = 0; unit.path = null; return; }
            Strike(unit, enemy, true);
        }
        private bool Fight(EntityState unit, bool pursue)
        {
            var type = Type(unit); if (type.damage <= 0) return false;
            EntityState best = null; float bestDistance = pursue ? type.vision : type.range;
            foreach (var enemy in Entities.Values)
            {
                if (enemy.owner == unit.owner || enemy.health <= 0 || !Visible(unit.owner, Center(enemy))) continue;
                float distance = Center(unit).Distance(Center(enemy)) - (Type(enemy).building ? Type(enemy).footprint * .4f : 0);
                if (distance <= bestDistance) { best = enemy; bestDistance = distance; }
            }
            if (best == null) return false;
            Strike(unit, best, pursue); return true;
        }
        private void Strike(EntityState unit, EntityState target, bool pursue)
        {
            var type = Type(unit); float range = type.range + (Type(target).building ? Type(target).footprint * .4f : 0);
            if (Center(unit).Distance(Center(target)) > range || !Map.HasLineOfSight(Center(unit), Center(target)))
            { if (pursue && !type.building) Travel(unit, Center(target), range * .85f); return; }
            if (Tick < unit.attackReadyTick) return;
            float damage = Math.Max(1, type.damage * (1 + Players[unit.owner].attackLevel * .2f) - Players[target.owner].armorLevel * 2);
            target.health -= damage; target.lastDamageTick = Tick; unit.attackReadyTick = Tick + type.attackPeriod;
            if (type.role == "siege") foreach (var other in Entities.Values)
                if (other.id != target.id && other.owner != unit.owner && Center(other).Distance(Center(target)) < 2)
                { other.health -= damage * .35f; other.lastDamageTick = Tick; }
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
                complete = entity.complete, progress = entity.complete ? 1 : 1 - entity.remaining / (float)type.duration, remembered = remembered, order = entity.order };
        }
        public PlayerView View(int player)
        {
            var visible = new List<VisibleEntity>();
            foreach (var entity in Entities.Values)
            {
                if (entity.owner != player && !Visible(player, Center(entity))) continue;
                var item = Observe(entity);
                if (entity.owner == player) { item.target = entity.target; item.queue = entity.queue.Select(q => new ProductionOrder { definition = q.definition, research = q.research, remaining = q.remaining, total = q.total }).ToArray(); }
                else item.order = CommandKind.Stop;
                visible.Add(item);
            }
            foreach (var entry in memories[player]) if (!visible.Any(e => e.id == entry.Key))
            { var e = entry.Value; visible.Add(new VisibleEntity { id = e.id, owner = e.owner, definition = e.definition, position = e.position, health = e.health, complete = e.complete, progress = e.progress, remembered = true }); }
            var assignments = GatherAssignments(player, Array.Empty<int>());
            var knownResources = Resources.Where(r => Explored(player, r.position)).Select(r => ObserveResource(player, r, assignments)).ToArray();
            var upgrades = new List<string>(); if (Players[player].attackLevel > 0) upgrades.Add("attack"); if (Players[player].armorLevel > 0) upgrades.Add("armor");
            return new PlayerView { player = player, tick = Tick, winner = Winner, faction = Players[player].faction, minerals = Players[player].minerals, catalyst = Players[player].catalyst,
                supply = Units(player) + Reserved(player), capacity = Capacity(player), entities = visible.ToArray(), resources = knownResources, visibility = (byte[])vision[player].Clone(), researched = upgrades.ToArray() };
        }
        public MatchSnapshot Snapshot()
        {
            // Authoritative diagnostic snapshot. Network code must send PlayerView, never this full state.
            return new MatchSnapshot { mapId = Map.Definition.id, tick = Tick, winner = Winner, entities = Entities.Values.OrderBy(e => e.id).ToArray(), players = Players, resources = Resources.ToArray() };
        }
    }
}
