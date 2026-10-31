#if UNITY_EDITOR && FRONTIER_BROWSER && FRONTIER_MOBILE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Frontier.Mobile.Editor
{
    // An edit-mode fixture of the actual mobile runtime. It requires a graphics
    // device, but no Editor window, Game view, Play mode or connected phone.
    public static class MobileHudPreview
    {
        private static int Width, Height;
        private const float Scale = 1.5f;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private static Vector2 CanvasSize;
        private static Rect SafePixels, SafeCanvas;

        [Serializable] private sealed class CheckResult { public string name, detail; public bool passed; }
        [Serializable] private sealed class CaptureResult
        {
            public string name, file;
            public int selectionCount, visibleTargets, drawnGraphics, drawnWorldGraphics;
            public float inputBlockedPercent;
        }
        [Serializable] private sealed class Report
        {
            public string unityVersion, renderer, output, orientation, limitation;
            public int width, height;
            public float[] simulatedSafeArea;
            public bool passed;
            public List<CheckResult> checks = new List<CheckResult>();
            public List<CaptureResult> captures = new List<CaptureResult>();
        }
        [Serializable] private sealed class Summary
        {
            public bool passed;
            public List<Report> orientations = new List<Report>();
        }

        private static T Field<T>(object target, string name)
            => (T)target.GetType().GetField(name, Private).GetValue(target);
        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, Private).SetValue(target, value);
        private static object Call(object target, string name, params object[] args)
        {
            var methods = target.GetType().GetMethods(Private).Where(method => method.Name == name
                && method.GetParameters().Length == args.Length).ToArray();
            if (methods.Length != 1) throw new MissingMethodException(target.GetType().Name, name + "/" + args.Length);
            return methods[0].Invoke(target, args);
        }

        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int outputIndex = Array.IndexOf(args, "-frontier-preview-output");
            string output = outputIndex >= 0 && outputIndex + 1 < args.Length ? args[outputIndex + 1]
                : Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../TestResults/MobileHudCompact"));
            if (!Path.IsPathRooted(output)) throw new ArgumentException("Use an absolute preview output path.");
            Directory.CreateDirectory(output);
            var summary = new Summary();
            summary.orientations.Add(RenderOrientation(Path.Combine(output, "landscape"), "landscape", 2340, 1080,
                new Rect(132, 63, 2340 - 264, 1080 - 63)));
            summary.orientations.Add(RenderOrientation(Path.Combine(output, "portrait"), "portrait", 1080, 2340,
                new Rect(0, 102, 1080, 2340 - 141 - 102)));
            summary.passed = summary.orientations.All(report => report.passed);
            File.WriteAllText(Path.Combine(output, "checks.json"), JsonUtility.ToJson(summary, true));
            if (!summary.passed) throw new InvalidOperationException("Mobile HUD preview failed. Inspect checks.json and PNG captures.");
        }

        private static Report RenderOrientation(string output, string orientation, int width, int height, Rect safePixels)
        {
            ConfigureFrame(width, height, safePixels);
            Directory.CreateDirectory(output);
            var report = new Report { unityVersion = Application.unityVersion, renderer = SystemInfo.graphicsDeviceName,
                orientation = orientation, width = Width, height = Height,
                simulatedSafeArea = new[] { SafePixels.x, SafePixels.y, SafePixels.width, SafePixels.height },
                output = output, limitation = "Actual shared Unity scene/UI rendered offscreen on Mac Metal. Safe area is simulated; this is not a capture of the physical iPhone or a touch-performance test." };
            PreviewRenderUtility preview = null;
            RenderTexture runtimeTarget = null;
            GameObject fixture = null, boardObject = null, console = null;
            GameSession session = null; BoardView board = null; QuestBridge quest = null; CommandPanel panel = null;
            string language = Localization.Preference;
            var previousPipeline = GraphicsSettings.defaultRenderPipeline;
            var previousQualityPipeline = QualitySettings.renderPipeline;
            Exception failure = null;
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new InvalidOperationException("Mobile HUD capture needs -batchmode -force-metal without -nographics.");
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Frontier/Settings/MobileURP.asset");
                if (pipeline == null) throw new InvalidOperationException("Shared mobile rendering pipeline is missing.");
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
                Localization.SetPreference("es");
                preview = new PreviewRenderUtility();
                preview.ambientColor = new Color(.45f, .55f, .65f);
                preview.lights[0].intensity = .8f; preview.lights[0].color = Color.white;
                preview.lights[0].transform.rotation = Quaternion.Euler(50, -30, 0);
                preview.lights[0].shadows = LightShadows.None; preview.lights[1].intensity = 0;

                fixture = new GameObject("Mobile HUD session fixture");
                session = fixture.AddComponent<GameSession>();
                if (session.Catalog == null) Call(session, "Awake");
                session.LoadMap("training");
                boardObject = new GameObject("Mobile HUD real board");
                board = boardObject.AddComponent<BoardView>(); board.Session = session;
                Call(board, "Start"); preview.AddSingleGO(boardObject);
                quest = fixture.AddComponent<QuestBridge>(); quest.Session = session; quest.Board = board;
                if (quest.Camera == null) Call(quest, "Awake");
                if (quest.Navigation.Camera == null) Call(quest.Navigation, "Awake");
                ResizeRuntimeTarget();
                board.Viewer = quest.Camera; Call(quest, "Start"); Call(quest.Navigation, "Start");
                console = new GameObject("Mobile HUD real interface");
                panel = console.AddComponent<CommandPanel>(); panel.Session = session; panel.Board = board; panel.Quest = quest;
                Call(panel, "Start"); quest.Navigation.Panel = panel;
                var canvas = Field<Canvas>(panel, "canvas");
                // ScreenSpaceOverlay cannot be submitted by a preview camera.
                // Keep its runtime pixel mapping for layout and public hit tests;
                // the real Canvas meshes are projected onto the camera plane below.
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)canvas.transform;
                canvasRect.sizeDelta = CanvasSize;
                canvasRect.SetPositionAndRotation(new Vector3(Width * .5f, Height * .5f, 0), Quaternion.identity);
                canvasRect.localScale = Vector3.one * Scale;
                session.StartMatch();
                // Record fixture commands in memory only, so input regressions can
                // inspect the submitted intent as well as the resulting unit task.
                session.Match.BeginRecording();
                Check(BrowserMobileInput.IsTouchMode, "native mobile input mode is compiled");

                Refresh();
                ValidateFriendlyTapAfterMove();
                ValidateShiftQueue();
                ValidateWorkerBuildingTaps();
                Check(Mathf.Abs(quest.Navigation.Distance * Mathf.Min(1, quest.Camera.aspect) - 34) < .05f,
                    "initial camera preserves battlefield scale on the short screen edge", "distance=" + quest.Navigation.Distance);
                ValidateLayout("idle"); Capture("01-idle");
                var own = session.View.entities.Where(entity => entity.owner == session.LocalPlayer).ToArray();
                var worker = own.First(entity => session.Catalog[entity.definition].role == "worker");
                var hq = own.First(entity => session.Catalog[entity.definition].role == "hq");
                Check(session.SelectVisibleEntities(new[] { worker.id, hq.id }), "real mobile selection accepts worker and headquarters");
                Refresh();
                var groupAction = Targets().FirstOrDefault(target => ActionId(target) == "mobile-group");
                Check(groupAction != null, "mixed selection exposes the group action");
                if (groupAction != null) Click(groupAction);
                Refresh();
                Check(session.GroupSize(0) == 2 && session.MobileGroupMatchesSelection(0), "group action saves the actual mixed selection");
                Check(Field<Array>(panel, "groupTargets").Cast<object>().Skip(1).All(target => !TargetRect(target).gameObject.activeInHierarchy),
                    "empty group slots leave the map visible");
                ValidateLayout("mixed selection"); Capture("02-mixed-selection");

                // Visit every real action page and its actual callback before
                // returning to the production screenshot's selection.
                session.SelectVisibleEntities(new[] { worker.id }); Refresh(); ValidateActionPages();
                session.SelectVisibleEntities(new[] { hq.id }); Refresh();
                Check(session.Send(CommandKind.Train, worker.definition), "headquarters accepts a real production order");
                Refresh();
                Check(session.View.entities.First(entity => entity.id == hq.id).queue.Length > 0, "production queue is present in the real player view");
                ValidateLayout("production and saved group"); Capture("03-production-groups");
                ValidateBuildingBadges();
                ValidateOrdersPopup();
                if (Width > Height) ValidateSameSessionRotation();
                session.CancelMode(); session.AttackMoveMode = true;
                Call(panel, "HandleMobileGroupTap", 0); Refresh();
                Check(session.MobileGroupMatchesSelection(0) && !session.HasTargetingMode,
                    "mobile group tap recalls the saved units and clears targeting");
                ValidateTutorialShift();
                ValidateMobileMenus();
                report.passed = report.checks.All(check => check.passed);

                void Check(bool passed, string name, string detail = "")
                    => report.checks.Add(new CheckResult { passed = passed, name = name, detail = detail });
                IEnumerable<object> Targets() => Field<IList>(panel, "targets").Cast<object>()
                    .Where(target => TargetRect(target) != null && TargetRect(target).gameObject.activeInHierarchy);
                void Click(object target) => Field<Action>(target, "click")?.Invoke();
                void ValidateBuildingBadges()
                {
                    void Badge(bool construction)
                    {
                        var badge = boardObject.GetComponentsInChildren<Canvas>().FirstOrDefault(item => item.name ==
                            (construction ? "Construction " : "Mineral workers ") + hq.id);
                        Check(badge != null, "building badge exists for " + (construction ? "construction" : "workforce"));
                        if (badge == null) return;
                        var corners = new Vector3[4]; ((RectTransform)badge.transform).GetWorldCorners(corners);
                        var projected = corners.Select(point => quest.Camera.WorldToScreenPoint(point)).ToArray();
                        float height = (projected.Max(point => point.y) - projected.Min(point => point.y))
                            * 720 / Mathf.Min(Width, Height);
                        Check(height >= (construction ? 44 : 24) && height <= (construction ? 66 : 40),
                            "building badge is readable and compact at overview zoom", "short-axis height=" + height);
                        Check(badge.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget),
                            "building badge never intercepts touch input");
                        var count = badge.GetComponentsInChildren<Text>().First(text => text.name ==
                            (construction ? "Construction percent" : "Assigned / optimal"));
                        Check(count.text.Contains(construction ? "%" : "/"), "building badge retains its actual game value");
                    }
                    Badge(false);
                    var actor = session.Match.Entities[hq.id];
                    bool complete = actor.complete; int remaining = actor.remaining;
                    try
                    {
                        actor.complete = false; actor.remaining = session.Catalog[actor.definition].duration / 2;
                        Refresh(); Badge(true); Capture("05-construction");
                    }
                    finally { actor.complete = complete; actor.remaining = remaining; Refresh(); }
                }
                void ValidateFriendlyTapAfterMove()
                {
                    var input = fixture.AddComponent<CommanderInput>();
                    input.Session = session; input.Board = board; input.Quest = quest; input.Panel = panel;
                    var units = session.View.entities.Where(entity => entity.owner == session.LocalPlayer
                        && session.Catalog[entity.definition].role == "worker").Take(2).ToArray();
                    Ray To(Vector3 world) => new Ray(world + board.transform.up * 10, -board.transform.up);
                    void TapUnit(int id)
                    {
                        Check(board.TryEntityVisualCenter(id, out var center), "tap regression finds the real unit visual");
                        Call(input, "TapMobileBoard", To(center), false, false, false);
                    }
                    session.CancelMode(); session.Select(0, false);
                    TapUnit(units[0].id);
                    Check(session.Selection.SetEquals(new[] { units[0].id }), "ordinary mobile tap selects unit A");
                    var ground = board.Content.TransformPoint(new Vector3(session.Map.width * .5f, 0, session.Map.height * .5f));
                    Call(input, "TapMobileBoard", To(ground), false, false, false);
                    Check(session.LastCommandKind == CommandKind.Move && session.LastCommandSequence > 0,
                        "mobile terrain tap orders unit A to move");
                    int movement = session.LastCommandSequence;
                    TapUnit(units[1].id);
                    Check(session.Selection.SetEquals(new[] { units[1].id }), "mobile tap after movement replaces unit A with unit B");
                    Check(session.LastCommandSequence == movement, "selecting unit B does not send another order to unit A");
                    session.Select(units[0].id, false);
                    Check(board.TryEntityVisualCenter(units[1].id, out var touchedCenter), "moving target regression finds unit B");
                    var touchedRay = To(touchedCenter);
                    var gesture = Field<MobileGesture>(input, "mobileGesture");
                    var press = new MobileGesture.Sample { Count = 1, Finger = 42, X = 100, Y = 100,
                        Down = true, AllowMap = true, SecondOverMap = true, Time = 10 };
                    Call(input, "TrackMobileSelection", touchedRay, press, gesture.Step(press, 12));
                    Check(session.Selection.SetEquals(new[] { units[0].id }), "pressing a friendly unit defers selection until release");
                    var actor = session.Match.Entities[units[1].id];var before = actor.position;
                    actor.position = new Point(session.Map.width * .5f, session.Map.height * .5f);Refresh();
                    var release = press;release.Down = false;release.Up = true;release.Count = 0;release.Time = 10.1f;
                    var tap = gesture.Step(release, 12);
                    Call(input, "TrackMobileSelection", touchedRay, release, tap);
                    Call(input, "CompleteMobileTap", touchedRay, false, false, false);
                    Check(session.Selection.SetEquals(new[] { units[1].id }) && session.LastCommandSequence == movement,
                        "unit moving away between press and release remains selected without another order");
                    actor.position = before;Refresh();
                    press.Time = 11;
                    Call(input, "TrackMobileSelection", touchedRay, press, gesture.Step(press, 12));
                    var drag = press;drag.Down = false;drag.X += 40;
                    Call(input, "TrackMobileSelection", touchedRay, drag, gesture.Step(drag, 12));
                    Check(Field<int>(input, "mobilePressedEntity") == 0 && session.LastCommandSequence == movement,
                        "one-finger map swipe cancels the captured unit without sending an order");
                    var finishDrag = drag;finishDrag.Up = true;finishDrag.Count = 0;
                    Call(input, "TrackMobileSelection", touchedRay, finishDrag, gesture.Step(finishDrag, 12));
                    session.Select(0, false);press.Time = 12;
                    Call(input, "TrackMobileSelection", touchedRay, press, gesture.Step(press, 12));
                    release.Time = 12.7f;
                    var held = gesture.Step(release, 12);
                    Call(input, "TrackMobileSelection", touchedRay, release, held);
                    Call(input, "CompleteMobileTap", touchedRay, held.Kind == MobileGesture.Kind.LongPress, false, false);
                    Check(session.Selection.SetEquals(new[] { units[1].id }) && session.LastCommandSequence == movement,
                        "slow first touch selects a friendly unit instead of sending an empty order");
                    Object.DestroyImmediate(input);
                    session.Select(0, false); Refresh();
                }
                void ValidateShiftQueue()
                {
                    var input = fixture.AddComponent<CommanderInput>();
                    input.Session = session; input.Board = board; input.Quest = quest; input.Panel = panel;
                    var router = (MobileTouchRouter)typeof(BrowserMobileInput).GetField("touchRouter",
                        BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    var gesture = Field<MobileGesture>(input, "mobileGesture");
                    var queueWorker = session.View.entities.First(entity => entity.owner == session.LocalPlayer
                        && session.Catalog[entity.definition].role == "worker");
                    var workerState = session.Match.Entities[queueWorker.id];
                    var headquarters = session.Match.Entities.Values.First(entity => entity.owner == session.LocalPlayer
                        && session.Catalog[entity.definition].role == "hq");
                    float health = headquarters.health;
                    Ray To(Vector3 world) => new Ray(world + board.transform.up * 10, -board.transform.up);
                    Ray Ground(float x, float y) => To(board.Content.TransformPoint(new Vector3(x, 0, y)));
                    MobileTouchRouter.Contact Contact(int id, Vector2 position, bool down = false, bool up = false, bool shift = false)
                        => new MobileTouchRouter.Contact { Id = id, X = position.x, Y = position.y,
                            Held = !up, Down = down, Up = up, OnShift = shift };
                    MobileTouchRouter.Contact Shift(bool down = false, bool up = false)
                        => Contact(700, Bounds(BrowserMobileInput.ShiftButton).center, down, up, true);
                    void ShiftTap(float time)
                    {
                        router.Read(new[] { Shift(down: true) }, time, true);
                        router.Read(new[] { Shift(up: true) }, time + .05f, true);
                    }
                    MobileGesture.Result Feed(Ray ray, MobileGesture.Sample sample)
                    {
                        sample.AllowMap = true; sample.SecondOverMap = true; sample.LassoSelection = true;
                        var result = gesture.Step(sample, 12);
                        Call(input, "TrackMobileSelection", ray, sample, result);
                        return result;
                    }
                    void Tap(Ray ray, float time, bool heldShift = false)
                    {
                        // Synthetic contacts exercise the actual shared router, classifier
                        // and tap dispatcher; no command is issued by a fixture shortcut.
                        var position = new Vector2(Width * .5f, Height * .55f);
                        var down = Contact(701, position, down: true);
                        Feed(ray, router.Read(heldShift ? new[] { Shift(), down } : new[] { down }, time, true));
                        var up = Contact(701, position, up: true);
                        var tap = Feed(ray, router.Read(heldShift ? new[] { Shift(), up } : new[] { up }, time + .08f, true));
                        Check(tap.Kind == MobileGesture.Kind.Tap, "queue fixture releases one confirmed map tap");
                        if (tap.Kind == MobileGesture.Kind.Tap)
                            Call(input, "CompleteMobileTap", ray, false, tap.Shift, tap.Append);
                    }
                    void Mode(MobileTouchRouter.ShiftMode expected, ConsoleGlyph.Kind icon, string capture)
                    {
                        Refresh();
                        Check(BrowserMobileInput.ShiftMode == expected, "real Shift router enters " + expected);
                        var glyph = BrowserMobileInput.ShiftButton.GetComponentsInChildren<ConsoleGlyph>().FirstOrDefault();
                        Check(glyph != null && glyph.Glyph == icon && glyph.isActiveAndEnabled,
                            "Shift displays the actual " + expected + " icon");
                        ValidateLayout("Shift " + expected); Capture(capture);
                    }
                    try
                    {
                        BrowserMobileInput.ResetShift(); session.CancelMode(); session.Select(0, false); Refresh();
                        Check(board.TryEntityVisualCenter(queueWorker.id, out var center), "queue fixture finds its real worker visual");
                        int beforeSelect = Field<int>(session, "sequence");
                        Tap(To(center), 20);
                        Check(session.Selection.SetEquals(new[] { queueWorker.id }) && Field<int>(session, "sequence") == beforeSelect,
                            "normal router tap selects one worker without dispatching an order");
                        Tap(Ground(24, 24), 21);
                        int firstSequence = workerState.activeOrderSequence;
                        Point firstDestination = workerState.destination;
                        Check(workerState.order == CommandKind.Move && firstSequence == beforeSelect + 1
                            && workerState.pendingOrders.Count == 0, "normal mobile tap dispatches exactly one initial Move");
                        Mode(MobileTouchRouter.ShiftMode.Normal, ConsoleGlyph.Kind.Move, "06-shift-normal");
                        ShiftTap(22);
                        Mode(MobileTouchRouter.ShiftMode.Attack, ConsoleGlyph.Kind.Attack, "07-shift-attack");
                        ShiftTap(22.15f);
                        Mode(MobileTouchRouter.ShiftMode.Queue, ConsoleGlyph.Kind.Queue, "08-shift-queue");
                        Check(Field<int>(session, "sequence") == firstSequence && session.Selection.SetEquals(new[] { queueWorker.id }),
                            "Shift tap and double tap change mode without dispatching or changing selection");
                        Tap(Ground(27, 24), 23); Tap(Ground(27, 27), 24);
                        Check(workerState.activeOrderSequence == firstSequence && workerState.order == CommandKind.Move
                            && workerState.destination.Distance(firstDestination) < .001f,
                            "queued mobile waypoints preserve the initial active movement and destination");
                        Check(workerState.pendingOrders.Count == 2 && workerState.pendingOrders.All(order => order.kind == CommandKind.Move)
                            && workerState.pendingOrders.Select(order => order.sourceSequence).SequenceEqual(new[] { firstSequence + 1, firstSequence + 2 })
                            && Field<int>(session, "sequence") == firstSequence + 2,
                            "two queued taps append exactly two FIFO moves without duplicate dispatch");
                        var resource = session.View.resources.First(item => item.kind == "mineral" && item.remaining != 0);
                        Tap(Ground(resource.position.x, resource.position.y), 25);
                        Check(workerState.pendingOrders.Count == 3 && workerState.pendingOrders[2].kind == CommandKind.Gather
                            && workerState.pendingOrders[2].target == resource.id && workerState.pendingOrders[2].sourceSequence == firstSequence + 3,
                            "queue mode appends one contextual Gather after movement");
                        headquarters.health = health - 50; Refresh();
                        Check(board.TryEntityVisualCenter(headquarters.id, out var repairCenter), "queue fixture finds the damaged headquarters visual");
                        Tap(To(repairCenter), 26);
                        Check(workerState.pendingOrders.Count == 4 && workerState.pendingOrders[3].kind == CommandKind.Repair
                            && workerState.pendingOrders[3].target == headquarters.id && workerState.pendingOrders[3].sourceSequence == firstSequence + 4,
                            "queued friendly tap repairs the building without selecting it");
                        BrowserMobileInput.ResetShift();
                        router.Read(new[] { Shift(down: true) }, 27, true);
                        Tap(Ground(24, 27), 27.6f, heldShift: true);
                        router.Read(new[] { Shift(up: true) }, 27.8f, true);
                        Check(BrowserMobileInput.ShiftMode == MobileTouchRouter.ShiftMode.Normal
                            && workerState.pendingOrders.Count == 5 && workerState.pendingOrders[4].kind == CommandKind.Move,
                            "holding Shift appends one move and release restores normal mode");
                        Check(workerState.activeOrderSequence == firstSequence && workerState.order == CommandKind.Move
                            && workerState.destination.Distance(firstDestination) < .001f
                            && workerState.pendingOrders.Select(order => order.sourceSequence).SequenceEqual(Enumerable.Range(firstSequence + 1, 5))
                            && Field<int>(session, "sequence") == firstSequence + 5 && session.Selection.SetEquals(new[] { queueWorker.id }),
                            "all queued actions preserve selection and active order with one dispatch per tap");
                        Refresh();
                        var orders = Targets().FirstOrDefault(target => ActionId(target) == "mobile-orders");
                        Check(orders != null, "queued unit actions expose the real Orders popup");
                        if (orders != null) { Click(orders); Refresh(); }
                        Check(Field<RectTransform>(panel, "browserQueue").gameObject.activeInHierarchy
                            && Field<IList>(panel, "browserQueueTargets").Count > 0,
                            "queued unit actions are visible in the existing cancellable queue UI");
                        // Expanded order details intentionally cover more map than the
                        // compact HUD; validate the compact area budget after closing.
                        ValidateLayout("queued unit actions"); Capture("09-mobile-queued-orders", false);
                        if (orders != null) { Click(orders); Refresh(); }
                        Check(!Field<RectTransform>(panel, "browserQueue").gameObject.activeInHierarchy,
                            "Orders control returns to compact gameplay after inspecting queued tasks");
                        Capture("10-mobile-queue-collapsed");
                    }
                    finally
                    {
                        headquarters.health = health;
                        BrowserMobileInput.ResetShift();
                        session.CancelMode(); session.Select(queueWorker.id, false); session.Send(CommandKind.Stop);
                        Object.DestroyImmediate(input);
                        session.Select(0, false); Refresh();
                    }
                }
                void ValidateWorkerBuildingTaps()
                {
                    var input = fixture.AddComponent<CommanderInput>();
                    input.Session = session; input.Board = board; input.Quest = quest; input.Panel = panel;
                    var gesture = Field<MobileGesture>(input, "mobileGesture");
                    var workers = session.Match.Entities.Values.Where(entity => entity.owner == session.LocalPlayer
                        && session.Catalog[entity.definition].role == "worker").Take(3).ToArray();
                    var selectedWorkers = workers.Take(2).ToArray();
                    int[] workerIds = selectedWorkers.Select(entity => entity.id).ToArray();
                    var previousGather = selectedWorkers.Where(entity => entity.order == CommandKind.Gather)
                        .Select(entity => (id: entity.id, resource: entity.resourceId)).ToArray();
                    var temporary = new List<EntityState>();
                    int nextFixtureId = session.Match.Entities.Keys.Max() + 1000;
                    float inputTime = 50;
                    EntityState Add(string role, Point point)
                    {
                        var type = session.Catalog.Role(session.Faction, role);
                        if (type.building && !session.Match.Map.CanPlace(point, type.footprint))
                            throw new InvalidOperationException("Worker tap fixture needs an unoccupied building footprint.");
                        var entity = new EntityState { id = nextFixtureId++, owner = session.LocalPlayer,
                            definition = type.id, position = point, health = type.health };
                        session.Match.Entities.Add(entity.id, entity); temporary.Add(entity);
                        if (type.building) session.Match.Map.Occupy(point, type.footprint, entity.id);
                        return entity;
                    }
                    void SelectForCase(params int[] ids)
                    {
                        session.CancelMode(); session.SelectVisibleEntities(ids);
                        Call(input, "ResetMapGestures"); Refresh();
                    }
                    Ray Aim(int targetId, string context)
                    {
                        Check(board.TryEntityVisualCenter(targetId, out var center), context + ": target has a visible model");
                        var candidates = new List<Vector3> { center };
                        var target = session.Match.Entities[targetId]; var type = session.Catalog[target.definition];
                        if (type.building)
                        {
                            float middle = type.footprint * .5f;
                            foreach (var offset in new[] { new Vector2(.25f, middle), new Vector2(type.footprint - .25f, middle),
                                new Vector2(middle, .25f), new Vector2(middle, type.footprint - .25f) })
                                candidates.Add(board.Content.TransformPoint(new Vector3(target.position.x + offset.x, 0, target.position.y + offset.y)));
                        }
                        foreach (var candidate in candidates)
                        {
                            var ray = new Ray(candidate + board.transform.up * 10, -board.transform.up);
                            if (!board.RayToMap(ray, out var point, out var hit)) continue;
                            board.ResolvePointerTarget(ray, point, hit, out int entity, out int resource);
                            if (entity != targetId || resource != 0) continue;
                            Check(true, context + ": ray resolves the intended entity, not its resource deposit");
                            return ray;
                        }
                        throw new InvalidOperationException(context + ": no ray resolves the intended entity.");
                    }
                    RecordedCommand[] Touch(int targetId, string context, bool swipe = false)
                    {
                        var ray = Aim(targetId, context);
                        int before = session.Match.RecordedCommandCount, sequence = Field<int>(session, "sequence");
                        int[] selection = session.Selection.ToArray();
                        var press = new MobileGesture.Sample { Count = 1, Finger = 801, X = Width * .5f, Y = Height * .55f,
                            Down = true, AllowMap = true, SecondOverMap = true, LassoSelection = true, Time = inputTime++ };
                        Call(input, "TrackMobileSelection", ray, press, gesture.Step(press, 12));
                        Check(session.Selection.SetEquals(selection) && session.Match.RecordedCommandCount == before
                            && Field<int>(session, "sequence") == sequence, context + ": DOWN does not select or dispatch early");
                        var release = press; release.Down = false; release.Up = true; release.Count = 0; release.Time += .1f;
                        if (swipe)
                        {
                            var drag = press; drag.Down = false; drag.X += 40; drag.Time += .05f;
                            var moved = gesture.Step(drag, 12); Call(input, "TrackMobileSelection", ray, drag, moved);
                            Check(moved.Cancelled, context + ": map swipe cancels the pending tap");
                            if (moved.Cancelled) Call(input, "CancelMobileGesture");
                            release.X = drag.X;
                        }
                        var result = gesture.Step(release, 12);
                        Call(input, "TrackMobileSelection", ray, release, result);
                        Check(swipe ? result.Kind != MobileGesture.Kind.Tap : result.Kind == MobileGesture.Kind.Tap,
                            context + ": release has the expected gesture classification");
                        if (result.Kind == MobileGesture.Kind.Tap)
                            Call(input, "CompleteMobileTap", ray, false, result.Shift, result.Append);
                        var commands = session.Match.CopyRecordedCommands(before);
                        Check(Field<int>(session, "sequence") - sequence == commands.Length,
                            context + ": sequence and recorded dispatch counts agree");
                        return commands;
                    }
                    void WorkOrder(EntityState target, CommandKind intent, CommandKind task, int commandTarget, string context)
                    {
                        SelectForCase(workerIds);
                        var commands = Touch(target.id, context);
                        Check(commands.Length == 1 && commands[0].accepted && commands[0].command.kind == intent
                            && commands[0].command.target == commandTarget && !commands[0].command.append
                            && commands[0].command.entities.OrderBy(id => id).SequenceEqual(workerIds.OrderBy(id => id)),
                            context + ": one accepted contextual command addresses only the selected workers");
                        Check(session.Selection.SetEquals(workerIds), context + ": workers remain selected");
                        Check(commands.Length == 1 && selectedWorkers.All(entity => entity.order == task && entity.target == commandTarget
                            && entity.activeOrderSequence == commands[0].command.sequence && entity.pendingOrders.Count == 0),
                            context + ": every worker starts the expected task without queued duplicates");
                    }
                    void SelectionOnly(int targetId, int[] selection, string context)
                    {
                        SelectForCase(selection);
                        var commands = Touch(targetId, context);
                        Check(commands.Length == 0 && session.Selection.SetEquals(new[] { targetId }),
                            context + ": normal tap changes selection without issuing work orders");
                    }
                    try
                    {
                        BrowserMobileInput.ResetShift();
                        var gas = session.View.resources.First(resource => resource.kind == "catalyst" && resource.remaining > 0);
                        var extractorType = session.Catalog.Role(session.Faction, "extractor");
                        var extractor = Add("extractor", new Point(gas.position.x - extractorType.footprint * .5f,
                            gas.position.y - extractorType.footprint * .5f));
                        var factory = Add("barracks", new Point(19, 12));
                        var soldier = Add("melee", new Point(20.5f, 17.5f));
                        var factoryType = session.Catalog[factory.definition];
                        Refresh();
                        Check(session.View.resources.Any(resource => resource.id == gas.id && resource.extractorId == extractor.id),
                            "worker tap fixture exposes the real extractor-to-gas link in PlayerView");
                        WorkOrder(extractor, CommandKind.Gather, CommandKind.Gather, gas.id, "workers tap completed refinery");
                        factory.complete = false; factory.remaining = factoryType.duration / 2; factory.health = factoryType.health * .5f;
                        WorkOrder(factory, CommandKind.Repair, CommandKind.Build, factory.id, "workers tap incomplete construction");
                        factory.complete = true; factory.remaining = 0; factory.health = factoryType.health - 50;
                        WorkOrder(factory, CommandKind.Repair, CommandKind.Repair, factory.id, "workers tap damaged factory");
                        factory.health = factoryType.health;
                        SelectionOnly(factory.id, workerIds, "workers tap healthy completed factory");
                        SelectionOnly(workers[2].id, workerIds, "workers tap another friendly unit");
                        int[] mixed = workerIds.Concat(new[] { soldier.id }).ToArray();
                        SelectionOnly(extractor.id, mixed, "mixed workers and soldier tap refinery");
                        factory.complete = false; factory.remaining = factoryType.duration / 2;
                        SelectionOnly(factory.id, mixed, "mixed workers and soldier tap construction");
                        factory.complete = true; factory.remaining = 0; factory.health = factoryType.health - 50;
                        SelectionOnly(factory.id, mixed, "mixed workers and soldier tap damaged factory");
                        SelectForCase(workerIds);
                        int[] active = selectedWorkers.Select(entity => entity.activeOrderSequence).ToArray();
                        var cancelled = Touch(extractor.id, "workers swipe over refinery", swipe: true);
                        Check(cancelled.Length == 0 && session.Selection.SetEquals(workerIds)
                            && selectedWorkers.Select(entity => entity.activeOrderSequence).SequenceEqual(active),
                            "cancelled building swipe preserves worker selection and current orders");
                    }
                    finally
                    {
                        session.CancelMode(); session.SelectVisibleEntities(workerIds); session.Send(CommandKind.Stop);
                        foreach (var entity in temporary)
                        {
                            var type = session.Catalog[entity.definition];
                            session.Match.Entities.Remove(entity.id);
                            if (type.building) session.Match.Map.Occupy(entity.position, type.footprint, 0);
                        }
                        // Keep subsequent screenshots' original mining assignments.
                        foreach (var assignment in previousGather)
                        {
                            session.Select(assignment.id, false);
                            session.Send(CommandKind.Gather, target: assignment.resource);
                        }
                        BrowserMobileInput.ResetShift(); Object.DestroyImmediate(input);
                        session.Select(0, false); Refresh();
                    }
                }
                void ResizeRuntimeTarget()
                {
                    var previous = runtimeTarget;
                    runtimeTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                        { name = "Mobile fixture screen", hideFlags = HideFlags.HideAndDontSave };
                    runtimeTarget.Create(); quest.Camera.targetTexture = runtimeTarget;
                    quest.Camera.aspect = Width / (float)Height;
                    if (previous != null) { previous.Release(); Object.DestroyImmediate(previous); }
                }
                void Refresh()
                {
                    Call(session, "RefreshView"); Call(board, "LateUpdate");
                    Set(panel, "nextText", 0f); Call(panel, "LateUpdate");
                    Call(panel, "LayoutMobileHud", CanvasSize, SafeCanvas);
                    var actions = Field<RectTransform>(panel, "actions");
                    for (int i = actions.childCount - 1; i >= 0; i--) Object.DestroyImmediate(actions.GetChild(i).gameObject);
                    Field<IList>(panel, "targets").Clear();
                    Call(panel, "BuildTutorialCard");
                    Call(panel, "BuildMobileActions");
                    Call(quest.Navigation, "LateUpdate");
                    var minimap = Field<BrowserMinimap>(panel, "browserMinimap");
                    Set(minimap, "nextRefresh", 0f); Call(minimap, "Update");
                    Canvas.ForceUpdateCanvases();
                }
                void ValidateLayout(string state)
                {
                    Check(quest.Navigation.Viewport == new Rect(0, 0, 1, 1), state + ": battlefield retains full viewport");
                    var actionRect = Bounds(Field<RectTransform>(panel, "actions"));
                    var footer = Bounds(Field<RectTransform>(panel, "mobileCameraFooter"));
                    Check(Contains(SafePixels, footer), state + ": camera footer fits the safe area");
                    Check(panel.BrowserContainsScreenPoint(footer.center) && panel.BrowserCameraPanHandleContains(footer.center),
                        state + ": footer owns camera touch without leaking to map");
                    float maximumActionHeight = Height > Width ? 280 : 150;
                    Check(actionRect.height <= maximumActionHeight && actionRect.yMin >= footer.yMax
                        && actionRect.yMax <= SafePixels.y + maximumActionHeight + footer.height + 30,
                        state + ": compact action rows stay above the home indicator", actionRect.ToString());
                    foreach (var target in Targets())
                    {
                        var rect = TargetRect(target); var bounds = Bounds(rect);
                        string label = rect.GetComponentInChildren<Text>()?.text ?? rect.name;
                        Check(Contains(SafePixels, bounds), state + ": control fits simulated safe area: " + label, bounds.ToString());
                        Check(panel.BrowserContainsScreenPoint(bounds.center), state + ": control consumes its center: " + label);
                        Check(!bounds.Overlaps(footer), state + ": action does not overlap camera footer: " + label);
                        Check(!panel.BrowserCameraPanHandleContains(bounds.center), state + ": button cannot start camera drag: " + label);
                    }
                    var activeTargets = Targets().Select(TargetRect).Distinct().ToArray();
                    for (int i = 0; i < activeTargets.Length; i++)
                        for (int j = i + 1; j < activeTargets.Length; j++)
                        {
                            var first = activeTargets[i]; var second = activeTargets[j];
                            // A popup may deliberately contain its own controls.
                            if (first.IsChildOf(second) || second.IsChildOf(first)) continue;
                            var a = Bounds(first); var b = Bounds(second);
                            float overlapWidth = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                            float overlapHeight = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                            Check(overlapWidth <= 1 || overlapHeight <= 1,
                                state + ": controls do not overlap: " + TargetLabel(first) + " / " + TargetLabel(second),
                                "intersection=" + overlapWidth.ToString("F1") + "x" + overlapHeight.ToString("F1"));
                        }
                    foreach (string name in new[] { "browserBar", "mobileToolbar", "browserCommandCard" })
                    {
                        var image = Field<RectTransform>(panel, name).GetComponent<Image>();
                        Check(image == null || image.color.a <= .01f, state + ": layout container is transparent: " + name);
                    }
                    foreach (string name in new[] { "browserMinerals", "browserGas", "browserSupply" })
                    {
                        var label = Field<Text>(panel, name);
                        Check(!panel.BrowserContainsScreenPoint(Bounds(label.rectTransform).center), state + ": passive resource label lets touches reach map: " + name);
                    }
                    Check(!panel.BrowserContainsScreenPoint(new Vector2(Width * .5f, Height * .55f)), state + ": center battlefield accepts gestures");
                    var actionBounds = Targets().Where(target => TargetRect(target).IsChildOf(Field<RectTransform>(panel, "actions")))
                        .Select(target => Bounds(TargetRect(target))).OrderBy(rect => rect.x).ToArray();
                    for (int i = 1; i < actionBounds.Length; i++)
                    {
                        var previous = actionBounds[i - 1]; var current = actionBounds[i];
                        if (current.xMin - previous.xMax < 2 || Mathf.Abs(previous.center.y - current.center.y) > 1) continue;
                        var gap = new Vector2((previous.xMax + current.xMin) * .5f, current.center.y);
                        Check(!panel.BrowserContainsScreenPoint(gap), state + ": gap between action buttons passes through");
                    }
                }
                void ValidateActionPages()
                {
                    var expected = new HashSet<string>(SelectionActions.For(session).Items.Select(action => action.Id));
                    if (session.CanSaveMobileGroup) expected.Add("mobile-group");
                    var visited = new HashSet<string>(); var pages = new HashSet<int>();
                    for (int attempt = 0; attempt < 16; attempt++)
                    {
                        int page = Field<int>(panel, "browserActionPage"); if (!pages.Add(page)) break;
                        foreach (var target in Targets()) if (ActionId(target) != null) visited.Add(ActionId(target));
                        var actions = Field<RectTransform>(panel, "actions");
                        var next = Targets().FirstOrDefault(target => TargetRect(target).IsChildOf(actions)
                            && ActionId(target) == null && Field<Action>(target, "click") != null);
                        if (next == null) break;
                        Check(panel.BrowserContainsScreenPoint(Bounds(TargetRect(next)).center), "action page control remains reachable");
                        Click(next); Refresh();
                    }
                    Check(expected.IsSubsetOf(visited), "every real worker action remains reachable through paging",
                        "Missing: " + string.Join(", ", expected.Except(visited)) + "; pages=" + pages.Count);
                    Check(expected.Count <= 5 || pages.Count > 1, "worker action paging is exercised when required");
                }
                void ValidateOrdersPopup()
                {
                    var toggle = Targets().FirstOrDefault(target => ActionId(target) == "mobile-orders");
                    Check(toggle != null, "nonempty production exposes Orders control");
                    if (toggle == null) return;
                    var queue = Field<RectTransform>(panel, "browserQueue");
                    Check(!queue.gameObject.activeInHierarchy, "production queue starts collapsed");
                    Click(toggle); Refresh();
                    Check(queue.gameObject.activeInHierarchy && Field<IList>(panel, "browserQueueTargets").Count > 0,
                        "Orders control opens real cancellable production items");
                    foreach (object target in Field<IList>(panel, "browserQueueTargets"))
                    {
                        Check(Contains(SafePixels, Bounds(TargetRect(target))), "production item fits safe area");
                        Check(panel.BrowserContainsScreenPoint(Bounds(TargetRect(target)).center), "production item intercepts its center");
                    }
                    ValidateLayout("expanded production popup");
                    toggle = Targets().FirstOrDefault(target => ActionId(target) == "mobile-orders");
                    if (toggle != null) Click(toggle);
                    Refresh(); Check(!queue.gameObject.activeInHierarchy, "Orders control closes the production popup");
                }
                void ValidateSameSessionRotation()
                {
                    var identity = session.SessionIdentity;
                    int[] selection = session.Selection.ToArray();
                    int[][] groups = Field<List<int>[]>(session, "groups").Select(group => group.ToArray()).ToArray();
                    int tick = session.Match.Tick;
                    int queued = session.View.entities.Sum(entity => entity.queue.Length);
                    float framing = quest.Navigation.Distance * Mathf.Min(1, quest.Camera.aspect);
                    Rotate(1080, 2340, new Rect(0, 102, 1080, 2340 - 141 - 102), "portrait");
                    Rotate(2340, 1080, new Rect(132, 63, 2340 - 264, 1080 - 63), "landscape again");

                    void Rotate(int nextWidth, int nextHeight, Rect nextSafeArea, string label)
                    {
                        ConfigureFrame(nextWidth, nextHeight, nextSafeArea);
                        canvasRect.sizeDelta = CanvasSize;
                        canvasRect.SetPositionAndRotation(new Vector3(Width * .5f, Height * .5f, 0), Quaternion.identity);
                        ResizeRuntimeTarget();
                        Refresh();
                        Check(ReferenceEquals(session.SessionIdentity, identity) && session.Match.Tick == tick,
                            "rotation to " + label + " retains the same simulation without advancing it");
                        Check(session.Selection.SetEquals(selection), "rotation to " + label + " preserves selection");
                        Check(Field<List<int>[]>(session, "groups").Select((group, index) => group.SequenceEqual(groups[index])).All(same => same),
                            "rotation to " + label + " preserves every control group member");
                        Check(session.View.entities.Sum(entity => entity.queue.Length) == queued,
                            "rotation to " + label + " preserves production");
                        Check(Mathf.Abs(quest.Navigation.Distance * Mathf.Min(1, quest.Camera.aspect) - framing) < .05f,
                            "rotation to " + label + " preserves camera scale on the short edge", "distance=" + quest.Navigation.Distance);
                        ValidateLayout("rotation to " + label);
                    }
                }
                void ValidateTutorialShift()
                {
                    session.StartTutorial(false);
                    session.ContinueTutorial(); session.ContinueTutorial();
                    Refresh();
                    var card = Field<RectTransform>(panel, "tutorialCard");
                    var shift = Targets().FirstOrDefault(target => ActionId(target) == "mobile-shift");
                    Check(session.IsTutorial && session.Tutorial.Step == TutorialStep.SelectWorker
                        && !session.Tutorial.Briefing && panel.AcceptsGameInput,
                        "tutorial fixture reaches active SelectWorker through real Continue actions");
                    Check(card.gameObject.activeInHierarchy, "active tutorial instructions are visible");
                    Check(shift != null && panel.BrowserShiftAvailable, "Shift remains available beside the visible tutorial");
                    if (shift != null)
                    {
                        var shiftRect = TargetRect(shift); var bounds = Bounds(shiftRect);
                        Check(BrowserMobileInput.ShiftButton == shiftRect, "visible Shift is the input adapter's actual target");
                        Check(Contains(SafePixels, bounds), "tutorial Shift fits the simulated safe area");
                        Check(!bounds.Overlaps(Bounds(card)), "tutorial card does not cover Shift");
                        Check(panel.BrowserContainsScreenPoint(bounds.center), "tutorial Shift intercepts its touch position");
                    }
                    Capture("04-tutorial-shift", false);
                }
                void ValidateMobileMenus()
                {
                    session.LeaveMatch();
                    Set(session, "rankingEndpoint", session.PublicBackendUrl);
                    Set(session, "ranking", new RankSnapshot {
                        profile = new RankProfile { rating = 1248, games = 12, wins = 7, losses = 5, rank = 4 },
                        entries = Enumerable.Range(1, 10).Select(i => new RankEntry { rank = i, rating = 1600-i*70,
                            displayName = "Commander " + i, isSelf = i == 4 }).ToArray()
                    });
                    foreach (string locale in new[] { "es", "en" })
                    {
                        Localization.SetPreference(locale);
                        foreach (var page in new[] { CommandPanel.MenuPage.Main, CommandPanel.MenuPage.MatchSetup,
                            CommandPanel.MenuPage.Settings, CommandPanel.MenuPage.Guide, CommandPanel.MenuPage.OnlineSetup,
                            CommandPanel.MenuPage.LeaveConfirmation, CommandPanel.MenuPage.Ranking })
                        {
                            Call(panel, "Show", page);
                            Call(panel, "LayoutMobileMenu", CanvasSize, SafeCanvas);
                            var actions = Field<RectTransform>(panel, "actions");
                            for (int i = actions.childCount-1; i >= 0; i--) Object.DestroyImmediate(actions.GetChild(i).gameObject);
                            Field<IList>(panel, "targets").Clear();
                            Call(panel, "BuildMobileMenu"); Call(panel, "UpdateMenuText");
                            Canvas.ForceUpdateCanvases();
                            string context = locale + " " + page;
                            var menuTargets = Targets().ToArray();
                            Check(menuTargets.Length > 0, context + ": menu has touch targets");
                            Check(!panel.BrowserCameraPanHandleContains(SafePixels.center), context + ": menu cannot start footer navigation");
                            foreach (var target in menuTargets)
                            {
                                var bounds = Bounds(TargetRect(target));
                                Check(Contains(SafePixels, bounds), context + ": menu button fits safe area: " + TargetLabel(TargetRect(target)), bounds.ToString());
                                Check(bounds.height >= 70, context + ": menu button retains a usable touch height");
                            }
                            for (int a = 0; a < menuTargets.Length; a++) for (int b = a+1; b < menuTargets.Length; b++)
                                Check(!Bounds(TargetRect(menuTargets[a])).Overlaps(Bounds(TargetRect(menuTargets[b]))), context + ": menu buttons do not overlap");
                            var primary = menuTargets.FirstOrDefault(target => ActionId(target) == "mobile-menu-primary");
                            if (page == CommandPanel.MenuPage.Main || page == CommandPanel.MenuPage.MatchSetup
                                || page == CommandPanel.MenuPage.OnlineSetup || page == CommandPanel.MenuPage.LeaveConfirmation)
                                Check(primary != null && Field<Color>(primary, "normal") == ConsoleTheme.Primary, context + ": primary action is visually distinct");
                            if (page == CommandPanel.MenuPage.Ranking)
                            {
                                Check(actions.GetComponentsInChildren<RectTransform>().Count(rect => rect.name.StartsWith("Mobile ranking entry ")) == 10,
                                    context + ": all top ten entries are visible");
                                foreach (var row in actions.GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith("Mobile ranking entry ")))
                                    Check(Contains(SafePixels, Bounds(row)), context + ": ranking row fits safe area");
                                Check(menuTargets.Any(target => ActionId(target) == "mobile-ranking-back")
                                    && menuTargets.Any(target => ActionId(target) == "mobile-ranking-refresh"), context + ": ranking preserves back and refresh");
                            }
                            Capture("menu-" + locale + "-" + page.ToString().ToLowerInvariant(), false);
                            if (page == CommandPanel.MenuPage.Main && primary != null)
                            { Click(primary); Check(panel.Page == CommandPanel.MenuPage.MatchSetup, context + ": primary action opens real match setup"); }
                        }
                    }
                    Localization.SetPreference("es");
                }
                void Capture(string name, bool checkMapAvailability = true)
                {
                    var camera = preview.camera; var source = quest.Camera;
                    camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                    camera.orthographic = false; camera.fieldOfView = source.fieldOfView;
                    camera.nearClipPlane = source.nearClipPlane; camera.farClipPlane = source.farClipPlane;
                    camera.rect = new Rect(0, 0, 1, 1); camera.aspect = Width / (float)Height;
                    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = source.backgroundColor;
                    camera.allowHDR = false; camera.allowMSAA = false;
                    var meshes = new List<Mesh>(); var materials = new List<Material>(); int worldGraphics = 0;
                    Texture2D image = null;
                    try
                    {
                        preview.BeginStaticPreview(new Rect(0, 0, Width, Height));
                        Canvas.ForceUpdateCanvases();
                        float distance = Mathf.Max(.001f, camera.nearClipPlane * 4);
                        Vector3 lowerLeft = camera.ViewportToWorldPoint(new Vector3(0, 0, distance));
                        Vector3 lowerRight = camera.ViewportToWorldPoint(new Vector3(1, 0, distance));
                        Vector3 upperLeft = camera.ViewportToWorldPoint(new Vector3(0, 1, distance));
                        var pixelToWorld = Matrix4x4.TRS(lowerLeft, camera.transform.rotation,
                            new Vector3(Vector3.Distance(lowerLeft, lowerRight) / Width,
                                Vector3.Distance(lowerLeft, upperLeft) / Height, 1));
                        // World-space building badges are not part of the HUD's
                        // pixel canvas. Submit their actual meshes in world space.
                        foreach (var graphic in boardObject.GetComponentsInChildren<Graphic>())
                        {
                            if (!graphic.isActiveAndEnabled) continue;
                            graphic.Rebuild(CanvasUpdate.PreRender);
                            var sourceMesh = graphic.canvasRenderer.GetMesh();
                            if (sourceMesh == null || sourceMesh.vertexCount == 0) continue;
                            var mesh = Object.Instantiate(sourceMesh); meshes.Add(mesh);
                            var material = new Material(graphic.materialForRendering) { mainTexture = graphic.mainTexture, renderQueue = 3000 };
                            materials.Add(material);
                            if (graphic is Text) material.SetVector("_TextureSampleAdd", new Vector4(1, 1, 1, 0));
                            preview.DrawMesh(mesh, graphic.transform.localToWorldMatrix, material, 0); worldGraphics++;
                        }
                        foreach (var graphic in console.GetComponentsInChildren<Graphic>())
                        {
                            if (!graphic.isActiveAndEnabled) continue;
                            graphic.Rebuild(CanvasUpdate.PreRender);
                            var sourceMesh = graphic.canvasRenderer.GetMesh();
                            if (sourceMesh == null || sourceMesh.vertexCount == 0) continue;
                            var mesh = Object.Instantiate(sourceMesh); meshes.Add(mesh);
                            var material = new Material(graphic.materialForRendering) { mainTexture = graphic.mainTexture,
                                renderQueue = 3000 + meshes.Count };
                            materials.Add(material);
                            if (graphic is Text) material.SetVector("_TextureSampleAdd", new Vector4(1, 1, 1, 0));
                            var matrix = Matrix4x4.Translate(-camera.transform.forward * (distance * .00001f * meshes.Count))
                                * pixelToWorld * graphic.transform.localToWorldMatrix;
                            preview.DrawMesh(mesh, matrix, material, 0);
                        }
                        // Preserve the RTS field of view in portrait too;
                        // PreviewRenderUtility normally widens narrow previews.
                        preview.Render(true, false); image = preview.EndStaticPreview();
                        string file = name + ".png"; File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG());
                        var pixels = image.GetPixels32(); var colors = new HashSet<int>();
                        for (int y = Height / 3; y < Height * 2 / 3; y += 11)
                            for (int x = Width / 3; x < Width * 2 / 3; x += 11)
                            { var color = pixels[y * Width + x]; colors.Add(color.r << 16 | color.g << 8 | color.b); }
                        int minimumGraphics = panel.Page == CommandPanel.MenuPage.Actions ? 20 : 8;
                        Check(meshes.Count > minimumGraphics && meshes.Sum(mesh => mesh.vertexCount) > 200, name + ": real Canvas generated visible geometry");
                        if (panel.Page == CommandPanel.MenuPage.Actions)
                        {
                        Check(worldGraphics > 0, name + ": actual world-space building badges are rendered");
                        Check(colors.Count > 32, name + ": center map render contains varied terrain pixels", "colors=" + colors.Count);
                        var minimap = Field<BrowserMinimap>(panel, "browserMinimap");
                        var overview = minimap.mainTexture as Texture2D;
                        Check(overview != null && overview != Texture2D.whiteTexture
                            && overview.width == session.Map.width && overview.height == session.Map.height
                            && overview.GetPixels32().Select(color => color.r << 16 | color.g << 8 | color.b).Distinct().Take(4).Count() >= 4,
                            name + ": minimap contains actual filtered-view terrain and markers");
                        }
                        int samples = 0, blocked = 0;
                        for (float y = SafePixels.yMin + 12; y < SafePixels.yMax; y += 24)
                            for (float x = SafePixels.xMin + 12; x < SafePixels.xMax; x += 24)
                            { samples++; if (panel.BrowserContainsScreenPoint(new Vector2(x, y))) blocked++; }
                        float blockedPercent = blocked * 100f / samples;
                        if (checkMapAvailability)
                            Check(blockedPercent < 35, name + ": most of the safe area remains available for map gestures", blockedPercent.ToString("F1") + "% of safe area blocked");
                        report.captures.Add(new CaptureResult { name = name, file = file, selectionCount = session.Selection.Count,
                            visibleTargets = Targets().Count(), drawnGraphics = meshes.Count, drawnWorldGraphics = worldGraphics, inputBlockedPercent = blockedPercent });
                    }
                    finally
                    {
                        if (image != null) Object.DestroyImmediate(image);
                        foreach (var mesh in meshes) Object.DestroyImmediate(mesh);
                        foreach (var material in materials) Object.DestroyImmediate(material);
                    }
                }
            }
            catch (Exception error)
            {
                failure = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                report.checks.Add(new CheckResult { name = "fixture completed", passed = false, detail = failure.ToString() });
                report.passed = false;
            }
            finally
            {
                if (console != null) Object.DestroyImmediate(console);
                if (quest != null)
                {
                    if (quest.Camera != null) Object.DestroyImmediate(quest.Camera.gameObject);
                    var background = Field<Camera>(quest, "backgroundCamera");
                    if (background != null) Object.DestroyImmediate(background.gameObject);
                }
                if (fixture != null) Object.DestroyImmediate(fixture);
                if (boardObject != null) Object.DestroyImmediate(boardObject);
                if (runtimeTarget != null) { runtimeTarget.Release(); Object.DestroyImmediate(runtimeTarget); }
                preview?.Cleanup();
                Localization.SetPreference(language);
                GraphicsSettings.defaultRenderPipeline = previousPipeline; QualitySettings.renderPipeline = previousQualityPipeline;
                File.WriteAllText(Path.Combine(output, "checks.json"), JsonUtility.ToJson(report, true));
            }
            Debug.Log("FRONTIER_MOBILE_HUD_PREVIEW " + (report.passed ? "passed" : "failed") + ": "
                + report.checks.Count(check => check.passed) + "/" + report.checks.Count + " checks; "
                + report.captures.Count + " captures; " + output);
            return report;
        }

        private static string ActionId(object target) => Field<string>(target, "actionId");
        private static RectTransform TargetRect(object target) => Field<RectTransform>(target, "rect");
        private static string TargetLabel(RectTransform target) => target.GetComponentInChildren<Text>()?.text ?? target.name;
        private static void ConfigureFrame(int width, int height, Rect safePixels)
        {
            Width = width; Height = height; SafePixels = safePixels;
            CanvasSize = new Vector2(Width / Scale, Height / Scale);
            SafeCanvas = new Rect(SafePixels.x / Scale, SafePixels.y / Scale, SafePixels.width / Scale, SafePixels.height / Scale);
        }
        private static Rect Bounds(RectTransform transform)
        {
            var corners = new Vector3[4]; transform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(point => point.x), corners.Min(point => point.y),
                corners.Max(point => point.x), corners.Max(point => point.y));
        }
        private static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.yMin >= outer.yMin - 1
            && inner.xMax <= outer.xMax + 1 && inner.yMax <= outer.yMax + 1;
    }
}
#endif
