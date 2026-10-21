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
    public static class ConstructionPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        [MenuItem("Frontier/Check construction feedback")]
        public static void Run()
        {
            Render();
            HarvestingMarkersPreview.Render();
            ActionWheelPreview.Render();
        }

        public static void Render()
        {
            const string output = "TestResults/Construction";
            var preview = new PreviewRenderUtility();
            var fixture = new GameObject("Construction checks");
            var boardObject = new GameObject("Construction board");
            var console = new GameObject("Construction console");
            var bounds = Shader.GetGlobalVector("_FrontierBounds");
            var matrix = Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            int checks = 0;
            try
            {
                Directory.CreateDirectory(output);
                var session = fixture.AddComponent<GameSession>();
                if (session.Catalog == null) Invoke(session, "Awake");
                session.LoadMap("training");
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; board.Viewer = preview.camera;
                Invoke(board, "Start"); preview.AddSingleGO(boardObject);
                var quest = fixture.AddComponent<QuestBridge>(); quest.Session = session; quest.Board = board;
                typeof(QuestBridge).GetProperty("Camera").SetValue(quest, preview.camera);
                var panel = console.AddComponent<CommandPanel>(); panel.Session = session; panel.Board = board; panel.Quest = quest;
                Invoke(panel, "Start"); preview.AddSingleGO(console);
                foreach (string faction in new[] { "industrial", "biological" })
                {
                    session.Faction = faction; session.StartMatch(); session.Match.Players[0].minerals = 5000;
                    var worker = session.Match.Entities.Values.First(e => e.owner == 0 && session.Catalog[e.definition].role == "worker");
                    session.Select(worker.id, false); RefreshPanel();
                    var barracks = session.Catalog.Role(faction, "barracks");
                    var supply = session.Catalog.Role(faction, "supply");
                    int minerals = session.Match.Players[0].minerals;
                    Click("Build " + barracks.name);
                    Check(session.BuildDefinition == barracks.id && SelectedButtons() == 1 && Selected(barracks.name), "Barracks stays visibly selected " + faction);
                    Check(Field<Text>(panel, "actionHeading").text == "PLACING · " + barracks.name && session.Message.Contains(barracks.name), "Placement names the exact building " + faction);
                    Check(session.Match.Players[0].minerals == minerals, "Selecting a blueprint does not spend resources " + faction);
                    panel.Point(new Ray(Vector3.one * 100, Vector3.up), false, false);
                    CapturePanel(faction + "-barracks-selected");
                    Click("Build " + supply.name);
                    Check(SelectedButtons() == 1 && Selected(supply.name) && !Selected(barracks.name), "Switching blueprint moves the persistent highlight " + faction);
                    CapturePanel(faction + "-supply-selected");
                    // The longest name and selected mineral/gas cost must fit next to its model.
                    Click("Build " + session.Catalog.Role(faction, "academy").name);
                    CapturePanel(faction + "-academy-selected");
                    Click("Build " + supply.name);
                    var blocker = session.Match.Entities.Values.First(e => e.owner == 0 && !session.Catalog[e.definition].building && e.id != worker.id);
                    blocker.position = new Point(20.5f, 14.5f); blocker.order = CommandKind.Hold;
                    Check(session.Context(new Point(20, 14), 0, 0) > 0, "Selected blueprint places through the normal command path " + faction);
                    var foundation = session.Match.Entities.Values.Single(e => e.definition == supply.id && !e.complete);
                    Check(foundation.awaitingClearance && session.Match.View(0).entities.First(e => e.id == foundation.id).awaitingClearance,
                        "Runtime placement accepts own obstruction and publishes clearance " + faction);
                    for (int tick = 0; tick < 300 && foundation.awaitingClearance; tick++) session.Match.Step();
                    Check(!foundation.awaitingClearance && blocker.order == CommandKind.Hold && session.Match.Map.CanStand(blocker.position, session.Catalog[blocker.definition].collisionRadius),
                        "Runtime obstruction walks clear and keeps its order " + faction);
                    RefreshPanel();
                    Check(session.BuildDefinition == null && SelectedButtons() == 0 && session.Message == supply.name + " construction ordered.", "Successful placement clears the highlight and placement prompt " + faction);
                    SelectionActions.For(session).Items.First(a => a.Id == "build:" + barracks.id).Execute(); RefreshPanel();
                    Check(Selected(barracks.name), "Shared wheel action also updates the console " + faction);
                    session.CancelMode(); RefreshPanel();
                    Check(SelectedButtons() == 0 && !Field<Text>(panel, "actionHeading").text.StartsWith("PLACING") && session.Message == "Construction placement cancelled.", "Cancelling clears the selected blueprint and placement prompt " + faction);

                    // Three real runtime models show progress and the completed color transition.
                    console.SetActive(false); session.Selection.Clear();
                    var type = session.Catalog.Role(faction, "hq");
                    var actors = new[] {
                        new VisibleEntity { id = 1000, owner = 0, definition = type.id, position = new Point(10, 28), progress = .2f, health = type.health * .32f },
                        new VisibleEntity { id = 1001, owner = 0, definition = type.id, position = new Point(23, 28), progress = .65f, health = type.health * .7f },
                        new VisibleEntity { id = 1002, owner = 0, definition = type.id, position = new Point(36, 28), progress = 1, complete = true, health = type.health }
                    };
                    Publish(); CameraAt(); Invoke(board, "LateUpdate");
                    var visuals = Field<Dictionary<int, Transform>>(board, "entities");
                    var readyMaterials = Model(1002).sharedMaterials;
                    Check(Model(1000).sharedMaterials.All(m => Gray(m.GetColor("_BaseColor"))) && Model(1001).sharedMaterials.All(m => Gray(m.GetColor("_BaseColor"))), "Unfinished structures have no team or role color " + faction);
                    Check(readyMaterials.Any(m => !Gray(m.GetColor("_BaseColor"))), "Finished structures retain their normal colors " + faction);
                    Check(Marker(1000) != null && Marker(1001) != null && Marker(1002) == null, "Only unfinished buildings have construction labels " + faction);
                    Check(Marker(1000).Find("Construction percent").GetComponent<Text>().text == "20%" && Marker(1001).Find("Construction percent").GetComponent<Text>().text == "65%", "Labels display actual progress " + faction);
                    Check(Marker(1000).Find("Availability").GetComponent<Text>().text == "NO RESOURCE DELIVERY YET", "Unfinished base explains why workers cannot deposit " + faction);
                    CaptureBoard(faction + "-construction-stages");
                    actors[0].progress = 0; actors[0].awaitingClearance = true; Publish(); Invoke(board, "LateUpdate");
                    Check(Marker(1000).Find("Construction status").GetComponent<Text>().text == "CLEARING AREA", "Reserved site explains why construction is waiting " + faction);
                    CaptureBoard(faction + "-clearing-area");
                    actors[0].awaitingClearance = false; Publish(); Invoke(board, "LateUpdate");
                    Check(Marker(1000).Find("Construction status").GetComponent<Text>().text == "UNDER CONSTRUCTION", "Cleared site restores normal construction label " + faction);
                    actors[0].progress = .999f; Publish(); Invoke(board, "LateUpdate");
                    Check(Marker(1000).Find("Construction percent").GetComponent<Text>().text == "99%", "Incomplete building never claims 100 percent " + faction);
                    actors[0].complete = true; actors[0].progress = 1; Publish(); Invoke(board, "LateUpdate");
                    Check(Marker(1000) == null && Model(1000).sharedMaterials.SequenceEqual(readyMaterials), "Completion removes warning and restores colors immediately " + faction);
                    actors[1].remembered = true; Publish(); Invoke(board, "LateUpdate");
                    Check(Marker(1001) == null, "Remembered structures do not show live construction progress " + faction);
                    actors[1].remembered = false; Publish(); Invoke(board, "LateUpdate");
                    Check(Marker(1001) != null && Model(1001).sharedMaterials.All(m => Gray(m.GetColor("_BaseColor"))), "Revealed construction restores its gray state " + faction);
                    actors[1].owner = 1; Publish(); Invoke(board, "LateUpdate");
                    Check(Marker(1001) == null && Model(1001).sharedMaterials.All(m => Gray(m.GetColor("_BaseColor"))), "Enemy construction stays gray without private own-base labels " + faction);
                    actors[1].owner = 0; Publish(); Invoke(board, "LateUpdate");
                    session.Selection.Add(1001); console.SetActive(true); RefreshPanel();
                    Check(Field<Text>(panel, "selectionDetails").text.Contains("Cannot receive resources yet"), "Selected unfinished base explains delivery state in console " + faction);
                    CapturePanel(faction + "-unfinished-base");
                    session.LeaveMatch();
                    Check(!board.Content.GetComponentsInChildren<Text>().Any(t => t.text == "UNDER CONSTRUCTION"), "Leaving match removes construction labels " + faction);

                    Renderer Model(int id) => visuals[id].Find("Building silhouette").GetComponent<Renderer>();
                    Transform Marker(int id) => board.Content.Find("Harvesting counters/Construction " + id);
                    void Publish()
                    {
                        typeof(GameSession).GetProperty("View").SetValue(session, new PlayerView { player = 0, faction = faction, entities = actors,
                            tick = 100, visibility = Enumerable.Repeat((byte)2, session.Map.width * session.Map.height).ToArray() });
                    }
                }
                Debug.Log($"FRONTIER_CONSTRUCTION passed: {checks} checks; construction stages, completion/fog/cleanup and blueprint selection for both factions; no physical Quest test");

                void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Debug.Log("CONSTRUCTION_PASS: " + message); }
                bool Gray(Color c) => Mathf.Abs(c.r - c.g) < .001f && Mathf.Abs(c.g - c.b) < .001f;
                void RefreshPanel() { typeof(CommandPanel).GetField("nextText", Private).SetValue(panel, 0f); Invoke(panel, "LateUpdate"); }
                RectTransform Button(string name) => Field<RectTransform>(panel, "actions").GetComponentsInChildren<RectTransform>().First(t => t.name == name);
                bool Selected(string name) => Button("Build " + name).GetComponentsInChildren<Text>().Any(t => t.text.StartsWith("SELECTED ·"));
                int SelectedButtons() => Field<RectTransform>(panel, "actions").GetComponentsInChildren<Text>().Count(t => t.text.StartsWith("SELECTED ·"));
                void Click(string name)
                {
                    RefreshPanel(); var rect = Button(name); var hit=rect.GetComponentInChildren<SelectionPortrait>()?.rectTransform??rect;
                    var center = hit.TransformPoint(hit.rect.center); var ray = new Ray(center - rect.forward, rect.forward);
                    Check(panel.Point(ray, true, false) && panel.Point(ray, false, true), "Real menu click hits " + name); RefreshPanel();
                }
                void CapturePanel(string name) { RefreshPanel(); ProductionQueuePreview.CheckActionModels(panel,session,Check); boardObject.SetActive(false); HarvestingPreview.SavePanel(panel, output + "/" + name + ".png", preview); boardObject.SetActive(true); }
                void CameraAt()
                {
                    preview.camera.orthographic = true; preview.camera.orthographicSize = .24f; preview.camera.nearClipPlane = .001f; preview.camera.farClipPlane = 10;
                    preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.025f, .04f, .06f);
                    var focus = board.Content.TransformPoint(new Vector3(25, 1.2f, 30));
                    preview.camera.transform.position = focus + new Vector3(0, .42f, -.58f); preview.camera.transform.LookAt(focus);
                }
                void CaptureBoard(string name)
                {
                    CameraAt(); Invoke(board, "LateUpdate");
                    var meshes = new List<Mesh>(); var materials = new List<Material>();
                    preview.BeginStaticPreview(new Rect(0, 0, 1800, 1000)); Canvas.ForceUpdateCanvases();
                    // Preview cameras need explicit Canvas meshes, including world-space status labels.
                    foreach (var graphic in board.Content.GetComponentsInChildren<Graphic>())
                    {
                        graphic.Rebuild(CanvasUpdate.PreRender); var source = graphic.canvasRenderer.GetMesh(); if (source == null) continue;
                        var mesh = Object.Instantiate(source); meshes.Add(mesh); if (mesh.vertexCount == 0) continue;
                        // Billboard children have different distances to the preview camera.
                        // Preserve Canvas hierarchy order instead of transparent distance sorting.
                        var material = new Material(graphic.materialForRendering) { mainTexture = graphic.mainTexture, renderQueue = 3000 + meshes.Count }; materials.Add(material);
                        if (graphic is Text) material.SetVector("_TextureSampleAdd", new Vector4(1, 1, 1, 0));
                        var layer = Matrix4x4.Translate(-preview.camera.transform.forward * (.00001f * meshes.Count));
                        preview.DrawMesh(mesh, layer * graphic.transform.localToWorldMatrix, material, 0);
                    }
                    preview.Render(true); var image = preview.EndStaticPreview(); File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG()); Object.DestroyImmediate(image);
                    foreach (var mesh in meshes) Object.DestroyImmediate(mesh); foreach (var material in materials) Object.DestroyImmediate(material);
                }
            }
            finally
            {
                Object.DestroyImmediate(console); Object.DestroyImmediate(boardObject); Object.DestroyImmediate(fixture); preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds", bounds); Shader.SetGlobalMatrix("_FrontierWorldToBoard", matrix);
            }
        }
    }
}
