using Frontier.Core;

static class CollisionChecks
{
    public static void Run(Catalog catalog, Action<bool, string> check)
    {
        foreach (string faction in new[] { "industrial", "biological" })
        {
            // Give the scouts older IDs/orders: physical weight, not creation order,
            // must decide which opposing flow makes room at a narrow entrance.
            var sim = Fixture(catalog, faction);
            Lane(sim, 20, 32);
            var scouts = Enumerable.Range(0, 6).Select(i => Unit(sim, 28.5f + i, 20.5f, "scout")).ToArray();
            var heavy = Unit(sim, 22.5f, 20.5f, "siege");
            Move(sim, scouts, new Point(14.5f, 20.5f));
            Move(sim, new[] { heavy }, new Point(39.5f, 20.5f));
            var traffic = scouts.Append(heavy).ToArray();
            var destinations = traffic.Select(e => e.destination).ToArray();
            int heavyPassed = -1, scoutPassed = -1;
            for (int tick = 0; tick < 800; tick++)
            {
                Step(sim, traffic, "weighted head-on traffic " + faction);
                if (heavyPassed < 0 && heavy.position.x > 33) heavyPassed = sim.Tick;
                if (scoutPassed < 0 && scouts.Any(e => e.position.x < 20)) scoutPassed = sim.Tick;
                if (traffic.Where((e, i) => e.position.Distance(destinations[i]) > .15f).Count() == 0) break;
            }
            check(heavyPassed > 0 && (scoutPassed < 0 || heavyPassed < scoutPassed),
                faction + ": heavier artillery gets passage before an older light opposing flow");
            check(traffic.Where((e, i) => e.position.Distance(destinations[i]) > .15f).Count() == 0,
                faction + ": heavy and light opposing flows both finish without overlap");
            check(traffic.Where((e, i) => e.destination.Distance(destinations[i]) > .001f).Count() == 0,
                faction + ": collision yielding preserves the player's destinations");

            sim = Fixture(catalog, faction);
            Lane(sim, 20, 32);
            var light = Unit(sim, 22.5f, 20.5f, "scout");
            heavy = Unit(sim, 24.5f, 20.5f, "siege");
            Point parkedHeavy = heavy.position;
            Move(sim, new[] { light }, new Point(29.5f, 20.5f));
            for (int tick = 0; tick < 120; tick++) Step(sim, new[] { light, heavy }, "light cannot push heavy " + faction);
            check(heavy.position.Distance(parkedHeavy) < .001f && heavy.order == CommandKind.Stop,
                faction + ": a light scout cannot push stationary artillery in a forced lane");

            sim = Fixture(catalog, faction);
            Lane(sim, 20, 32);
            light = Unit(sim, 24.5f, 20.5f, "scout");
            heavy = Unit(sim, 22.5f, 20.5f, "siege");
            Point parkedLight = light.position;
            Move(sim, new[] { heavy }, new Point(29.5f, 20.5f));
            for (int tick = 0; tick < 300; tick++) Step(sim, new[] { light, heavy }, "heavy pushes light " + faction);
            check(light.position.Distance(parkedLight) > 1 && heavy.position.Distance(heavy.destination) <= .15f,
                faction + ": artillery can push a passive scout aside and complete its Move");

            sim = Fixture(catalog, faction);
            var held = Unit(sim, 23.5f, 20.5f, "scout");
            heavy = Unit(sim, 20.5f, 20.5f, "siege");
            sim.Submit(new GameCommand { player = 0, kind = CommandKind.Hold, entities = new[] { held.id } });
            Point holdPosition = held.position;
            Move(sim, new[] { heavy }, new Point(28.5f, 20.5f));
            for (int tick = 0; tick < 300; tick++) Step(sim, new[] { held, heavy }, "hold resists mass " + faction);
            check(held.position.Distance(holdPosition) < .001f && held.order == CommandKind.Hold,
                faction + ": Hold Position resists a heavier unit without changing its order");
            check(heavy.position.Distance(heavy.destination) <= .15f,
                faction + ": artillery routes around a lighter Hold Position unit");

            sim = Fixture(catalog, faction);
            var micro = Unit(sim, 20.5f, 20.5f, "scout");
            Move(sim, new[] { micro }, new Point(60.5f, 20.5f));
            Point start = micro.position;
            Step(sim, new[] { micro }, "acceleration first tick " + faction);
            float firstStep = micro.position.x - start.x;
            check(firstStep > .0001f && firstStep < sim.Type(micro).speed / Simulation.TickRate - .001f,
                faction + ": a fresh Move accelerates on the first tick instead of jumping to full speed");
            for (int tick = 0; tick < 30; tick++) Step(sim, new[] { micro }, "accelerating " + faction);
            Point beforeCruise = micro.position;
            Step(sim, new[] { micro }, "cruise " + faction);
            float cruiseStep = micro.position.x - beforeCruise.x;
            Move(sim, new[] { micro }, new Point(10.5f, 20.5f));
            Point beforeReverse = micro.position;
            Step(sim, new[] { micro }, "reverse first tick " + faction);
            float reverseStep = micro.position.x - beforeReverse.x;
            check(reverseStep < cruiseStep - .0001f,
                faction + ": replacing Move begins braking on the next simulation tick");
            bool reversed = reverseStep < -.0001f;
            for (int tick = 0; tick < Simulation.TickRate && !reversed; tick++)
            {
                Point previous = micro.position;
                Step(sim, new[] { micro }, "micro reversal " + faction);
                reversed = micro.position.x < previous.x - .0001f;
            }
            check(reversed, faction + ": a light unit reverses promptly after braking for a new Move");

            // Reissuing Move under a moving unit still needs a physical braking
            // phase: reaching the arrival radius must not erase its current velocity.
            sim = Fixture(catalog, faction);
            var shortMove = Unit(sim, 20.5f, 20.5f, "siege");
            Move(sim, new[] { shortMove }, new Point(60.5f, 20.5f));
            for (int tick = 0; tick < 30; tick++) Step(sim, new[] { shortMove }, "before short Move " + faction);
            for (int tick = 0; tick < 80 && shortMove.position.Distance(sim.Map.Center(sim.Map.Cell(shortMove.position))) > .08f; tick++)
                Step(sim, new[] { shortMove }, "align short Move " + faction);
            Point cruisingVelocity = shortMove.velocity;
            Move(sim, new[] { shortMove }, shortMove.position);
            Point shortDestination = shortMove.destination;
            check(shortMove.position.Distance(shortDestination) <= .1f && cruisingVelocity.Distance(default) > 1,
                faction + ": short Move regression starts at cruise speed inside the new arrival radius");
            Step(sim, new[] { shortMove }, "short Move first braking tick " + faction);
            float decelerationStep = sim.Type(shortMove).deceleration / Simulation.TickRate;
            check(shortMove.velocity.Distance(cruisingVelocity) <= decelerationStep + .005f && shortMove.velocity.Distance(default) > .001f,
                faction + ": Move under cruising artillery brakes without erasing momentum");
            bool smoothBraking = true;
            for (int tick = 0; tick < 300 && shortMove.order != CommandKind.Stop; tick++)
            {
                Point velocity = shortMove.velocity;
                Step(sim, new[] { shortMove }, "short Move settles " + faction);
                if (shortMove.velocity.Distance(velocity) > decelerationStep + .005f) smoothBraking = false;
            }
            check(smoothBraking && shortMove.order == CommandKind.Stop && shortMove.position.Distance(shortDestination) <= .15f,
                faction + ": short Move brakes smoothly and returns to its assigned position");

            foreach (string role in new[] { "scout", "siege" })
            {
                sim = Fixture(catalog, faction);
                var diagonal = Unit(sim, 20.5f, 20.5f, role);
                Point diagonalStart = diagonal.position;
                Move(sim, new[] { diagonal }, new Point(58.5f, 37.5f));
                Point diagonalGoal = diagonal.destination;
                Point direction = Point.Towards(default, new Point(diagonalGoal.x - diagonalStart.x, diagonalGoal.y - diagonalStart.y), 1);
                Step(sim, new[] { diagonal }, "diagonal first tick " + faction + " " + role);
                Point firstDelta = new Point(diagonal.position.x - diagonalStart.x, diagonal.position.y - diagonalStart.y);
                check(firstDelta.x > 0 && firstDelta.y > 0 && Math.Abs(firstDelta.x * direction.y - firstDelta.y * direction.x) < .002f,
                    faction + ": " + role + " follows its diagonal Move target from the first tick");
                bool straight = true, boundedAcceleration = true;
                float accelerationLimit = Math.Max(sim.Type(diagonal).acceleration, sim.Type(diagonal).deceleration) / Simulation.TickRate;
                for (int tick = 0; tick < 800; tick++)
                {
                    Point previous = diagonal.position, velocity = diagonal.velocity;
                    Step(sim, new[] { diagonal }, "diagonal travel " + faction + " " + role);
                    Point displacement = new Point(diagonal.position.x - previous.x, diagonal.position.y - previous.y);
                    Point fromStart = new Point(diagonal.position.x - diagonalStart.x, diagonal.position.y - diagonalStart.y);
                    if (Math.Abs(displacement.x * direction.y - displacement.y * direction.x) > .002f
                        || Math.Abs(fromStart.x * direction.y - fromStart.y * direction.x) > .01f) straight = false;
                    if (diagonal.velocity.Distance(velocity) > accelerationLimit + .005f) boundedAcceleration = false;
                    if (diagonal.order == CommandKind.Stop && diagonal.velocity.Distance(default) < .001f) break;
                }
                check(straight && diagonal.order == CommandKind.Stop && diagonal.position.Distance(diagonalGoal) <= .15f,
                    faction + ": " + role + " reaches an unobstructed diagonal target without grid zigzags");
                check(boundedAcceleration,
                    faction + ": " + role + " accelerates and brakes within configured limits throughout diagonal travel");

                sim = Fixture(catalog, faction);
                var stopping = Unit(sim, 20.5f, 20.5f, role);
                Move(sim, new[] { stopping }, new Point(60.5f, 20.5f));
                for (int tick = 0; tick < 30; tick++) Step(sim, new[] { stopping }, "before Stop " + faction);
                float movingSpeed = stopping.velocity.Distance(default);
                Point beforeStop = stopping.position;
                sim.Submit(new GameCommand { player = 0, kind = CommandKind.Stop, entities = new[] { stopping.id } });
                Step(sim, new[] { stopping }, "Stop brakes " + faction);
                check(stopping.position.x > beforeStop.x && stopping.velocity.Distance(default) < movingSpeed - .001f,
                    faction + ": Stop immediately brakes " + role + " without deleting momentum");
                for (int tick = 0; tick < Simulation.TickRate; tick++) Step(sim, new[] { stopping }, "Stop settles " + faction);
                Point rest = stopping.position;
                Step(sim, new[] { stopping }, "stays stopped " + faction);
                check(stopping.position.Distance(rest) < .001f && stopping.order == CommandKind.Stop,
                    faction + ": " + role + " stays still after Stop finishes braking");
                Move(sim, new[] { stopping }, new Point(60.5f, 20.5f));
                for (int tick = 0; tick < 30; tick++) Step(sim, new[] { stopping }, "before Hold " + faction);
                rest = stopping.position;
                sim.Submit(new GameCommand { player = 0, kind = CommandKind.Hold, entities = new[] { stopping.id } });
                Step(sim, new[] { stopping }, "Hold anchors " + faction);
                check(stopping.position.Distance(rest) < .001f && stopping.order == CommandKind.Hold,
                    faction + ": Hold anchors a moving " + role + " immediately");
            }

            sim = Fixture(catalog, faction);
            var yielding = Unit(sim, 25.5f, 20.5f, "scout");
            heavy = Unit(sim, 23.5f, 20.5f, "siege");
            Move(sim, new[] { yielding }, new Point(15.5f, 20.5f));
            Move(sim, new[] { heavy }, new Point(35.5f, 20.5f));
            for (int tick = 0; tick < 100 && yielding.yieldUntil <= sim.Tick; tick++)
                Step(sim, new[] { yielding, heavy }, "begin yield " + faction);
            check(yielding.yieldUntil > sim.Tick, faction + ": crossing traffic exercises a real yielding unit");
            Move(sim, new[] { yielding }, new Point(yielding.position.x, 32.5f));
            check(yielding.yieldUntil <= sim.Tick && yielding.order == CommandKind.Move,
                faction + ": explicit Move immediately replaces automatic yielding");
            Point previousVelocity = yielding.velocity;
            Point previousPosition = yielding.position;
            Point newDirection = Point.Towards(default, new Point(yielding.destination.x - previousPosition.x, yielding.destination.y - previousPosition.y), 1);
            Step(sim, new[] { yielding, heavy }, "override yield " + faction);
            Point changedVelocity = new Point((yielding.position.x - previousPosition.x) * Simulation.TickRate - previousVelocity.x,
                (yielding.position.y - previousPosition.y) * Simulation.TickRate - previousVelocity.y);
            check(changedVelocity.x * newDirection.x + changedVelocity.y * newDirection.y > .0001f,
                faction + ": a new Move steers or brakes toward its target on the first tick during yielding");
        }
    }

    private static Simulation Fixture(Catalog catalog, string faction)
    {
        var map = new MapDefinition
        {
            id = "collision-fixture", title = "Collision regression", width = 80, height = 48,
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

    private static void Lane(Simulation sim, int from, int to)
    {
        for (int x = from; x <= to; x++)
            for (int y = 0; y < sim.Map.Height; y++)
                if (y != 20) sim.Map.Walkable[sim.Map.Index(x, y)] = false;
    }

    private static EntityState Unit(Simulation sim, float x, float y, string role)
    {
        var type = sim.Catalog.Role(sim.Players[0].faction, role);
        var unit = new EntityState { id = sim.Entities.Keys.Max() + 1, owner = 0, definition = type.id, position = new Point(x, y), health = type.health };
        sim.Entities.Add(unit.id, unit);
        return unit;
    }

    private static void Move(Simulation sim, IEnumerable<EntityState> units, Point destination)
    {
        if (!sim.Submit(new GameCommand { player = 0, kind = CommandKind.Move, entities = units.Select(e => e.id).ToArray(), position = destination }).accepted)
            throw new Exception("Collision fixture rejected its Move order.");
    }

    private static void Step(Simulation sim, EntityState[] units, string scenario)
    {
        var before = units.Select(e => e.position).ToArray();
        sim.Step();
        for (int i = 0; i < units.Length; i++)
        {
            var unit = units[i];
            var type = sim.Type(unit);
            if (!sim.Map.CanStand(unit.position, type.collisionRadius)
                || !sim.Map.Traversable(sim.Map.Cell(before[i]), sim.Map.Cell(unit.position)))
                throw new Exception(scenario + ": collision entered solid terrain or crossed a cliff");
            if (before[i].Distance(unit.position) > type.speed / Simulation.TickRate + .002f)
                throw new Exception(scenario + ": collision exceeded movement speed");
            for (int j = i + 1; j < units.Length; j++)
                if (unit.position.Distance(units[j].position) < type.collisionRadius + sim.Type(units[j]).collisionRadius - .001f)
                    throw new Exception(scenario + ": unit bodies overlap");
        }
    }
}
