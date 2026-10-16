using System;
using System.Collections.Generic;
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
    public static class ResourceVisualPreview
    {
        [MenuItem("Frontier/Preview resource models")]
        public static void Render()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            const string output = "TestResults/ResourceModels";
            Directory.CreateDirectory(output);
            var preview = new PreviewRenderUtility(); var fixture = new GameObject("Resource model session"); var boardObject = new GameObject("Resource model board");
            var previousBounds = Shader.GetGlobalVector("_FrontierBounds"); var previousMatrix = Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            int checks = 0, captures = 0;
            void Invoke(object target, string method) => target.GetType().GetMethod(method, flags).Invoke(target, null);
            void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Debug.Log("RESOURCE_MODEL_PASS: " + message); }
            try
            {
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Invoke(session, "Awake");
                session.LoadMap("rift"); session.StartMatch();
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; board.Viewer = preview.camera; Invoke(board, "Start"); preview.AddSingleGO(boardObject);
                var patch = session.View.resources.First(r => r.kind == "mineral"); var gas = session.View.resources.First(r => r.kind == "catalyst");
                session.View.entities = Array.Empty<VisibleEntity>(); session.View.tick = 32;
                var models = (Dictionary<int, Renderer>)typeof(BoardView).GetField("resourceRenderers", flags).GetValue(board);
                Capture("minerals", patch.position, 3.4f, new Vector3(0, 1.4f, -2));
                Check(models.Values.All(r => r.sharedMaterials.Length == 1 && r.GetComponent<Collider>() == null), "Deposits use one surface material each and do not add colliders");
                Check(models.Values.All(r => r.GetComponent<MeshFilter>().sharedMesh.vertexCount < 1300), "Resource models remain below 1300 vertices each");
                Capture("mineral-line", new Point(13, 20), 8, new Vector3(0, 1.4f, -2));
                Capture("minerals-top", patch.position, 2.7f, new Vector3(0, 2, -.01f));
                Capture("gas", gas.position, 3.2f, new Vector3(0, 1.2f, -2));
                var vapor = board.Content.Find("Gas vapor"); var vaporMesh = vapor.GetComponent<MeshFilter>().sharedMesh;
                Check(vapor.gameObject.activeSelf && vaporMesh.vertexCount > 0, "Visible uncovered gas emits vapor");
                Check(models[gas.id].localBounds.max.y < .6f, "Gas is a low open vent instead of a tall solid egg");
                var previous = vaporMesh.vertices; session.Pause("menu", true); session.Match.Step(); Invoke(board, "LateUpdate");
                Check(previous.SequenceEqual(vaporMesh.vertices), "Gas vapor freezes during pause"); session.Pause("menu", false);
                session.View.tick += 30; Invoke(board, "LateUpdate"); Check(!previous.SequenceEqual(vaporMesh.vertices), "Gas vapor moves as simulation time advances");
                Capture("gas-later", gas.position, 3.2f, new Vector3(0, 1.2f, -2));
                Capture("gas-top", gas.position, 2.5f, new Vector3(0, 2, -.01f));
                board.Width = .6f; Capture("compact-board", new Point(13, 21), 11, new Vector3(0, 1.4f, -2)); board.Width = 1.2f;
                board.Pointer = patch.position; board.HoveredResource = patch.id; Capture("mineral-hover", patch.position, 3.4f, new Vector3(0, 1.4f, -2));
                board.Pointer = gas.position; board.HoveredResource = gas.id; Capture("gas-hover", gas.position, 3.2f, new Vector3(0, 1.2f, -2)); board.Pointer = null;
                var type = session.Catalog.Role("industrial", "extractor");
                session.View.entities = new[] { new VisibleEntity { id = 900, owner = 0, definition = type.id, position = new Point(11, 24), health = type.health, complete = false, progress = .7f } };
                Invoke(board, "LateUpdate"); Check(!vapor.gameObject.activeSelf, "An extractor under construction covers the natural gas plume");
                session.View.entities[0].complete = true; Capture("gas-extractor", gas.position, 4.4f, new Vector3(0, 1.2f, -2));
                Check(!vapor.gameObject.activeSelf, "Completed extractor has no natural plume passing through its model");
                session.View.entities = Array.Empty<VisibleEntity>();
                int cell = (int)gas.position.y * session.Map.width + (int)gas.position.x;
                session.View.visibility[cell] = 1; Invoke(board, "LateUpdate"); Check(!vapor.gameObject.activeSelf, "Remembered gas does not emit live vapor through fog");
                session.View.visibility[cell] = 2; gas.remaining = 0; Invoke(board, "LateUpdate");
                Check(!models[gas.id].gameObject.activeSelf && !vapor.gameObject.activeSelf, "Depleted gas hides both vent and vapor");
                gas.remaining = 1200; Invoke(board, "LateUpdate"); board.SetVisible(false); Check(!vapor.gameObject.activeInHierarchy, "Hiding the board hides the gas effect");
                board.SetVisible(true); session.LeaveMatch(); Invoke(board, "LateUpdate"); Check(board.Content.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh.name != "Gas vapor batch" || f.sharedMesh.vertexCount == 0), "Leaving a match clears gas geometry");
                Debug.Log($"FRONTIER_RESOURCE_MODELS passed: {checks} checks, {captures} views; no player build");

                void Capture(string name, Point point, float size, Vector3 direction)
                {
                    Invoke(board, "LateUpdate");
                    var center = board.Content.TransformPoint(new Vector3(point.x, board.HeightAt(point) + .7f, point.y));
                    preview.camera.orthographic = true; preview.camera.orthographicSize = board.CellScale * size;
                    preview.camera.nearClipPlane = .001f; preview.camera.farClipPlane = 10;
                    preview.camera.transform.position = center + direction * .25f; preview.camera.transform.LookAt(center);
                    preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.025f, .04f, .06f);
                    Invoke(board, "LateUpdate"); preview.BeginStaticPreview(new Rect(0, 0, 1200, 900)); preview.Render(true);
                    var image = preview.EndStaticPreview(); File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image); captures++;
                }
            }
            finally
            {
                Object.DestroyImmediate(boardObject); Object.DestroyImmediate(fixture); preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds", previousBounds); Shader.SetGlobalMatrix("_FrontierWorldToBoard", previousMatrix);
            }
        }
    }
}
