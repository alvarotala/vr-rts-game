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
                    f.Check(hint.GetComponentsInChildren<RadialSector>().Length == 0
                        && hint.GetComponentsInChildren<Image>().Length == 14
                        && hint.GetComponentsInChildren<Graphic>().All(graphic => !graphic.raycastTarget)
                        && new[] { "↑ Groups", "→ Selection", "← Unit orders" }.All(text => hint.GetComponentsInChildren<Text>().Any(label => label.text == text))
                        && hint.GetComponentsInChildren<Text>().Length == 8,
                        "All directional and button callouts use thin leaders without a small wheel or input-blocking graphics");
                    var hintPosition = hint.position;
                    f.Ray = new Ray(f.Ray.origin + new Vector3(.12f, .04f, 0), f.Ray.direction); f.Frame();
                    f.Check(hint.position != hintPosition, "The compact hint follows the active pointer pose");
                    f.Capture(faction + "-idle-hint", "hintRoot");
                    CheckHandAttachment(f, faction);
                    CheckNavigationHaptics(f);

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

                    f.Frame(press: true); f.Frame(clickHeld: true); f.Frame(clickUp: true); f.Drain();
                    f.Check(!f.Wheel.HintsEnabled && !f.Wheel.HintVisible && !f.Wheel.IsOpen && f.Session.Selection.Contains(worker.id),
                        "Click hides every hint once; holding and releasing do not toggle again or change selection");
                    f.OpenGroups(); f.Dismiss();
                    f.Check(!f.Wheel.HintVisible && !f.Wheel.HintsEnabled, "Tilt still opens with hints disabled and closing preserves the preference");
                    f.Session.Pause("tracking", true); f.Frame(); f.Session.Pause("tracking", false); f.Drain();
                    f.Check(!f.Wheel.HintVisible, "Resuming does not restore hidden hints");
                    f.Frame(press: true); f.Frame(clickUp: true); f.Drain();
                    f.Check(f.Wheel.HintsEnabled && f.Wheel.HintVisible, "A second click restores every hint");
                    f.Frame(gripDown: true); f.Frame(); f.Frame(gripDown: true);
                    f.Check(!f.Wheel.IsOpen && f.Session.Selection.Contains(worker.id), "Double grip does not open a wheel or recall a group");
                    f.Drain(); f.Frame(new Vector2(.15f, .12f)); f.Frame(new Vector2(.30f, 0));
                    f.Check(!f.Wheel.IsOpen, "Ordinary drift and the deadzone shoulder do not open the wheel");
                    f.Session.Group(0, true);
                    f.Frame(Vector2.up);
                    f.Check(f.Wheel.InGroupMenu && f.Wheel.HighlightedId == "group-1", "Up enters Groups with group 1 already highlighted");
                    f.Check(!f.Wheel.HintVisible, "The small hint is hidden while the full wheel is open");
                    f.Frame(Vector2.up, elapsed: 1f);
                    f.Check(f.Wheel.InGroupMenu && f.Wheel.HighlightedId == "group-1", "Holding the opening direction keeps the group ready without recalling it");
                    f.Capture(faction + "-direct-groups");
                    f.Frame(Vector2.up, down: true, held: true);
                    f.Check(!f.Wheel.IsOpen && f.Session.Selection.Contains(worker.id), "A fresh trigger recalls group 1 without ever centering the opening direction");
                    f.Frame(held: true); f.Frame(up: true); f.Drain();
                    f.OpenSelection();
                    f.Check(f.Wheel.HighlightedId.StartsWith("build:") && f.Wheel.Items.Count(a => a.Building) == 7,
                        "Right enters Selection with all buildings available and one highlighted immediately");
                    f.Capture(faction + "-direct-selection");
                    f.OpenOrders();
                    f.Check(f.Wheel.HighlightedId == "attack-move", "Left enters Orders with Attack Move already highlighted");
                    f.Capture(faction + "-direct-orders");
                    f.Dismiss(); f.Frame(Vector2.down);
                    f.Check(!f.Wheel.IsOpen && f.Wheel.HintVisible && f.Session.GroupSize(2) == 0,
                        "Down from the hint has no action and cannot open assignment");
                    f.OpenRoot(); f.Frame();
                    foreach (var target in new[] { ("groups-menu", Vector2.up), ("selection-menu", Vector2.right), ("orders-menu", Vector2.left) })
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
                    f.Check(f.Wheel.Page == WheelPage.Orders && f.Wheel.HighlightedId == "attack-move", "Trigger enters unit orders and immediately carries the left direction into the child page");
                    f.Frame(Vector2.left, held: true, elapsed: 1f);
                    f.Check(f.Wheel.IsOpen && f.Wheel.HighlightedId == "attack-move" && !f.Session.AttackMoveMode, "A held trigger cannot execute the already highlighted child command");
                    f.Frame(Vector2.right, held: true);
                    f.Check(f.Wheel.HighlightedId == "hold" && worker.order != CommandKind.Hold, "Sweeping directly to another direction while holding Trigger changes only the highlight");
                    f.Frame(Vector2.left, held: true);
                    f.Frame(Vector2.left);
                    f.Check(f.Wheel.HighlightedId == "attack-move" && !f.Session.AttackMoveMode, "Releasing only Trigger keeps the held direction ready");
                    f.Frame(Vector2.left, down: true, held: true);
                    f.Check(!f.Wheel.IsOpen && f.Session.AttackMoveMode, "A new trigger press confirms the child without centering the stick");
                    f.Frame(held: true); f.Frame(up: true); f.Drain(); f.Session.CancelMode(); f.OpenOrders();
                    f.Frame();
                    f.Check(f.Wheel.Highlighted == -1 && f.Wheel.IsOpen, "Centering clears the highlight without confirming");
                    f.Choose("back"); f.Confirm();
                    f.Check(f.Wheel.IsOpen && f.Wheel.Page == WheelPage.Root && f.Wheel.HighlightedId == null, "Back reaches the root with no action on down");
                    f.Frame(); f.Frame(elapsed: .3f);
                    f.Check(f.Wheel.IsOpen, "A 0.3-second centered pause leaves the wheel available");
                    f.Frame(elapsed: .15f);
                    f.Check(!f.Wheel.IsOpen, "The centered wheel closes after 0.4 seconds"); f.Drain();
                    f.Check(f.Wheel.HintVisible, "Closing restores the compact hint");

                    f.OpenSelection();
                    f.Frame(Vector2.right, b: true);
                    f.Check(f.Wheel.Page == WheelPage.Root && f.Wheel.HighlightedId == "selection-menu", "B preserves the held direction when returning to the parent");
                    f.Frame(Vector2.right, down: true, held: true);
                    f.Frame(Vector2.right, held: true);
                    f.Check(f.Wheel.Page == WheelPage.Selection && f.Wheel.HighlightedId.StartsWith("build:"),
                        "One trigger enters Selection directly with a building highlighted");
                    string building = f.Wheel.HighlightedId.Substring("build:".Length);
                    f.Frame(Vector2.right, held: true);
                    f.Check(f.Wheel.Page == WheelPage.Selection && f.Session.BuildDefinition == null, "Holding the entering Trigger does not arm construction");
                    f.Frame(Vector2.right); f.Frame(Vector2.right, down: true, held: true);
                    f.Check(f.Wheel.IsOpen && f.Session.BuildDefinition == building, "Root to Selection to construction can be chained without centering and keeps Selection open");
                    f.Check(f.Frame(held: true) && f.Frame(up: true), "The final continuous-chain trigger is drained before map placement");
                    f.Drain(); f.Session.CancelMode(); f.OpenSelection();
                    f.Choose("back"); f.Confirm();
                    f.Check(f.Wheel.Page == WheelPage.Root, "Back from construction options returns directly to the root");
                    f.Frame(Vector2.down, down: true, held: true);
                    f.Check(f.Wheel.Page == WheelPage.Root && f.Wheel.HighlightedId == null, "Down and Trigger on the root do nothing");
                    f.Frame(Vector2.up); f.Frame(Vector2.up, down: true, held: true);
                    f.Check(f.Wheel.InGroupMenu && f.Wheel.HighlightedId == "group-1", "Up and Trigger enter the single Groups wheel");
                    f.Frame(Vector2.down, held: true);
                    f.Check(f.Session.GroupSize(2) == 0 && f.Wheel.IsOpen, "Holding the entering trigger cannot recall or assign the child group");
                    f.Frame(Vector2.down); f.Frame(Vector2.down, press: true);
                    f.Check(!f.Wheel.IsOpen && f.Session.GroupSize(2) == 1 && !f.Session.IsPaused && f.Wheel.HintsEnabled,
                        "Stick press assigns group 3 from Groups without centering or hiding hints");
                    f.Check(f.Frame(clickHeld: true) && f.Frame(clickUp: true), "Assignment click and release drain before map input resumes"); f.Drain();
                    f.OpenRoot(); f.Frame(b: true);
                    f.Check(!f.Wheel.IsOpen && !f.Session.IsPaused, "B from the root closes the wheel without pausing"); f.Drain();
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

        private sealed class HapticRecorder : OVRInput.OVRControllerLTouch
        {
            internal readonly System.Collections.Generic.List<float> Amplitudes = new System.Collections.Generic.List<float>();
            internal HapticRecorder(OVRInput.Controller hand) { controllerType = hand; }
            public override void SetControllerVibration(float frequency, float amplitude) { Amplitudes.Add(amplitude); }
        }

        private static void CheckNavigationHaptics(UnifiedWheelFixture f)
        {
            var previousControllers = OVRInput.controllers; var previousDevice = OVRManager.loadedXRDevice;
            var previousBindings = f.Quest.Bindings; bool previousLeft = f.Panel.Bindings.LeftHand;
            var left = new HapticRecorder(OVRInput.Controller.LTouch); var right = new HapticRecorder(OVRInput.Controller.RTouch);
            // Record the real SDK dispatch without sending vibration to physical hardware.
            OVRInput.controllers = new System.Collections.Generic.List<OVRInput.OVRControllerBase> { left, right };
            OVRManager.loadedXRDevice = OVRManager.XRDevice.Oculus; f.Quest.Bindings = f.Panel.Bindings;
            try
            {
                foreach (bool useLeft in new[] { false, true })
                {
                    UnifiedWheelFixture.Call(f.Input, "StopHaptic"); f.Dismiss();
                    f.Panel.Bindings.SelectHand(useLeft); left.Amplitudes.Clear(); right.Amplitudes.Clear();
                    var active = useLeft ? left : right; var inactive = useLeft ? right : left;
                    f.Frame(Vector2.up);
                    f.Check(active.Amplitudes.Count == 1 && active.Amplitudes[0] > 0 && inactive.Amplitudes.Count == 0, "Opening sends exactly one level pulse to the active hand");
                    float level = active.Amplitudes.Last();
                    f.Check(Mathf.Abs(UnifiedWheelFixture.Field<float>(f.Input, "hapticEnd") - Time.unscaledTime - .055f) < .005f, "Level vibration is bounded to 55 ms");
                    f.Frame(Vector2.up); f.Frame(new Vector2(.71f, .70f)); f.Frame(Vector2.up, elapsed: 1f);
                    f.Check(active.Amplitudes.Count == 1, "Held directions and sector-boundary jitter do not repeat vibration");
                    f.Frame(Vector2.right);
                    f.Check(active.Amplitudes.Count == 2 && active.Amplitudes.Last() > 0 && active.Amplitudes.Last() < level, "Changing group emits one lighter item pulse");
                    f.Check(Mathf.Abs(UnifiedWheelFixture.Field<float>(f.Input, "hapticEnd") - Time.unscaledTime - .025f) < .005f, "Item vibration is bounded to 25 ms");
                    f.Frame(); f.Frame();
                    f.Check(active.Amplitudes.Count == 2, "Centering the stick stays silent");
                    f.Frame(Vector2.up); f.Frame(Vector2.up, b: true);
                    f.Check(active.Amplitudes.Count == 4 && active.Amplitudes.Last() == level, "Back emits one level pulse despite also highlighting the parent option");
                    f.Frame(Vector2.up, down: true, held: true);
                    f.Check(active.Amplitudes.Count == 5 && active.Amplitudes.Last() == level, "Entering a submenu emits one level pulse without a duplicate item pulse");
                    f.Frame(Vector2.up, held: true); f.Refresh(); f.Frame(Vector2.up, held: true);
                    f.Check(active.Amplitudes.Count == 5, "A held trigger and description refresh do not repeat vibration");
                    typeof(CommanderInput).GetField("hapticEnd", UnifiedWheelFixture.Private).SetValue(f.Input, Time.unscaledTime - 1);
                    UnifiedWheelFixture.Call(f.Input, "UpdateHaptics"); UnifiedWheelFixture.Call(f.Input, "UpdateHaptics");
                    f.Check(active.Amplitudes.Count == 6 && active.Amplitudes.Last() == 0, "Pulse expiry sends a single stop instead of vibrating indefinitely");
                    UnifiedWheelFixture.Call(f.Input, "Haptic", false);
                    f.Check(active.Amplitudes.Last() > level, "Rejected commands retain their stronger existing feedback");
                    UnifiedWheelFixture.Call(f.Input, "OnApplicationFocus", false);
                    f.Check(active.Amplitudes.Last() == 0 && UnifiedWheelFixture.Field<float>(f.Input, "hapticEnd") == 0, "Focus loss immediately stops the active pulse");
                    UnifiedWheelFixture.Call(f.Input, "OnApplicationFocus", true); f.Drain(); f.Frame(Vector2.up);
                    UnifiedWheelFixture.Call(f.Input, "ChangeInputController", useLeft ? OVRInput.Controller.RTouch : OVRInput.Controller.LTouch);
                    f.Check(active.Amplitudes.Last() == 0 && inactive.Amplitudes.Count == 0, "Changing hand stops the original hand without pulsing the other controller");
                    f.Drain(); f.Frame(Vector2.up); UnifiedWheelFixture.Call(f.Input, "OnDisable");
                    f.Check(active.Amplitudes.Last() == 0, "Disabling input immediately stops navigation vibration");
                    int count = active.Amplitudes.Count;
                    typeof(QuestBridge).GetProperty("Active").SetValue(f.Quest, false); f.Drain(); f.Frame(Vector2.up);
                    f.Check(active.Amplitudes.Count == count && inactive.Amplitudes.Count == 0, "Desktop does not send controller vibration");
                    typeof(QuestBridge).GetProperty("Active").SetValue(f.Quest, true);
                }
            }
            finally
            {
                UnifiedWheelFixture.Call(f.Input, "StopHaptic"); f.Wheel.Close();
                OVRInput.controllers = previousControllers; OVRManager.loadedXRDevice = previousDevice;
                typeof(QuestBridge).GetProperty("Active").SetValue(f.Quest, true);
                f.Panel.Bindings.SelectHand(previousLeft); f.Quest.Bindings = previousBindings; f.Drain();
            }
        }

        private static void CheckHandAttachment(UnifiedWheelFixture f, string faction)
        {
            var tracking = new GameObject("Synthetic wheel tracking space");
            var previousTracking = f.Quest.TrackingSpace; var previousBindings = f.Quest.Bindings;
            var previousRay = f.Ray; bool previousLeft = f.Panel.Bindings.LeftHand;
            var camera = f.Quest.Camera.transform;
            var cameraPosition = camera.position; var cameraRotation = camera.rotation;
            typeof(QuestBridge).GetProperty("TrackingSpace").SetValue(f.Quest, tracking.transform);
            f.Quest.Bindings = f.Panel.Bindings;
            try
            {
                foreach (bool left in new[] { false, true })
                {
                    f.Panel.Bindings.SelectHand(left);
                    string hand = left ? "left" : "right";
                    var model = Resources.Load<GameObject>("Controllers/TouchPlus" + (left ? "Left" : "Right"));
                    var stick = model.GetComponentsInChildren<Transform>(true).Single(part => part.name.EndsWith("_b_thumbstick"));
                    var rotation = Quaternion.Euler(20, left ? -15 : 15, left ? 30 : -30);
                    // Native controller tracking is absent in Editor. Apply a synthetic
                    // world pose through the real tracking-space conversion for each hand.
                    tracking.transform.rotation = rotation * Quaternion.Inverse(OVRInput.GetLocalControllerRotation(f.Quest.ActiveController));
                    tracking.transform.position = new Vector3(left ? -.08f : .08f, .8f, -.15f);
                    f.Ray = f.Quest.PointerRay(); f.Dismiss();
                    CheckPose("hintRoot", true);
                    f.CaptureHandPose(faction + "-" + hand + "-hand-hint", "hintRoot");
                    f.OpenGroups(); CheckPose("root", false);
                    f.CaptureHandPose(faction + "-" + hand + "-hand-groups", "root");
                    f.OpenSelection(); CheckPose("root", false);
                    f.OpenOrders(); CheckPose("root", false);
                    f.Dismiss();

                    void CheckPose(string field, bool hint)
                    {
                        var view = UnifiedWheelFixture.Field<RectTransform>(f.Wheel, field);
                        var fromStick = Quaternion.Inverse(rotation) * (view.position - f.Ray.origin) - stick.position;
                        f.Check(Vector3.ProjectOnPlane(fromStick, stick.up).magnitude < .0001f
                            && Mathf.Abs(Vector3.Dot(fromStick, stick.up) - (hint ? .012f : .033f)) < .0001f,
                            hand + " " + field + " keeps its anchor near the real stick, with only 2.5 cm clearance for the open wheel");
                        var surfaceNormal = rotation * stick.up;
                        if (hint)
                        {
                            f.Check(Vector3.Dot(view.forward, surfaceNormal) < -.999f,
                                hand + " callout leaders follow the controller surface");
                            float side = left ? -1 : 1;
                            f.Check(view.GetComponentsInChildren<Text>().All(label => Mathf.Abs(label.rectTransform.anchoredPosition.x) * view.lossyScale.x
                                    - label.rectTransform.rect.width * view.lossyScale.x * .5f > .035f),
                                hand + " labels stay beside the controller and mirror sides without mirroring text");
                        }
                        else CheckFacing();
                        float width = view.rect.width * view.lossyScale.x;
                        f.Check(width < (hint ? .29f : .19f) && width > .15f, hand + " " + field + " keeps bounded physical dimensions");
                        var position = view.position; var orientation = view.rotation;
                        camera.SetPositionAndRotation(f.Ray.origin + Vector3.back * .12f, Quaternion.Euler(-15, 55, 25));
                        f.Frame();
                        f.Check(Vector3.Distance(view.position, position) < .0001f && (!hint || Quaternion.Angle(view.rotation, orientation) < .1f),
                            hand + " " + field + " stays at the hand when the head moves");
                        if (!hint)
                        {
                            CheckFacing(); orientation = view.rotation;
                            camera.rotation = Quaternion.Euler(80, -70, 40); f.Frame();
                            f.Check(Quaternion.Angle(view.rotation, orientation) < .1f, "Head rotation alone cannot spin the wheel or change its directional layout");
                        }
                        void CheckFacing()
                        {
                            var toEye = (camera.position - view.position).normalized;
                            f.Check(Vector3.Angle(-view.forward, surfaceNormal) <= 35.1f
                                && Vector3.Dot(-view.forward, toEye) >= Vector3.Dot(surfaceNormal, toEye) - .001f,
                                hand + " wheel tilts toward the eyes by at most 35 degrees");
                        }
                    }
                }
            }
            finally
            {
                f.Wheel.Close(); f.Ray = previousRay; f.Panel.Bindings.SelectHand(previousLeft); f.Quest.Bindings = previousBindings;
                typeof(QuestBridge).GetProperty("TrackingSpace").SetValue(f.Quest, previousTracking);
                camera.SetPositionAndRotation(cameraPosition, cameraRotation); Object.DestroyImmediate(tracking); f.Drain();
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
        internal void OpenRoot() { OpenGroups(); Frame(b: true); Check(Wheel.IsOpen && Wheel.Page == WheelPage.Root, "Back from a directly opened category reaches the root wheel"); }
        internal void Choose(string id)
        {
            Frame(); Check(Wheel.Items.Any(a => a.Id == id), "Wheel offers " + id);
            Frame(Wheel.DirectionFor(id)); Check(Wheel.HighlightedId == id, "Stick highlights " + id);
        }
        internal void Confirm() { var direction = Wheel.HighlightedId == null ? Vector2.zero : Wheel.DirectionFor(Wheel.HighlightedId); Frame(direction, down: true, held: true); Frame(direction); }
        internal void Enter(string id) { Choose(id); Confirm(); }
        private void OpenCategory(Vector2 direction)
        { if (Wheel.IsOpen) Wheel.Close(); Drain(); Frame(direction); Check(Wheel.IsOpen, "Fresh tilt opens its category without a trigger"); }
        internal void OpenSelection() { OpenCategory(Vector2.right); }
        internal void OpenOrders() { OpenCategory(Vector2.left); Check(Wheel.Page == WheelPage.Orders, "Left directly opens unit orders"); }
        internal void OpenGroups() { OpenCategory(Vector2.up); Check(Wheel.InGroupMenu, "Up directly opens groups"); }
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
        internal void CaptureHandPose(string name, string rootField)
        {
            // Fixed viewpoint and real world scale: yellow marks the ray origin.
            // Unlike layout captures, these views retain the runtime hand attachment.
            var reference = new GameObject("Wheel ray reference");
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.color = new Color(1, .7f, .3f);
            var origin = GameObject.CreatePrimitive(PrimitiveType.Sphere); origin.transform.SetParent(reference.transform);
            origin.transform.position = Ray.origin; origin.transform.localScale = Vector3.one * .012f;
            origin.GetComponent<Renderer>().sharedMaterial = material;
            var line = reference.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.positionCount = 2; line.widthMultiplier = .002f; line.SetPosition(0, Ray.origin); line.SetPosition(1, Ray.GetPoint(.3f));
            preview.AddSingleGO(reference);
            try
            {
                preview.camera.orthographic = false; preview.camera.fieldOfView = 50;
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 10;
                preview.camera.transform.position = new Vector3(0, 1.05f, -.6f);
                preview.camera.transform.LookAt(new Vector3(0, .8f, -.15f));
                Frame();
                HarvestingPreview.SaveCanvas(Wheel.transform, Field<RectTransform>(Wheel, rootField), output + "/" + name + ".png", preview, 1000, 900, preservePose: true);
                captures++;
            }
            finally { Object.DestroyImmediate(reference); Object.DestroyImmediate(material); }
        }
        internal void Report(string label) => Debug.Log($"FRONTIER_{label} passed: {checks} checks, {captures} views; no player build");
        public void Dispose() { Object.DestroyImmediate(wheelObject); Object.DestroyImmediate(console); Object.DestroyImmediate(fixture); preview.Cleanup(); }
    }
}
