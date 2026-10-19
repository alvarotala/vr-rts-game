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
                    CheckTriggerAssignment();
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
                    f.Frame(press: true); f.Frame(Vector2.left, clickHeld: true);
                    f.Check(f.Wheel.InGroupMenu && f.Wheel.GroupSaveMode && f.Wheel.HighlightedId == "group-4" && f.Session.GroupSize(3) == 1, "Inside Groups, holding the stick click and tilting previews replacement without saving early");
                    f.Capture(faction + "-save"); f.Frame(Vector2.left, clickUp: true);
                    f.Check(!f.Wheel.IsOpen && f.Session.GroupSize(3) == 2 && f.Session.Selection.Count == 2, "Releasing the stick click saves the selected units into the highlighted group"); f.Drain();
                    f.OpenGroups(); f.Frame(press: true); f.Frame(Vector2.left, clickHeld: true); f.Frame(clickHeld: true);
                    f.Check(f.Wheel.InGroupMenu && f.Wheel.HighlightedId == "group-4" && f.Session.GroupSize(3) == 2, "Centering while still clicked keeps the pending save destination highlighted without saving early");
                    f.Frame(clickUp: true); f.Drain();
                    f.Check(!f.Wheel.IsOpen && f.Session.GroupSize(3) == 2, "A click release at center saves the last explicitly highlighted group");
                    f.Session.Select(hq.id, false); f.OpenGroups(); f.Choose("group-4"); f.Frame(Vector2.left, grip: true, gripDown: true);
                    f.Check(!f.Wheel.GroupSaveMode, "Grip does not turn group recall into replacement");
                    f.Frame(Vector2.left, down: true, held: true, grip: true); f.Drain();
                    f.Check(f.Session.Selection.SetEquals(new[] { workers[0].id, workers[1].id }) && f.Session.GroupSize(3) == 2, "Grip plus trigger recalls without overwriting the group");
                    for (int i = 0; i < 4; i++)
                    {
                        f.Session.Select(hq.id, false); f.OpenGroups(); Save(i); f.Session.Select(0, false); f.Session.Group(i, false);
                        f.Check(f.Session.Selection.SetEquals(new[] { hq.id }) && f.Session.GroupSize(i) == 1, "Saving replaces every previous member of group " + (i + 1));
                    }
                    for (int i = 0; i < 4; i++) { f.Session.Select(workers[i].id, false); f.Session.Group(i, true); }
                    f.Session.Select(workers[0].id, false); f.OpenGroups(); f.Frame(press: true); f.Frame(Vector2.left, clickHeld: true);
                    f.Frame(Vector2.left, b: true, clickHeld: true); f.Frame(Vector2.left, clickUp: true); f.Dismiss(); f.Session.Group(3, false);
                    f.Check(f.Session.Selection.SetEquals(new[] { workers[3].id }), "Back cancels replacement; the later click release cannot save it");
                    f.Session.Select(workers[0].id, false); f.OpenGroups(); f.Frame(press: true); f.Frame(Vector2.left, clickHeld: true); f.Frame(Vector2.left);
                    f.Check(f.Session.GroupSize(3) == 1, "A missing click-release edge cannot commit replacement"); f.Dismiss();
                    f.OpenGroups(); f.Choose("group-4"); f.Frame(Vector2.left, press: true); f.Frame(Vector2.left, clickUp: true); f.Dismiss(); f.Session.Group(3, false);
                    f.Check(f.Session.Selection.SetEquals(new[] { workers[3].id }), "Clicking a previously tilted group does not overwrite it without the neutral save gesture");
                    f.Session.Select(workers[0].id, false); f.OpenGroups(); f.Frame(press: true); f.Frame(Vector2.left, clickHeld: true);
                    f.Session.Pause("tracking", true); f.Frame(Vector2.left, clickUp: true); f.Session.Pause("tracking", false); f.Drain(); f.Session.Group(3, false);
                    f.Check(f.Session.Selection.SetEquals(new[] { workers[3].id }), "Tracking loss cancels a pending replacement before its release");
                    f.Session.Select(workers[0].id, false); f.OpenGroups(); f.Frame(press: true); f.Frame(Vector2.left, clickHeld: true);
                    UnifiedWheelFixture.Call(f.Input, "OnApplicationFocus", false); f.Frame(Vector2.left, clickUp: true); UnifiedWheelFixture.Call(f.Input, "OnApplicationFocus", true); f.Drain(); f.Session.Group(3, false);
                    f.Check(f.Session.Selection.SetEquals(new[] { workers[3].id }), "Focus loss cannot commit a pending group replacement");
                    f.Session.Select(workers[0].id, false); f.OpenGroups(); f.Frame(press: true); f.Frame(Vector2.left, clickHeld: true);
                    f.Session.Select(hq.id, false); f.Frame(Vector2.left, clickUp: true); f.Drain(); f.Session.Group(3, false);
                    f.Check(f.Session.Selection.SetEquals(new[] { workers[3].id }), "Selection changes during a save cancel instead of storing a different selection");
                    f.Session.Select(hq.id, false); ClickGroup4(true); f.Session.Select(0, false); ClickGroup4(false);
                    f.Check(f.Session.Selection.SetEquals(new[] { hq.id }) && f.Session.GroupSize(3) == 1, "The fourth HUD button still saves on hold and recalls on click");
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
                    f.Refresh(); f.Check(UnifiedWheelFixture.Field<Text[]>(f.Panel, "groupCounts")[3].text == "2 assigned" && UnifiedWheelFixture.Field<Text>(f.Panel, "productionTitle").text == "GROUP PRODUCTION", "Recalled barracks refresh the HUD count and combined production summary"); f.CapturePanel(faction + "-barracks-group");
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

                    void CheckTriggerAssignment()
                    {
                        for (int slot = 0; slot < 4; slot++)
                        {
                            f.Session.Select(workers[0].id, false); f.Session.Select(workers[1].id, true);
                            f.OpenAssignGroups(); f.Frame(directions[slot]);
                            f.Check(f.Wheel.HighlightedId == "group-" + (slot + 1) && f.Session.GroupSize(slot) == 1, "Assign group previews slot " + (slot + 1) + " without changing it");
                            f.Frame(directions[slot], down: true, held: true);
                            f.Check(!f.Wheel.IsOpen && f.Session.GroupSize(slot) == 2 && f.Session.Selection.SetEquals(new[] { workers[0].id, workers[1].id }), "Trigger assigns slot " + (slot + 1) + " and preserves the selection");
                            f.Check(f.Frame(held: true) && f.Frame(up: true), "Assignment consumes its held trigger and release"); f.Drain();
                            f.Session.Select(0, false); f.Session.Group(slot, false);
                            f.Check(f.Session.Selection.SetEquals(new[] { workers[0].id, workers[1].id }), "Assigned slot " + (slot + 1) + " contains exactly the chosen members");
                            f.Session.Select(workers[slot].id, false); f.Session.Group(slot, true);
                        }
                        f.Session.Select(hq.id, false); f.OpenAssignGroups(); f.Frame(Vector2.left);
                        f.Capture(faction + "-trigger-assign-building");
                        f.Frame(Vector2.left, press: true); f.Frame(Vector2.left, clickUp: true);
                        f.Check(f.Wheel.GroupSaveMode && f.Session.GroupSize(3) == 1, "Stick click release does not confirm the dedicated assignment page");
                        f.Frame(Vector2.left, down: true, held: true); f.Drain(); f.Session.Select(0, false); f.Session.Group(3, false);
                        f.Check(f.Session.Selection.SetEquals(new[] { hq.id }), "Trigger assignment replaces the old group with a building");
                        f.Session.Select(workers[3].id, false); f.Session.Group(3, true);

                        f.Session.Select(hq.id, false); f.OpenAssignGroups(); f.Frame(Vector2.up, b: true);
                        f.Check(f.Wheel.Page == WheelPage.Root && !f.Wheel.GroupSaveMode, "Back leaves assignment mode without saving");
                        f.Frame(Vector2.up, down: true, held: true); f.Frame(Vector2.up); f.Frame(Vector2.up, down: true, held: true); f.Drain();
                        f.Check(f.Session.Selection.SetEquals(new[] { workers[0].id }), "Entering Groups after assignment cancellation recalls instead of assigning");
                        f.OpenAssignGroups(); f.Frame(); f.Frame(elapsed: .81f);
                        f.Check(!f.Wheel.IsOpen && Enumerable.Range(0, 4).All(i => f.Session.GroupSize(i) == 1), "Center timeout cancels assignment without saving"); f.Drain();
                        f.OpenAssignGroups(); f.Session.Select(hq.id, false); f.Frame(Vector2.up, down: true, held: true); f.Drain();
                        f.Check(!f.Wheel.IsOpen && f.Session.GroupSize(0) == 1, "Selection change cancels pending assignment before a trigger can save");
                        f.OpenAssignGroups(); f.Session.Pause("tracking", true); f.Frame(Vector2.up, down: true, held: true);
                        f.Session.Pause("tracking", false); f.Drain(); f.Session.Group(0, false);
                        f.Check(f.Session.Selection.SetEquals(new[] { workers[0].id }), "Tracking interruption cancels assignment without overwriting a group");
                        f.Session.Select(0, false); f.OpenAssignGroups(); f.Frame(Vector2.left, down: true, held: true); f.Drain();
                        f.Check(f.Session.GroupSize(3) == 0 && f.Session.Selection.Count == 0, "Explicit assignment of an empty selection clears only the chosen slot");
                        f.Session.Select(workers[3].id, false); f.Session.Group(3, true); f.Session.Select(workers[0].id, false);
                    }
                }
                f.Report("CONTROL_GROUPS");

                void Save(int slot)
                {
                    f.Frame(); f.Frame(press: true); f.Frame(f.Wheel.DirectionFor("group-" + (slot + 1)), clickHeld: true);
                    f.Frame(f.Wheel.DirectionFor("group-" + (slot + 1)), clickUp: true); f.Drain();
                }
                void ClickGroup4(bool save)
                {
                    f.Refresh(); var rect = f.Panel.GetComponentsInChildren<RectTransform>().First(r => r.name == "Group 4" && r.gameObject.activeInHierarchy);
                    var center = rect.TransformPoint(rect.rect.center); var pointer = new Ray(center - rect.forward, rect.forward);
                    f.Check(f.Panel.Point(pointer, true, false), "Group 4 HUD press hits its target");
                    if (save) typeof(CommandPanel).GetField("pressedAt", UnifiedWheelFixture.Private).SetValue(f.Panel, Time.unscaledTime - 1);
                    f.Check(f.Panel.Point(pointer, false, true), "Group 4 HUD release hits its target"); f.Refresh();
                }
            }
        }
    }
}
