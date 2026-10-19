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
                    f.Check(f.Wheel.Items.Any(a => a.Id == "build-menu") && !f.Wheel.Items.Any(a => a.Id == "hold"), "Worker selection page separates construction from unit orders");
                    f.Enter("build-menu");
                    f.Check(f.Wheel.InBuildMenu && f.Wheel.Items.Count(a => a.Building) == 7, "Build submenu offers all seven faction buildings");
                    f.Choose("build:" + f.Session.Catalog.Role(faction, "barracks").id); f.Capture(faction + "-build");
                    f.Enter("back"); f.Check(f.Wheel.Page == WheelPage.Selection, "Build returns to selection actions");
                    f.Enter("build-menu"); f.Choose("build:" + f.Session.Catalog.Role(faction, "supply").id);
                    int buildings = f.Session.Match.Buildings(0);
                    f.Frame(f.Wheel.DirectionFor(f.Wheel.HighlightedId), down: true, held: true);
                    f.Check(!f.Wheel.IsOpen && f.Session.BuildDefinition == f.Session.Catalog.Role(faction, "supply").id && f.Session.Match.Buildings(0) == buildings, "Trigger arms construction without placing a building underneath the wheel");
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
                    int minerals = f.Session.View.minerals; f.Confirm(); f.Drain();
                    f.Check(hq.queue.Count == 1 && f.Session.View.minerals == minerals - f.Session.Catalog[worker.definition].minerals, "Training spends the real unit cost and creates one production order");
                    f.OpenSelection(); f.Enter("cancel"); f.Drain();
                    f.Check(hq.queue.Count == 0 && f.Session.View.minerals == minerals, "Cancel refunds the real queued order");
                    f.Session.Match.Players[0].minerals = 0; f.Refresh(); f.OpenSelection(); f.Choose("train:" + worker.definition); f.Confirm();
                    f.Check(f.Wheel.IsOpen && f.Wheel.Page == WheelPage.Selection && hq.queue.Count == 0 && UnifiedWheelFixture.Field<Text>(f.Wheel, "detail").text == f.Session.Message, "Rejected production keeps its submenu and displays the reason");
                    f.Capture(faction + "-not-enough-resources");
                    f.Session.Match.Players[0].minerals = 5000; f.Refresh(); f.Choose("train:" + worker.definition); f.Confirm(); f.Drain();
                    f.Check(hq.queue.Count == 1, "Production can be retried after resources recover without reopening another menu");
                    f.Session.Select(tech.id, false); f.OpenSelection(); f.Choose("research:" + f.Session.Catalog.Definition.upgrades[0].id); f.Capture(faction + "-research"); f.Confirm(); f.Drain();
                    f.Check(tech.queue.Count == 1 && tech.queue[0].research, "Research uses the shared command path");
                    f.Session.Select(barracks.id, false); f.OpenSelection(); f.Enter("train:" + f.Session.Catalog.Role(faction, "ranged").id); f.Drain();
                    f.Check(barracks.queue.Count == 1, "Barracks production is reachable from selection actions");
                    f.OpenSelection(); f.Choose("cancel"); var staleDirection = f.Wheel.DirectionFor("cancel"); barracks.queue.Clear(); f.Refresh(); f.Frame(staleDirection, down: true, held: true);
                    f.Check(f.Wheel.IsOpen && f.Wheel.Highlighted == -1 && !f.Wheel.Items.Any(a => a.Id == "cancel"), "Queue changes clear stale highlights instead of executing a remapped sector"); f.Dismiss();

                    f.Session.Select(supply.id, false); f.OpenSelection();
                    f.Check(f.Wheel.Page == WheelPage.Root && UnifiedWheelFixture.Field<Text>(f.Wheel, "detail").text.Contains("No actions"), "Automatic structures keep the root open and explain unavailable selection actions");
                    f.Capture(faction + "-automatic"); f.Dismiss(); supply.complete = false; f.Refresh();
                    f.OpenSelection(); f.Choose("cancel"); f.Capture(faction + "-construction"); f.Confirm(); f.Drain();
                    f.Check(!f.Session.Match.Entities.ContainsKey(supply.id), "An unfinished building can be cancelled through selection actions");
                    f.Session.Select(0, false); f.OpenSelection();
                    f.Check(f.Wheel.Page == WheelPage.Root && UnifiedWheelFixture.Field<Text>(f.Wheel, "detail").text.Contains("No actions"), "An empty selection keeps root navigation without executable selection commands"); f.Capture(faction + "-no-selection"); f.Dismiss();
                    f.Session.Select(worker.id, false); f.OpenOrders(); f.Choose("hold"); f.Session.Select(other.id, false); f.Frame(Vector2.up, down: true, held: true);
                    f.Check(!f.Wheel.IsOpen && other.order == CommandKind.Stop, "External selection changes invalidate the wheel before a confirmation can affect another unit"); f.Drain();
                    f.OpenOrders(); f.Session.Match.Entities.Remove(other.id); f.Refresh(); f.Frame(Vector2.up, down: true, held: true);
                    f.Check(!f.Wheel.IsOpen, "A destroyed selected unit invalidates the wheel before the selection is pruned"); f.Drain();

                    var caster = f.Add("caster", 1100, new Point(8, 12)); f.Session.Select(caster.id, false); f.OpenSelection();
                    string ability = f.Session.Catalog[caster.definition].abilities[0]; f.Choose("ability:" + ability); f.Capture(faction + "-caster");
                    f.Frame(f.Wheel.DirectionFor(f.Wheel.HighlightedId), down: true, held: true);
                    f.Check(!f.Wheel.IsOpen && f.Session.AbilityDefinitionId == ability && caster.order != CommandKind.Cast, "Caster selection actions arm one ability without casting on the wheel trigger");
                    f.Check(f.Frame(held: true) && f.Frame(up: true), "Ability confirmation owns the trigger until release"); f.Drain(); f.Session.CancelMode();
                    caster.energy = 0; f.Refresh(); f.OpenSelection(); f.Choose("ability:" + ability); f.Confirm();
                    f.Check(f.Wheel.IsOpen && f.Session.AbilityDefinitionId == null && UnifiedWheelFixture.Field<Text>(f.Wheel, "detail").text == f.Session.Message, "An unavailable ability stays in its submenu and explains the missing energy"); f.Dismiss();
                    f.OpenOrders(); f.Check(f.Wheel.Items.Any(a => a.Id == "attack-move") && !f.Wheel.Items.Any(a => a.Id.StartsWith("ability:")), "Caster orders and abilities remain in their respective root categories"); f.Dismiss();
                }
                f.Report("ACTION_WHEEL");
            }
        }
    }
}
