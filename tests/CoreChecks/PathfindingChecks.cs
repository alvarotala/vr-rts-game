using Frontier.Core;

static class PathfindingChecks
{
    // Keep these as complete movement orders, rather than testing A* in isolation:
    // a valid route is not sufficient if two friendly groups deadlock while following it.
    public static void Run(Catalog catalog, Action<bool,string> check)
    {
        foreach (string faction in new[] { "industrial", "biological" })
        {
            foreach (int width in new[] { 1, 2 })
            {
                var sim = Fixture(catalog, faction);
                for (int x = 30; x <= 40; x++)
                    for (int y = 0; y < sim.Map.Height; y++)
                        if (y < 22 || y >= 22 + width) sim.Map.Walkable[sim.Map.Index(x, y)] = false;
                var left = new List<EntityState>();
                var right = new List<EntityState>();
                for (int n = 0; n < 15; n++)
                {
                    left.Add(Unit(sim, 20.5f + n % 3, 20.5f + n / 3, Roles[n % Roles.Length]));
                    right.Add(Unit(sim, 48.5f + n % 3, 20.5f + n / 3, Roles[n % Roles.Length]));
                }
                string label = $"{faction}: two mixed groups cross a {width}-cell corridor";
                Move(sim, left, new Point(57.5f, 24.5f), check, label + " eastbound order");
                Move(sim, right, new Point(13.5f, 24.5f), check, label + " westbound order");
                Complete(sim, left.Concat(right).ToArray(), 2400, check, label);
            }

            {
                var sim = Fixture(catalog, faction);
                for (int x = 30; x <= 40; x++)
                    for (int y = 0; y < sim.Map.Height; y++)
                        if (y != 22) sim.Map.Walkable[sim.Map.Index(x, y)] = false;
                // Reselect one member while its old companion keeps the earlier order.
                // The two opposing commands share their lowest entity ID, but are distinct flows.
                var reselected = Unit(sim, 14.5f, 18.5f, "worker");
                var east = new List<EntityState>();
                var west = new List<EntityState>();
                for (int n = 0; n < 15; n++)
                {
                    east.Add(Unit(sim, 20.5f + n % 3, 20.5f + n / 3, Roles[n % Roles.Length]));
                    west.Add(Unit(sim, 48.5f + n % 3, 20.5f + n / 3, Roles[n % Roles.Length]));
                }
                string label = faction + ": overlapping selections retain independent movement orders";
                Move(sim, new[] { reselected }.Concat(east), new Point(57.5f, 24.5f), check, label + " original eastbound order");
                var originalEastDestinations = east.Select(unit => unit.destination).ToArray();
                Move(sim, new[] { reselected }.Concat(west), new Point(13.5f, 24.5f), check, label + " replacement westbound order");
                check(east.Where((unit, index) => unit.destination.Distance(originalEastDestinations[index]) > .001f).Count() == 0,
                    label + " leaves the unselected members' destinations intact");
                Complete(sim, new[] { reselected }.Concat(east).Concat(west).ToArray(), 2400, check, label);
            }

            {
                var sim = Fixture(catalog, faction);
                for (int x = 30; x <= 40; x++)
                    for (int y = 0; y < sim.Map.Height; y++)
                        if (y != 22) sim.Map.Walkable[sim.Map.Index(x, y)] = false;
                var parked = new List<EntityState>();
                for (int n = 0; n < 6; n++) parked.Add(Unit(sim, 44.5f + n % 2, 19.5f + n / 2, "worker"));
                string label = faction + ": settled formation lets later traffic pass and returns to its slots";
                Move(sim, parked, new Point(42.5f, 22.5f), check, label + " parking order");
                Complete(sim, parked.ToArray(), 400, check, label + " initially settles");
                var crossing = new List<EntityState>();
                for (int n = 0; n < 10; n++) crossing.Add(Unit(sim, 51.5f + n % 2, 20.5f + n / 2, Roles[n % Roles.Length]));
                Move(sim, crossing, new Point(17.5f, 22.5f), check, label + " later westbound order");
                Complete(sim, parked.Concat(crossing).ToArray(), 2400, check, label);
            }

            {
                var sim = Fixture(catalog, faction);
                var east = new List<EntityState>();
                var north = new List<EntityState>();
                for (int n = 0; n < 20; n++)
                {
                    east.Add(Unit(sim, 18.5f + n % 4, 20.5f + n / 4, Roles[n % Roles.Length]));
                    north.Add(Unit(sim, 37.5f + n % 4, 8.5f + n / 4, Roles[n % Roles.Length]));
                }
                string label = faction + ": perpendicular mixed groups clear their intersection";
                Move(sim, east, new Point(60.5f, 22.5f), check, label + " eastbound order");
                Move(sim, north, new Point(39.5f, 38.5f), check, label + " northbound order");
                Complete(sim, east.Concat(north).ToArray(), 1200, check, label);
            }

            {
                var sim = Fixture(catalog, faction);
                var convoy = new List<EntityState>();
                for (int n = 0; n < 10; n++)
                {
                    convoy.Add(Unit(sim, 25.5f + n % 2, 20.5f + n / 2, "siege"));
                    convoy.Add(Unit(sim, 17.5f + n % 2, 20.5f + n / 2, "scout"));
                }
                string label = faction + ": fast scouts behind slow artillery reach their formation";
                Move(sim, convoy, new Point(60.5f, 23.5f), check, label + " order");
                Complete(sim, convoy.ToArray(), 1200, check, label);
            }
        }
    }

    private static readonly string[] Roles = { "worker", "scout", "melee", "ranged", "siege" };

    private static Simulation Fixture(Catalog catalog, string faction)
    {
        var map = new MapDefinition
        {
            id = "crossing-traffic", title = "Crossing traffic regression", width = 80, height = 48,
            spawns = new[] { new SpawnDefinition { player = 0, x = 2, y = 2 }, new SpawnDefinition { player = 1, x = 72, y = 40 } },
            resources = new[]
            {
                new ResourceDefinition { id = "a", x = 10, y = 12, amount = 10000 },
                new ResourceDefinition { id = "b", x = 69, y = 35, amount = 10000 },
                new ResourceDefinition { id = "c", kind = "catalyst", x = 8, y = 8 },
                new ResourceDefinition { id = "d", kind = "catalyst", x = 74, y = 35 }
            }
        };
        var sim = new Simulation(map, catalog, faction);
        foreach (var unit in sim.Entities.Values.Where(e => !sim.Type(e).building).ToArray()) sim.Entities.Remove(unit.id);
        return sim;
    }

    private static EntityState Unit(Simulation sim, float x, float y, string role)
    {
        var type = sim.Catalog.Role(sim.Players[0].faction, role);
        var unit = new EntityState { id = sim.Entities.Keys.Max() + 1, owner = 0, definition = type.id, position = new Point(x, y), health = type.health };
        sim.Entities.Add(unit.id, unit);
        return unit;
    }

    private static void Move(Simulation sim, IEnumerable<EntityState> units, Point goal, Action<bool,string> check, string label)
    {
        var selected = units.ToArray();
        check(sim.Submit(new GameCommand { player = 0, kind = CommandKind.Move, entities = selected.Select(e => e.id).ToArray(), position = goal }).accepted
            && selected.All(e => e.order == CommandKind.Move), label);
    }

    private static void Complete(Simulation sim, EntityState[] units, int timeout, Action<bool,string> check, string label)
    {
        // Remember original slots: yielding must not silently rewrite the user's command.
        var destinations = units.Select(e => e.destination).ToArray();
        var before = new Point[units.Length];
        int halfAt = -1, allAt = -1;
        for (int tick = 0; tick < timeout; tick++)
        {
            for (int i = 0; i < units.Length; i++) before[i] = units[i].position;
            sim.Step();
            for (int i = 0; i < units.Length; i++)
            {
                var unit = units[i];
                var type = sim.Type(unit);
                // A unit may follow two legal segments in one tick when an ally pushes it.
                // Their straight chord can clip a corner even though the actual movement did not.
                if (!sim.Map.CanStand(unit.position, type.collisionRadius)
                    || !sim.Map.Traversable(sim.Map.Cell(before[i]), sim.Map.Cell(unit.position)))
                    throw new Exception($"{label}: unit {unit.id} crossed solid terrain or a cliff at tick {sim.Tick}: {before[i].x:F4},{before[i].y:F4} -> {unit.position.x:F4},{unit.position.y:F4}");
                if (before[i].Distance(unit.position) > type.speed / Simulation.TickRate + .002f)
                    throw new Exception(label + ": displacement exceeded the unit's movement budget");
                for (int j = i + 1; j < units.Length; j++)
                    if (unit.position.Distance(units[j].position) < type.collisionRadius + sim.Type(units[j]).collisionRadius - .001f)
                        throw new Exception(label + ": unit bodies overlap");
            }
            int arrived = units.Where((unit, i) => unit.position.Distance(destinations[i]) <= .15f).Count();
            if (arrived >= units.Length / 2 && halfAt < 0) halfAt = sim.Tick;
            if (arrived == units.Length) { allAt = sim.Tick; break; }
        }
        int remaining = units.Where((unit, i) => unit.position.Distance(destinations[i]) > .15f).Count();
        Console.WriteLine($"TRAFFIC {label}: halfway={(halfAt < 0 ? "timeout" : (halfAt / (float)Simulation.TickRate).ToString("F2") + "s")}, all={(allAt < 0 ? "timeout" : (allAt / (float)Simulation.TickRate).ToString("F2") + "s")}, remaining={remaining}/{units.Length}");
        if (remaining > 0)
            Console.WriteLine(string.Join("; ", units.Where((unit, i) => unit.position.Distance(destinations[i]) > .15f)
                .Take(8).Select(unit => $"{unit.id}: {unit.position.x:F2},{unit.position.y:F2} -> {unit.destination.x:F1},{unit.destination.y:F1}, {unit.order}, blocked={unit.blockedTicks}")));
        check(remaining == 0, label);
        check(units.Where((unit, i) => unit.destination.Distance(destinations[i]) > .001f).Count() == 0,
            label + " preserves every assigned destination after yielding");
    }
}
