using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    // Exercise the production input router with explicit controller edges and an unscaled clock.
    public static class UnifiedWheelPreview
    {
        [MenuItem("Frontier/Preview and check unified wheel")]
        public static void Render()
        {
            using (var f = new UnifiedWheelFixture("UnifiedWheel"))
            {
                foreach (string faction in new[] { "industrial", "biological" })
                {
                    f.Start(faction);
                    var worker = f.Session.Match.Entities.Values.First(e => e.owner == 0 && f.Session.Catalog[e.definition].role == "worker");
                    f.Session.Select(worker.id, false); f.Drain();
                    var originalPose = f.Board.transform.localToWorldMatrix;
                    f.Check(f.Wheel.HintVisible && !f.Wheel.IsOpen, "Gameplay starts with a compact controller hint and no full wheel");
                    var hint = UnifiedWheelFixture.Field<RectTransform>(f.Wheel, "hintRoot");
                    var hintPosition = hint.position;
                    f.Ray = new Ray(f.Ray.origin + new Vector3(.12f, .04f, 0), f.Ray.direction); f.Frame();
                    f.Check(hint.position != hintPosition, "The compact hint follows the active pointer pose");
                    f.Capture(faction + "-idle-hint", "hintRoot");

                    var movePoint = new Point(worker.position.x + 2, worker.position.y);
                    UnifiedWheelFixture.Call(f.Input, "OrderClick", movePoint, 0, 0, false, 100f);
                    f.Check(UnifiedWheelFixture.Field<float>(f.Input, "lastActionPress") >= 0, "A real contextual order starts the map's double-A gesture");
                    f.OpenRoot();
                    f.Check(UnifiedWheelFixture.Field<float>(f.Input, "lastActionPress") < 0, "Opening the wheel invalidates the pending map double-A gesture"); f.Dismiss();
                    UnifiedWheelFixture.Call(f.Input, "OrderClick", movePoint, 0, 0, false, 100.1f);
                    f.Check(worker.order == CommandKind.Move && !f.Session.AttackPointerActive, "A quick contextual order after wheel navigation is a fresh Move, not a stale double-A attack");
                    typeof(CommanderInput).GetField("pressedEntity", UnifiedWheelFixture.Private).SetValue(f.Input, worker.id);
                    UnifiedWheelFixture.Call(f.Input, "SelectClick", worker.id, false, 101f);
                    f.Check(UnifiedWheelFixture.Field<float>(f.Input, "lastSelectPress") >= 0, "A real map selection starts its double-selection gesture");
                    f.OpenRoot();
                    f.Check(UnifiedWheelFixture.Field<float>(f.Input, "lastSelectPress") < 0, "Wheel navigation also invalidates a pending double-selection gesture"); f.Dismiss();
                    typeof(CommanderInput).GetField("pressedEntity", UnifiedWheelFixture.Private).SetValue(f.Input, worker.id);
                    UnifiedWheelFixture.Call(f.Input, "SelectClick", worker.id, false, 101.1f);
                    f.Check(f.Session.Selection.SetEquals(new[] { worker.id }), "A fresh selection after the wheel does not select every nearby worker");

                    f.Frame(press: true); f.Frame(); f.Frame(gripDown: true); f.Frame(); f.Frame(gripDown: true);
                    f.Check(!f.Wheel.IsOpen && f.Session.Selection.Contains(worker.id), "Stick click and double grip do not open a wheel or recall a group");
                    f.Drain(); f.Frame(new Vector2(.15f, .12f)); f.Frame(new Vector2(.30f, 0));
                    f.Check(!f.Wheel.IsOpen, "Ordinary drift and the deadzone shoulder do not open the wheel");
                    f.Frame(Vector2.up);
                    f.Check(f.Wheel.IsOpen && f.Wheel.Page == WheelPage.Root && f.Wheel.HighlightedId == "groups-menu", "A neutral-to-up tilt opens the root with Groups highlighted");
                    f.Check(!f.Wheel.HintVisible, "The small hint is hidden while the full wheel is open");
                    foreach (var target in new[] { ("groups-menu", Vector2.up), ("selection-menu", Vector2.right), ("close-wheel", Vector2.down), ("orders-menu", Vector2.left) })
                    {
                        f.Frame(target.Item2);
                        f.Check(f.Wheel.HighlightedId == target.Item1, "Root cardinal direction selects " + target.Item1);
                    }
                    f.Capture(faction + "-root");
                    f.Frame(Vector2.left, elapsed: 2f);
                    f.Check(f.Wheel.IsOpen && f.Wheel.Page == WheelPage.Root, "Holding a direction keeps the root open without executing");
                    f.Frame(Vector2.left, a: true);
                    f.Check(f.Wheel.Page == WheelPage.Root, "The contextual order button cannot execute a wheel option");
                    f.Frame(Vector2.left, down: true, held: true);
                    f.Check(f.Wheel.Page == WheelPage.Orders && f.Wheel.Highlighted == -1, "Trigger enters unit orders without inheriting the root direction");
                    f.Frame(Vector2.left, held: true, elapsed: 1f);
                    f.Check(f.Wheel.IsOpen && f.Wheel.Page == WheelPage.Orders && f.Wheel.Highlighted == -1, "A held trigger and direction cannot execute a child command");
                    f.Frame(Vector2.left);
                    f.Check(f.Wheel.Highlighted == -1, "Releasing the trigger alone does not select a child without neutral");
                    f.Frame();
                    f.Check(f.Wheel.Highlighted == -1 && f.Wheel.IsOpen, "Centering clears the highlight without confirming");
                    f.Choose("back"); f.Confirm();
                    f.Check(f.Wheel.Page == WheelPage.Root && f.Wheel.Highlighted == -1, "Down and trigger return to root without selecting another category");
                    f.Frame(); f.Frame(elapsed: .65f);
                    f.Check(f.Wheel.IsOpen, "A brief centered pause leaves the wheel available");
                    f.Frame(elapsed: .20f);
                    f.Check(!f.Wheel.IsOpen, "The centered wheel closes after its idle timeout"); f.Drain();
                    f.Check(f.Wheel.HintVisible, "Closing restores the compact hint");

                    f.OpenRoot(); f.Choose("selection-menu"); f.Confirm();
                    f.Check(f.Wheel.Page == WheelPage.Selection, "Right and trigger enter contextual selection actions");
                    f.Choose("build-menu"); f.Confirm();
                    f.Check(f.Wheel.InBuildMenu, "A worker's selection page reaches the building submenu");
                    f.Choose("back"); f.Confirm();
                    f.Check(f.Wheel.Page == WheelPage.Selection, "Build Back returns one level to selection actions");
                    f.Choose("back"); f.Confirm(); f.Choose("close-wheel");
                    f.Frame(f.Wheel.DirectionFor("close-wheel"), down: true, held: true);
                    f.Check(!f.Wheel.IsOpen && !f.Session.IsPaused, "Root Down and trigger close the wheel without pausing");
                    f.Check(f.Frame(held: true) && f.Frame(up: true), "The closing trigger and release are drained before map input resumes"); f.Drain();
                    f.Check(f.Session.Selection.SetEquals(new[] { worker.id }), "Wheel navigation never selects terrain underneath it");

                    f.OpenRoot(); f.Session.Pause("tracking", true); f.Frame(Vector2.up);
                    f.Check(!f.Wheel.IsOpen && !f.Wheel.HintVisible, "Tracking pause hides both full wheel and hint");
                    f.Session.Pause("tracking", false); f.Frame(Vector2.up);
                    f.Check(!f.Wheel.IsOpen, "A direction held through tracking recovery cannot reopen the wheel"); f.Drain();
                    f.OpenRoot(); UnifiedWheelFixture.Call(f.Input, "OnApplicationFocus", false);
                    f.Check(!f.Wheel.IsOpen && !f.Wheel.HintVisible, "Focus loss hides both wheel layers immediately");
                    UnifiedWheelFixture.Call(f.Input, "OnApplicationFocus", true); f.Frame(Vector2.up);
                    f.Check(!f.Wheel.IsOpen, "Focus recovery requires a fresh neutral-to-direction gesture"); f.Drain();
                    f.OpenRoot(); f.Quest.BeginPlacement(); f.Frame(Vector2.right);
                    f.Check(!f.Wheel.IsOpen && !f.Wheel.HintVisible, "Board placement dismisses wheel and compact hint");
                    f.Quest.CancelPlacement(); f.Frame(Vector2.right);
                    f.Check(!f.Wheel.IsOpen, "Ending placement with a held stick cannot open the wheel"); f.Drain();
                    f.OpenRoot(); typeof(CommandPanel).GetField("moving", UnifiedWheelFixture.Private).SetValue(f.Panel, true); f.Frame(Vector2.right);
                    f.Check(!f.Wheel.IsOpen && !f.Wheel.HintVisible, "Dragging the console suspends the unified wheel");
                    typeof(CommandPanel).GetField("moving", UnifiedWheelFixture.Private).SetValue(f.Panel, false); f.Frame(Vector2.right);
                    f.Check(!f.Wheel.IsOpen, "Releasing a dragged console requires neutral before opening"); f.Drain();
                    f.OpenRoot(); UnifiedWheelFixture.Call(f.Input, "ChangeInputController", OVRInput.Controller.LTouch);
                    f.Check(!f.Wheel.IsOpen, "Switching controller hands dismisses the wheel"); f.Drain();
                    f.OpenRoot(); UnifiedWheelFixture.Call(f.Input, "OnDisable");
                    f.Check(!f.Wheel.IsOpen && !f.Wheel.HintVisible, "Disabling input hides both wheel layers"); f.Drain();
                    f.OpenRoot(); f.Session.StartMatch(); f.Frame();
                    f.Check(!f.Wheel.IsOpen, "A new match invalidates a wheel even if entity IDs are reused"); f.Drain();
                    f.OpenRoot(); f.Session.Send(CommandKind.Surrender); f.Frame();
                    f.Check(!f.Wheel.IsOpen && !f.Wheel.HintVisible, "The match result hides both wheel layers");
                    f.Session.LeaveMatch(); f.Drain(); f.Frame(Vector2.up);
                    f.Check(!f.Wheel.IsOpen && !f.Wheel.HintVisible, "The main menu never shows the gameplay wheel hint");
                    f.Check(f.Board.transform.localToWorldMatrix == originalPose, "Unified wheel gestures leave the board placement unchanged");
                }
                f.Report("UNIFIED_WHEEL");
            }
        }
    }

    internal sealed class UnifiedWheelFixture : IDisposable
    {
        internal const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        internal static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        internal static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
        internal readonly GameSession Session;
        internal readonly BoardView Board;
        internal readonly QuestBridge Quest;
        internal readonly CommandPanel Panel;
        internal readonly ActionWheel Wheel;
        internal readonly CommanderInput Input;
        internal Ray Ray = new Ray(new Vector3(0, .8f, -.3f), Vector3.forward);
        private readonly PreviewRenderUtility preview;
        private readonly GameObject fixture, console, wheelObject;
        private readonly string output;
        private float now = 10;
        private int checks, captures;
        internal UnifiedWheelFixture(string name)
        {
            output = "TestResults/" + name; Directory.CreateDirectory(output);
            preview = new PreviewRenderUtility(); fixture = new GameObject(name + " fixture");
            console = new GameObject(name + " console"); wheelObject = new GameObject(name + " wheel");
            Session = fixture.AddComponent<GameSession>(); if (Session.Catalog == null) Call(Session, "Awake"); Session.LoadMap("training");
            Board = fixture.AddComponent<BoardView>(); Board.Session = Session; Call(Board, "Start");
            Quest = fixture.AddComponent<QuestBridge>(); Quest.Session = Session; Quest.Board = Board;
            typeof(QuestBridge).GetProperty("Camera").SetValue(Quest, preview.camera); typeof(QuestBridge).GetProperty("Active").SetValue(Quest, true);
            Panel = console.AddComponent<CommandPanel>(); Panel.Session = Session; Panel.Board = Board; Panel.Quest = Quest; Call(Panel, "Start");
            Wheel = wheelObject.AddComponent<ActionWheel>(); Wheel.Session = Session; Wheel.Panel = Panel; Wheel.Quest = Quest; preview.AddSingleGO(wheelObject);
            Input = fixture.AddComponent<CommanderInput>(); Input.Session = Session; Input.Board = Board; Input.Panel = Panel; Input.Quest = Quest; Input.Wheel = Wheel;
        }
        internal void Start(string faction)
        {
            Wheel.Close(); Session.Faction = faction; Session.StartMatch();
            Session.Match.Players[0].minerals = 5000; Session.Match.Players[0].catalyst = 5000; Refresh(); Drain();
        }
        internal bool Frame(Vector2 stick = default, bool press = false, bool a = false, bool b = false, bool down = false, bool up = false,
            bool held = false, bool grip = false, bool gripDown = false, float elapsed = .05f, bool clickHeld = false, bool clickUp = false)
        {
            now += elapsed; held |= down; grip |= gripDown; clickHeld |= press;
            bool consumed = (bool)Call(Input, "RouteWheel", Ray, stick, press, a, down, held, b, grip, gripDown, now, clickHeld, clickUp);
            if (!consumed && Panel.AcceptsGameInput) Call(Input, "PointBoard", Ray, down, up, a, grip);
            return consumed;
        }
        internal void Drain() { Frame(); Frame(); Frame(); }
        internal void OpenRoot() { if (Wheel.IsOpen) Wheel.Close(); Drain(); Frame(Vector2.up); Check(Wheel.IsOpen && Wheel.Page == WheelPage.Root, "Fresh tilt opens the root wheel"); }
        internal void Choose(string id)
        {
            Frame(); Check(Wheel.Items.Any(a => a.Id == id), "Wheel offers " + id);
            Frame(Wheel.DirectionFor(id)); Check(Wheel.HighlightedId == id, "Stick highlights " + id);
        }
        internal void Confirm() { var direction = Wheel.HighlightedId == null ? Vector2.zero : Wheel.DirectionFor(Wheel.HighlightedId); Frame(direction, down: true, held: true); Frame(direction); }
        internal void Enter(string id) { Choose(id); Confirm(); }
        internal void OpenSelection() { OpenRoot(); Enter("selection-menu"); }
        internal void OpenOrders() { OpenRoot(); Enter("orders-menu"); }
        internal void OpenGroups() { OpenRoot(); Enter("groups-menu"); Check(Wheel.InGroupMenu, "Trigger enters the group submenu"); }
        internal void Dismiss() { Wheel.Close(); Drain(); }
        internal void Refresh() { typeof(GameSession).GetProperty("View").SetValue(Session, Session.Match.View(0)); typeof(CommandPanel).GetField("nextText", Private).SetValue(Panel, 0f); Call(Panel, "LateUpdate"); }
        internal EntityState Add(string role, int id, Point position)
        {
            var type = Session.Catalog.Role(Session.Faction, role);
            var entity = new EntityState { id = id, owner = 0, definition = type.id, health = type.health, energy = type.energyMax, position = position };
            Session.Match.Entities.Add(id, entity); if (type.building) Session.Match.Map.Occupy(position, type.footprint, id); Refresh(); return entity;
        }
        internal void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; Debug.Log("WHEEL_PASS: " + message); }
        internal void Capture(string name, string rootField = "root")
        {
            // Check fixed labels at their configured font size, and best-fit labels at
            // their readable minimum. Compare actual generated glyphs as well as height.
            foreach (var label in Wheel.GetComponentsInChildren<Text>())
            {
                var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                settings.resizeTextForBestFit = false; settings.fontSize = label.resizeTextForBestFit ? label.resizeTextMinSize : label.fontSize;
                float height = label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text, settings) / label.pixelsPerUnit;
                Check(height <= label.rectTransform.rect.height + .5f, "Wheel text fits at readable font size: " + label.text);
                using (var bounded = new TextGenerator()) using (var complete = new TextGenerator())
                {
                    settings.verticalOverflow = VerticalWrapMode.Truncate; bounded.Populate(label.text, settings);
                    settings.verticalOverflow = VerticalWrapMode.Overflow; complete.Populate(label.text, settings);
                    Check(bounded.vertexCount == complete.vertexCount, "Wheel text generates every glyph without clipping: " + label.text);
                }
            }
            HarvestingPreview.SaveCanvas(Wheel.transform, Field<RectTransform>(Wheel, rootField), output + "/" + name + ".png", preview, 900, 1050); captures++;
        }
        internal void CapturePanel(string name) { Refresh(); HarvestingPreview.SavePanel(Panel, output + "/" + name + ".png", preview); captures++; }
        internal void Report(string label) => Debug.Log($"FRONTIER_{label} passed: {checks} checks, {captures} views; no player build");
        public void Dispose() { Object.DestroyImmediate(wheelObject); Object.DestroyImmediate(console); Object.DestroyImmediate(fixture); preview.Cleanup(); }
    }
}
