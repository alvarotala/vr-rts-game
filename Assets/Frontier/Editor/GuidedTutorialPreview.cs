using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Editor
{
    // Drives the production session, panel hit targets and Touch wheel router. No
    // tutorial state, resources, health or entity positions are changed by tests.
    public static class GuidedTutorialPreview
    {
        public static void RunFocusBatch()
        {
            TutorialBoardFocusPreview.Run();
            RunBatch();
        }
        public static void RunBatch()
        {
            bool hadLanguage = PlayerPrefs.HasKey(Localization.PreferenceKey);
            string savedLanguage = PlayerPrefs.GetString(Localization.PreferenceKey);
            try
            {
                Run();
                // These existing regression fixtures assert their English copy.
                Localization.SetPreference("en");
                MenuFlowPreview.Render();
                UnifiedWheelPreview.Render();
                Debug.Log("FRONTIER_GUIDED_TUTORIAL_BATCH passed: tutorial, menu flow and unified wheel regressions; no player build");
            }
            finally
            {
                if (hadLanguage) PlayerPrefs.SetString(Localization.PreferenceKey, savedLanguage);
                else PlayerPrefs.DeleteKey(Localization.PreferenceKey);
                Localization.ReloadPreference();
            }
        }

        [MenuItem("Frontier/Preview and check guided tutorial")]
        public static void Run()
        {
            bool hadLanguage = PlayerPrefs.HasKey(Localization.PreferenceKey);
            string savedLanguage = PlayerPrefs.GetString(Localization.PreferenceKey);
            int captures = 0;
            try
            {
                foreach (string language in new[] { "es", "en" })
                    foreach (bool touch in new[] { false, true }) RunLesson(language, touch, ref captures);
                Debug.Log("FRONTIER_GUIDED_TUTORIAL passed: four complete real-command lessons, lifecycle and input checks, " + captures + " views; no player build");
            }
            finally
            {
                if (hadLanguage) PlayerPrefs.SetString(Localization.PreferenceKey, savedLanguage);
                else PlayerPrefs.DeleteKey(Localization.PreferenceKey);
                Localization.ReloadPreference();
            }
        }

        private static void RunLesson(string language, bool touch, ref int totalCaptures)
        {
            string prefix = language + (touch ? "-touch" : "-desktop");
            string directory = Path.Combine(Path.GetTempPath(), "frontier-tutorial-" + Guid.NewGuid().ToString("N"));
            var preview = new PreviewRenderUtility();
            var boardPreview = new PreviewRenderUtility();
            bool boardAdded = false, consoleAdded = false;
            float emptyTutorialHudHeight = 0;
            int captures = 0;
            try
            {
                Localization.SetPreference(language);
                using (var f = new UnifiedWheelFixture("GuidedTutorial"))
                {
                    var s = f.Session;
                    var store = new MatchArchiveStore(directory);
                    Set(s, "history", store); Set(s, "archiveInEditor", true);
                    typeof(QuestBridge).GetProperty("Active").SetValue(f.Quest, touch);
                    f.Quest.Camera.transform.position = touch ? new Vector3(0, 1.7f, -2) : new Vector3(0, 2.2f, -2.25f);
                    f.Quest.Camera.transform.LookAt(touch ? new Vector3(0, 1, 0) : new Vector3(0, 1.45f, .1f));
                    f.Quest.Camera.orthographic = false; f.Quest.Camera.fieldOfView = 60; f.Quest.Camera.aspect = 16f / 9;
                    f.Quest.Camera.nearClipPlane = touch ? .03f : .02f; f.Quest.Camera.farClipPlane = touch ? 30 : 40;
                    f.Board.transform.position = new Vector3(0, .75f, 0);
                    Set(f.Panel, "positionInitialized", false); Set(f.Panel, "preferredWindowOffset", new Vector3(0, -.12f, 1.25f));
                    var scales = UnifiedWheelFixture.Field<Dictionary<CommandPanel.MenuPage, float>>(f.Panel, "windowScaleFactors");
                    foreach (var page in scales.Keys.ToArray()) scales[page] = 1;
                    UnifiedWheelFixture.Call(f.Panel, "ApplyWindowScale");
                    f.Board.Placed = true;
                    s.LoadMap("desert"); s.Faction = language == "es" ? "industrial" : "biological";
                    s.EnemyFaction = language == "es" ? "biological" : "industrial"; s.Difficulty = "hard";
                    var setup = s.Map; string setupFaction = s.Faction, setupEnemy = s.EnemyFaction;
                    f.Panel.Bindings.Reset(false); f.Panel.Bindings.Reset(true); f.Panel.Bindings.SelectHand(false);
                    if (language == "en")
                    {
                        f.Panel.Bindings.SelectHand(true);
                        f.Check(f.Panel.Bindings.Assign(false, "select", "mouse:middle", false, out _), "Desktop selection remap accepted");
                        f.Check(f.Panel.Bindings.Assign(false, "order", "key:Q", false, out _), "Desktop order remap accepted");
                        f.Check(f.Panel.Bindings.Assign(false, "group1", "key:G", false, out _), "Desktop group remap accepted");
                        f.Check(f.Panel.Bindings.Assign(true, "select", "touch:a", true, out _), "Touch selection/order swap accepted");
                    }
                    Refresh(); Capture("01-main");
                    f.Board.Placed = false; f.Quest.RequestTutorial();
                    f.Check(f.Quest.Placing && f.Quest.StartPending && !s.IsTutorial && !s.Running,
                        "First-time tutorial waits for board placement before creating a match");
                    f.Quest.CancelPlacement();
                    f.Check(!f.Quest.Placing && !f.Quest.StartPending && !s.IsTutorial && !s.IsPaused,
                        "Cancelling first-time placement clears the tutorial request without creating a match or anchor");
                    f.Board.Placed = true;
                    Click("Guided tutorial");
                    f.Check(s.IsTutorial && s.IsCountingDown && s.MapId == "training" && !s.IsOnline, "Main-menu tutorial starts a separate local lesson with countdown");
                    f.Check(!s.Send(CommandKind.Surrender), "Countdown rejects real commands");
                    for (int i = 0; i < 14; i++) UnifiedWheelFixture.Call(s, "AdvanceCountdown", .25f);
                    UnifiedWheelFixture.Call(s, "UpdateTutorial"); Refresh();
                    f.Check(!s.IsCountingDown && s.Tutorial.Step == TutorialStep.Welcome && s.IsPaused && !s.HasSystemPause,
                        "Opening briefing pauses simulation without being a tracking/system interruption");
                    f.Check(!s.CanIssueOrders && f.Panel.Page == CommandPanel.MenuPage.Actions, "Briefing retains the tutorial interface and blocks map orders");
                    CheckFloatingWindow();
                    f.Check((string)typeof(CommandPanel).GetProperty("MatchDescription", UnifiedWheelFixture.Private).GetValue(f.Panel)
                        == "GUIDED TUTORIAL · PRACTICE AT YOUR OWN PACE", "Tutorial identification does not claim the retained skirmish AI difficulty");
                    Capture("02-welcome");

                    var seen = new System.Collections.Generic.HashSet<TutorialStep>();
                    int transitions = 0;
                    while (!s.Tutorial.Complete && transitions++ < 70)
                    {
                        var lesson = s.Tutorial; var step = lesson.Step;
                        if (lesson.Briefing)
                        {
                            seen.Add(step);
                            int tick = s.Match.Tick, units = s.Match.Entities.Count;
                            f.Check(!s.Send(CommandKind.Move, position: new Point(1, 1)) && !s.Send(CommandKind.Surrender), step + ": briefing rejects production Session.Send calls");
                            s.Match.Step();
                            f.Check(s.Match.Tick == tick && s.Match.Entities.Count == units, step + ": briefing freezes simulation");
                            s.Pause("tracking", true); s.ContinueTutorial();
                            f.Check(lesson.Briefing && lesson.Step == step, step + ": tracking interruption cannot dismiss a lesson");
                            s.Pause("tracking", false);
                            if (step == TutorialStep.ResumeMenu)
                            {
                                f.Panel.Back(); f.Panel.ToggleMenu(); Click("Resume match");
                                f.Check(f.Panel.Page == CommandPanel.MenuPage.Pause && lesson.Briefing && lesson.Step == step,
                                    "Back, ToggleMenu and Resume retain the resume briefing until Continue is pressed");
                            }
                            Click("Continue tutorial");
                            if (step == TutorialStep.Welcome) continue;
                            f.Check(!lesson.Briefing && !s.HasSystemPause, step + ": Continue arms the current objective");
                        }

                        step = lesson.Step;
                        if (!lesson.AdvanceSimulation)
                        {
                            int idleTick = s.Match.Tick;
                            Set(s, "accumulator", .25f); UnifiedWheelFixture.Call(s, "Update");
                            f.Check(s.Match.Tick == idleTick, step + ": runtime waits for input without draining deposits");
                        }
                        f.Check(!s.Send(CommandKind.Surrender) && !s.Send(CommandKind.Research, definition: "attack"), step + ": unrelated commands are rejected during the objective");
                        switch (step)
                        {
                            case TutorialStep.SelectWorker:
                                InstructionContains(f.Panel.Bindings.Name(touch, "select"));
                                CheckCue(entity: lesson.FocusEntity);
                                CaptureBoard("06-board-worker", false);
                                s.Select(lesson.FocusEntity, false); break;
                            case TutorialStep.SelectHeadquarters:
                                s.Select(lesson.FocusEntity, false); break;
                            case TutorialStep.MoveWorker:
                                InstructionContains(f.Panel.Bindings.Name(touch, "order"));
                                f.Check(!s.Send(CommandKind.Move, position: new Point(1, 1)), "Off-objective movement is rejected");
                                f.Check(s.Context(lesson.FocusPoint.Value, 0, 0) > 0, "Context order accepts the indicated move destination");
                                f.Check(lesson.Step == step && !lesson.Briefing, "Movement waits for arrival rather than command submission");
                                Pump(step); break;
                            case TutorialStep.GatherMinerals:
                            case TutorialStep.GatherGas:
                                s.Select(lesson.FocusEntity, false);
                                f.Check(s.Context(lesson.FocusPoint.Value, 0, lesson.FocusResource) > 0, step + ": contextual gather command accepted");
                                f.Check(lesson.Step == step, step + ": collection waits for the completed return trip");
                                Pump(step); break;
                            case TutorialStep.TrainWorker:
                                s.Select(0, false); CheckCue(entity: lesson.FocusEntity);
                                s.Select(lesson.FocusEntity, false);
                                CheckCue(action: "train:" + lesson.ExpectedDefinition);
                                ClickAction("train:" + lesson.ExpectedDefinition);
                                f.Check(s.Tutorial.AcceptedOrderCount == 1, "Worker enters the ordinary production queue through its menu button");
                                f.Check(lesson.Step == step && !s.Send(CommandKind.Train, definition: lesson.ExpectedDefinition), "Worker objective waits for deployment and blocks duplicate training");
                                Pump(step); break;
                            case TutorialStep.OpenMenu:
                                CheckCue(action: "pause");
                                f.Panel.ToggleMenu(); Refresh(); Capture("03-pause");
                                f.Check(f.Panel.Page == CommandPanel.MenuPage.Pause && s.IsPaused && lesson.Step == TutorialStep.ResumeMenu,
                                    "Real in-game menu advances to its paused resume lesson"); break;
                            case TutorialStep.ResumeMenu:
                                CheckCue(action: "resume");
                                if (touch) f.Panel.Back(); else f.Panel.ToggleMenu();
                                Refresh();
                                f.Check(f.Panel.Page == CommandPanel.MenuPage.Actions && lesson.Step != step, "Real Back/ToggleMenu resumes the guided flow"); break;
                            case TutorialStep.OpenWheel:
                                s.Select(lesson.FocusEntity, false);
                                f.Drain(); f.Frame(Vector2.right); Refresh();
                                f.Check(lesson.Step == step && lesson.WheelExplored && f.Wheel.IsOpen && s.CanIssueOrders,
                                    "Opening the selection wheel keeps its lesson active so the player can explore");
                                f.Frame(); f.Frame(elapsed: ActionWheel.NeutralCloseDelay + 2); Refresh();
                                f.Check(lesson.Step == step && f.Wheel.IsOpen,
                                    "The first wheel lesson remains visible while the stick rests at neutral");
                                f.Wheel.Close(); Refresh();
                                f.Check(lesson.Step == step, "An involuntary wheel close does not complete its lesson");
                                var reopenedInstruction=UnifiedWheelFixture.Field<Text>(f.Panel,"tutorialInstruction") as LocalizedText;
                                f.Check(reopenedInstruction.Source=="Tilt the "+f.Panel.Bindings.HandName.ToLowerInvariant()+" stick right. That's your action wheel!",
                                    "An involuntary wheel close restores the instruction to open it before continuing");
                                f.Drain(); f.Frame(Vector2.right); Refresh();
                                f.Frame(b: true); Refresh();
                                f.Check(lesson.Step == step && f.Wheel.IsOpen && f.Wheel.Page == WheelPage.Root,
                                    "Back still returns from the selection category to the wheel root during teaching");
                                f.Frame(b: true); Refresh();
                                f.Check(lesson.Step == TutorialStep.BuildSupply && lesson.Briefing && !f.Wheel.IsOpen,
                                    "Deliberately closing the explored wheel starts the next briefing");
                                f.Drain(); break;
                            case TutorialStep.BuildSupply:
                            case TutorialStep.BuildBarracks:
                            case TutorialStep.BuildExtractor:
                                s.Select(0, false); CheckCue(entity: lesson.FocusEntity);
                                s.Select(lesson.FocusEntity, false);
                                CheckCue(action: "build:" + lesson.ExpectedDefinition);
                                if (step == TutorialStep.BuildSupply)
                                {
                                    f.Panel.ToggleMenu(); Refresh(); CheckCue(action: "resume");
                                    Click("Settings"); CheckCue(action: "back");
                                    Click("Back"); Click("Resume match");
                                    f.Check(lesson.Step == step && !lesson.Briefing, "Detouring through Pause and Settings returns to the same active building objective");
                                    CheckCue(action: "build:" + lesson.ExpectedDefinition);
                                    if (touch)
                                    {
                                        f.OpenGroups(); Refresh();
                                        f.Check(UnifiedWheelFixture.Field<string>(f.Panel, "tutorialCueAction") == "back",
                                            "Opening the wrong wheel category first teaches Back instead of an unavailable building action");
                                        InstructionContains(f.Panel.Bindings.MenuBackName);
                                        f.Frame(b: true); Refresh(); CheckWheelCue("selection-menu");
                                        f.Dismiss(); Refresh(); CheckCue(action: "build:" + lesson.ExpectedDefinition);
                                    }
                                }
                                if (step == TutorialStep.BuildSupply) CaptureScene("09-scene-build-option");
                                ClickAction("build:" + lesson.ExpectedDefinition);
                                f.Check(s.BuildDefinition == lesson.ExpectedDefinition, step + ": guided blueprint is available");
                                CheckCue(point: lesson.FocusPoint.Value);
                                if (step == TutorialStep.BuildSupply)
                                {
                                    s.Select(0, false); Refresh();
                                    f.Check(UnifiedWheelFixture.Field<string>(f.Panel, "tutorialCueAction") == "cancel",
                                        "Losing the worker selection while placing a blueprint explains how to cancel first");
                                    InstructionContains(f.Panel.Bindings.Name(touch, "back"));
                                    s.CancelMode(); s.Select(lesson.FocusEntity, false); ClickAction("build:" + lesson.ExpectedDefinition);
                                    CheckCue(point: lesson.FocusPoint.Value);
                                }
                                if (step == TutorialStep.BuildSupply) { Capture("04-construction"); CaptureBoard("07-board-construction", false); }
                                f.Check(s.Context(lesson.FocusPoint.Value, 0, lesson.FocusResource) > 0, step + ": real placement creates the foundation");
                                f.Check(lesson.Step == step && !lesson.Briefing, step + ": construction waits for the worker to finish");
                                Pump(step); break;
                            case TutorialStep.TrainArmy:
                                s.Select(0, false); CheckCue(entity: lesson.FocusEntity);
                                s.Select(lesson.FocusEntity, false);
                                CheckCue(action: "train:" + lesson.ExpectedDefinition);
                                for (int i = 0; i < GuidedTutorial.ArmySize; i++)
                                {
                                    ClickAction("train:" + lesson.ExpectedDefinition);
                                    f.Check(s.Tutorial.AcceptedOrderCount == i + 1, "Army unit " + (i + 1) + " enters the ordinary queue through its menu button");
                                }
                                f.Check(!s.Send(CommandKind.Train, definition: lesson.ExpectedDefinition), "The guided army size cannot be exceeded");
                                Pump(step); break;
                            case TutorialStep.SelectArmy:
                                f.Check(s.SelectArmy(), "Army quick selection uses the production session action"); break;
                            case TutorialStep.SaveGroup:
                                CheckCue(action: "group:0"); CaptureScene("10-scene-control-group");
                                if (touch)
                                {
                                    f.OpenGroups(); CheckWheelCue("group-1"); f.Frame(Vector2.up, press: true); f.Frame(clickUp: true); f.Drain();
                                }
                                else s.Group(0, true);
                                f.Check(s.GroupSize(0) == GuidedTutorial.ArmySize && lesson.Step == TutorialStep.RecallGroup,
                                    "Saving the army uses the actual control group action"); break;
                            case TutorialStep.RecallGroup:
                                s.Select(0, false);
                                CheckCue(action: "group:0");
                                if (touch)
                                {
                                    f.OpenGroups(); CheckWheelCue("group-1"); f.Frame(Vector2.up, down: true, held: true); f.Frame(up: true); f.Drain();
                                }
                                else s.Group(0, false);
                                f.Check(s.Selection.Count == GuidedTutorial.ArmySize && lesson.Step == TutorialStep.AttackMove,
                                    "Recalling group 1 restores the army after clearing selection"); break;
                            case TutorialStep.AttackMove:
                            case TutorialStep.DestroyEnemy:
                                CheckCue(action: "attack-move"); ClickAction("attack-move");
                                CheckCue(point: lesson.FocusPoint.Value);
                                if (step == TutorialStep.DestroyEnemy) CaptureBoard("08-board-enemy-destination", true);
                                f.Check(s.AttackMoveMode && s.Context(lesson.FocusPoint.Value, 0, 0) > 0, step + ": Attack Move accepts the indicated destination");
                                Pump(step, 360 * Simulation.TickRate); break;
                            default: throw new Exception("Unexpected active tutorial step: " + step);
                        }
                        Refresh();
                        f.Check(s.Tutorial.Step != step, step + ": a real action completes the objective");
                    }
                    f.Check(s.Tutorial.Complete && s.Match.Winner == 0 && s.IsPaused && !s.HasSystemPause,
                        "The lesson completes only after ordinary combat destroys the enemy and records victory");
                    f.Check(seen.Count == (touch ? 19 : 18) && seen.Contains(TutorialStep.OpenWheel) == touch,
                        "The entire lesson is visited; desktop omits only the controller wheel exercise");
                    f.Check(f.Panel.Page == CommandPanel.MenuPage.Actions && store.List().Length == 0 && !s.IsOnline,
                        "Tutorial completion keeps its own UI and creates no ordinary match history or online session");
                    UnifiedWheelFixture.Call(f.Board, "LateUpdate");
                    f.Check(!f.Board.Content.Find("Tutorial objective marker").gameObject.activeSelf, "Victory hides the completed objective marker");
                    Refresh();
                    var completedConnector = UnifiedWheelFixture.Field<LineRenderer>(f.Panel, "tutorialConnector");
                    f.Check(!completedConnector.enabled || !completedConnector.gameObject.activeInHierarchy,
                        "Victory hides the tutorial connector while retaining the independent completion window");
                    Capture("05-complete");
                    if (language == "es") Click("Repeat tutorial");
                    else
                    {
                        Click("Tutorial main menu");
                        f.Check(!s.IsTutorial && !s.Running && !s.IsPaused && ReferenceEquals(s.Map, setup),
                            "Victory Main menu button restores skirmish setup without a residual pause");
                        s.StartTutorial(touch); s.RestartTutorial();
                    }
                    f.Check(s.IsTutorial && s.Tutorial.Step == TutorialStep.Welcome && s.Match.Tick == 0 && s.Selection.Count == 0 && s.GroupSize(0) == 0,
                        "Repeat starts a clean tutorial with no retained selection or groups");
                    s.Pause("tracking", true); s.LeaveMatch();
                    f.Check(!s.IsTutorial && !s.Running && s.HasSystemPause && ReferenceEquals(s.Map, setup) && s.MapId == "desert"
                        && s.Faction == setupFaction && s.EnemyFaction == setupEnemy && s.Difficulty == "hard", "Leaving restores the exact skirmish setup and retains tracking pause");
                    s.Pause("tracking", false); s.StartTutorial(touch); Refresh();
                    f.Panel.ToggleMenu(); Refresh(); Click("Return to main menu"); Click("Leave match");
                    f.Check(!s.IsTutorial && !s.Running && !s.IsPaused && f.Panel.Page == CommandPanel.MenuPage.Main && store.List().Length == 0,
                        "Pause-menu exit closes the tutorial cleanly without recording a match");
                    s.StartMatch();
                    Refresh();
                    f.Check(!s.IsTutorial && s.MapId == "desert" && s.CanIssueOrders && store.List().Length == 1,
                        "Ordinary skirmish starts normally and still records history after a tutorial");
                    f.Check(Mathf.Abs(UnifiedWheelFixture.Field<RectTransform>(f.Panel, "root").rect.height - emptyTutorialHudHeight) < .1f,
                        "The floating tutorial adds no height to the ordinary empty-selection console");
                    f.Check(s.Send(CommandKind.Surrender), "Ordinary skirmish commands are no longer tutorial-gated");
                    s.LeaveMatch();
                    f.Report("GUIDED_TUTORIAL_" + prefix.ToUpperInvariant());

                    void Refresh()
                    {
                        if (s.Match != null) UnifiedWheelFixture.Call(s, "RefreshView");
                        Set(f.Panel, "nextText", 0f); UnifiedWheelFixture.Call(f.Panel, "LateUpdate");
                        UnifiedWheelFixture.Call(f.Panel, "UpdateTutorialPresentation");
                        Canvas.ForceUpdateCanvases();
                    }
                    void Click(string name)
                    {
                        Refresh();
                        var target = f.Panel.GetComponentsInChildren<RectTransform>().Single(r => r.name == name && r.gameObject.activeInHierarchy);
                        var center = target.TransformPoint(target.rect.center); var ray = new Ray(center - target.forward * .6f, target.forward);
                        f.Check(f.Panel.Point(ray, true, false) && f.Panel.Point(ray, false, true), "Panel pointer activates " + name);
                        Refresh();
                    }
                    RectTransform ActionTarget(string id)
                    {
                        if (id == "pause" || id == "resume" || id == "back" || id.StartsWith("group:"))
                        {
                            string name = id == "pause" ? "Pause" : id == "resume" ? "Resume match" : id == "back" ? "Back" : "Group " + (int.Parse(id.Substring(6)) + 1);
                            return f.Panel.GetComponentsInChildren<RectTransform>().Single(r => r.name == name && r.gameObject.activeInHierarchy);
                        }
                        var targets = (System.Collections.IEnumerable)typeof(CommandPanel).GetField("targets", UnifiedWheelFixture.Private).GetValue(f.Panel);
                        return targets.Cast<object>().Where(target => (string)target.GetType().GetField("actionId").GetValue(target) == id)
                            .Select(target => (RectTransform)target.GetType().GetField("rect").GetValue(target))
                            .Single(rect => rect.gameObject.activeInHierarchy);
                    }
                    void ClickAction(string id)
                    {
                        Refresh(); var target = ActionTarget(id);
                        var center = target.TransformPoint(target.rect.center); var ray = new Ray(center - target.forward * .6f, target.forward);
                        f.Check(f.Panel.Point(ray, true, false) && f.Panel.Point(ray, false, true), "Real console pointer activates " + id);
                        Refresh();
                    }
                    void CheckFloatingWindow()
                    {
                        Refresh();
                        var card = UnifiedWheelFixture.Field<RectTransform>(f.Panel, "tutorialCard");
                        var consoleRoot = UnifiedWheelFixture.Field<RectTransform>(f.Panel, "root");
                        emptyTutorialHudHeight = consoleRoot.rect.height;
                        f.Check(card.parent == consoleRoot.parent && card.parent == f.Panel.transform && !card.IsChildOf(consoleRoot)
                            && card.GetComponent<Canvas>() != null, "Tutorial instructions keep a separate world-space Canvas over the console");
                        var relative=consoleRoot.InverseTransformPoint(card.position);
                        f.Check(Mathf.Abs(relative.x)<1&&relative.y>0&&relative.y<consoleRoot.rect.yMax,
                            "The opening guide sits centered over the upper console, not beside it");
                        f.Check(!UnifiedWheelFixture.Field<RectTransform>(f.Panel,"tutorialInstructionBox").gameObject.activeSelf
                            &&UnifiedWheelFixture.Field<Text>(f.Panel,"tutorialInstruction").text=="",
                            "A Continue briefing hides the extra command box and duplicate instructions");
                        f.Check(card.GetComponentsInChildren<Collider>(true).Length == 0, "The floating instructions add no physics input blockers");
                        var local = new Vector2(card.rect.xMin + 12, card.rect.yMax - 12);
                        var corner = card.TransformPoint(local); var ray = new Ray(corner - card.forward * .5f, card.forward);
                        f.Check(f.Panel.ContainsPointer(ray) && f.Panel.Point(ray, false, false),
                            "The separate instruction background consumes pointer hover outside the main console");
                        var before = s.Selection.ToArray(); var step = s.Tutorial.Step;
                        f.Check(f.Panel.Point(ray, true, false) && f.Panel.Point(ray, false, true)
                            && s.Selection.SetEquals(before) && s.Tutorial.Step == step,
                            "Clicking the floating background never sends an order or skips a lesson");
                    }
                    void CheckCue(string action = null, int entity = 0, Point? point = null)
                    {
                        Refresh();
                        if (action != null) f.Check(UnifiedWheelFixture.Field<string>(f.Panel, "tutorialCueAction") == action,
                            "The active instruction points to its concrete menu command: " + action);
                        if (entity > 0) f.Check(UnifiedWheelFixture.Field<int>(f.Panel, "tutorialCueEntity") == entity,
                            "The active instruction first points to the entity that must be selected");
                        if (point.HasValue)
                        {
                            var actual = UnifiedWheelFixture.Field<Point?>(f.Panel, "tutorialCuePoint");
                            f.Check(actual.HasValue && actual.Value.Distance(point.Value) < .01f,
                                "Arming the blueprint redirects the instruction to its required map position");
                        }
                        object[] args = { Vector3.zero };
                        bool resolved = (bool)typeof(CommandPanel).GetMethod("TryTutorialCue", UnifiedWheelFixture.Private).Invoke(f.Panel, args);
                        f.Check(resolved, "The instruction resolves a real spatial target");
                        var destination = (Vector3)args[0];
                        var connector = UnifiedWheelFixture.Field<LineRenderer>(f.Panel, "tutorialConnector");
                        f.Check(connector.enabled && connector.gameObject.activeInHierarchy && connector.useWorldSpace && connector.positionCount >= 2
                            && Vector3.Distance(connector.GetPosition(connector.positionCount - 1), destination) < .001f,
                            "The floating instruction's line ends exactly at the resolved target");
                        f.Check(connector.GetComponents<Collider>().Length == 0, "The visual connector cannot intercept input");
                        bool mapFocus=action==null;
                        var dim=UnifiedWheelFixture.Field<Image>(f.Panel,"tutorialMenuDim");
                        f.Check(mapFocus?dim.gameObject.activeSelf&&dim.color.a>.4f:!dim.gameObject.activeSelf,
                            "Only map teaching dims the in-game console; menu teaching restores it");
                        var card=UnifiedWheelFixture.Field<RectTransform>(f.Panel,"tutorialCard");
                        if(mapFocus)
                        {
                            var console=UnifiedWheelFixture.Field<RectTransform>(f.Panel,"root");
                            var eye=f.Quest.Camera.transform;
                            float angularScale=card.lossyScale.y/console.lossyScale.y
                                *eye.InverseTransformPoint(console.position).z/eye.InverseTransformPoint(card.position).z;
                            f.Check(angularScale>1.15f,"Map instructions appear larger even after compensating for distance");
                        }
                        int attention=UnifiedWheelFixture.Field<int>(f.Panel,"tutorialAttentionChanges");Refresh();
                        f.Check(UnifiedWheelFixture.Field<int>(f.Panel,"tutorialAttentionChanges")==attention,
                            "An unchanged tutorial state does not repeatedly play its attention hint");
                        if (action != null)
                        {
                            var target = ActionTarget(action);
                            var center=target.TransformPoint(target.rect.center);
                            object[] hitArgs={card,new Ray(center-target.forward*.6f,target.forward),0f};
                            bool covered=(bool)typeof(CommandPanel).GetMethod("Hit",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,hitArgs);
                            f.Check(!covered,"The instruction leaves its taught command's center visible and clickable");
                            var local = target.InverseTransformPoint(destination);
                            f.Check(local.x >= target.rect.xMin - .1f && local.x <= target.rect.xMax + .1f
                                && local.y >= target.rect.yMin - .1f && local.y <= target.rect.yMax + .1f && Mathf.Abs(local.z) < 12,
                                "The menu connector endpoint lies on the real command button");
                            UnifiedWheelFixture.Call(f.Board, "LateUpdate");
                            f.Check(!f.Board.Content.Find("Tutorial objective marker").gameObject.activeSelf,
                                "A console command cue hides map markers until a world action is armed");
                        }
                        if (entity > 0)
                        {
                            var target = s.View.entities.Single(e => e.id == entity);
                            float half = s.Catalog[target.definition].building ? s.Catalog[target.definition].footprint * .5f : 0;
                            var local = f.Board.Content.InverseTransformPoint(destination);
                            f.Check(new Point(local.x, local.z).Distance(new Point(target.position.x + half, target.position.y + half)) < 1.5f,
                                "The entity connector ends above that unit or building on the actual board");
                        }
                        if (point.HasValue)
                        {
                            var local = f.Board.Content.InverseTransformPoint(destination);
                            f.Check(new Point(local.x, local.z).Distance(point.Value) < .01f,
                                "The destination connector follows the board's transform exactly");
                        }
                        s.Pause("tracking", true); Refresh();
                        f.Check(!connector.enabled || !connector.gameObject.activeInHierarchy, "A tracking interruption hides the floating connector");
                        s.Pause("tracking", false); Refresh();
                    }
                    void CheckWheelCue(string id)
                    {
                        Refresh();
                        f.Check(f.Wheel.TryTutorialTarget(id, out var target), "The open wheel exposes its real target sector " + id);
                        var connector = UnifiedWheelFixture.Field<LineRenderer>(f.Panel, "tutorialConnector");
                        f.Check(connector.gameObject.activeInHierarchy && Vector3.Distance(connector.GetPosition(connector.positionCount - 1), target) < .001f,
                            "The tutorial connector follows the open wheel sector rather than its console shortcut");
                        UnifiedWheelFixture.Call(f.Board, "LateUpdate");
                        f.Check(!f.Board.Content.Find("Tutorial objective marker").gameObject.activeSelf,
                            "A wheel cue hides stale map markers");
                    }
                    void Pump(TutorialStep before, int limit = 180 * Simulation.TickRate)
                    {
                        f.Check(s.Tutorial.AdvanceSimulation, before + ": accepted work enables the runtime simulation clock");
                        for (int i = 0; i < limit && s.Tutorial.Step == before; i++)
                        {
                            if (s.Match.Paused) throw new Exception(before + ": exercise unexpectedly paused at tick " + s.Match.Tick);
                            s.Match.Step(); UnifiedWheelFixture.Call(s, "UpdateTutorial");
                        }
                        Refresh();
                        f.Check(s.Tutorial.Step != before, before + ": ordinary simulation finishes before timeout (tick " + s.Match.Tick + ")");
                    }
                    void InstructionContains(string control)
                    {
                        Refresh(); string instruction = UnifiedWheelFixture.Field<Text>(f.Panel, "tutorialInstruction").text;
                        f.Check(instruction.Contains(Localization.Text(control)), "Instruction reflects active hand/remap: " + control + " => " + instruction);
                    }
                    void Capture(string name)
                    {
                        Refresh();
                        var card = UnifiedWheelFixture.Field<RectTransform>(f.Panel, "tutorialCard");
                        if (card != null && card.gameObject.activeInHierarchy)
                        {
                            foreach (var text in card.GetComponentsInChildren<Text>())
                            {
                                var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
                                settings.resizeTextForBestFit = false;
                                settings.fontSize = text.resizeTextForBestFit ? text.resizeTextMinSize : text.fontSize;
                                float height = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
                                f.Check(height <= text.rectTransform.rect.height + .5f, "Tutorial text fits at readable minimum: " + text.text);
                            }
                            var position = card.localPosition; var rotation = card.localRotation; var scale = card.localScale;
                            try { HarvestingPreview.SaveCanvas(card, card, "TestResults/GuidedTutorial/" + prefix + "-" + name + ".png", preview, 1400, 800); }
                            finally { card.localPosition = position; card.localRotation = rotation; card.localScale = scale; }
                            captures++; CaptureScene(name + "-scene");
                        }
                        else { HarvestingPreview.SavePanel(f.Panel, "TestResults/GuidedTutorial/" + prefix + "-" + name + ".png", preview); captures++; }
                    }
                    void CaptureBoard(string name, bool entireMap)
                    {
                        Refresh();
                        if (!boardAdded) { boardPreview.AddSingleGO(f.Board.gameObject); boardAdded = true; }
                        UnifiedWheelFixture.Call(f.Board, "LateUpdate");
                        var marker = f.Board.Content.Find("Tutorial objective marker");
                        f.Check(marker != null && marker.gameObject.activeInHierarchy && marker.GetComponentsInChildren<Collider>().Length == 0,
                            "The spatial objective marker is visible and cannot intercept map input");
                        var ring = marker.Find("Objective ring").GetComponent<LineRenderer>();
                        f.Check(ring.positionCount == 48 && marker.Find("Objective arrow").GetComponent<LineRenderer>().positionCount == 5,
                            "The target has a complete ground ring and raised directional arrow");
                        f.Check(marker.GetComponentsInChildren<LineRenderer>().All(line => line.widthMultiplier <= f.Board.CellScale * .2f),
                            "Objective line widths follow board scale and cannot obscure nearby units or structures");
                        float originalZoom = f.Board.Zoom, widthInCells = ring.widthMultiplier / f.Board.CellScale;
                        try
                        {
                            f.Board.Zoom = originalZoom == 1 ? 2 : 1;
                            UnifiedWheelFixture.Call(f.Board, "LateUpdate");
                            f.Check(Mathf.Abs(ring.widthMultiplier / f.Board.CellScale - widthInCells) < .0001f,
                                "Zooming the board preserves the objective line width relative to units and structures");
                        }
                        finally { f.Board.Zoom = originalZoom; UnifiedWheelFixture.Call(f.Board, "LateUpdate"); }
                        if (s.Tutorial.Step == TutorialStep.BuildSupply)
                        {
                            var footprint = marker.Find("Required foundation").GetComponent<LineRenderer>();
                            f.Check(footprint.gameObject.activeSelf && footprint.positionCount == 4,
                                "The construction objective outlines the exact required foundation");
                        }
                        s.Pause("tracking", true); UnifiedWheelFixture.Call(f.Board, "LateUpdate");
                        f.Check(!marker.gameObject.activeSelf, "Tracking interruption hides the objective annotation");
                        s.Pause("tracking", false); UnifiedWheelFixture.Call(f.Board, "LateUpdate");
                        f.Check(marker.gameObject.activeSelf, "Tracking recovery restores the same objective annotation");
                        CaptureScene(name);
                    }
                    void CaptureScene(string name)
                    {
                        Refresh();
                        if (!boardAdded) { boardPreview.AddSingleGO(f.Board.gameObject); boardAdded = true; }
                        if (!consoleAdded) { boardPreview.AddSingleGO(f.Panel.gameObject); consoleAdded = true; }
                        UnifiedWheelFixture.Call(f.Board, "LateUpdate");
                        SaveComposedScene(f, boardPreview, "TestResults/GuidedTutorial/" + prefix + "-" + name + ".png"); captures++;
                        if (!touch)
                        {
                            SaveComposedScene(f, boardPreview, "TestResults/GuidedTutorial/" + prefix + "-" + name + "-player.png", true); captures++;
                            var card = UnifiedWheelFixture.Field<RectTransform>(f.Panel, "tutorialCard");
                            if (card.gameObject.activeInHierarchy)
                            {
                                var corners = new Vector3[4]; card.GetWorldCorners(corners);
                                var viewport = corners.Select(f.Quest.Camera.WorldToViewportPoint).ToArray();
                                f.Check(viewport.All(point => point.z >= f.Quest.Camera.nearClipPlane && point.z <= f.Quest.Camera.farClipPlane && point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1),
                                    "Default desktop 16:9 view contains the complete floating tutorial: " + string.Join(" / ", viewport.Select(p => p.ToString("F3"))));
                            }
                        }
                    }
                }
                totalCaptures += captures;
            }
            finally
            {
                preview.Cleanup();
                boardPreview.Cleanup();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void SaveComposedScene(UnifiedWheelFixture fixture, PreviewRenderUtility preview, string path, bool playerView = false)
        {
            // Keep every production world transform intact. Render the real board
            // and connector, then submit Canvas meshes that preview cameras omit.
            Canvas.ForceUpdateCanvases();
            var graphics = fixture.Panel.GetComponentsInChildren<Graphic>().Where(graphic => graphic.isActiveAndEnabled)
                .OrderBy(graphic => graphic.canvas == null ? 0 : graphic.canvas.sortingOrder).ToArray();
            var points = new List<Vector3>(); var corners = new Vector3[4];
            foreach (var graphic in graphics)
            {
                graphic.rectTransform.GetWorldCorners(corners); points.AddRange(corners);
            }
            foreach (float x in new[] { 0f, (float)fixture.Session.Map.width })
                foreach (float z in new[] { 0f, (float)fixture.Session.Map.height })
                    points.Add(fixture.Board.Content.TransformPoint(new Vector3(x, 0, z)));
            var rotation = Quaternion.Euler(20, 0, 0); var inverse = Quaternion.Inverse(rotation);
            var projected = new Bounds(inverse * points[0], Vector3.zero);
            foreach (var point in points) projected.Encapsulate(inverse * point);
            int width = playerView ? 1920 : 1800, height = playerView ? 1080 : 1200;
            if (playerView)
            {
                // Copy projection, not CopyFrom: the latter also inherits the
                // other PreviewRenderUtility's hidden scene-culling mask.
                preview.camera.orthographic = fixture.Quest.Camera.orthographic;
                preview.camera.orthographicSize = fixture.Quest.Camera.orthographicSize;
                preview.camera.fieldOfView = fixture.Quest.Camera.fieldOfView;
                preview.camera.nearClipPlane = fixture.Quest.Camera.nearClipPlane;
                preview.camera.farClipPlane = fixture.Quest.Camera.farClipPlane;
                preview.camera.transform.SetPositionAndRotation(fixture.Quest.Camera.transform.position, fixture.Quest.Camera.transform.rotation);
                preview.camera.aspect = 16f / 9;
            }
            else
            {
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = Mathf.Max(projected.size.y * .5f + .1f, projected.size.x * .5f * height / width + .1f);
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 20;
                preview.camera.transform.SetPositionAndRotation(rotation * projected.center - rotation * Vector3.forward * 6, rotation);
            }
            preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.025f, .023f, .043f);
            var meshes = new List<Mesh>(); var materials = new List<Material>();
            try
            {
                preview.BeginStaticPreview(new Rect(0, 0, width, height));
                foreach (var graphic in graphics)
                {
                    graphic.Rebuild(CanvasUpdate.PreRender);
                    var source = graphic.canvasRenderer.GetMesh(); if (source == null || source.vertexCount == 0) continue;
                    var mesh = UnityEngine.Object.Instantiate(source); meshes.Add(mesh);
                    var material = new Material(graphic.materialForRendering) { mainTexture = graphic.mainTexture, renderQueue = 3000 + meshes.Count };
                    materials.Add(material);
                    if (graphic is Text) material.SetVector("_TextureSampleAdd", new Vector4(1, 1, 1, 0));
                    var matrix = Matrix4x4.Translate(-preview.camera.transform.forward * .0001f * meshes.Count) * graphic.transform.localToWorldMatrix;
                    preview.DrawMesh(mesh, matrix, material, 0);
                }
                preview.Render(true); var image = preview.EndStaticPreview();
                File.WriteAllBytes(path, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
                foreach (var material in materials) UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, UnifiedWheelFixture.Private).SetValue(target, value);
    }
}
