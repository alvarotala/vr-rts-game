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
    // Exercises the real wheel/router with synthetic right-controller frames, no XR device or APK.
    public static class ActionWheelPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        [MenuItem("Frontier/Preview and check action wheel")]
        public static void Render()
        {
            Directory.CreateDirectory("TestResults/ActionWheel");
            var preview = new PreviewRenderUtility(); var fixture = new GameObject("Wheel fixture");
            var console = new GameObject("Wheel console"); var wheelObject = new GameObject("Wheel preview");
            int checks = 0, captures = 0;
            try
            {
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Invoke(session, "Awake");
                session.LoadMap("training");
                var board = fixture.AddComponent<BoardView>(); board.Session = session; Invoke(board, "Start");
                var quest = fixture.AddComponent<QuestBridge>(); quest.Session = session; quest.Board = board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest, preview.camera);
                var panel = console.AddComponent<CommandPanel>(); panel.Session = session; panel.Board = board; panel.Quest = quest; Invoke(panel, "Start");
                var wheel = wheelObject.AddComponent<ActionWheel>(); wheel.Session = session; wheel.Panel = panel; wheel.Quest = quest;
                var input = fixture.AddComponent<CommanderInput>(); input.Session = session; input.Board = board; input.Panel = panel; input.Quest = quest; input.Wheel = wheel;
                preview.AddSingleGO(wheelObject);
                var pointer = new Ray(new Vector3(0, .8f, -.3f), Vector3.forward); float inputTime = 10;

                foreach (string faction in new[] { "industrial", "biological" })
                {
                    session.Faction = faction; session.StartMatch(); session.Match.Players[0].minerals = 5000; session.Match.Players[0].catalyst = 5000;
                    var worker = session.Match.Entities.Values.First(e => e.owner == 0 && session.Catalog[e.definition].role == "worker");
                    var other = session.Match.Entities.Values.First(e => e.owner == 0 && session.Catalog[e.definition].role == "worker" && e.id != worker.id);
                    var hq = session.Match.Entities.Values.First(e => e.owner == 0 && session.Catalog[e.definition].role == "hq");
                    var barracks = Add("barracks", 1001, new Point(22, 12)); var tech = Add("tech", 1002, new Point(22, 17));
                    var supply = Add("supply", 1003, new Point(20, 8));
                    Refresh(); session.Select(worker.id, false);
                    pointer = new Ray(board.Content.TransformPoint(new Vector3(worker.position.x, 0, worker.position.y)) + Vector3.up, Vector3.down);
                    var boardPosition = board.transform.position; var boardRotation = board.transform.rotation; float width = board.Width;

                    Check(Frame(press: true) && wheel.IsOpen && wheel.Items.Count == 4, "Stick press opens four worker actions");
                    Frame(stick: Vector2.right, a: true);
                    Check(wheel.Highlighted < 0 && !session.AttackMoveMode, "Opening requires a neutral stick before choosing; A cannot execute an implicit default");
                    Choose("hold"); Capture(faction + "-worker");
                    Frame(down: true, held: true);
                    Check(!wheel.IsOpen && worker.order == CommandKind.Hold, "Index trigger executes the highlighted action like A");
                    Check(Frame(held: true) && Frame(up: true), "Confirming trigger and release never reach the map after closing the wheel");
                    Check(session.Selection.SetEquals(new[] { worker.id }), "Wheel trigger input never changes selection"); Drain();

                    Check(!Frame(gripDown: true) && !wheel.IsOpen, "A single grip press keeps its outside-wheel modifier behavior");
                    Frame(); Check(Frame(gripDown: true) && wheel.IsOpen, "Double grip opens the action wheel");
                    Check(Frame(grip: true) && wheel.IsOpen, "Holding the opening grip does not immediately close the wheel");
                    Frame(); Choose("build-menu"); Frame(down: true, held: true);
                    Check(wheel.InBuildMenu && session.BuildDefinition == null, "Trigger opens the Build submenu without placing a building");
                    Frame(stick: Vector2.up, held: true);
                    Check(wheel.InBuildMenu && session.BuildDefinition == null, "Holding trigger cannot confirm another submenu action");
                    Frame(); Choose("build:" + session.Catalog.Role(faction, "supply").id);
                    int beforeTriggerBuild = session.Match.Buildings(0); Frame(down: true, held: true);
                    Check(!wheel.IsOpen && session.BuildDefinition != null && session.Match.Buildings(0) == beforeTriggerBuild, "Trigger arms construction without applying that press to terrain");
                    Check(Frame(held: true) && Frame(up: true), "Construction waits until the confirming trigger has been released"); Drain(); session.CancelMode();

                    Open(); Choose("build-menu"); Frame(a: true); Frame(gripDown: true);
                    Check(wheel.IsOpen && !wheel.InBuildMenu && !session.IsPaused, "Grip goes back from Build like B");
                    Frame(grip: true); Check(wheel.IsOpen, "A held grip does not go back twice");
                    Frame(); Frame(gripDown: true);
                    Check(!wheel.IsOpen && !session.IsPaused, "A new grip press closes the main wheel without pausing");
                    Check(Frame(grip: true) && Frame(), "Closing grip and release are consumed");
                    Check(!Frame(gripDown: true) && !wheel.IsOpen, "Closing grip is not reused as the first tap to reopen");
                    Frame(held: true); Drain();

                    Frame(gripDown: true); Frame(elapsed: .5f); Frame(gripDown: true);
                    Check(!wheel.IsOpen, "Two slow grip presses do not open the wheel"); Frame(held: true); Drain();
                    Frame(gripDown: true); Frame(grip: true, elapsed: .5f); Frame(); Frame(gripDown: true);
                    Check(!wheel.IsOpen, "A long grip hold is not treated as a double tap"); Frame(held: true); Drain();
                    Frame(gripDown: true); Frame(down: true, held: true, grip: true); Frame(up: true, grip: true);
                    Check(!session.Selection.Contains(worker.id), "Grip plus trigger still toggles a selected unit outside the wheel");
                    Frame(); Frame(gripDown: true);
                    Check(!wheel.IsOpen, "Using grip for selection interrupts the double-tap gesture"); Frame(held: true); Drain(); session.Select(worker.id, false);
                    Frame(gripDown: true); session.Pause("tracking", true); Frame(gripDown: true);
                    Check(!wheel.IsOpen, "Double grip cannot open during tracking pause"); session.Pause("tracking", false); Frame(gripDown: true);
                    Check(!wheel.IsOpen, "Tracking recovery starts a fresh grip gesture"); Frame(held: true); Drain();
                    Frame(gripDown: true); typeof(CommanderInput).GetMethod("OnApplicationFocus", Private).Invoke(input, new object[] { false });
                    Frame(); Frame(gripDown: true); Check(!wheel.IsOpen, "Focus loss clears a pending double grip"); Frame(held: true); Drain();

                    Open(); Choose("build-menu"); Frame(a: true);
                    Check(wheel.InBuildMenu && wheel.Items.Count == 6 && wheel.Highlighted == -1, "Build opens six faction buildings without preselecting one");
                    Frame(stick: Vector2.up, a: true);
                    Check(session.BuildDefinition == null, "Submenu requires neutral before another selection");
                    Choose("build:" + session.Catalog.Role(faction, "barracks").id); Capture(faction + "-build"); Frame(b: true);
                    Check(wheel.IsOpen && !wheel.InBuildMenu && !session.IsPaused, "B returns from Build without pausing");
                    Choose("build-menu"); Frame(a: true); Choose("build:" + session.Catalog.Role(faction, "supply").id);
                    int buildings = session.Match.Buildings(0); Frame(a: true);
                    Check(!wheel.IsOpen && session.BuildDefinition == session.Catalog.Role(faction, "supply").id && session.Match.Buildings(0) == buildings, "A arms construction without placing a building under the wheel");
                    Check(Frame(held: true) && Frame(up: true), "A held trigger and its release remain consumed after the wheel closes");
                    Drain(); Open(); Choose("hold"); Frame(a: true); Drain();
                    Check(session.BuildDefinition == null, "A new unit order clears a pending construction destination");

                    Open(); Choose("attack-move"); Frame(a: true);
                    Check(!wheel.IsOpen && session.AttackMoveMode && worker.order == CommandKind.Hold, "Attack Move awaits a separate destination instead of executing A on the map");
                    Drain(); session.CancelMode();
                    session.Select(other.id, true); Open(); Choose("stop"); Frame(a: true); Drain();
                    Check(worker.order == CommandKind.Stop && other.order == CommandKind.Stop && session.Selection.Count == 2, "Group command applies to all selected units");

                    session.Select(hq.id, false); Open(); Choose("train:" + worker.definition); Capture(faction + "-train");
                    int minerals = session.View.minerals; Frame(a: true); Drain();
                    Check(hq.queue.Count == 1 && session.View.minerals == minerals - session.Catalog[worker.definition].minerals, "Training from wheel uses real costs and production queue");
                    Open(); Choose("cancel"); Frame(a: true); Drain();
                    Check(hq.queue.Count == 0 && session.View.minerals == minerals, "Cancel refunds the actual queued order");

                    session.Match.Players[0].minerals = 0; Refresh(); Open(); Choose("train:" + worker.definition); Frame(a: true);
                    Check(wheel.IsOpen && hq.queue.Count == 0 && Field<Text>(wheel, "detail").text == session.Message, "Rejected commands keep the wheel open and show the reason");
                    Capture(faction + "-not-enough-resources"); Frame(b: true); Drain(); session.Match.Players[0].minerals = 5000; Refresh();

                    session.Select(tech.id, false); Open(); Choose("research:" + session.Catalog.Definition.upgrades[0].id); Capture(faction + "-research"); Frame(a: true); Drain();
                    Check(tech.queue.Count == 1 && tech.queue[0].research, "Research reaches the shared command path");
                    session.Select(barracks.id, false); Open(); Choose("train:" + session.Catalog.Role(faction, "ranged").id); Frame(a: true); Drain();
                    Check(barracks.queue.Count == 1, "Barracks trains the selected combat unit");
                    Open(); Choose("cancel"); barracks.queue.Clear(); Refresh(); Frame(a: true);
                    Check(wheel.IsOpen && wheel.Highlighted == -1 && !wheel.Items.Any(a => a.Id == "cancel"), "Queue changes clear stale selection instead of executing a different action");
                    Frame(b: true); Drain();

                    session.Select(supply.id, false); Open();
                    Check(wheel.Items.Count == 0 && Field<Text>(wheel, "detail").text.Contains("automatically"), "Automatic buildings explain the absence of actions");
                    Capture(faction + "-automatic"); Frame(a: true); Check(wheel.IsOpen, "A without an action leaves the wheel open"); Frame(b: true); Drain();
                    supply.complete = false; Refresh(); Open(); Choose("cancel"); Capture(faction + "-construction");
                    Frame(a: true); Drain(); Check(!session.Match.Entities.ContainsKey(supply.id), "Unfinished building can be cancelled");

                    session.Select(0, false); Open(); Check(wheel.Items.Count == 0, "No selection has no executable actions"); Capture(faction + "-no-selection"); Frame(b: true); Drain();
                    session.Select(worker.id, false); Open(); session.Select(other.id, false); Frame(a: true);
                    Check(!wheel.IsOpen && other.order == CommandKind.Stop, "Changed selection invalidates an open wheel before confirmation"); Drain();
                    Open(); session.Pause("tracking", true); Frame(a: true); Check(!wheel.IsOpen && session.IsPaused, "Tracking interruption closes the wheel"); session.Pause("tracking", false); Drain();
                    Open(); typeof(CommanderInput).GetMethod("OnApplicationFocus", Private).Invoke(input, new object[] { false });
                    Check(!wheel.IsOpen, "Losing application focus closes the wheel"); Drain();
                    Open(); quest.BeginPlacement(); Frame(a: true);
                    Check(!wheel.IsOpen && quest.Placing, "Placement closes the wheel and takes priority over actions"); quest.CancelPlacement(); Drain();
                    Open(); Invoke(input, "OnDisable"); Check(!wheel.IsOpen, "Disabling input closes the wheel"); Drain();
                    Open(); session.Match.Entities.Remove(other.id); Refresh(); Frame(a: true); Check(!wheel.IsOpen, "Lost selected unit invalidates commands even before selection is pruned"); Drain();
                    Check(board.transform.position == boardPosition && board.transform.rotation == boardRotation && board.Width == width, "Wheel input leaves the board placement unchanged");
                    var fighterType = session.Catalog.Role(faction, "melee");
                    var fighter = new EntityState { id = 1100, owner = 0, definition = fighterType.id, health = fighterType.health, position = new Point(8, 12) };
                    session.Match.Entities.Add(fighter.id, fighter); Refresh(); session.Select(fighter.id, false); Open();
                    Check(wheel.Items.Count == 3 && !wheel.Items.Any(a => a.Id == "build-menu"), "Combat units expose three orders without building actions");
                    Choose("attack-move"); Capture(faction + "-soldier"); Frame(b: true); Drain();
                    session.Select(worker.id, false); Open(); session.StartMatch(); Frame(a: true);
                    Check(!wheel.IsOpen, "A replacement match invalidates the wheel even when entity IDs are reused"); Drain();
                    session.Select(session.View.entities.First(e => e.owner == 0).id, false); Open(); session.Send(CommandKind.Surrender); Frame(a: true);
                    Check(!wheel.IsOpen, "Match result dismisses the wheel"); Drain(); session.StartMatch();
                    session.Select(worker.id, false); Open(); session.LeaveMatch(); Frame(a: true); Check(!wheel.IsOpen, "Leaving a match dismisses the wheel"); Drain();
                    Check(!Frame(press: true) && !wheel.IsOpen, "Wheel cannot open from the main menu");
                    Frame(gripDown: true); Frame(); Frame(gripDown: true); Check(!wheel.IsOpen, "Double grip cannot open from the main menu");

                    EntityState Add(string role, int id, Point position)
                    {
                        var type = session.Catalog.Role(faction, role); var entity = new EntityState { id = id, owner = 0, definition = type.id, health = type.health, position = position };
                        session.Match.Entities.Add(id, entity); session.Match.Map.Occupy(position, type.footprint, id); return entity;
                    }
                }
                Debug.Log($"FRONTIER_ACTION_WHEEL passed: {checks} checks, {captures} wheel views; no player build");

                bool Frame(Vector2 stick = default, bool press = false, bool a = false, bool b = false, bool down = false, bool up = false, bool held = false, bool grip = false, bool gripDown = false, float elapsed = .05f)
                {
                    inputTime += elapsed; grip |= gripDown; held |= down;
                    bool consumed = (bool)typeof(CommanderInput).GetMethod("RouteWheel", Private).Invoke(input, new object[] { pointer, stick, press, a, down, held, b, grip, gripDown, inputTime, press, false });
                    if (!consumed && panel.AcceptsGameInput) typeof(CommanderInput).GetMethod("PointBoard", Private).Invoke(input, new object[] { pointer, down, up, a, grip });
                    return consumed;
                }
                void Drain() { Frame(); Frame(); }
                void Open() { Drain(); Frame(press: true); Frame(); }
                void Choose(string id)
                {
                    Frame(); int index = wheel.Items.ToList().FindIndex(a => a.Id == id); if (index < 0) throw new Exception("Missing wheel action: " + id);
                    float angle = index * 2 * Mathf.PI / wheel.Items.Count; Frame(stick: new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)));
                    Check(wheel.HighlightedId == id, "Stick selects " + id);
                }
                void Refresh() { typeof(GameSession).GetProperty("View").SetValue(session, session.Match.View(0)); }
                void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Debug.Log("WHEEL_PASS: " + message); }
                void Capture(string name)
                {
                    HarvestingPreview.SaveCanvas(wheel.transform, Field<RectTransform>(wheel, "root"), "TestResults/ActionWheel/" + name + ".png", preview, 900, 1050); captures++;
                }
            }
            finally { Object.DestroyImmediate(wheelObject); Object.DestroyImmediate(console); Object.DestroyImmediate(fixture); preview.Cleanup(); }
        }
    }
}
