using System.Reflection;
using System.Text.Json;
using Frontier.Core;
using Frontier.Runtime;

internal static class Program
{
    private static int checks;
    private static Catalog catalog;
    private static MapDefinition training;
    private static string worker, soldier, headquarters;
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
    }
    private static void Select(GameSession session, params int[] ids)
    { session.Selection.Clear(); session.Selection.UnionWith(ids); }
    private static VisibleEntity Entity(int id, string definition = null) => new()
    { id = id, owner = 0, definition = definition ?? worker, health = 100, complete = true };
    private static GameSession Fixture() => new()
    {
        Catalog = catalog,
        View = new PlayerView { player = 0, entities = new[]
        { Entity(1), Entity(2, soldier), Entity(3, soldier), Entity(4, headquarters), Entity(5), Entity(6), Entity(7), Entity(8) } }
    };
    private static void Target(GameSession session)
    {
        session.BuildDefinition = headquarters; session.AttackMoveMode = true;
        session.AbilityDefinitionId = "fixture-ability"; session.RallyPointMode = true;
    }
    private static int[] Sizes(GameSession session) => Enumerable.Range(0, GameSession.ControlGroupCount).Select(session.GroupSize).ToArray();

    public static int Main()
    {
        try
        {
            var json = new JsonSerializerOptions { IncludeFields = true };
            T Read<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)), json);
            catalog = new Catalog(Read<CatalogDefinition>("catalog.json"));
            training = Read<MapDefinition>("training.json");
            worker = catalog.Role("industrial", "worker").id;
            soldier = catalog.Role("industrial", "ranged").id;
            headquarters = catalog.Role("industrial", "hq").id;
            CreateReplaceAndDuplicate();
            FilteringAndRecall();
            InvalidStates();
            AtomicSelection();
            TutorialRoute();
            Console.WriteLine($"Mobile group/selection checks passed: {checks} (production APIs and tutorial core; host state/effects only, no Unity/HUD validation).");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void CreateReplaceAndDuplicate()
    {
        var session = Fixture();
        int changes = 0, feedback = 0;
        session.Changed += () => changes++;
        session.Feedback += _ => feedback++;
        Select(session, 1, 2); Target(session);
        Check(session.CanSaveMobileGroup, "valid squad can create a group while leaving a targeting mode");
        Check(session.SaveMobileSelectionGroup() == 0 && session.GroupSize(0) == 2, "first group uses first empty slot");
        Check(session.Selection.SetEquals(new[] { 1, 2 }) && !session.HasTargetingMode, "saving preserves selection and cancels targeting");
        Check(changes == 1 && feedback == 1, "save delegates to shared group notifications once");
        int beforeChanges = changes, beforeFeedback = feedback;
        Select(session, 2, 1); Target(session);
        Check(session.SaveMobileSelectionGroup() == 0, "same membership in a different order reuses group");
        Check(Sizes(session).SequenceEqual(new[] { 2, 0, 0, 0 }) && !session.HasTargetingMode, "duplicate creates no extra slot and cancels targeting");
        Check(changes == beforeChanges && feedback == beforeFeedback, "duplicate does not repeat shared save notifications");

        Select(session, 3); Check(session.SaveMobileSelectionGroup() == 1, "next squad takes slot two");
        Select(session, 4); Check(session.SaveMobileSelectionGroup() == 2, "third squad takes slot three");
        Select(session, 5); Check(session.SaveMobileSelectionGroup() == 3, "fourth squad takes slot four");
        var full = Sizes(session); Select(session, 6); Target(session);
        Check(session.SaveMobileSelectionGroup() == -1 && Sizes(session).SequenceEqual(full), "full group bar never silently overwrites a squad");
        Check(session.HasTargetingMode && session.Selection.SetEquals(new[] { 6 }), "failed create leaves targeting and selection untouched");
        Check(session.ReplaceMobileSelectionGroup(1) && session.MobileGroupMatchesSelection(1), "explicit replacement changes the chosen slot");
        Check(!session.HasTargetingMode, "explicit replacement leaves target selection mode");
        Check(session.RecallMobileGroup(0) && session.Selection.SetEquals(new[] { 1, 2 }), "replacement retains the first squad");
        Check(session.RecallMobileGroup(2) && session.Selection.SetEquals(new[] { 4 }), "replacement retains the third squad");
        Check(session.RecallMobileGroup(3) && session.Selection.SetEquals(new[] { 5 }), "replacement retains the fourth squad");
        Check(session.RecallMobileGroup(1) && session.Selection.SetEquals(new[] { 6 }), "replacement recalls the new membership");
        session.ResetFixtureGroups();
        Check(Sizes(session).All(size => size == 0) && session.Selection.Count == 0, "fixture lifecycle clear removes all groups");
        Check(Enumerable.Range(0, 4).All(slot => session.MobileGroupDefinition(slot) == null && !session.MobileGroupMatchesSelection(slot)), "empty lifecycle state exposes no stale icons or active group");
        Select(session, 8); Check(session.SaveMobileSelectionGroup() == 0, "new fixture match starts creating at first slot");
    }

    private static void FilteringAndRecall()
    {
        var session = Fixture(); Select(session, 1, 2, 3);
        Check(session.SaveMobileSelectionGroup() == 0, "mixed squad is saved");
        Check(session.MobileGroupDefinition(0) == soldier, "group icon represents the most numerous live definition");
        session.View.entities.Single(e => e.id == 3).health = 0;
        string tie = new[] { worker, soldier }.OrderBy(value => value, StringComparer.Ordinal).First();
        Check(session.MobileGroupDefinition(0) == tie, "icon ties are deterministic after a casualty");
        session.View.entities = session.View.entities.Reverse().ToArray();
        Check(session.MobileGroupDefinition(0) == tie, "entity snapshot order cannot change the representative icon");
        Select(session, 1, 2);
        Check(session.MobileGroupMatchesSelection(0) && session.SaveMobileSelectionGroup() == 0, "duplicate comparison uses surviving members");

        session = Fixture(); Select(session, 1, 2, 3, 4, 5); session.SaveMobileSelectionGroup();
        session.View.entities.Single(e => e.id == 1).health = 0;
        session.View.entities.Single(e => e.id == 2).owner = 1;
        session.View.entities.Single(e => e.id == 3).remembered = true;
        session.View.entities = session.View.entities.Where(e => e.id != 4).ToArray();
        Check(session.GroupSize(0) == 1 && session.MobileGroupDefinition(0) == worker, "dead, enemy, remembered and missing members disappear from count and icon");
        Select(session, 6); session.SelectedResource = 42; Target(session);
        Check(session.RecallMobileGroup(0) && session.Selection.SetEquals(new[] { 5 }), "recall restores only live own members");
        Check(!session.HasTargetingMode && session.CancelCalls >= 2 && session.SelectedResource == 0, "recall cancels every targeting mode and resource selection");
        Check(session.MobileGroupMatchesSelection(0), "restored survivors highlight the matching group");
        session.View.entities.Single(e => e.id == 5).health = 0;
        Select(session, 6); Target(session);
        int cancels = session.CancelCalls;
        Check(!session.RecallMobileGroup(0) && session.Selection.SetEquals(new[] { 6 }), "empty live group recall cannot erase current selection");
        Check(session.HasTargetingMode && session.CancelCalls == cancels && session.MobileGroupDefinition(0) == null, "empty group recall has no targeting or icon side effect");
        Check(session.SaveMobileSelectionGroup() == 0 && session.GroupSize(0) == 1, "slot whose members all disappeared can be reused");

        session = Fixture();
        session.View.entities[1].owner = 1; session.View.entities[2].remembered = true; session.View.entities[3].health = 0;
        Select(session, 1, 2, 3, 4, 999); session.Group(0, true);
        Check(session.GroupSize(0) == 1 && session.MobileGroupDefinition(0) == worker, "shared Group API itself rejects forged enemy, dead, remembered and missing IDs");
    }

    private static void InvalidStates()
    {
        foreach (string state in new[] { "paused", "countdown", "replay", "stopped", "finished", "missing-view" })
        {
            var session = Fixture(); Select(session, 1); session.SaveMobileSelectionGroup(); Select(session, 2); Target(session);
            if (state == "paused") session.SystemPaused = true;
            if (state == "countdown") session.IsCountingDown = true;
            if (state == "replay") session.IsReplay = true;
            if (state == "stopped") session.Running = false;
            if (state == "finished") session.View.winner = 0;
            if (state == "missing-view") session.View = null;
            int cancels = session.CancelCalls;
            Check(!session.CanSaveMobileGroup && session.SaveMobileSelectionGroup() == -1 && !session.ReplaceMobileSelectionGroup(0), state + " cannot create or replace groups");
            Check(!session.RecallMobileGroup(0) && session.Selection.SetEquals(new[] { 2 }) && session.CancelCalls == cancels, state + " cannot recall or cancel a targeting mode");
        }
        foreach (string invalid in new[] { "empty", "enemy", "remembered", "dead", "missing" })
        {
            var session = Fixture(); Select(session, 1); session.SaveMobileSelectionGroup(); Select(session, 2);
            if (invalid == "empty") session.Selection.Clear();
            if (invalid == "enemy") session.View.entities[1].owner = 1;
            if (invalid == "remembered") session.View.entities[1].remembered = true;
            if (invalid == "dead") session.View.entities[1].health = 0;
            if (invalid == "missing") session.Selection.Add(999);
            Check(!session.CanSaveMobileGroup && session.SaveMobileSelectionGroup() == -1 && !session.ReplaceMobileSelectionGroup(0), invalid + " selection cannot save or replace");
            Check(session.GroupSize(0) == 1, invalid + " selection preserves an existing valid group");
        }
        var indices = Fixture(); Select(indices, 1); indices.SaveMobileSelectionGroup(); Target(indices);
        foreach (int slot in new[] { -1, GameSession.ControlGroupCount, int.MaxValue })
        {
            Check(!indices.ReplaceMobileSelectionGroup(slot) && !indices.RecallMobileGroup(slot), "out-of-range slot is rejected");
            Check(!indices.MobileGroupMatchesSelection(slot) && indices.MobileGroupDefinition(slot) == null && indices.GroupSize(slot) == 0, "out-of-range queries are safe");
        }
        Check(indices.HasTargetingMode && indices.Selection.SetEquals(new[] { 1 }), "invalid indices never mutate the interaction");
    }

    private static void TutorialRoute()
    {
        foreach (string faction in new[] { "industrial", "biological" })
        {
            var sim = new Simulation(training, catalog, faction, faction == "industrial" ? "biological" : "industrial");
            var army = sim.Entities.Values.Where(e => e.owner == 0 && sim.Type(e).role == "worker").Take(GuidedTutorial.ArmySize).ToArray();
            string ranged = catalog.Role(faction, "ranged").id;
            // Fixture setup only: fast-forward directly to the group lessons;
            // their notification and progression logic remains production code.
            foreach (var unit in army) { unit.definition = ranged; unit.health = catalog[ranged].health; }
            var tutorial = new GuidedTutorial(sim, true);
            var session = new GameSession { Catalog = catalog, View = sim.View(0), Tutorial = tutorial };
            int[] ids = army.Select(e => e.id).ToArray(); Select(session, ids);
            Check(!session.CanSaveMobileGroup && session.SaveMobileSelectionGroup() == -1, faction + " welcome briefing rejects early group save");
            Enter(tutorial, TutorialStep.SelectArmy); tutorial.Continue();
            Check(!session.CanSaveMobileGroup && !session.ReplaceMobileSelectionGroup(0), faction + " selection lesson cannot be skipped by saving");
            Check(session.SelectVisibleEntities(ids), faction + " real mobile selection supplies the tutorial army");
            Check(tutorial.Step == TutorialStep.SaveGroup && tutorial.Briefing && !session.CanSaveMobileGroup, faction + " real tutorial reaches group briefing");
            tutorial.Continue();
            Check(session.CanSaveMobileGroup && !session.ReplaceMobileSelectionGroup(1), faction + " save lesson only allows slot zero");
            Check(!session.RecallMobileGroup(0), faction + " recall cannot satisfy save lesson");
            Check(session.SaveMobileSelectionGroup() == 0 && session.GroupSize(0) == GuidedTutorial.ArmySize, faction + " mobile save invokes shared group storage");
            Check(tutorial.Step == TutorialStep.RecallGroup && tutorial.Briefing && session.Selection.Count == 0, faction + " successful save advances real tutorial and clears selection for recall practice");
            Check(!session.RecallMobileGroup(0), faction + " recall waits for briefing continuation");
            tutorial.Continue(); Target(session);
            Check(!session.RecallMobileGroup(1) && session.HasTargetingMode, faction + " tutorial rejects other group slots without cancelling targeting");
            Check(session.RecallMobileGroup(0) && session.Selection.SetEquals(ids) && !session.HasTargetingMode, faction + " correct recall restores army and cancels targeting");
            Check(tutorial.Step == TutorialStep.AttackMove && tutorial.Briefing, faction + " real recall notification advances to attack-move lesson");

            // A wrong-sized first save must be explicitly replaceable; merely
            // selecting the right army later cannot repair its tutorial event.
            var partialGuide = new GuidedTutorial(sim, true); Enter(partialGuide, TutorialStep.SaveGroup); partialGuide.Continue();
            var partial = new GameSession { Catalog = catalog, View = sim.View(0), Tutorial = partialGuide };
            Select(partial, ids.Take(2).ToArray());
            Check(partial.SaveMobileSelectionGroup() == 0 && partialGuide.Step == TutorialStep.SaveGroup, faction + " partial army save does not finish the lesson");
            Select(partial, ids);
            Check(partial.SaveMobileSelectionGroup() == -1 && partial.GroupSize(0) == 2, faction + " occupied tutorial slot requires explicit replacement");
            Check(partial.ReplaceMobileSelectionGroup(0) && partialGuide.Step == TutorialStep.RecallGroup, faction + " explicit corrected save completes the actual tutorial objective");
        }
    }

    private static void AtomicSelection()
    {
        var session = Fixture(); Select(session, 6); Target(session); session.SelectedResource = 42;
        session.View.entities.Single(entity => entity.id == 2).owner = 1;
        session.View.entities.Single(entity => entity.id == 3).health = 0;
        session.View.entities.Single(entity => entity.id == 4).remembered = true;
        int changed = 0, feedback = 0;
        int[] expected = { 1, 5 };
        session.Changed += () =>
        {
            Check(session.Selection.SetEquals(expected) && session.SelectedResource == 0 && !session.HasTargetingMode,
                "selection notification observes the complete replacement, never partial membership or targeting");
            changed++;
        };
        session.Feedback += success => { Check(success, "valid selection emits successful feedback"); feedback++; };
        Check(session.SelectVisibleEntities(new[] { 1, 1, 2, 3, 4, 5, 999 }), "mobile selection accepts a screen-tested candidate list");
        Check(session.Selection.SetEquals(expected), "selection deduplicates and filters enemies, dead, remembered and absent IDs");
        Check(changed == 1 && feedback == 1, "selection emits Changed and Feedback exactly once");
        Check(session.ValidationCalls == 1, "selection invokes shared ability-selection validation");
        Target(session); session.SelectedResource = 43; expected = Array.Empty<int>();
        Check(session.SelectVisibleEntities(Array.Empty<int>()) && session.Selection.Count == 0, "valid empty lasso clears previous selection");
        Check(session.SelectedResource == 0 && !session.HasTargetingMode && changed == 2 && feedback == 2,
            "empty selection clears resource and every targeting mode with one notification pair");
        Check(!session.SelectVisibleEntities(null) && changed == 2 && feedback == 2, "missing candidate list rejects without notifications");

        foreach (string state in new[] { "paused", "countdown", "replay", "stopped", "finished", "missing-view" })
        {
            var blocked = Fixture(); Select(blocked, 6); Target(blocked); blocked.SelectedResource = 51;
            int notifications = 0; blocked.Changed += () => notifications++; blocked.Feedback += _ => notifications++;
            if (state == "paused") blocked.SystemPaused = true;
            if (state == "countdown") blocked.IsCountingDown = true;
            if (state == "replay") blocked.IsReplay = true;
            if (state == "stopped") blocked.Running = false;
            if (state == "finished") blocked.View.winner = 0;
            if (state == "missing-view") blocked.View = null;
            Check(!blocked.SelectVisibleEntities(new[] { 1, 2 }) && !blocked.SelectVisibleEntities(Array.Empty<int>()), state + " rejects nonempty and empty lasso results");
            Check(blocked.Selection.SetEquals(new[] { 6 }) && blocked.SelectedResource == 51 && blocked.HasTargetingMode
                && notifications == 0 && blocked.ValidationCalls == 0, state + " leaves the entire existing interaction unchanged");
        }

        var pipeline = Fixture();
        Check(pipeline.SelectVisibleEntities(new[] { 1, 2, 3 }) && pipeline.SaveMobileSelectionGroup() == 0,
            "lasso selection can immediately create a persistent group");
        Check(pipeline.SelectVisibleEntities(new[] { 4 }) && !pipeline.MobileGroupMatchesSelection(0),
            "a later selection changes focus without changing the stored squad");
        Target(pipeline);
        Check(pipeline.RecallMobileGroup(0) && pipeline.Selection.SetEquals(new[] { 1, 2, 3 }) && !pipeline.HasTargetingMode,
            "persistent group restores the lasso squad after another selection");
        pipeline.View.player = 1; pipeline.View.entities.Single(entity => entity.id == 2).owner = 1;
        Check(pipeline.SelectVisibleEntities(new[] { 1, 2, 3 }) && pipeline.Selection.SetEquals(new[] { 2 }),
            "selection uses the current view's player ownership instead of assuming player zero");
    }

    private static void Enter(GuidedTutorial tutorial, TutorialStep step)
    {
        typeof(GuidedTutorial).GetMethod("Enter", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(tutorial, new object[] { step });
    }
}
