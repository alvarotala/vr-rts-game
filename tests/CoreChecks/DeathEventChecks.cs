using Frontier.Core;

static class DeathEventChecks
{
    public static void Run(Catalog catalog, MapDefinition map, Action<bool, string> check)
    {
        foreach (string faction in new[] { "industrial", "biological" })
        {
            var sim = new Simulation(map, catalog, faction);
            var worker = sim.Entities.Values.First(e => e.owner == 0 && sim.Type(e).role == "worker");
            var enemy = sim.Entities.Values.First(e => e.owner == 1 && sim.Type(e).role == "worker");
            foreach (var unit in sim.Entities.Values) unit.order = CommandKind.Hold;
            int units = sim.Units(0), enemyId = enemy.id; Point enemyPosition = enemy.position;
            worker.health = 0; enemy.health = 0; sim.Step();
            var death = sim.View(0).deaths.Single();
            check(death.unit == worker.id && death.definition == worker.definition && death.tick == sim.Tick && death.position.Distance(worker.position) < .001f,
                "death retains identity, model, location and tick after removal " + faction);
            check(!sim.Entities.ContainsKey(worker.id) && sim.Units(0) == units - 1 && !sim.View(0).entities.Any(e => e.id == worker.id),
                "dead units immediately leave simulation, supply and selectable entities " + faction);
            check(!sim.View(0).deaths.Any(e => e.unit == enemyId) && sim.View(1).deaths.Single().unit == enemyId,
                "unseen enemy death is private to its owner " + faction);
            var scout = sim.Entities.Values.First(e => e.owner == 0 && !sim.Type(e).building);
            scout.position = enemyPosition;
            for (int i = 0; i < 4; i++) sim.Step();
            check(sim.Visible(0, enemyPosition) && !sim.View(0).deaths.Any(e => e.unit == enemyId),
                "arriving later never reveals an unwitnessed enemy corpse " + faction);
            sim.Paused = true; int tick = sim.Tick; sim.Step();
            check(sim.Tick == tick && sim.View(0).deaths.Single().unit == worker.id, "pause preserves death history " + faction);
            sim.Paused = false;
            while (sim.Tick < death.tick + Simulation.DeathHistoryTicks) sim.Step();
            check(sim.View(0).deaths.Length == 0, "death history expires after six simulation seconds " + faction);

            sim = new Simulation(map, catalog, faction);
            foreach (var unit in sim.Entities.Values) unit.order = CommandKind.Hold;
            var own = sim.Entities.Values.First(e => e.owner == 0 && !sim.Type(e).building);
            enemy = sim.Entities.Values.First(e => e.owner == 1 && !sim.Type(e).building);
            enemy.position = own.position;
            for (int i = 0; i < 4; i++) sim.Step();
            check(sim.View(0).entities.Any(e => e.id == enemy.id), "enemy is visible before its death " + faction);
            enemy.health = 1;
            check(sim.Submit(new GameCommand { player = 0, kind = CommandKind.Attack, entities = new[] { own.id }, target = enemy.id }).accepted,
                "lethal combat order is accepted " + faction);
            for (int i = 0; i < 40 && sim.Entities.ContainsKey(enemy.id); i++) sim.Step();
            check(sim.View(0).deaths.Single().unit == enemy.id, "witnessed enemy death reaches the observer " + faction);
            for (int i = 0; i < 3; i++) sim.Step();
            check(sim.View(0).deaths.Count(e => e.unit == enemy.id) == 1, "one casualty creates exactly one event " + faction);

            var building = sim.Entities.Values.First(e => e.owner == 0 && sim.Type(e).building);
            building.complete = false;
            check(sim.Submit(new GameCommand { player = 0, kind = CommandKind.Cancel, entities = new[] { building.id } }).accepted,
                "construction can still be cancelled " + faction);
            check(!sim.View(0).deaths.Any(e => e.unit == building.id), "refund and other noncombat removal never create corpses " + faction);
        }
    }
}
