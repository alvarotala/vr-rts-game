using System.Linq;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Editor
{
    public static class ControlGroupPreview
    {
        public static void RunValidation()
        {
            UnifiedWheelPreview.Render(); Render(); ActionWheelPreview.Render();
            QueuedInputChecks.Run(); RallyPointInputChecks.Run(); MenuFlowPreview.Render(); HarvestingPreview.RenderHud();
        }

        [MenuItem("Frontier/Preview and check control groups")]
        public static void Render()
        {
            using (var f = new UnifiedWheelFixture("ControlGroups"))
            {
                foreach (string faction in new[] { "industrial", "biological" })
                {
                    f.Start(faction);
                    var workers = f.Session.View.entities.Where(e => e.owner == 0 && f.Session.Catalog[e.definition].role == "worker").Take(4).ToArray();
                    var hq = f.Session.View.entities.First(e => e.owner == 0 && f.Session.Catalog[e.definition].role == "hq");
                    var directions = new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
                    f.Ray = new Ray(f.Board.Content.TransformPoint(new Vector3(workers[0].position.x, 0, workers[0].position.y)) + Vector3.up, Vector3.down);
                    var boardPose = f.Board.transform.localToWorldMatrix;
                    for (int i = 0; i < 4; i++) { f.Session.Select(workers[i].id, false); f.Session.Group(i, true); }
                    f.Check(Enumerable.Range(0, 4).All(i => f.Session.GroupSize(i) == 1), "All four independent group slots can be saved");
                    f.Session.Select(workers[0].id, false); f.Refresh();
                    f.Check(UnifiedWheelFixture.Field<Text[]>(f.Panel, "groupCounts").Length == 4 && UnifiedWheelFixture.Field<Text[]>(f.Panel, "groupCounts").All(t => t.text == "1 assigned"), "HUD displays all four group counts");
                    f.CapturePanel(faction + "-hud");
                    CheckHudAssignment();
                    CheckStickAssignment();
                    f.OpenGroups(); f.Choose("group-2"); f.Frame();
                    f.Check(f.Wheel.InGroupMenu && f.Wheel.Highlighted == -1 && f.Session.Selection.Contains(workers[0].id), "Centering a group highlight clears it without recalling the group");
                    f.Choose("group-2"); f.Frame(directions[1], a: true);
                    f.Check(f.Wheel.InGroupMenu && f.Session.Selection.Contains(workers[0].id), "The contextual order button does not recall a highlighted group");
                    f.Capture(faction + "-recall");
                    f.Frame(directions[1], down: true, held: true);
                    f.Check(!f.Wheel.IsOpen && f.Session.Selection.SetEquals(new[] { workers[1].id }), "Trigger recalls the highlighted group");
                    f.Check(f.Frame(held: true) && f.Frame(up: true), "Group recall drains its trigger and release before map selection"); f.Drain();
                    for (int i = 0; i < 4; i++)
                    {
                        f.OpenGroups(); f.Frame(); f.Frame(directions[i]);
                        f.Check(f.Wheel.HighlightedId == "group-" + (i + 1), "Cardinal direction highlights group " + (i + 1));
                        f.Confirm(); f.Drain();
                        f.Check(f.Session.Selection.SetEquals(new[] { workers[i].id }), "Trigger recalls group " + (i + 1));
                    }
                    f.OpenGroups(); f.Frame(); f.Frame(Vector2.up); f.Frame(new Vector2(.71f, .70f));
                    f.Check(f.Wheel.HighlightedId == "group-1", "Sector boundary jitter keeps the previous group highlight");
                    f.Frame(Vector2.right); f.Frame(Vector2.down); f.Frame(Vector2.left);
                    f.Check(f.Session.Selection.Contains(workers[3].id) && f.Wheel.HighlightedId == "group-4", "Rotating across groups changes only the preview");
                    f.Frame(b: true);
                    f.Check(f.Wheel.Page == WheelPage.Root && f.Session.Selection.Contains(workers[3].id), "Back from Groups returns to root without recalling"); f.Dismiss();

                    f.Session.Select(workers[0].id, false); f.Session.Select(workers[1].id, true); f.OpenGroups();
                    bool hintsBefore = f.Wheel.HintsEnabled;
                    f.Choose("group-4"); f.Capture(faction + "-assign"); f.Frame(Vector2.left, press: true);
                    f.Check(!f.Wheel.IsOpen && f.Session.GroupSize(3) == 2 && f.Wheel.HintsEnabled == hintsBefore,
                        "Stick press immediately assigns the highlighted group without toggling hints");
                    f.Check(f.Frame(clickHeld: true) && f.Frame(clickUp: true), "Assignment drains the held click and its release"); f.Drain();
                    f.Session.Select(hq.id, false); f.OpenGroups(); f.Choose("group-4"); f.Frame(Vector2.left, grip: true, gripDown: true);
                    f.Frame(Vector2.left, down: true, held: true, grip: true); f.Drain();
                    f.Check(f.Session.Selection.SetEquals(new[] { workers[0].id, workers[1].id }) && f.Session.GroupSize(3) == 2, "Grip plus trigger recalls without overwriting the group");
                    for (int i = 0; i < 4; i++)
                    {
                        f.Session.Select(hq.id, false); Save(i); f.Session.Select(0, false); f.Session.Group(i, false);
                        f.Check(f.Session.Selection.SetEquals(new[] { hq.id }) && f.Session.GroupSize(i) == 1, "Saving replaces every previous member of group " + (i + 1));
                    }
                    for (int i = 0; i < 4; i++) { f.Session.Select(workers[i].id, false); f.Session.Group(i, true); }
                    f.Session.Select(hq.id, false); ClickGroup4(true); f.Session.Select(0, false); ClickGroup4(false);
                    f.Check(f.Session.Selection.SetEquals(new[] { hq.id }) && f.Session.GroupSize(3) == 1, "The fourth HUD button saves with a modifier click and recalls with a plain click");
                    f.Session.Select(workers[0].id, false); f.Session.ChooseBuild(f.Session.Catalog.Role(faction, "supply").id); f.OpenGroups(); f.Enter("group-4"); f.Drain();
                    f.Check(f.Session.Selection.Contains(hq.id) && f.Session.BuildDefinition == null, "Recalling a group clears armed construction without placing a building");
                    f.Session.Select(workers[0].id, false); f.Session.ChooseAttackMove(); f.OpenGroups(); f.Enter("group-2"); f.Drain();
                    f.Check(f.Session.Selection.Contains(workers[1].id) && !f.Session.AttackMoveMode && !f.Session.AttackPointerActive, "Recalling another group cancels the old attack destination and red targeting pointer");
                    f.Session.Select(0, false); f.OpenGroups(); f.Enter("group-2"); f.Drain();
                    f.Check(f.Session.Selection.SetEquals(new[] { workers[1].id }), "Groups can be recalled with no current selection");
                    f.Session.Match.Entities.Remove(workers[2].id); f.Refresh();
                    f.Check(f.Session.GroupSize(2) == 0, "Destroyed members no longer count toward a group");
                    f.OpenGroups(); f.Enter("group-3"); f.Drain();
                    f.Check(f.Session.Selection.Count == 0 && f.Session.Message.Contains("empty"), "Empty groups are explicit and never restore dead units");
                    var enemy = f.Session.Match.Entities.Values.First(e => e.owner == 1); f.Session.Selection.Add(enemy.id); f.Session.Group(2, true);
                    f.Check(f.Session.GroupSize(2) == 0, "Groups never store forged enemy selection IDs"); f.Session.Selection.Clear();

                    var first = f.Add("barracks", 2001, new Point(18, 18)); var second = f.Add("barracks", 2002, new Point(25, 18));
                    f.Session.Select(first.id, false); f.Session.Select(second.id, true);
                    f.Check(f.Session.Send(CommandKind.SetRallyPoint, position: new Point(20.5f, 25.5f)), "Multiple barracks accept a shared rally point");
                    string trainId = "train:" + f.Session.Catalog.Role(faction, "ranged").id;
                    f.Check(SelectionActions.For(f.Session).Items.First(a => a.Id == trainId).Execute(), "Multiple barracks offer real production before grouping");
                    int queued = first.queue.Count + second.queue.Count;
                    f.OpenGroups(); Save(3); f.Check(f.Session.GroupSize(3) == 2, "Groups save multiple buildings together");
                    f.Session.Select(workers[0].id, false); f.OpenGroups(); f.Enter("group-4"); f.Drain();
                    f.Check(f.Session.Selection.SetEquals(new[] { first.id, second.id }) && first.queue.Count + second.queue.Count == queued && first.hasRallyPoint && second.hasRallyPoint, "Recalling a barracks group preserves production and shared rally points");
                    f.Refresh(); f.Check(UnifiedWheelFixture.Field<Text[]>(f.Panel, "groupCounts")[3].text == "2 assigned" && UnifiedWheelFixture.Field<Text>(f.Panel, "productionTitle").text == "PRODUCTION", "Recalled barracks refresh the HUD count and combined production summary"); f.CapturePanel(faction + "-barracks-group");
                    f.OpenSelection(); f.Enter(trainId); f.Drain();
                    f.Check(first.queue.Count == 1 && second.queue.Count == 1 && f.Session.Selection.Count == 2, "Wheel training buys one unit at the idle barracks while retaining group selection");
                    foreach (string role in new[] { "tech", "academy" }) { var producer = f.Add(role, role == "tech" ? 2003 : 2004, new Point(18, role == "tech" ? 30 : 35)); f.Session.Select(producer.id, true); }
                    f.Session.Select(hq.id, true); f.OpenSelection();
                    f.Check(f.Wheel.Items.Count(a => a.Id != "back") == 9, "All producer roles and shared rally points expose nine commands plus Back");
                    foreach (var action in f.Wheel.Items.ToArray()) f.Choose(action.Id);
                    f.Capture(faction + "-producer-actions"); f.Dismiss();
                    f.Session.Match.Entities.Remove(second.id); f.Refresh(); f.Session.Select(0, false); f.OpenGroups(); f.Enter("group-4"); f.Drain();
                    f.Check(f.Session.Selection.SetEquals(new[] { first.id }) && f.Session.GroupSize(3) == 1, "Recall excludes destroyed buildings while retaining the remaining producer");
                    f.Check(f.Board.transform.localToWorldMatrix == boardPose, "Group navigation leaves board placement unchanged");
                    f.OpenGroups(); f.Choose("group-2"); f.Session.StartMatch(); f.Frame();
                    f.Check(!f.Wheel.IsOpen && Enumerable.Range(0, 4).All(i => f.Session.GroupSize(i) == 0), "A rematch resets all groups and invalidates the open wheel"); f.Drain();

                    void CheckHudAssignment()
                    {
                        int feedback = 0;
                        System.Action<bool> countFeedback = success => { if (success) feedback++; };
                        f.Session.Feedback += countFeedback;
                        try
                        {
                            PreparePair(); var pointer = GroupPointer();
                            f.Panel.Point(pointer, true, false); Age(2); f.Panel.Point(pointer, false, false);
                            UnifiedWheelFixture.Call(f.Panel, "UpdateGroupFeedback");
                            f.Check(f.Session.GroupSize(3) == 1 && feedback == 0 && f.Session.Selection.SetEquals(new[] { workers[0].id, workers[1].id })
                                && !f.Panel.GetComponentsInChildren<RectTransform>(true).Any(r => r.name == "Assignment progress track"),
                                "A long plain Trigger press neither assigns nor shows an assignment loader");
                            f.Panel.Point(pointer, false, true);
                            f.Check(f.Session.Selection.SetEquals(new[] { workers[3].id }) && f.Session.GroupSize(3) == 1 && feedback == 1,
                                "Releasing a long plain Trigger press recalls the group exactly once");

                            Prepare(); pointer = GroupPointer(); f.Panel.Point(pointer, true, false); f.Panel.Point(pointer, false, true);
                            f.Check(f.Session.Selection.SetEquals(new[] { workers[3].id }) && feedback == 1,
                                "A short plain HUD click recalls the saved group");

                            PreparePair(); pointer = GroupPointer(); f.Panel.Point(pointer, true, false, 0, true);
                            Age(2); f.Panel.Point(pointer, false, false, 0, true); UnifiedWheelFixture.Call(f.Panel, "UpdateGroupFeedback");
                            f.Check(f.Session.GroupSize(3) == 1 && feedback == 0, "Grip plus Trigger waits for its owning release before assigning");
                            f.Panel.Point(pointer, false, true, 0, false);
                            f.Check(f.Session.GroupSize(3) == 2 && feedback == 1 && Count() == "Saved · 2"
                                && f.Session.Selection.SetEquals(new[] { workers[0].id, workers[1].id }),
                                "Grip captured on press assigns once even when released before Trigger, with visual and haptic confirmation");
                            var groupCard=f.Panel.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Group 4");
                            f.Check(groupCard.GetComponent<Image>().color==ConsoleTheme.Accent
                                &&groupCard.GetComponentsInChildren<Text>().All(t=>t.color==ConsoleTheme.HoverInk),
                                "Saved groups retain the shared accent hover with readable confirmation text");
                            f.Panel.Point(new Ray(pointer.origin+Vector3.right*100,pointer.direction),false,false);
                            UnifiedWheelFixture.Call(f.Panel,"UpdateGroupFeedback");
                            var savedFill=groupCard.GetComponent<Image>().color;
                            f.Check(savedFill.g>savedFill.r&&savedFill.g>savedFill.b
                                &&groupCard.GetComponentsInChildren<Text>().All(t=>t.color!=ConsoleTheme.HoverInk),
                                "Leaving a hovered saved group restores its green confirmation and original text colors");
                            f.Panel.Point(pointer,false,false);
                            f.CapturePanel(faction + "-hud-group-saved");
                            // Flat previews move the canvas; use its current pose for later edges.
                            pointer = GroupPointer(); f.Panel.Point(pointer, false, true);
                            f.Check(feedback == 1, "Duplicate release never repeats the group assignment");
                            UnifiedWheelFixture.Field<float[]>(f.Panel, "groupConfirmedUntil")[3] = Time.unscaledTime - 1;
                            UnifiedWheelFixture.Call(f.Panel, "UpdateGroupFeedback");
                            f.Check(Count() == "2 assigned", "The saved confirmation returns to the normal count after its display interval");

                            Prepare(); pointer = GroupPointer(); f.Panel.Point(pointer, true, false, 0, false); f.Panel.Point(pointer, false, true, 0, true);
                            f.Check(f.Session.GroupSize(3) == 2 && feedback == 1 && f.Session.Selection.SetEquals(new[] { workers[0].id, workers[1].id }),
                                "Adding Grip after the press cannot turn a recall into assignment");

                            Prepare(); pointer = GroupPointer(); Neutral(pointer);
                            Route(pointer, true, false, true, true, false, true, true);
                            Route(pointer, false, false, true, false, true, false, false);
                            f.Check(feedback == 0 && f.Session.GroupSize(3) == 2, "The A alias release cannot confirm a Trigger-owned group assignment");
                            Route(pointer, false, true, false, false, false, false, false);
                            Route(pointer, false, true, false, false, true, false, false);
                            f.Check(feedback == 1 && f.Session.GroupSize(3) == 1 && f.Session.Selection.SetEquals(new[] { workers[2].id }),
                                "Simultaneous Trigger and A save only once on the owning release");

                            PreparePair(); pointer = GroupPointer(); Neutral(pointer);
                            Route(pointer, false, false, false, true, false, true, true);
                            Route(pointer, true, false, true, false, false, true, false);
                            Route(pointer, false, true, false, false, false, true, false);
                            f.Check(feedback == 0 && f.Session.GroupSize(3) == 1, "Trigger cannot confirm an A-owned modifier click");
                            Route(pointer, false, false, false, false, true, false, false);
                            f.Check(feedback == 1 && f.Session.GroupSize(3) == 2, "The menu A alias also preserves the modifier captured at press");

                            Prepare(); pointer = GroupPointer(); Neutral(pointer);
                            Route(pointer, false, false, false, true, false, true, false);
                            Route(pointer, false, false, false, false, true, false, false);
                            f.Check(feedback == 1 && f.Session.Selection.SetEquals(new[] { workers[0].id, workers[1].id }),
                                "The unmodified A alias recalls the group without assigning");

                            Prepare(); pointer = GroupPointer(); f.Panel.Point(pointer, true, false, 0, true);
                            var outside = new Ray(pointer.origin + f.Panel.transform.right * 100, pointer.direction);
                            f.Panel.Point(outside, false, false, 0, true); f.Panel.Point(pointer, false, false, 0, true); f.Panel.Point(pointer, false, true, 0, true);
                            f.Check(f.Session.GroupSize(3) == 2 && feedback == 0 && !GestureActive(),
                                "Leaving a group cancels the modifier click and returning cannot resume it");

                            Prepare(); pointer = GroupPointer(); f.Panel.Point(pointer, true, false, 0, true);
                            UnifiedWheelFixture.Call(f.Panel, "OnApplicationFocus", false); f.Panel.Point(pointer, false, true, 0, true);
                            UnifiedWheelFixture.Call(f.Panel, "OnApplicationFocus", true);
                            f.Check(f.Session.GroupSize(3) == 2 && feedback == 0 && !GestureActive(), "Focus loss cancels a pending modifier assignment");

                            Prepare(); pointer = GroupPointer(); f.Panel.Point(pointer, true, false, 0, true);
                            f.Session.Select(hq.id, false); feedback = 0; f.Panel.Point(pointer, false, true, 0, true);
                            f.Check(f.Session.GroupSize(3) == 2 && feedback == 0 && !GestureActive(), "A selection change invalidates the captured group gesture");

                            Prepare(); pointer = GroupPointer(); f.Panel.Point(pointer, true, false, 0, true);
                            f.Session.Pause("tracking", true); f.Refresh(); f.Session.Pause("tracking", false); f.Refresh();
                            f.Panel.Point(GroupPointer(), false, true, 0, true);
                            f.Check(f.Session.GroupSize(3) == 2 && feedback == 0 && !GestureActive(), "Tracking interruption cancels a pending group assignment");

                            Prepare(); pointer = GroupPointer(); Neutral(pointer);
                            Route(pointer, true, false, true, false, false, false, true);
                            f.Check(!f.Session.HasSystemPause && GestureActive(), "Controller tracking test starts a modifier click while the match remains unpaused");
                            f.Check(!(bool)UnifiedWheelFixture.Call(f.Input, "ValidatePointerTracking", false), "Lost controller tracking stops input routing");
                            Age(2); UnifiedWheelFixture.Call(f.Panel, "UpdateGroupFeedback");
                            UnifiedWheelFixture.Call(f.Input, "ValidatePointerTracking", true);
                            Route(pointer, false, true, false, false, false, false, true);
                            f.Check(!f.Session.HasSystemPause && f.Session.GroupSize(3) == 2 && feedback == 0 && !GestureActive(),
                                "Losing only controller tracking cancels assignment and drains the recovered release");

                            Prepare(); pointer = GroupPointer(); f.Panel.Point(pointer, true, false, 0, true);
                            UnifiedWheelFixture.Call(f.Panel, "Rebuild"); f.Panel.Point(GroupPointer(), false, true, 0, true);
                            f.Check(f.Session.GroupSize(3) == 2 && feedback == 0 && !GestureActive(), "HUD rebuild cancels a modifier click without saving or recalling");
                        }
                        finally
                        {
                            f.Session.Feedback -= countFeedback; f.Panel.CancelPointerInteraction();
                            System.Array.Clear(UnifiedWheelFixture.Field<float[]>(f.Panel, "groupConfirmedUntil"), 0, GameSession.ControlGroupCount);
                            for (int i = 0; i < 4; i++) { f.Session.Select(workers[i].id, false); f.Session.Group(i, true); }
                            f.Session.Select(workers[0].id, false); f.Refresh();
                        }

                        void Prepare() { f.Panel.CancelPointerInteraction(); f.Session.Select(workers[2].id, false); f.Refresh(); feedback = 0; }
                        void PreparePair() { f.Panel.CancelPointerInteraction(); f.Session.Select(workers[0].id, false); f.Session.Select(workers[1].id, true); f.Refresh(); feedback = 0; }
                        void Age(float elapsed) => typeof(CommandPanel).GetField("pressedAt", UnifiedWheelFixture.Private).SetValue(f.Panel, Time.unscaledTime - elapsed);
                        bool GestureActive() => UnifiedWheelFixture.Field<object>(f.Panel, "groupGestureTarget") != null;
                        string Count() => UnifiedWheelFixture.Field<Text[]>(f.Panel, "groupCounts")[3].text;
                        bool Route(Ray pointer,bool down,bool up,bool held,bool aDown,bool aUp,bool aHeld,bool modifier)
                            => (bool)UnifiedWheelFixture.Call(f.Input, "RoutePanelInput", pointer, down, up, held, aDown, aUp, aHeld, 0f, modifier);
                        void Neutral(Ray pointer)
                        {
                            Route(pointer, false, false, false, false, false, false, false);
                            f.Check(!UnifiedWheelFixture.Field<bool>(f.Input, "panelReleasePending"), "A neutral controller frame drains earlier panel releases");
                        }
                        Ray GroupPointer()
                        {
                            var rect = f.Panel.GetComponentsInChildren<RectTransform>().First(r => r.name == "Group 4" && r.gameObject.activeInHierarchy);
                            var center = rect.TransformPoint(rect.rect.center); return new Ray(center - rect.forward, rect.forward);
                        }
                    }

                    void CheckStickAssignment()
                    {
                        bool hints = f.Wheel.HintsEnabled;
                        for (int slot = 0; slot < 4; slot++)
                        {
                            f.Session.Select(workers[0].id, false); f.Session.Select(workers[1].id, true);
                            f.OpenGroups(); f.Frame(directions[slot]);
                            f.Check(f.Wheel.HighlightedId == "group-" + (slot + 1) && f.Session.GroupSize(slot) == 1,
                                "Highlighting group " + (slot + 1) + " does not assign it");
                            f.Frame(directions[slot], press: true);
                            f.Check(!f.Wheel.IsOpen && f.Session.GroupSize(slot) == 2 && f.Session.Selection.SetEquals(new[] { workers[0].id, workers[1].id }) && f.Wheel.HintsEnabled == hints,
                                "Stick press assigns group " + (slot + 1) + " immediately, keeping selection and hints");
                            f.Check(f.Frame(directions[(slot + 1) % 4], clickHeld: true) && f.Frame(clickUp: true), "Holding, moving and releasing the click are consumed without another assignment"); f.Drain();
                            f.Session.Select(0, false); f.Session.Group(slot, false);
                            f.Check(f.Session.Selection.SetEquals(new[] { workers[0].id, workers[1].id }), "Assigned group contains exactly the chosen members");
                            f.Session.Select(workers[slot].id, false); f.Session.Group(slot, true);
                        }
                        f.Session.Select(hq.id, false); f.OpenGroups(); f.Frame(press: true); f.Frame(Vector2.left, clickHeld: true); f.Frame(Vector2.left, clickUp: true);
                        f.Check(f.Wheel.InGroupMenu && f.Session.GroupSize(3) == 1 && f.Wheel.HintsEnabled == hints,
                            "Clicking at center then tilting and releasing does not assign or toggle hints"); f.Dismiss();
                        f.OpenGroups(); f.Frame(Vector2.left, b: true, press: true); f.Frame(clickUp: true); f.Dismiss(); f.Session.Group(3, false);
                        f.Check(f.Session.Selection.SetEquals(new[] { workers[3].id }), "Back takes priority over a simultaneous assignment click");
                        f.OpenGroups(); f.Frame(); f.Frame(elapsed: .81f); f.Drain();
                        f.Check(!f.Wheel.IsOpen && Enumerable.Range(0, 4).All(i => f.Session.GroupSize(i) == 1), "Center timeout closes Groups without assigning");
                        f.OpenGroups(); f.Session.Select(hq.id, false); f.Frame(Vector2.up, press: true); f.Frame(clickUp: true); f.Drain(); f.Session.Group(0, false);
                        f.Check(f.Session.Selection.SetEquals(new[] { workers[0].id }), "Selection change cancels the wheel before a click can assign");
                        f.OpenGroups(); f.Session.Pause("tracking", true); f.Frame(Vector2.left, press: true);
                        f.Session.Pause("tracking", false); f.Frame(clickUp: true); f.Drain(); f.Session.Group(3, false);
                        f.Check(f.Session.Selection.SetEquals(new[] { workers[3].id }), "Tracking interruption prevents assignment");
                        f.OpenGroups(); UnifiedWheelFixture.Call(f.Input, "OnApplicationFocus", false); f.Frame(Vector2.left, press: true);
                        UnifiedWheelFixture.Call(f.Input, "OnApplicationFocus", true); f.Frame(clickUp: true); f.Drain(); f.Session.Group(3, false);
                        f.Check(f.Session.Selection.SetEquals(new[] { workers[3].id }), "Focus loss drains a click without assignment");
                        f.Session.Select(0, false); f.OpenGroups(); f.Frame(Vector2.left, press: true); f.Frame(clickUp: true); f.Drain();
                        f.Check(f.Session.GroupSize(3) == 0 && f.Session.Selection.Count == 0, "Click with an empty selection clears only the highlighted group");
                        f.Session.Select(workers[3].id, false); f.Session.Group(3, true); f.Session.Select(workers[0].id, false);
                    }
                }
                f.Report("CONTROL_GROUPS");

                void Save(int slot)
                {
                    f.OpenGroups(); f.Choose("group-" + (slot + 1)); f.Frame(f.Wheel.DirectionFor(f.Wheel.HighlightedId), press: true); f.Frame(clickUp: true); f.Drain();
                }
                void ClickGroup4(bool save)
                {
                    f.Refresh(); var rect = f.Panel.GetComponentsInChildren<RectTransform>().First(r => r.name == "Group 4" && r.gameObject.activeInHierarchy);
                    var center = rect.TransformPoint(rect.rect.center); var pointer = new Ray(center - rect.forward, rect.forward);
                    f.Check(f.Panel.Point(pointer, true, false, 0, save), "Group 4 HUD press hits its target");
                    f.Check(f.Panel.Point(pointer, false, true, 0, save), "Group 4 HUD release hits its target"); f.Refresh();
                }
            }
        }
    }
}
