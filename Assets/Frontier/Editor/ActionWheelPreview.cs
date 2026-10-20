using System.Linq;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Frontier.Editor
{
    // Real commands through the unified wheel, including destination ownership and failures.
    public static class ActionWheelPreview
    {
        [MenuItem("Frontier/Preview and check action wheel")]
        public static void Render()
        {
            using (var f = new UnifiedWheelFixture("ActionWheel"))
            {
                foreach (string faction in new[] { "industrial", "biological" })
                {
                    f.Start(faction);
                    var worker = f.Session.Match.Entities.Values.First(e => e.owner == 0 && f.Session.Catalog[e.definition].role == "worker");
                    var other = f.Session.Match.Entities.Values.First(e => e.owner == 0 && e.definition == worker.definition && e.id != worker.id);
                    var hq = f.Session.Match.Entities.Values.First(e => e.owner == 0 && f.Session.Catalog[e.definition].role == "hq");
                    var barracks = f.Add("barracks", 1001, new Point(22, 12));
                    var tech = f.Add("tech", 1002, new Point(22, 17));
                    var supply = f.Add("supply", 1003, new Point(20, 8));
                    f.Session.Select(worker.id, false);
                    f.Ray = new Ray(f.Board.Content.TransformPoint(new Vector3(worker.position.x, 0, worker.position.y)) + Vector3.up, Vector3.down);
                    f.OpenSelection();
                    f.Check(!f.Wheel.Items.Any(a => a.Id == "build-menu" || a.Id == "hold"), "Worker selection directly exposes construction and keeps unit orders separate");
                    f.Check(f.Wheel.Page == WheelPage.Selection && f.Wheel.Items.Count(a => a.Building) == 7, "Selection offers all seven faction buildings without an intermediate menu");
                    f.Choose("build:" + f.Session.Catalog.Role(faction, "barracks").id); f.Capture(faction + "-build");
                    f.Enter("back"); f.Check(f.Wheel.Page == WheelPage.Root, "Back from worker actions returns directly to the root");
                    f.Session.Select(other.id, true); f.OpenSelection();
                    f.Check(f.Wheel.Items.Count(a => a.Building) == 7, "Multiple workers also expose the same buildings directly");
                    f.Session.Select(worker.id, false); f.OpenSelection();
                    f.Choose("build:" + f.Session.Catalog.Role(faction, "supply").id);
                    int buildings = f.Session.Match.Buildings(0);
                    f.Frame(f.Wheel.DirectionFor(f.Wheel.HighlightedId), down: true, held: true);
                    f.Check(f.Wheel.IsOpen && f.Session.BuildDefinition == f.Session.Catalog.Role(faction, "supply").id && f.Session.Match.Buildings(0) == buildings, "Trigger arms construction and keeps Selection open without placing a building underneath the wheel");
                    f.Check(f.Frame(held: true) && f.Frame(up: true), "Construction waits for a separate trigger after the wheel confirmation is released"); f.Drain();
                    f.OpenOrders(); f.Choose("hold"); f.Capture(faction + "-worker-orders"); f.Confirm(); f.Drain();
                    f.Check(worker.order == CommandKind.Hold && f.Session.BuildDefinition == null, "Hold reaches the shared command path and clears an older construction destination");
                    f.OpenOrders(); f.Choose("attack-move"); f.Confirm();
                    f.Check(!f.Wheel.IsOpen && f.Session.AttackMoveMode && worker.order == CommandKind.Hold, "Attack awaits a separate destination without applying the confirming trigger to terrain");
                    f.Check(f.Session.AttackPointerActive && f.Board.PointerColor == BoardView.AttackPointerColor, "Wheel attack displays the red destination pointer");
                    f.Drain(); var target = new Point(20.5f, 12.5f);
                    f.Ray = new Ray(f.Board.Content.TransformPoint(new Vector3(target.x, 0, target.y)) + Vector3.up, Vector3.down);
                    f.Frame(down: true, held: true); f.Frame(up: true);
                    f.Check(!f.Session.AttackMoveMode && worker.order == CommandKind.AttackMove, "A later terrain trigger gives the armed attack destination"); f.Drain();
                    f.Session.Select(other.id, true); f.OpenOrders(); f.Enter("stop"); f.Drain();
                    f.Check(worker.order == CommandKind.Stop && other.order == CommandKind.Stop && f.Session.Selection.Count == 2, "Unit orders apply to the whole selected group");

                    f.Session.Select(hq.id, false); f.OpenSelection(); f.Choose("train:" + worker.definition); f.Capture(faction + "-train");
                    var trainId = "train:" + worker.definition;
                    var trainingSlots = f.Wheel.Items.Select(a => a.Id).ToArray();
                    var trainingDirection = f.Wheel.DirectionFor(trainId);
                    int minerals = f.Session.View.minerals; f.Frame(trainingDirection, down: true, held: true);
                    f.Check(hq.queue.Count == 1 && f.Session.View.minerals == minerals - f.Session.Catalog[worker.definition].minerals, "Training spends the real unit cost and creates one production order");
                    f.Check(f.Wheel.IsOpen && f.Wheel.Page == WheelPage.Selection && f.Wheel.HighlightedId == trainId, "Selection stays open on the same item after confirmation");
                    f.Frame(trainingDirection, held: true, elapsed: 1);
                    f.Check(hq.queue.Count == 1 && f.Wheel.IsOpen, "Holding Trigger never repeats production or closes a tilted wheel");
                    f.Frame(trainingDirection, up: true); f.Frame(trainingDirection, down: true, held: true);
                    f.Frame(trainingDirection, up: true); f.Frame(trainingDirection, down: true, held: true);
                    f.Check(hq.queue.Count == 3 && f.Session.View.minerals == minerals - 3 * f.Session.Catalog[worker.definition].minerals && f.Session.Selection.SetEquals(new[] { hq.id }),
                        "Three fresh Trigger presses queue three units without recentering, reopening or leaking selection to the board");
                    f.Check(f.Wheel.Items.Select(a => a.Id).SequenceEqual(trainingSlots) && f.Wheel.DirectionFor(trainId) == trainingDirection && f.Wheel.HighlightedId == trainId,
                        "Starting production preserves all sectors and the held highlight without adding Cancel");
                    f.Capture(faction + "-repeat-production");
                    f.Frame(trainingDirection, up: true); f.Frame(); f.Frame(elapsed: .6f);
                    f.Check(f.Wheel.IsOpen, "A brief neutral pause after production leaves the wheel available");
                    f.Frame(elapsed: .25f);
                    f.Check(!f.Wheel.IsOpen && f.Wheel.HintVisible, "Releasing the stick closes Selection after the existing neutral delay"); f.Drain();
                    CancelFromPanel(f); CancelFromPanel(f); CancelFromPanel(f);
                    f.Check(hq.queue.Count == 0 && f.Session.View.minerals == minerals, "The main panel still cancels and refunds each queued order");
                    f.Session.Match.Players[0].minerals = 0; f.Refresh(); f.OpenSelection(); f.Choose("train:" + worker.definition); f.Confirm();
                    f.Check(f.Wheel.IsOpen && f.Wheel.Page == WheelPage.Selection && hq.queue.Count == 0 && UnifiedWheelFixture.Field<Text>(f.Wheel, "detail").text == f.Session.Message, "Rejected production keeps its submenu and displays the reason");
                    f.Capture(faction + "-not-enough-resources");
                    f.Session.Match.Players[0].minerals = 5000; f.Refresh(); f.Choose(trainId); f.Confirm();
                    f.Check(hq.queue.Count == 1 && f.Wheel.IsOpen && UnifiedWheelFixture.Field<Text>(f.Wheel, "detail").text == f.Wheel.Items.First(a => a.Id == trainId).Detail,
                        "Retrying after resources recover keeps Selection open and clears the old failure message");
                    f.Session.Select(tech.id, false); f.OpenSelection(); f.Choose("research:" + f.Session.Catalog.Definition.upgrades[0].id); f.Capture(faction + "-research"); f.Confirm(); f.Drain();
                    f.Check(tech.queue.Count == 1 && tech.queue[0].research && f.Wheel.IsOpen, "Research uses the shared command path while keeping Selection open");
                    f.Choose("research:" + f.Session.Catalog.Definition.upgrades[1].id); f.Confirm();
                    f.Check(tech.queue.Count == 2 && tech.queue.All(q => q.research), "Another research option can be queued in the same open wheel");
                    string rangedId = "train:" + f.Session.Catalog.Role(faction, "ranged").id;
                    f.Session.Select(barracks.id, false); f.OpenSelection(); f.Enter(rangedId);
                    f.Check(barracks.queue.Count == 1, "Barracks production is reachable from selection actions");
                    var productionSlots = f.Wheel.Items.Select(a => a.Id).ToArray(); var direction = f.Wheel.DirectionFor(rangedId);
                    barracks.queue.Clear(); f.Refresh(); f.Frame(direction);
                    f.Check(f.Wheel.HighlightedId == rangedId && f.Wheel.Items.Select(a => a.Id).SequenceEqual(productionSlots) && !f.Wheel.Items.Any(a => a.Id == "cancel"),
                        "Emptying production preserves sector positions and the held highlight");
                    f.Frame(direction, down: true, held: true);
                    f.Check(f.Wheel.IsOpen && barracks.queue.Count == 1 && barracks.queue[0].definition == rangedId.Substring("train:".Length), "The next Trigger still trains the highlighted unit after the queue empties"); f.Dismiss();

                    f.Session.Select(supply.id, false); f.OpenSelection();
                    f.Check(f.Wheel.Page == WheelPage.Root && UnifiedWheelFixture.Field<Text>(f.Wheel, "detail").text.Contains("No actions"), "Automatic structures keep the root open and explain unavailable selection actions");
                    f.Capture(faction + "-automatic"); f.Dismiss(); supply.complete = false; f.Refresh();
                    f.OpenSelection();
                    f.Check(f.Wheel.Page == WheelPage.Root && !f.Wheel.Items.Any(a => a.Id == "cancel") && UnifiedWheelFixture.Field<Text>(f.Wheel, "detail").text.Contains("No actions"), "An unfinished building does not expose a cancellation wheel");
                    f.Dismiss(); f.CapturePanel(faction + "-construction-panel"); CancelFromPanel(f);
                    f.Check(!f.Session.Match.Entities.ContainsKey(supply.id), "An unfinished building can still be cancelled from the main panel");
                    f.Session.Select(0, false); f.OpenSelection();
                    f.Check(f.Wheel.Page == WheelPage.Root && UnifiedWheelFixture.Field<Text>(f.Wheel, "detail").text.Contains("No actions"), "An empty selection keeps root navigation without executable selection commands"); f.Capture(faction + "-no-selection"); f.Dismiss();
                    f.Session.Select(worker.id, false); f.OpenOrders(); f.Choose("hold"); f.Session.Select(other.id, false); f.Frame(Vector2.up, down: true, held: true);
                    f.Check(!f.Wheel.IsOpen && other.order == CommandKind.Stop, "External selection changes invalidate the wheel before a confirmation can affect another unit"); f.Drain();
                    f.OpenOrders(); f.Session.Match.Entities.Remove(other.id); f.Refresh(); f.Frame(Vector2.up, down: true, held: true);
                    f.Check(!f.Wheel.IsOpen, "A destroyed selected unit invalidates the wheel before the selection is pruned"); f.Drain();

                    var caster = f.Add("caster", 1100, new Point(8, 12)); f.Session.Select(caster.id, false); f.OpenSelection();
                    string ability = f.Session.Catalog[caster.definition].abilities[0]; f.Choose("ability:" + ability); f.Capture(faction + "-caster");
                    f.Frame(f.Wheel.DirectionFor(f.Wheel.HighlightedId), down: true, held: true);
                    f.Check(f.Wheel.IsOpen && f.Session.AbilityDefinitionId == ability && caster.order != CommandKind.Cast, "Caster selection actions stay open and arm one ability without casting on the wheel trigger");
                    f.Check(f.Frame(held: true) && f.Frame(up: true), "Ability confirmation owns the trigger until release"); f.Drain(); f.Session.CancelMode();
                    caster.energy = 0; f.Refresh(); f.OpenSelection(); f.Choose("ability:" + ability); f.Confirm();
                    f.Check(f.Wheel.IsOpen && f.Session.AbilityDefinitionId == null && UnifiedWheelFixture.Field<Text>(f.Wheel, "detail").text == f.Session.Message, "An unavailable ability stays in its submenu and explains the missing energy"); f.Dismiss();
                    f.OpenOrders(); f.Check(f.Wheel.Items.Any(a => a.Id == "attack-move") && !f.Wheel.Items.Any(a => a.Id.StartsWith("ability:")), "Caster orders and abilities remain in their respective root categories"); f.Dismiss();
                }
                f.Report("ACTION_WHEEL");
            }
        }

        private static void CancelFromPanel(UnifiedWheelFixture f)
        {
            f.Refresh();
            var rect = f.Panel.GetComponentsInChildren<RectTransform>().First(r => r.name == "Cancel / refund" && r.gameObject.activeInHierarchy);
            var pointer = new Ray(rect.TransformPoint(rect.rect.center) - rect.forward, rect.forward);
            f.Check(f.Panel.Point(pointer, true, false) && f.Panel.Point(pointer, false, true), "Cancel / refund remains clickable on the main panel");
            f.Refresh();
        }
    }
}
