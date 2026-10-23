using Frontier.Core;

static class GuidedTutorialChecks
{
    public static void Run(Catalog catalog, MapDefinition map, Action<bool, string> check)
    {
        foreach (string faction in new[] { "industrial", "biological" })
        foreach (bool touch in new[] { false, true })
        {
            string label = faction + (touch ? " Touch" : " desktop");
            var sim = new Simulation(map, catalog, faction, faction == "industrial" ? "biological" : "industrial");
            var guide = new GuidedTutorial(sim, touch);
            int[] selected = Array.Empty<int>();
            var seen = new HashSet<TutorialStep>();
            var issued = new HashSet<TutorialStep>();
            check(sim.Tick == 0 && sim.Players[0].minerals == 400 && sim.Players[0].catalyst == 0
                && sim.Units(0) == 6 && sim.Units(1) == 6 && sim.Buildings(1) == 1 && sim.Capacity(0) == 10,
                label + ": tutorial construction preserves ordinary match rules and opening state");
            var initialOrders = sim.Entities.Values.Select(e => e.order).ToArray();
            guide.Observe(new[] { Own("worker").id });
            guide.NotifyMenu(true); guide.NotifyWheel(); guide.NotifyGroup(0, true);
            check(guide.Step == TutorialStep.Welcome && guide.Briefing && sim.Tick == 0
                && sim.Entities.Values.Select(e => e.order).SequenceEqual(initialOrders),
                label + ": briefing ignores early controls and does not advance or mutate simulation");
            for (int frame = 0; frame < 18000 && !guide.Complete; frame++)
            {
                var step = guide.Step;
                if (seen.Add(step))
                {
                    check(guide.Briefing && !guide.AdvanceSimulation && !guide.Allows(new GameCommand { player = 0, kind = CommandKind.Surrender }),
                        label + ": " + step + " starts paused with command gating");
                    check(!guide.Allows(new GameCommand { player = 0, kind = CommandKind.Move, entities = new[] { Own("worker").id }, position = new Point(1, 1) }),
                        label + ": " + step + " cannot be bypassed by an unrelated order");
                }
                if (guide.Briefing) { guide.Continue(); continue; }
                if (!issued.Contains(step))
                {
                    check(!guide.AdvanceSimulation, label + ": " + step + " freezes economy while waiting for the demonstrated action");
                    switch (step)
                    {
                        case TutorialStep.SelectWorker:
                            guide.Observe(new[] { sim.Entities.Values.First(e => e.owner == 1 && sim.Type(e).role == "worker").id });
                            check(guide.Step == step, label + ": enemy selection cannot fulfill worker objective");
                            selected = new[] { Own("worker").id };
                            break;
                        case TutorialStep.MoveWorker:
                            selected = new[] { guide.FocusEntity };
                            Send(CommandKind.Move, guide.FocusPoint.Value);
                            break;
                        case TutorialStep.GatherMinerals: case TutorialStep.GatherGas:
                            selected = new[] { guide.FocusEntity };
                            Send(CommandKind.Gather, target: guide.FocusResource);
                            guide.Observe(selected);
                            check(guide.Step == step, label + ": accepting gather is insufficient before a delivery");
                            break;
                        case TutorialStep.SelectHeadquarters:
                            selected = new[] { guide.FocusEntity };
                            break;
                        case TutorialStep.TrainWorker:
                            // A short, isolated affordability probe restores the exact
                            // balance before the ordinary tutorial route continues.
                            int minerals = sim.Players[0].minerals;
                            sim.Players[0].minerals = catalog[guide.ExpectedDefinition].minerals - 1;
                            check(guide.AdvanceSimulation, label + ": an unaffordable lesson permits existing workers to earn its cost");
                            sim.Players[0].minerals = minerals;
                            check(!guide.AdvanceSimulation, label + ": affordability stops idle income once the lesson can be performed");
                            Send(CommandKind.Train, definition: guide.ExpectedDefinition);
                            check(!guide.Allows(Command(CommandKind.Train, definition: guide.ExpectedDefinition)), label + ": worker lesson limits production to one worker");
                            break;
                        case TutorialStep.OpenMenu:
                            guide.NotifyMenu(false);
                            check(guide.Step == step, label + ": closing an unopened menu cannot satisfy opening it");
                            guide.NotifyMenu(true);
                            break;
                        case TutorialStep.ResumeMenu: guide.NotifyMenu(false); break;
                        case TutorialStep.OpenWheel: guide.NotifyWheel(); break;
                        case TutorialStep.BuildSupply: case TutorialStep.BuildBarracks: case TutorialStep.BuildExtractor:
                            selected = new[] { Own("worker").id };
                            Point point = guide.FocusPoint.Value;
                            int size = catalog[guide.ExpectedDefinition].footprint;
                            var origin = step == TutorialStep.BuildExtractor
                                ? new Point((float)Math.Floor(point.x - size * .5f), (float)Math.Floor(point.y - size * .5f))
                                : new Point((float)Math.Floor(point.x), (float)Math.Floor(point.y));
                            check(!guide.Allows(Command(CommandKind.Build, new Point(origin.x + 1, origin.y), definition: guide.ExpectedDefinition)),
                                label + ": " + step + " requires the marked foundation");
                            Send(CommandKind.Build, origin, definition: guide.ExpectedDefinition);
                            check(!guide.Allows(Command(CommandKind.Stop)), label + ": construction cannot be abandoned during its lesson");
                            break;
                        case TutorialStep.TrainArmy:
                            selected = new[] { Own("barracks").id };
                            for (int i = 0; i < GuidedTutorial.ArmySize; i++) Send(CommandKind.Train, definition: guide.ExpectedDefinition);
                            check(!guide.Allows(Command(CommandKind.Train, definition: guide.ExpectedDefinition)), label + ": army lesson reserves exactly three ranged units");
                            break;
                        case TutorialStep.SelectArmy:
                            selected = Army();
                            guide.Observe(selected.Take(2));
                            check(guide.Step == step, label + ": partial army selection does not skip selection practice");
                            break;
                        case TutorialStep.SaveGroup:
                            guide.NotifyGroup(1, true); guide.Observe(selected);
                            check(guide.Step == step, label + ": tutorial practices the first group specifically");
                            guide.NotifyGroup(0, true); guide.Observe(selected.Take(2)); guide.Observe(selected);
                            check(guide.Step == step, label + ": a partial saved group must be saved again after correcting selection");
                            guide.NotifyGroup(0, true);
                            break;
                        case TutorialStep.RecallGroup:
                            selected = Array.Empty<int>(); guide.Observe(selected);
                            check(guide.Step == step, label + ": recall requires a real recall event and army selection");
                            guide.NotifyGroup(0, false); guide.Observe(selected);
                            check(guide.Step == step, label + ": an empty recalled group cannot satisfy the objective");
                            selected = Army();
                            guide.Observe(selected);
                            check(guide.Step == step, label + ": manual selection cannot retroactively satisfy a failed recall");
                            guide.NotifyGroup(0, false);
                            break;
                        case TutorialStep.AttackMove:
                            Send(CommandKind.AttackMove, guide.FocusPoint.Value);
                            guide.Observe(selected);
                            check(guide.Step == step, label + ": attack-move practice waits for the army to arrive");
                            break;
                        case TutorialStep.DestroyEnemy:
                            check(sim.Winner == -1 && sim.Buildings(1) == 1 && !sim.Visible(0, guide.FocusPoint.Value),
                                label + ": exploration waypoint cannot defeat or reveal the enemy base prematurely");
                            Send(CommandKind.AttackMove, guide.FocusPoint.Value);
                            break;
                    }
                    issued.Add(step);
                }
                guide.Observe(selected);
                if (guide.AdvanceSimulation) { sim.Step(); guide.Observe(selected); }
            }
            check(guide.Complete && !guide.AdvanceSimulation && sim.Winner == 0 && sim.Buildings(1) == 0 && guide.Progress == 1,
                label + ": complete guided route wins through ordinary combat without bots or stat changes");
            check(seen.Contains(TutorialStep.OpenWheel) == touch && seen.Count == guide.StepCount - 1,
                label + ": desktop omits only the Touch wheel lesson and every other lesson is reached");
            check(sim.Units(0) == 10 && sim.Capacity(0) == 20 && sim.Players[0].catalyst > 0
                && sim.Resources.Sum(r => r.remaining) < map.resources.Sum(r => r.amount),
                label + ": real harvesting, construction, supply and production support the full tutorial");
            check(catalog.Role(faction, "hq").health == 1400 && catalog.Role(faction, "ranged").duration == 260,
                label + ": tutorial does not change catalog combat or training rules");

            EntityState Own(string role) => sim.Entities.Values.First(e => e.owner == 0 && sim.Type(e).role == role);
            int[] Army() => sim.Entities.Values.Where(e => e.owner == 0 && sim.Type(e).role == "ranged").Select(e => e.id).ToArray();
            GameCommand Command(CommandKind kind, Point position = default, string definition = null, int target = 0)
                => new GameCommand { player = 0, kind = kind, entities = selected, position = position, definition = definition, target = target };
            void Send(CommandKind kind, Point position = default, string definition = null, int target = 0)
            {
                var command = Command(kind, position, definition, target);
                check(guide.Allows(command), label + ": " + guide.Step + " permits the demonstrated command");
                var result = sim.Submit(command);
                check(result.accepted, label + ": " + guide.Step + " is legal with ordinary rules: " + result.message);
                guide.OnAccepted(command);
                check(guide.AdvanceSimulation, label + ": " + guide.Step + " advances time for the accepted order's real result");
            }
        }
    }
}
