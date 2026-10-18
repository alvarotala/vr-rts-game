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
        // Difficulty changes decisions, never the simulation's economy or unit stats.
        private sealed class Pace
        {
            public int reaction, workers, army, recruit, build, barracks, extractor, tech, defense, research, expansion;
            public int firstWave, waveSize, waveLimit, waveRest, waveDuration, waveGrowth = 1;
        }
        private readonly Pace pace;
        public AiDifficulty Difficulty { get; }
        private readonly Catalog catalog;
        private readonly GridMap terrain;
        private int sequence, lastRecruit = -10000, lastWorker = -10000, lastBuild = -10000, lastResearch = -10000, patrol;
        private int waveStarted, nextWave, waves, waveTarget;
        private Point waveDestination;
        private bool waveActive;
        private readonly HashSet<int> raiders = new HashSet<int>();
        public Strategist(int player, AiDifficulty difficulty, Catalog catalog, MapDefinition map)
        {
            this.player = player; Difficulty = difficulty; this.catalog = catalog; terrain = new GridMap(map);
            pace = difficulty == AiDifficulty.VeryEasy
                ? new Pace { reaction=6, workers=11, army=12, recruit=24, build=24, barracks=1, extractor=90, tech=240, defense=420, research=720, expansion=1080,
                    firstWave=720, waveSize=3, waveLimit=5, waveRest=180, waveDuration=90 }
                : difficulty == AiDifficulty.Easy
                ? new Pace { reaction=4, workers=12, army=18, recruit=20, build=16, barracks=1, extractor=60, tech=180, defense=360, research=600, expansion=840,
                    firstWave=600, waveSize=4, waveLimit=7, waveRest=150, waveDuration=105 }
                : new Pace { reaction=2, workers=16, army=28, recruit=12, build=8, barracks=2, extractor=40, tech=120, defense=300, research=480, expansion=600,
                    firstWave=480, waveSize=6, waveLimit=16, waveRest=90, waveDuration=120, waveGrowth=2 };
            nextWave = Seconds(pace.firstWave);
        }
        private static int Seconds(int seconds) => seconds * Simulation.TickRate;
        private Archetype Type(VisibleEntity e) => catalog[e.definition];
        public IEnumerable<GameCommand> Decide(PlayerView view)
        {
            if (view.winner != -1 || view.tick % Seconds(pace.reaction) != 0) yield break;
            var own = view.entities.Where(e => e.owner == player).ToArray();
            var workers = own.Where(e => Type(e).role == "worker").ToArray();
            var army = own.Where(e => !Type(e).building && Type(e).role != "worker").ToArray();
            var bases = own.Where(e => Type(e).role == "hq").ToArray();
            if (bases.Length == 0) yield break;
            Point home = bases[0].position;
            Point homeCenter = new Point(home.x + 2, home.y + 2);
            var opposingSpawn = terrain.Definition.spawns.First(s => s.player != player);
            Point opponent = new Point(opposingSpawn.x + 2, opposingSpawn.y + 2);
            var available = workers.Where(w => w.order != CommandKind.Build && w.order != CommandKind.Repair).ToList();
            var enemy = view.entities.Where(e => e.owner != player && !e.remembered).ToArray();

            var assigned = view.resources.ToDictionary(r => r.id, r => r.assignedWorkers);
            foreach (var worker in workers.Where(w => w.order == CommandKind.Stop || (view.tick % Seconds(30) == 0 && w.order == CommandKind.Gather)))
            {
                if (assigned.ContainsKey(worker.target)) assigned[worker.target] = Math.Max(0, assigned[worker.target] - 1);
                var resource = view.resources.Where(r => r.remaining != 0 && r.optimalWorkers > 0 && bases.Any(b => b.position.Distance(r.position) < 20))
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
                    if (repairer != null) { available.Remove(repairer); yield return Command(CommandKind.Repair, repairer.id, target: damaged.id); }
                }
            }
            string buildRole = null;
            bool Has(string role) => own.Any(e => Type(e).role == role);
            int Count(string role) => own.Count(e => Type(e).role == role);
            if (view.capacity < Simulation.UnitLimit && view.supply >= view.capacity - 2 && !own.Any(e => Type(e).role == "supply" && !e.complete)) buildRole = "supply";
            else if (!Has("barracks")) buildRole = "barracks";
            else if (!Has("extractor") && view.tick >= Seconds(pace.extractor)) buildRole = "extractor";
            else if (!Has("tech") && own.Any(e => Type(e).role == "barracks" && e.complete) && view.tick >= Seconds(pace.tech)) buildRole = "tech";
            else if (Count("barracks") < pace.barracks && view.minerals > 300 && Has("tech")) buildRole = "barracks";
            else if (!Has("defense") && view.minerals > 250 && view.tick >= Seconds(pace.defense)) buildRole = "defense";

            int minerals = view.minerals, catalyst = view.catalyst, supply = view.supply;
            bool Afford(Archetype t) => minerals >= t.minerals && catalyst >= t.catalyst;
            void Reserve(Archetype t) { minerals -= t.minerals; catalyst -= t.catalyst; }
            bool saving = false;
            if (buildRole != null && available.Count > 0 && view.tick - lastBuild >= Seconds(pace.build))
            {
                var type = catalog.Role(view.faction, buildRole);
                var planned = BuildSpot(view, own, home, type);
                saving = planned.HasValue;
                if (Afford(type))
                {
                    Point? spot = planned;
                    if (spot.HasValue)
                    {
                        var worker = available.OrderBy(w => w.position.Distance(spot.Value)).First(); available.Remove(worker);
                        Reserve(type); lastBuild = view.tick;
                        yield return Command(CommandKind.Build, worker.id, definition: type.id, position: spot.Value);
                    }
                }
            }
            // Expansion sites are public map data; a worker must scout the area before construction is accepted.
            if (!saving && available.Count > 0 && bases.Length < 2 && view.tick >= Seconds(pace.expansion)
                && view.tick - lastBuild >= Seconds(pace.build) && minerals >= 450)
            {
                var expansion = terrain.Definition.resources.Where(r => r.kind == "mineral" && bases.All(b => b.position.Distance(new Point(r.x, r.y)) > 20)
                    && new Point(r.x,r.y).Distance(opponent) > 24)
                    .OrderBy(r => home.Distance(new Point(r.x, r.y))).FirstOrDefault();
                if (expansion != null)
                {
                    var candidate = new Point(expansion.x + (player == 0 ? 5 : -6), expansion.y + 3);
                    var worker = available.OrderBy(w => w.position.Distance(candidate)).First();
                    var type = catalog.Role(view.faction, "hq");
                    var spot = BuildSpot(view, own, candidate, type);
                    available.Remove(worker); lastBuild = view.tick;
                    if (spot.HasValue) { Reserve(type); yield return Command(CommandKind.Build, worker.id, definition: type.id, position: spot.Value); }
                    else yield return Command(CommandKind.Move, worker.id, position: candidate);
                }
            }
            int CountUnits(string role) => own.Count(e => Type(e).role == role)
                + own.Sum(e => e.queue.Count(q => !q.research && catalog[q.definition].role == role));
            var workerType = catalog.Role(view.faction, "worker");
            var headquarters = bases.FirstOrDefault(b => b.complete && b.queue.Length == 0);
            if ((!saving || workers.Length < 8) && headquarters != null && CountUnits("worker") < pace.workers
                && view.tick - lastWorker >= Seconds(pace.recruit) && supply < view.capacity && Afford(workerType))
            {
                Reserve(workerType); supply++; lastWorker = view.tick;
                yield return Command(CommandKind.Train, headquarters.id, definition: workerType.id);
            }

            // One shared composition in every mode: infantry, scout and artillery.
            // Reserve room for missing roles instead of filling the cap before tech is ready.
            var roles = new[] { "ranged", "melee", "scout", "siege" };
            int militaryCount = roles.Sum(CountUnits);
            string recruit = roles.FirstOrDefault(r => CountUnits(r) == 0);
            if (recruit == null) recruit = roles.OrderBy(r => CountUnits(r) / (r == "ranged" || r == "melee" ? 2f : 1f)).First();
            var recruitType = catalog.Role(view.faction, recruit);
            var producer = own.FirstOrDefault(b => b.complete && Type(b).role == recruitType.producer && b.queue.Length == 0);
            if (!saving && militaryCount < pace.army && view.tick - lastRecruit >= Seconds(pace.recruit)
                && producer != null && supply < view.capacity && Afford(recruitType))
            {
                Reserve(recruitType); supply++; lastRecruit = view.tick;
                yield return Command(CommandKind.Train, producer.id, definition: recruitType.id);
            }
            var tech = own.FirstOrDefault(b => b.complete && Type(b).role == "tech" && b.queue.Length == 0);
            if (!saving && tech != null && !(producer == tech && lastRecruit == view.tick)
                && view.tick >= Seconds(pace.research) && view.tick - lastResearch >= Seconds(90) && CountUnits("siege") > 0)
            {
                var upgrade = catalog.Definition.upgrades.FirstOrDefault(u => !view.researched.Contains(u.id)
                    && !own.Any(e => e.queue.Any(q => q.research && q.definition == u.id)));
                if (upgrade != null && minerals >= upgrade.minerals && catalyst >= upgrade.catalyst)
                { lastResearch = view.tick; yield return Command(CommandKind.Research, tech.id, definition: upgrade.id); }
            }

            var rally = RallySpot(own, homeCenter, opponent);
            raiders.RemoveWhere(id => !army.Any(e => e.id == id));
            // A defeated or recalled wave gets a full recovery interval. No reinforcements
            // join it mid-fight and no second wave piles onto surviving attackers.
            if (waveActive && (raiders.Count == 0 || view.tick - waveStarted >= Seconds(pace.waveDuration)))
            {
                var returning = army.Where(e => raiders.Contains(e.id)).ToArray();
                if (returning.Length > 0) foreach (var command in MoveAndHold(returning, rally)) yield return command;
                raiders.Clear(); waveActive = false; nextWave = view.tick + Seconds(pace.waveRest);
            }
            if (waveActive)
            {
                var active = army.Where(e => raiders.Contains(e.id)).ToArray();
                bool targetGone = waveTarget != 0 && !view.entities.Any(e => e.id == waveTarget);
                bool scoutedBuilding = waveTarget == 0 && view.entities.Any(e => e.owner != player && Type(e).building);
                bool arrived = active.All(e => e.order == CommandKind.Stop && e.position.Distance(waveDestination) < 12);
                if (targetGone || scoutedBuilding || arrived)
                {
                    waveDestination = RaidDestination(view, active[0].position, out waveTarget);
                    yield return Group(CommandKind.AttackMove, active, waveDestination);
                }
            }
            var reserves = army.Where(e => !raiders.Contains(e.id)).ToArray();
            float defenseRadius = Math.Min(10, homeCenter.Distance(opponent) * .25f);
            var threat = enemy.FirstOrDefault(e => bases.Any(b => new Point(b.position.x + 2, b.position.y + 2).Distance(e.position) < defenseRadius));
            if (threat != null && reserves.Length > 0)
            {
                // Defend locally; the next decision recalls pursuers once the intruder leaves.
                yield return Group(CommandKind.AttackMove, reserves, threat.position);
                yield break;
            }
            int waveSize = Math.Min(pace.waveLimit, pace.waveSize + waves * pace.waveGrowth);
            var ready = reserves.Where(e => e.position.Distance(homeCenter) < 14 && e.health >= Type(e).health * .6f).ToArray();
            if (!waveActive && view.tick >= nextWave && ready.Length >= waveSize + 2)
            {
                // Round-robin roles keeps the small detachments mixed, including scouts.
                var attackers = new List<VisibleEntity>();
                var queues = roles.Select(r => new Queue<VisibleEntity>(ready.Where(e => Type(e).role == r).OrderBy(e => e.id))).ToArray();
                while (attackers.Count < waveSize)
                    foreach (var queue in queues) if (queue.Count > 0 && attackers.Count < waveSize) attackers.Add(queue.Dequeue());
                waveDestination = RaidDestination(view, home, out waveTarget);
                foreach (var attacker in attackers) raiders.Add(attacker.id);
                waveActive = true; waveStarted = view.tick; waves++;
                yield return Group(CommandKind.AttackMove, attackers.ToArray(), waveDestination);
            }
            // Hold prevents automatic pursuit/scouting from bypassing the opening grace
            // period, especially on the small training map. Queued Hold also covers arrival.
            var reposition = new List<VisibleEntity>();
            foreach (var unit in reserves.Where(e => !raiders.Contains(e.id)))
            {
                if (unit.order == CommandKind.Move && unit.pendingOrders.Any(o => o.kind == CommandKind.Hold)) continue;
                // Formation planning may settle a unit beside the rally. Keep that safe
                // slot instead of retrying a crowded destination every reaction tick.
                if (unit.order == CommandKind.Hold && unit.position.Distance(homeCenter) < 14) continue;
                if (unit.position.Distance(rally) > 5)
                    reposition.Add(unit);
                else if (unit.order != CommandKind.Hold) yield return Command(CommandKind.Hold, unit.id);
            }
            if (reposition.Count > 0) foreach (var command in MoveAndHold(reposition.ToArray(), rally)) yield return command;
        }
        private Point RaidDestination(PlayerView view, Point origin, out int target)
        {
            var known = view.entities.Where(e => e.owner != player && Type(e).building).OrderBy(e => e.position.Distance(origin)).FirstOrDefault();
            target = known?.id ?? 0;
            if (known != null) return known.position;
            var spawn = terrain.Definition.spawns.First(s => s.player != player);
            var destination = new Point(spawn.x + 5, spawn.y + 5);
            int cell = terrain.Cell(destination);
            if (cell >= 0 && view.visibility[cell] > 0)
            {
                patrol = (patrol + 1) % 16;
                destination = new Point(8 + patrol % 4 * (terrain.Width - 16) / 4f, 8 + patrol / 4 * (terrain.Height - 16) / 4f);
            }
            return destination;
        }
        private IEnumerable<GameCommand> MoveAndHold(VisibleEntity[] units, Point position)
        {
            yield return Group(CommandKind.Move, units, position);
            var hold = Group(CommandKind.Hold, units, position); hold.append = true; yield return hold;
        }
        private Point RallySpot(VisibleEntity[] own, Point home, Point opponent)
        {
            var behind = Point.Towards(home, new Point(2 * home.x - opponent.x, 2 * home.y - opponent.y), 7);
            var candidates = new List<Point>();
            for (int y = -10; y <= 10; y++) for (int x = -10; x <= 10; x++)
            {
                var point = new Point((int)home.x + x + .5f, (int)home.y + y + .5f);
                if (point.Distance(home) < 6 || point.Distance(home) > 12 || point.Distance(opponent) < home.Distance(opponent)
                    || !terrain.CanStand(point, .49f)) continue;
                if (own.Any(e => Type(e).building && point.x > e.position.x - 2 && point.x < e.position.x + Type(e).footprint + 2
                    && point.y > e.position.y - 2 && point.y < e.position.y + Type(e).footprint + 2)) continue;
                if (terrain.Definition.resources.Any(r => point.Distance(new Point(r.x+.5f,r.y+.5f)) < 4)) continue;
                candidates.Add(point);
            }
            return candidates.Count > 0 ? candidates.OrderBy(p => p.Distance(behind)).First() : home;
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
                // Keep production and towers on our side; forward placement on Training
                // Grounds must not become an accidental attack during the grace period.
                var spawn = terrain.Definition.spawns.First(s => s.player != player);
                var opponent = new Point(spawn.x + 2, spawn.y + 2);
                var center = new Point(point.x + type.footprint * .5f, point.y + type.footprint * .5f);
                if (center.Distance(opponent) < 18 || center.Distance(opponent) < center.Distance(origin)) continue;
                if ((type.role == "barracks" || type.role == "tech" || type.role == "defense")
                    && center.Distance(opponent) < new Point(origin.x + 2, origin.y + 2).Distance(opponent)) continue;
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
