using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Core;
using Frontier.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    public static class WorkerVisualPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);

        [MenuItem("Frontier/Preview worker activity")]
        public static void Render()
        {
            const string output = "TestResults/Workers"; Directory.CreateDirectory(output);
            var preview = new PreviewRenderUtility(); var fixture = new GameObject("Worker activity session");
            var boardObject = new GameObject("Worker activity board");
            var bounds = Shader.GetGlobalVector("_FrontierBounds"); var matrix = Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            int checks = 0, captures = 0;
            try
            {
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Invoke(session, "Awake");
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; board.Viewer = preview.camera;
                Invoke(board, "Start"); preview.AddSingleGO(boardObject);
                foreach (string faction in new[] { "industrial", "biological" })
                {
                    session.Faction = faction; session.StartMatch();
                    var type = session.Catalog.Role(faction, "worker"); var supply = session.Catalog.Role(faction, "supply");
                    var extractor = session.Catalog.Role(faction, "extractor");
                    var workers = new VisibleEntity[6];
                    for (int i = 0; i < workers.Length; i++) workers[i] = new VisibleEntity { id = 100 + i, owner = 0, definition = type.id,
                        position = new Point(10 + i * 4.8f, 20), complete = true, health = type.health, order = CommandKind.Stop };
                    workers[1].order = CommandKind.Gather; workers[1].target = 1; workers[1].working = true; workers[1].cargo = 3; workers[1].cargoKind = "mineral";
                    workers[2].order = CommandKind.Gather; workers[2].target = 2; workers[2].working = true; workers[2].cargo = 2; workers[2].cargoKind = "catalyst";
                    workers[3].order = CommandKind.Gather; workers[3].target = 2; workers[3].depositing = true; workers[3].cargo = 8; workers[3].cargoKind = "catalyst";
                    workers[4].order = CommandKind.Build; workers[4].target = 200; workers[4].working = true;
                    workers[5].order = CommandKind.Repair; workers[5].target = 201; workers[5].working = true;
                    var buildings = new[] {
                        new VisibleEntity { id = 200, owner = 0, definition = supply.id, health = supply.health * .4f, progress = .5f, position = new Point(workers[4].position.x - 1, 21), complete = false },
                        new VisibleEntity { id = 201, owner = 0, definition = supply.id, health = supply.health * .6f, progress = 1, position = new Point(workers[5].position.x - 1, 21), complete = true },
                        new VisibleEntity { id = 202, owner = 0, definition = extractor.id, health = extractor.health, progress = 1, position = new Point(workers[2].position.x - 1.5f, 21.15f), complete = true }
                    };
                    var view = new PlayerView { player = 0, tick = 20, entities = workers.Concat(buildings).ToArray(),
                        visibility = Enumerable.Repeat((byte)2, session.Map.width * session.Map.height).ToArray(), resources = new[] {
                            new ResourceState { id = 1, kind = "mineral", remaining = 1000, position = new Point(workers[1].position.x, 21.25f) },
                            new ResourceState { id = 2, kind = "catalyst", remaining = 1000, position = new Point(workers[2].position.x, 22.65f) }
                        }};
                    typeof(GameSession).GetProperty("View").SetValue(session, view);
                    var expected = new[] { WorkerTask.Idle, WorkerTask.Minerals, WorkerTask.Gas, WorkerTask.Gas, WorkerTask.Building, WorkerTask.Repairing };
                    for (int i = 0; i < workers.Length; i++)
                        Check(WorkerVisuals.Describe(workers[i], view, session.Catalog).task == expected[i], "Correct activity " + expected[i] + " / " + faction);
                    CameraAt(false); Refresh();
                    Check(ActivityMesh().vertexCount > 500, "Runtime generates tools, cargo and badges");
                    Check(board.Content.Find("Worker activity").GetComponents<Renderer>().Length == 1, "Workforce effects use a single renderer");
                    Check(board.Content.Find("Worker activity").GetComponentsInChildren<Collider>().Length == 0, "Activity never intercepts map input");
                    Capture(faction + "-activities");
                    var first = ActivityMesh().vertices;
                    view.tick += 4; Refresh();
                    Check(!first.SequenceEqual(ActivityMesh().vertices), "Animation advances between simulation ticks");
                    session.Pause("worker-preview", true); Refresh(); var frozen = ActivityMesh().vertices;
                    Refresh(); Check(frozen.SequenceEqual(ActivityMesh().vertices), "Pause freezes tools, badges and cargo"); session.Pause("worker-preview", false);
                    workers[1].working = false; workers[1].position = new Point(workers[1].position.x, 18);
                    Refresh(); int withoutRoute = ActivityMesh().vertexCount;
                    session.Selection.Add(workers[1].id); Refresh();
                    Check(ActivityMesh().vertexCount > withoutRoute, "Selected travelling worker connects to its exact mineral");
                    Capture(faction + "-assignment"); session.Selection.Clear(); workers[1].working = true; workers[1].position = new Point(workers[1].position.x, 20);
                    CameraAt(true); Refresh(); Capture(faction + "-top");
                    board.Width = .6f; CameraAt(false); Refresh(); Capture(faction + "-compact"); board.Width = 1.2f;
                    // Preview the runtime animation at a fixed camera and authoritative
                    // tick cadence, so frames can also be played as an animation strip.
                    CameraAt(false);
                    for (int frame = 0; frame < 16; frame++) { view.tick = 40 + frame; Refresh(); Capture(faction + "-frame-" + frame.ToString("00")); }
                    view.entities = buildings; Refresh(); Check(ActivityMesh().vertexCount == 0, "Removing workers clears their activity in the same frame");
                    view.entities = workers; workers[0].owner = 1; Refresh();
                    var withEnemy = ActivityMesh().vertexCount;
                    view.entities = workers.Skip(1).ToArray(); Refresh(); Check(ActivityMesh().vertexCount == withEnemy, "Enemy worker receives no private activity badge");
                    board.SetVisible(false); Check(!board.Content.gameObject.activeInHierarchy, "Board visibility hides all worker effects");
                    board.SetVisible(true); session.LeaveMatch(); Check(board.Content.Find("Worker activity").GetComponent<MeshFilter>().sharedMesh.vertexCount == 0, "Leaving a match discards worker effects");

                    // Also render a real, crowded starting mineral line. Synthetic
                    // gallery states alone cannot validate the simulation/view wiring.
                    session.StartMatch();
                    for (int tick = 0; tick < 180; tick++) session.Match.Step();
                    typeof(GameSession).GetProperty("View").SetValue(session, session.Match.View(0)); Refresh();
                    Check(session.View.entities.Any(e => e.working), "Real simulation drives active worker effects");
                    var lineCenter = board.Content.TransformPoint(new Vector3(13, .7f, 20));
                    preview.camera.orthographicSize = board.CellScale * 6;
                    preview.camera.transform.position = lineCenter + new Vector3(0, .32f, -.32f); preview.camera.transform.LookAt(lineCenter);
                    Refresh(); Capture(faction + "-mineral-line");
                    preview.camera.transform.position = lineCenter + new Vector3(0, .32f, .32f); preview.camera.transform.LookAt(lineCenter);
                    Refresh(); Capture(faction + "-opposite");
                    var effects = (WorkerVisuals)typeof(BoardView).GetField("workerVisuals", Private).GetValue(board);
                    foreach (var e in session.View.entities.Where(e => e.working))
                    {
                        var poses = (System.Collections.Generic.Dictionary<int, Transform>)typeof(BoardView).GetField("entities", Private).GetValue(board);
                        var work = WorkerVisuals.Describe(e, session.View, session.Catalog);
                        var facing = poses[e.id].localRotation * Vector3.forward;
                        var aim = new Vector3(work.target.x - e.position.x, 0, work.target.y - e.position.y).normalized;
                        Check(Vector3.Dot(facing, aim) > .99f, "Newly visible working model faces its exact deposit");
                    }
                    Check(effects.AnimationTime >= session.View.tick / (float)Simulation.TickRate, "Animation clock follows the new match");
                    session.LeaveMatch();

                    void CameraAt(bool top)
                    {
                        // Set the content transform before deriving camera coordinates.
                        Refresh(); var center = board.Content.TransformPoint(new Vector3(22, .7f, 21));
                        preview.camera.orthographic = true; preview.camera.orthographicSize = board.CellScale * 7.5f;
                        preview.camera.nearClipPlane = .001f; preview.camera.farClipPlane = 10;
                        preview.camera.transform.position = center + (top ? new Vector3(0, .7f, -.001f) : new Vector3(0, .36f, -.48f));
                        preview.camera.transform.LookAt(center);
                        preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.025f, .04f, .06f);
                    }
                }
                Debug.Log($"FRONTIER_WORKER_VISUALS passed: {checks} checks, {captures} views; no player build");
                void Refresh() => Invoke(board, "LateUpdate");
                Mesh ActivityMesh() => board.Content.Find("Worker activity").GetComponent<MeshFilter>().sharedMesh;
                void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Debug.Log("WORKER_PASS: " + message); }
                void Capture(string name)
                {
                    preview.BeginStaticPreview(new Rect(0, 0, 1800, 800)); preview.Render(true);
                    var image = preview.EndStaticPreview(); File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image); captures++;
                }
            }
            finally
            {
                Object.DestroyImmediate(boardObject); Object.DestroyImmediate(fixture); preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds", bounds); Shader.SetGlobalMatrix("_FrontierWorldToBoard", matrix);
            }
        }
    }
}
