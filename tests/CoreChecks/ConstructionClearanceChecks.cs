using Frontier.Core;

static class ConstructionClearanceChecks
{
    public static void Run(Catalog catalog, Action<bool, string> check)
    {
        Simulation Fixture(string faction)
        {
            var map = new MapDefinition { id = "construction-clearance", title = "Construction clearance", width = 48, height = 48,
                spawns = new[] { new SpawnDefinition { player = 0, x = 2, y = 2 }, new SpawnDefinition { player = 1, x = 40, y = 40 } },
                resources = new[] { new ResourceDefinition { id = "a", x = 10, y = 12, amount = 10000 }, new ResourceDefinition { id = "b", x = 37, y = 35, amount = 10000 },
                    new ResourceDefinition { id = "c", kind = "catalyst", x = 8, y = 8 }, new ResourceDefinition { id = "d", kind = "catalyst", x = 42, y = 35 } } };
            var sim = new Simulation(map, catalog, faction);
            foreach (var e in sim.Entities.Values.Where(e => !sim.Type(e).building).ToArray()) sim.Entities.Remove(e.id);
            sim.Players[0].minerals = 5000; sim.Players[0].catalyst = 5000;
            return sim;
        }
        EntityState Unit(Simulation sim, Point point, string role = "worker", int owner = 0)
        {
            var type = catalog.Role(sim.Players[owner].faction, role);
            var unit = new EntityState { id = 1000 + sim.Entities.Count, owner = owner, definition = type.id, position = point, health = type.health };
            sim.Entities.Add(unit.id, unit); return unit;
        }
        GameCommand Order(EntityState unit, CommandKind kind, Point point = default, string definition = null, bool append = false, int target = 0)
            => new GameCommand { player = unit.owner, entities = new[] { unit.id }, kind = kind, position = point, definition = definition, append = append, target = target };
        void Steps(Simulation sim, int ticks) { for (int i = 0; i < ticks; i++) sim.Step(); }
        bool Until(Simulation sim, Func<bool> done, int ticks = 1000) { for (int i = 0; i < ticks && !done(); i++) sim.Step(); return done(); }
        bool Overlap(Simulation sim, EntityState unit, EntityState site)
        {
            int size = sim.Type(site).footprint;
            var closest = new Point(Math.Clamp(unit.position.x, site.position.x, site.position.x + size), Math.Clamp(unit.position.y, site.position.y, site.position.y + size));
            return unit.position.Distance(closest) < sim.Type(unit).collisionRadius;
        }
        foreach (string faction in new[] { "industrial", "biological" })
        {
            var supply = catalog.Role(faction, "supply"); var origin = new Point(20, 20);
            var sim = Fixture(faction); var worker = Unit(sim, new Point(18.5f, 20.5f)); var blocker = Unit(sim, new Point(20.5f, 20.5f), "siege");
            blocker.order = CommandKind.Hold; Steps(sim, 4); int cash = sim.Players[0].minerals; Point start = blocker.position;
            check(sim.Submit(Order(worker, CommandKind.Build, origin, supply.id)).accepted, "own tank allows construction " + faction);
            var site = sim.Entities.Values.Single(e => e.definition == supply.id);
            check(site.awaitingClearance && !site.complete && site.remaining == supply.duration && blocker.position.Distance(start) == 0
                && sim.Players[0].minerals == cash - supply.minerals, "foundation reserves and charges once without teleporting " + faction);
            check(!sim.Map.CanPlace(origin, supply.footprint) && sim.Map.CanStand(blocker.position, sim.Type(blocker).collisionRadius), "reserved site rejects overlap but lets units walk out " + faction);
            check(!sim.Submit(Order(worker, CommandKind.Build, origin, supply.id)).accepted && sim.Players[0].minerals == cash - supply.minerals, "second build cannot spend on reserved footprint " + faction);
            sim.Step();
            check(blocker.position.Distance(start) > 0 && blocker.position.Distance(start) <= sim.Type(blocker).speed / Simulation.TickRate + .001f
                && site.remaining == supply.duration && !sim.View(0).entities.First(e => e.id == worker.id).working, "clearance uses normal speed and construction waits " + faction);
            check(Until(sim, () => !site.awaitingClearance, 200) && !Overlap(sim, blocker, site) && blocker.order == CommandKind.Hold
                && blocker.clearingSite == 0 && sim.Map.Occupant[sim.Map.Cell(origin)] == site.id, "tank exits and keeps hold order before foundation becomes solid " + faction);
            check(Until(sim, () => site.complete) && sim.Map.CanStand(blocker.position, sim.Type(blocker).collisionRadius), "cleared construction completes without trapping tank " + faction);

            // The builder itself can be the obstruction, for either faction.
            sim = Fixture(faction); worker = Unit(sim, new Point(21.5f, 21.5f)); Steps(sim, 4);
            check(sim.Submit(Order(worker, CommandKind.Build, origin, supply.id)).accepted, "builder may start inside its own footprint " + faction);
            site = sim.Entities.Values.Single(e => e.definition == supply.id);
            check(Until(sim, () => site.complete) && !Overlap(sim, worker, site) && worker.clearingSite == 0, "builder walks out and finishes construction " + faction);

            // Full 4x4 formation: no overlapping bodies or movement jumps at any step.
            sim = Fixture(faction); worker = Unit(sim, new Point(17.5f, 21.5f));
            var group = Enumerable.Range(0, 16).Select(i => Unit(sim, new Point(20.5f + i % 4, 20.5f + i / 4), "melee")).ToArray();
            foreach (var unit in group) unit.order = CommandKind.Hold;
            Steps(sim, 4); var hq = catalog.Role(faction, "hq");
            check(sim.Submit(Order(worker, CommandKind.Build, origin, hq.id)).accepted, "dense friendly formation accepts construction " + faction);
            site = sim.Entities.Values.Single(e => e.definition == hq.id && e.position.Distance(origin) < .1f);
            bool physical = true;
            for (int tick = 0; tick < 400 && site.awaitingClearance; tick++)
            {
                var before = group.Select(e => e.position).ToArray(); sim.Step();
                for (int i = 0; i < group.Length; i++)
                {
                    physical &= group[i].position.Distance(before[i]) <= sim.Type(group[i]).speed / Simulation.TickRate + .002f;
                    for (int j = i + 1; j < group.Length; j++) physical &= group[i].position.Distance(group[j].position) >= sim.Type(group[i]).collisionRadius + sim.Type(group[j]).collisionRadius - .001f;
                }
            }
            check(physical && !site.awaitingClearance && group.All(e => !Overlap(sim, e, site) && e.order == CommandKind.Hold), "dense formation clears physically without overlaps or lost hold orders " + faction);

            sim = Fixture(faction); worker = Unit(sim, new Point(16.5f, 21.5f));
            group = Enumerable.Range(0, 56).Select(i => Unit(sim, new Point(18.5f + i % 8, 18.5f + i / 8), "melee")).ToArray();
            foreach (var unit in group) unit.order = CommandKind.Hold;
            Steps(sim, 4);
            check(sim.Submit(Order(worker, CommandKind.Build, origin, hq.id)).accepted, "site surrounded by a full army accepts construction " + faction);
            site = sim.Entities.Values.Single(e => e.definition == hq.id && e.position.Distance(origin) < .1f);
            check(Until(sim, () => !site.awaitingClearance, 600) && group.All(e => !Overlap(sim, e, site) && e.order == CommandKind.Hold),
                "outer ranks also make room when a held army surrounds the site " + faction);

            // Moving and gathering units retain their original work and queued actions.
            sim = Fixture(faction); worker = Unit(sim, new Point(18.5f, 20.5f)); blocker = Unit(sim, new Point(20.5f, 20.5f)); Steps(sim, 4);
            sim.Submit(Order(blocker, CommandKind.Move, new Point(30.5f, 20.5f)));
            sim.Submit(Order(blocker, CommandKind.Move, new Point(30.5f, 27.5f), append: true));
            Point destination = blocker.destination; var queued = blocker.pendingOrders[0];
            sim.Submit(Order(worker, CommandKind.Build, origin, supply.id));
            check(blocker.order == CommandKind.Move && blocker.destination.Distance(destination) == 0 && blocker.pendingOrders.Single() == queued, "sidestep preserves active movement and queued destination " + faction);
            check(Until(sim, () => blocker.order == CommandKind.Stop && blocker.pendingOrders.Count == 0) && blocker.position.Distance(queued.position) < .2f, "unit resumes and completes original movement queue " + faction);
            sim = Fixture(faction); worker = Unit(sim, new Point(18.5f, 20.5f)); blocker = Unit(sim, new Point(20.5f, 20.5f)); Steps(sim, 4);
            var mineral = sim.Resources.First(r => r.kind == "mineral");
            // Reveal the deposit before issuing the real gather command.
            var scout = Unit(sim, mineral.position, "scout"); Steps(sim, 4);
            check(sim.Submit(Order(blocker, CommandKind.Gather, target: mineral.id)).accepted, "gathering blocker has a real resource assignment " + faction);
            blocker.cargo = 3; blocker.cargoKind = "mineral";
            sim.Submit(Order(worker, CommandKind.Build, origin, supply.id));
            check(blocker.order == CommandKind.Gather && blocker.resourceId == mineral.id && blocker.cargo == 3, "clearance keeps worker resource and carried minerals " + faction);
            check(Until(sim, () => blocker.clearingSite == 0, 200) && blocker.order == CommandKind.Gather && blocker.resourceId == mineral.id, "worker resumes gathering after clearance " + faction);

            // Cancellation and destroyed foundations release reservations and temporary travel.
            foreach (bool cancel in new[] { true, false })
            {
                sim = Fixture(faction); worker = Unit(sim, new Point(18.5f, 20.5f)); blocker = Unit(sim, new Point(20.5f, 20.5f), "siege"); blocker.order = CommandKind.Hold; Steps(sim, 4);
                sim.Submit(Order(worker, CommandKind.Build, origin, supply.id)); site = sim.Entities.Values.Single(e => e.definition == supply.id);
                if (cancel) sim.Submit(Order(site, CommandKind.Cancel)); else { site.health = 0; sim.Step(); }
                check(!sim.Entities.ContainsKey(site.id) && blocker.clearingSite == 0 && blocker.order == CommandKind.Hold && sim.Map.CanPlace(origin, supply.footprint),
                    (cancel ? "cancelled" : "destroyed") + " foundation restores terrain and unit order " + faction);
            }

            sim = Fixture(faction); worker = Unit(sim, new Point(18.5f, 20.5f)); blocker = Unit(sim, new Point(20.5f, 20.5f), owner: 1); Steps(sim, 4); cash = sim.Players[0].minerals; start = blocker.position;
            check(!sim.Submit(Order(worker, CommandKind.Build, origin, supply.id)).accepted && sim.Players[0].minerals == cash && blocker.position.Distance(start) == 0,
                "enemy obstruction still rejects without payment or displacement " + faction);
            sim = Fixture(faction); worker = Unit(sim, new Point(18.5f, 20.5f)); blocker = Unit(sim, new Point(20.5f, 20.5f)); Steps(sim, 4);
            sim.Map.Buildable[sim.Map.Cell(origin)] = false;
            check(!sim.Submit(Order(worker, CommandKind.Build, origin, supply.id)).accepted && blocker.clearingSite == 0, "allies do not bypass terrain restrictions " + faction);

            sim = Fixture(faction); worker = Unit(sim, new Point(18.5f, 20.5f)); blocker = Unit(sim, new Point(20.5f, 20.5f)); Steps(sim, 4); cash = sim.Players[0].minerals;
            // A legal foundation surrounded by impassable cells has no safe evacuation route.
            for (int y = 19; y <= 22; y++) for (int x = 19; x <= 22; x++)
                if (x == 19 || x == 22 || y == 19 || y == 22) sim.Map.Walkable[sim.Map.Index(x, y)] = false;
            check(!sim.Submit(Order(worker, CommandKind.Build, origin, supply.id)).accepted && sim.Players[0].minerals == cash && sim.Map.CanPlace(origin, supply.footprint)
                && blocker.clearingSite == 0, "no exit rejects before charging or reserving terrain " + faction);

            sim = Fixture(faction); worker = Unit(sim, new Point(18.5f, 20.5f)); blocker = Unit(sim, new Point(20.5f, 20.5f), "siege"); blocker.order = CommandKind.Hold; Steps(sim, 4);
            sim.Submit(Order(worker, CommandKind.Build, origin, supply.id)); site = sim.Entities.Values.Single(e => e.definition == supply.id);
            var entrant = Unit(sim, new Point(21.5f, 21.5f)); entrant.order = CommandKind.Hold;
            start = blocker.position; int tickBeforePause = sim.Tick; sim.Paused = true; Steps(sim, 10);
            check(sim.Tick == tickBeforePause && blocker.position.Distance(start) == 0 && site.remaining == supply.duration, "pause freezes automatic clearance and construction " + faction);
            sim.Paused = false;
            check(Until(sim, () => !site.awaitingClearance, 300) && !Overlap(sim, entrant, site) && entrant.order == CommandKind.Hold, "ally entering a reserved site is also moved out safely " + faction);

            sim = Fixture(faction); worker = Unit(sim, new Point(18.5f, 20.5f)); blocker = Unit(sim, new Point(20.5f, 20.5f), "siege"); blocker.order = CommandKind.Hold; Steps(sim, 4);
            sim.Submit(Order(worker, CommandKind.Build, origin, supply.id)); site = sim.Entities.Values.Single(e => e.definition == supply.id);
            var enemy = Unit(sim, new Point(21.5f, 21.5f), owner: 1); enemy.order = CommandKind.Hold;
            Steps(sim, 4);
            check(sim.View(0).entities.First(e => e.id == site.id).awaitingClearance && !sim.View(1).entities.First(e => e.id == site.id).awaitingClearance,
                "clearance status is available only to the building owner " + faction);
            Steps(sim, 100);
            check(site.awaitingClearance && site.remaining == supply.duration && enemy.clearingSite == 0 && sim.Map.CanStand(enemy.position, sim.Type(enemy).collisionRadius),
                "enemy entering later pauses foundation without displacing or trapping it " + faction);
            enemy.health = 0; blocker.health = 0;
            check(Until(sim, () => site.complete), "dead obstructions do not leave construction waiting forever " + faction);

            // Queued construction revalidates and clears blockers only when it becomes active.
            sim = Fixture(faction); worker = Unit(sim, new Point(17.5f, 20.5f)); blocker = Unit(sim, new Point(20.5f, 20.5f), "siege"); blocker.order = CommandKind.Hold; Steps(sim, 4); cash = sim.Players[0].minerals;
            sim.Submit(Order(worker, CommandKind.Move, new Point(18.5f, 20.5f)));
            check(sim.Submit(Order(worker, CommandKind.Build, origin, supply.id, true)).accepted && blocker.clearingSite == 0 && sim.Players[0].minerals == cash, "queued construction leaves units and resources untouched until activation " + faction);
            check(Until(sim, () => sim.Entities.Values.Any(e => e.definition == supply.id && e.complete)) && !Overlap(sim, blocker, sim.Entities.Values.Single(e => e.definition == supply.id)),
                "queued build activates clearance and completes normally " + faction);
        }
    }
}
