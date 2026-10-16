using System;
using System.Collections.Generic;
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
    public static class HarvestingMarkersPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
        [MenuItem("Frontier/Preview workforce counters")]
        public static void Render()
        {
            Directory.CreateDirectory("TestResults/Workforce");
            var preview = new PreviewRenderUtility(); var fixture = new GameObject("Workforce session"); var boardObject = new GameObject("Workforce board");
            var previousBounds = Shader.GetGlobalVector("_FrontierBounds"); var previousMatrix = Shader.GetGlobalMatrix("_FrontierWorldToBoard"); int checks = 0, captures = 0;
            try
            {
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Invoke(session, "Awake");
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; board.Viewer = preview.camera; Invoke(board, "Start"); preview.AddSingleGO(boardObject);
                foreach (string faction in new[] { "industrial", "biological" })
                {
                    session.Faction = faction; session.LoadMap("rift"); session.StartMatch(); board.Width = 1.2f; board.transform.rotation = Quaternion.identity;
                    var hq = session.Match.Entities.Values.First(e => e.owner == 0 && session.Catalog[e.definition].role == "hq");
                    var mineral = session.Match.Resources.First(r => r.kind == "mineral"); var gas = session.Match.Resources.First(r => r.kind == "catalyst");
                    var workerType = session.Catalog.Role(faction, "worker");
                    for (int i = 0; i < 7; i++) session.Match.Entities.Add(1000 + i, new EntityState { id = 1000 + i, owner = 0, definition = workerType.id, health = workerType.health, position = new Point(8 + i, 18) });
                    var workers = session.Match.Entities.Values.Where(e => e.owner == 0 && session.Catalog[e.definition].role == "worker").ToArray();
                    CameraAt(0); Refresh();
                    Check(Value(hq.id) == "6/8", "Unselected base shows the six starting workers and capacity across four mineral patches");
                    Check(Labels().Length == 1, "Unbuilt gas and enemy bases do not show workforce counters");
                    session.Selection.Clear(); foreach (var worker in workers) session.Selection.Add(worker.id); session.Send(CommandKind.Stop); Refresh();
                    Check(Value(hq.id) == "0/8", "Stopping all workers clears the assigned count");
                    Assign(workers.Take(5), mineral.id); Check(Value(hq.id) == "5/8" && Count(hq.id).color == Color.white, "Five mineral workers produce a normal 5/8 counter"); Capture(faction + "-5-of-8");
                    Assign(workers.Skip(5).Take(4), mineral.id);
                    Check(Value(hq.id) == "9/8" && Count(hq.id).color == HarvestingMarkers.ExcessColor, "Nine mineral workers turn 9/8 red");
                    Check(workers.Take(9).All(e => e.order == CommandKind.Gather && e.position.Distance(mineral.position) > 1.5f), "Travelling workers are counted before they reach the patches");
                    workers[0].cargo = 5; Assign(workers.Take(1), mineral.id); Check(Value(hq.id) == "9/8", "Delivering cargo does not remove a worker from the counter");
                    var extractorType = session.Catalog.Role(faction, "extractor");
                    var extractor = new EntityState { id = 1100, owner = 0, definition = extractorType.id, health = extractorType.health, position = new Point(11, 24), complete = false };
                    session.Match.Entities.Add(extractor.id, extractor); session.Match.Map.Occupy(extractor.position, extractorType.footprint, extractor.id); Refresh();
                    Check(Labels().Length == 1, "Gas counter waits for a completed extractor");
                    extractor.complete = true; Refresh(); Check(Value(extractor.id) == "0/3", "Completed gas extractor shows 0/3 without selection");
                    Assign(workers.Skip(9).Take(3), gas.id); Check(Value(extractor.id) == "3/3" && Count(extractor.id).color == Color.white, "Optimal gas staffing stays normal");
                    Assign(workers.Skip(12).Take(1), gas.id); Check(Value(extractor.id) == "4/3" && Count(extractor.id).color == HarvestingMarkers.ExcessColor, "Excess gas staffing turns 4/3 red");
                    session.Selection.Clear(); Refresh(); Check(Value(hq.id) == "9/8" && Value(extractor.id) == "4/3", "Both counters stay visible without selected units or buildings"); Capture(faction + "-excess");
                    foreach (int side in new[] { 1, 2, 3 })
                    {
                        CameraAt(side); Refresh();
                        Check(Labels().All(r => Vector3.Dot(r.forward, (r.position - preview.camera.transform.position).normalized) > .99f), "Counters face the viewer from side " + side);
                        Check(!ScreenRect(Label(hq.id)).Overlaps(ScreenRect(Label(extractor.id))), "Mineral and gas counters do not overlap from side " + side);
                    }
                    Capture(faction + "-opposite-side"); CameraAt(0);
                    board.Width = .6f; Refresh(); CameraAt(0); Refresh();
                    Check(Mathf.Abs(Label(hq.id).lossyScale.x - .00032f) < .000001f, "Compact board preserves readable physical text size");
                    Check(!ScreenRect(Label(hq.id)).Overlaps(ScreenRect(Label(extractor.id))), "Compact board separates nearby counters"); Capture(faction + "-compact");
                    board.Width = 1.2f; Refresh(); CameraAt(0); Refresh();
                    session.Selection.Clear(); foreach (var worker in workers.Skip(5).Take(4)) session.Selection.Add(worker.id); session.Send(CommandKind.Stop); Refresh();
                    Check(Value(hq.id) == "5/8" && Count(hq.id).color == Color.white, "Moving workers off mining updates totals and clears the red color");
                    mineral.remaining = 0; Refresh(); Check(Value(hq.id).EndsWith("/6"), "Exhausting one patch reduces the optimal mineral capacity by two");
                    session.Pause("tracking", true); var before = Value(hq.id); session.Match.Step(); Refresh(); Check(Value(hq.id) == before, "Paused match retains its accurate staffing counter"); session.Pause("tracking", false);
                    board.Zoom = 4; board.Pan = new Vector2(30, 30); Refresh(); Check(!Label(hq.id, true).gameObject.activeInHierarchy, "Counters hide when their building is clipped out of the map view"); board.Fit(); Refresh();
                    Check(Labels().SelectMany(r => r.GetComponentsInChildren<Graphic>()).All(g => !g.raycastTarget), "Floating counters never intercept pointer input");
                    session.Match.Entities.Remove(extractor.id); session.Match.Map.Occupy(extractor.position, extractorType.footprint, 0); Refresh(); Check(Labels().Length == 1, "Destroyed extractor loses its gas counter");
                    board.SetVisible(false); Check(Labels().Length == 0, "Hiding the board hides all counters"); board.SetVisible(true); Refresh();
                    Check(Value(hq.id).EndsWith("/6"), "Showing the board restores its counter"); session.LeaveMatch(); Check(Labels().Length == 0, "Leaving the match removes all counters");

                    void Assign(IEnumerable<EntityState> group, int resource)
                    {
                        session.Selection.Clear(); foreach (var worker in group) session.Selection.Add(worker.id);
                        Check(session.Send(CommandKind.Gather, target: resource), "Real gather command accepted"); Refresh();
                    }
                    void CameraAt(int side)
                    {
                        var local = new Vector3((13 - session.Map.width * .5f) * board.CellScale, 0, (20 - session.Map.height * .5f) * board.CellScale);
                        var center = board.transform.TransformPoint(local); float size = Mathf.Max(.12f, board.CellScale * 11);
                        preview.camera.orthographic = true; preview.camera.orthographicSize = size; preview.camera.nearClipPlane = .001f; preview.camera.farClipPlane = 10;
                        preview.camera.transform.position = center + Vector3.up * .3f + Quaternion.Euler(0, side * 90, 0) * new Vector3(0, 0, -.38f);
                        preview.camera.transform.LookAt(center); preview.camera.aspect = 1.5f;
                        preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.025f, .04f, .06f);
                    }
                }
                Debug.Log($"FRONTIER_WORKFORCE_MARKERS passed: {checks} checks, {captures} views; no player build");
                void Refresh() { if (session.Running) typeof(GameSession).GetProperty("View").SetValue(session, session.Match.View(0)); Invoke(board, "LateUpdate"); }
                RectTransform[] Labels(bool inactive = false) => board.GetComponentsInChildren<Canvas>(inactive).Where(c => c.name.Contains(" workers ")).Select(c => (RectTransform)c.transform).ToArray();
                RectTransform Label(int id, bool inactive = false) => Labels(inactive).Single(r => r.name.EndsWith(" " + id));
                Text Count(int id) => Label(id).GetComponentsInChildren<Text>().Single(t => t.name == "Assigned / optimal");
                string Value(int id) => Count(id).text;
                Rect ScreenRect(RectTransform rect)
                {
                    var corners = new Vector3[4]; rect.GetWorldCorners(corners); var points = corners.Select(p => preview.camera.WorldToViewportPoint(p)).ToArray();
                    return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
                }
                void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Debug.Log("WORKFORCE_PASS: " + message); }
                void Capture(string name)
                {
                    var meshes = new List<Mesh>(); var materials = new List<Material>();
                    preview.BeginStaticPreview(new Rect(0, 0, 1500, 1000)); Canvas.ForceUpdateCanvases();
                    foreach (var label in board.GetComponentsInChildren<Text>()) Check(label.preferredHeight <= label.rectTransform.rect.height + .5f, "Counter text fits: " + label.text);
                    foreach (var graphic in board.GetComponentsInChildren<Graphic>())
                    {
                        graphic.Rebuild(CanvasUpdate.PreRender); var source = graphic.canvasRenderer.GetMesh(); if (source == null || source.vertexCount == 0) continue;
                        var mesh = Object.Instantiate(source); meshes.Add(mesh); var material = new Material(graphic.materialForRendering) { mainTexture = graphic.mainTexture }; materials.Add(material);
                        // Preview meshes do not inherit Canvas sibling ordering. Preserve it
                        // explicitly so a tilted background cannot sort in front of its text.
                        material.renderQueue = 3000 + meshes.Count;
                        if (graphic is Text) material.SetVector("_TextureSampleAdd", new Vector4(1, 1, 1, 0));
                        preview.DrawMesh(mesh, Matrix4x4.Translate(-preview.camera.transform.forward * .00002f * meshes.Count) * graphic.transform.localToWorldMatrix, material, 0);
                    }
                    preview.Render(true); var image = preview.EndStaticPreview(); File.WriteAllBytes("TestResults/Workforce/" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
                    foreach (var mesh in meshes) Object.DestroyImmediate(mesh); foreach (var material in materials) Object.DestroyImmediate(material); captures++;
                }
            }
            finally { Object.DestroyImmediate(boardObject); Object.DestroyImmediate(fixture); preview.Cleanup(); Shader.SetGlobalVector("_FrontierBounds", previousBounds); Shader.SetGlobalMatrix("_FrontierWorldToBoard", previousMatrix); }
        }
    }
}
