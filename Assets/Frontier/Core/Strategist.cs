using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontier.Core
{
    public enum AiDifficulty { VeryEasy, Easy, Normal }

    // The bot receives exactly the player's information projection. It has no Simulation reference.
    public sealed class Strategist
    {
        private readonly int player;
        private readonly bool easy, veryEasy;
        public AiDifficulty Difficulty { get; }
        private readonly Catalog catalog;
        private readonly GridMap terrain;
        private int sequence, lastAttack = -10000, lastRecruit = -480, patrol;
        private readonly HashSet<int> recovering = new HashSet<int>();
        public Strategist(int player, AiDifficulty difficulty, Catalog catalog, MapDefinition map)
        { this.player = player; Difficulty = difficulty; easy = difficulty != AiDifficulty.Normal; veryEasy = difficulty == AiDifficulty.VeryEasy; this.catalog = catalog; terrain = new GridMap(map); }
        private Archetype Type(VisibleEntity e) => catalog[e.definition];
        public IEnumerable<GameCommand> Decide(PlayerView view)
        {
            if (view.winner != -1 || view.tick % (veryEasy ? 120 : easy ? 60 : 20) != 0) yield break;
            var own = view.entities.Where(e => e.owner == player).ToArray();
            var workers = own.Where(e => Type(e).role == "worker").ToArray();
            var army = own.Where(e => !Type(e).building && Type(e).role != "worker").ToArray();
            var bases = own.Where(e => Type(e).role == "hq").ToArray();
            if (bases.Length == 0) yield break;
            Point home = bases[0].position;
            var available = workers.Where(w => w.order != CommandKind.Build).ToArray();
            var enemy = view.entities.Where(e => e.owner != player && !e.remembered).ToArray();
            recovering.RemoveWhere(id => !army.Any(e => e.id == id && e.health < Type(e).health * .85f));
            foreach (var hurt in army.Where(e => e.health < Type(e).health * .22f)) recovering.Add(hurt.id);

            var assigned = view.resources.ToDictionary(r => r.id, r => r.assignedWorkers);
            foreach (var worker in workers.Where(w => w.order == CommandKind.Stop || (view.tick % 400 == 0 && w.order == CommandKind.Gather)))
            {
                if (assigned.ContainsKey(worker.target)) assigned[worker.target] = Math.Max(0, assigned[worker.target] - 1);
                var resource = view.resources.Where(r => r.remaining != 0 && r.optimalWorkers > 0)
                    .OrderBy(r => r.kind == "catalyst" && assigned[r.id] < r.optimalWorkers && view.catalyst < 350 ? -100 : worker.position.Distance(r.position) + assigned[r.id] * (r.kind == "catalyst" ? 25 : 8)).FirstOrDefault();
                if (resource != null) { assigned[resource.id]++; yield return Command(CommandKind.Gather, worker.id, target: resource.id); }
            }
            foreach (var building in own.Where(b => !b.complete && !catalog.Faction(view.faction).grows))
            {
                if (workers.Any(w => w.order == CommandKind.Build && w.target == building.id)) continue;
                // Clear abandoned foundations so the build planner can choose a new site.
                yield return Command(CommandKind.Cancel, building.id);
            }
            if (!catalog.Faction(view.faction).regenerates && view.minerals > 20)
            {
                var damaged = own.Where(e => e.complete && e.health < Type(e).health * .85f && e.position.Distance(home) < 20)
                    .OrderBy(e => e.health / Type(e).health).FirstOrDefault();
                if (damaged != null && !workers.Any(w => w.order == CommandKind.Repair && w.target == damaged.id))
                {
                    var repairer = available.Where(w => w.order != CommandKind.Repair).OrderBy(w => w.position.Distance(damaged.position)).FirstOrDefault();
                    if (repairer != null) yield return Command(CommandKind.Repair, repairer.id, target: damaged.id);
                }
            }
            string buildRole = null;
            bool Has(string role) => own.Any(e => Type(e).role == role);
            int Count(string role) => own.Count(e => Type(e).role == role);
            if (view.capacity < 50 && view.supply >= view.capacity - 2 && !own.Any(e => Type(e).role == "supply" && !e.complete)) buildRole = "supply";
            else if (!Has("barracks")) buildRole = "barracks";
            else if (!veryEasy && !Has("extractor") && view.tick > 600) buildRole = "extractor";
            else if (!veryEasy && !Has("tech") && own.Any(e => Type(e).role == "barracks" && e.complete) && view.tick > 1400) buildRole = "tech";
            else if (Count("barracks") < (veryEasy ? 1 : easy ? 2 : 3) && view.minerals > 240) buildRole = "barracks";
            else if (!veryEasy && !Has("defense") && view.minerals > 200 && view.tick > 1600) buildRole = "defense";

            bool saving = false;
            if (buildRole != null && available.Length > 0)
            {
                var type = catalog.Role(view.faction, buildRole);
                var planned = BuildSpot(view, own, home, type);
                saving = planned.HasValue;
                if (view.minerals >= type.minerals && view.catalyst >= type.catalyst)
                {
                    Point? spot = planned;
                    if (spot.HasValue)
                    { var worker = available.OrderBy(w => w.position.Distance(spot.Value)).First(); yield return Command(CommandKind.Build, worker.id, definition: type.id, position: spot.Value); }
                }
            }
            // Expansion sites are public map data; a worker must scout the area before construction is accepted.
            if (!veryEasy && available.Length > 0 && bases.Length < 3 && view.tick > 4800 && view.minerals >= 450)
            {
                var expansion = terrain.Definition.resources.Where(r => r.kind == "mineral" && bases.All(b => b.position.Distance(new Point(r.x, r.y)) > 20))
                    .OrderBy(r => home.Distance(new Point(r.x, r.y))).FirstOrDefault();
                if (expansion != null)
                {
                    var candidate = new Point(expansion.x + (player == 0 ? 5 : -6), expansion.y + 3);
                    var worker = available.OrderBy(w => w.position.Distance(candidate)).First();
                    var type = catalog.Role(view.faction, "hq");
                    var spot = BuildSpot(view, own, candidate, type);
                    yield return spot.HasValue ? Command(CommandKind.Build, worker.id, definition: type.id, position: spot.Value) : Command(CommandKind.Move, worker.id, position: candidate);
                }
            }
            int siegeLimit = easy ? 2 : 6;
            bool techSaving = !veryEasy && !saving && own.Any(e => e.complete && Type(e).role == "tech" && e.queue.Length == 0) && view.catalyst >= 65 &&
                ((!view.researched.Contains("attack") || !easy && !view.researched.Contains("armor")) || army.Count(e => Type(e).role == "siege") < Math.Min(siegeLimit, Math.Max(1, army.Length / 6)));
            foreach (var hq in bases.Where(b => b.complete && b.queue.Length < 1))
                if (((!saving && !techSaving) || workers.Length < 8) && workers.Length + own.Sum(e => e.queue.Count(q => !q.research && catalog[q.definition].role == "worker")) < (veryEasy ? 8 : easy ? 12 : 18))
                    yield return Command(CommandKind.Train, hq.id, definition: catalog.Role(view.faction, "worker").id);

            int militaryCount = army.Length + own.Sum(e => e.queue.Count(q => !q.research && catalog[q.definition].role != "worker"));
            foreach (var producer in own.Where(b => !saving && !techSaving && b.complete && b.queue.Length < 2 && Type(b).role == "barracks"))
            {
                // Count reservations as well as live soldiers, including orders issued this turn.
                if (veryEasy && (militaryCount >= 6 || view.tick - lastRecruit < 480)) continue;
                string role = !veryEasy && !army.Any(e => Type(e).role == "scout") ? "scout" : army.Count(e => Type(e).role == "melee") * (easy ? 1 : 2) < army.Count(e => Type(e).role == "ranged") ? "melee" : "ranged";
                yield return Command(CommandKind.Train, producer.id, definition: catalog.Role(view.faction, role).id);
                if (veryEasy) { lastRecruit = view.tick; militaryCount++; }
            }
            foreach (var tech in own.Where(b => !veryEasy && !saving && b.complete && Type(b).role == "tech" && b.queue.Length == 0))
            {
                if (view.catalyst >= 100 && !view.researched.Contains("attack")) yield return Command(CommandKind.Research, tech.id, definition: "attack");
                else if (!easy && view.catalyst >= 100 && !view.researched.Contains("armor")) yield return Command(CommandKind.Research, tech.id, definition: "armor");
                else if (army.Count(e => Type(e).role == "siege") < siegeLimit) yield return Command(CommandKind.Train, tech.id, definition: catalog.Role(view.faction, "siege").id);
            }
            var threat = enemy.FirstOrDefault(e => bases.Any(b => b.position.Distance(e.position) < (veryEasy ? 10 : 18)));
            if (threat != null && army.Length > 0)
            { yield return Group(CommandKind.AttackMove, army, threat.position); yield break; }
            if (!easy)
            {
                var hurt = army.Where(e => recovering.Contains(e.id) && e.position.Distance(home) > 8).ToArray();
                if (hurt.Length > 0) yield return Group(CommandKind.Move, hurt, new Point(home.x + 6, home.y + 6));
                army = army.Where(e => !recovering.Contains(e.id)).ToArray();
            }
            var attackers = veryEasy ? army.Where(e => e.position.Distance(home) < 12).OrderBy(e => e.id).Take(3).ToArray() : army;
            bool attackTime = veryEasy ? view.tick >= 18 * 60 * Simulation.TickRate : view.tick > (easy ? 16800 : 15600);
            if (attackers.Length >= (veryEasy ? 3 : easy ? 16 : 12) && attackTime && view.tick - lastAttack >= (veryEasy ? 3600 : 300))
            {
                var knownBuilding = view.entities.Where(e => e.owner != player && Type(e).building).OrderBy(e => e.position.Distance(home)).FirstOrDefault();
                Point destination;
                if (knownBuilding != null) destination = knownBuilding.position;
                else
                {
                    var opposingSpawn = terrain.Definition.spawns.First(s => s.player != player);
                    destination = new Point(opposingSpawn.x + 5, opposingSpawn.y + 5);
                    int cell = terrain.Cell(destination);
                    if (cell >= 0 && view.visibility[cell] > 0)
                    {
                        patrol = (patrol + 1) % 16;
                        destination = new Point(8 + patrol % 4 * (terrain.Width - 16) / 4f, 8 + patrol / 4 * (terrain.Height - 16) / 4f);
                    }
                }
                yield return Group(CommandKind.AttackMove, attackers, destination); lastAttack = view.tick;
            }
            else
            {
                // Move reinforcements away from production exits and worker lanes.
                // Leaving an army at its spawn cells can stall its own economy.
                var center=new Point(terrain.Width*.5f,terrain.Height*.5f);
                var rally=Point.Towards(new Point(home.x+2,home.y+2),center,veryEasy?6:16);
                var reserves=army.Where(e=>Type(e).role!="scout"&&e.order==CommandKind.Stop&&e.position.Distance(rally)>(veryEasy?2.5f:7)).ToArray();
                if(reserves.Length>0)yield return Group(CommandKind.Move,reserves,rally);
                var scout = army.FirstOrDefault(e => Type(e).role == "scout" && e.order == CommandKind.Stop);
                if (!veryEasy && scout != null)
                { var spawn = terrain.Definition.spawns.First(s => s.player != player); yield return Command(CommandKind.Move, scout.id, position: new Point(spawn.x + 6, spawn.y + 6)); }
            }
        }
        private Point? BuildSpot(PlayerView view, VisibleEntity[] own, Point origin, Archetype type)
        {
            IEnumerable<Point> candidates;
            if (type.role == "extractor") candidates = view.resources.Where(r => r.kind == "catalyst").Select(r => new Point((int)r.position.x - 1, (int)r.position.y - 1));
            else
            {
                var list = new List<Point>();
                for (int radius = 4; radius <= 14; radius += 2) for (int dy = -radius; dy <= radius; dy += 2) for (int dx = -radius; dx <= radius; dx += 2)
                    if (Math.Abs(dx) == radius || Math.Abs(dy) == radius) list.Add(new Point((int)origin.x + dx, (int)origin.y + dy));
                candidates = list;
            }
            foreach (Point point in candidates)
            {
                if (!terrain.CanPlace(point, type.footprint)) continue;
                bool valid = true;
                for (int y = (int)point.y; y < point.y + type.footprint; y++) for (int x = (int)point.x; x < point.x + type.footprint; x++)
                    if (view.visibility[terrain.Index(x, y)] != 2) valid = false;
                if (!valid || own.Any(e => Type(e).building && point.x < e.position.x + Type(e).footprint + 1 && point.x + type.footprint + 1 > e.position.x && point.y < e.position.y + Type(e).footprint + 1 && point.y + type.footprint + 1 > e.position.y)) continue;
                if (type.role != "extractor" && terrain.Definition.resources.Any(r => r.x >= point.x - 1 && r.x < point.x + type.footprint + 1 && r.y >= point.y - 1 && r.y < point.y + type.footprint + 1)) continue;
                return point;
            }
            return null;
        }
        private GameCommand Command(CommandKind kind, int entity, string definition = null, int target = 0, Point position = default)
            => new GameCommand { player = player, sequence = ++sequence, kind = kind, entities = new[] { entity }, definition = definition, target = target, position = position };
        private GameCommand Group(CommandKind kind, VisibleEntity[] entities, Point position)
            => new GameCommand { player = player, sequence = ++sequence, kind = kind, entities = entities.Select(e => e.id).ToArray(), position = position };
    }
}
