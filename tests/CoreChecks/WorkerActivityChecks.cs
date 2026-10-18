using Frontier.Core;

static class WorkerActivityChecks
{
    public static void Run(Catalog catalog, MapDefinition map, Action<bool, string> check)
    {
        foreach (string faction in new[] { "industrial", "biological" })
        {
            var sim = new Simulation(map, catalog, faction);
            var worker = sim.Entities.Values.First(e => e.owner == 0 && sim.Type(e).role == "worker");
            var mineral = sim.Resources.First(r => r.id == worker.resourceId);
            VisibleEntity Read() => sim.View(0).entities.First(e => e.id == worker.id);
            worker.position = new Point(mineral.position.x - 1.2f, mineral.position.y);
            worker.cargo = 3; worker.cargoKind = "mineral";
            check(Read().working && Read().cargo == 3 && Read().cargoKind == "mineral", "worker view exposes active extraction and cargo " + faction);
            worker.position = new Point(mineral.position.x - 5, mineral.position.y);
            check(!Read().working, "approaching a mineral does not play extraction " + faction);
            worker.position = new Point(mineral.position.x - 1.2f, mineral.position.y);
            worker.velocity = new Point(5, 0);
            check(!Read().working, "braking at a deposit does not play extraction " + faction);
            worker.velocity = default; worker.yieldUntil = sim.Tick + 20;
            check(!Read().working, "yielding worker does not play extraction " + faction);
            worker.yieldUntil = 0; worker.depositing = true;
            check(!Read().working && Read().depositing, "delivery near a deposit is not extraction " + faction);
            worker.depositing = false; worker.cargo = 8;
            check(!Read().working && Read().depositing, "full cargo immediately presents return phase " + faction);
            worker.cargo = 0; mineral.remaining = 0;
            check(!Read().working, "exhausted mineral stops work effects " + faction);
            var gas = sim.Resources.First(r => r.kind == "catalyst");
            worker.target = worker.resourceId = gas.id;
            worker.position = new Point(gas.position.x - 2.5f, gas.position.y);
            check(!Read().working, "gas without extractor never shows extraction " + faction);
            var type = catalog.Role(faction, "extractor");
            var building = new EntityState { id = 900, owner = 0, definition = type.id, health = type.health,
                position = new Point(gas.position.x - type.footprint * .5f, gas.position.y - type.footprint * .5f) };
            sim.Entities.Add(building.id, building);
            check(Read().working, "operating extractor enables gas animation " + faction);
            worker.cargo = 5; worker.cargoKind = "catalyst";
            var other = sim.Resources.First(r => r.kind == "mineral" && r.remaining > 0 && r.position.Distance(worker.position) < Harvesting.SearchRadius);
            check(sim.Submit(new GameCommand { player = 0, kind = CommandKind.Gather, entities = new[] { worker.id }, target = other.id }).accepted,
                "cargo reassignment accepted " + faction);
            check(Read().cargoKind == "catalyst" && Read().depositing && !Read().working, "reassigned worker retains gas cargo during delivery " + faction);
            worker.order = CommandKind.Build; worker.target = building.id; building.complete = false;
            check(Read().working && !Read().depositing, "construction exposes active tool without stale delivery " + faction);
            building.complete = true;
            check(!Read().working, "completed building stops construction effect " + faction);
            worker.order = CommandKind.Repair; building.health -= 20;
            check(Read().working, "damaged building enables repair effect " + faction);
            sim.Players[0].minerals = 0;
            check(!Read().working, "unfunded repair does not pretend to repair " + faction);
            sim.Players[0].minerals = 50; building.health = type.health;
            check(!Read().working, "full health stops repair effect " + faction);
            worker.order = CommandKind.Stop;
            check(!Read().working && !Read().depositing && Read().cargo == 5, "idle worker retains carried cargo without working " + faction);
            sim.Paused = true; var before = Read(); sim.Step();
            check(Read().cargo == before.cargo && Read().working == before.working, "pause preserves worker presentation state " + faction);
            sim.Paused = false;
            var enemy = sim.Entities.Values.First(e => e.owner == 1 && sim.Type(e).role == "worker");
            enemy.position = new Point(worker.position.x + 2, worker.position.y); enemy.cargo = 8; enemy.cargoKind = "catalyst";
            enemy.order = CommandKind.Gather; enemy.target = enemy.resourceId = gas.id; enemy.depositing = true;
            for (int i = 0; i < 4; i++) sim.Step();
            var visibleEnemy = sim.View(0).entities.First(e => e.id == enemy.id);
            check(visibleEnemy.cargo == 0 && visibleEnemy.cargoKind == null && !visibleEnemy.depositing && !visibleEnemy.working && visibleEnemy.target == 0,
                "worker presentation does not leak enemy assignments or cargo " + faction);
        }
    }
}
