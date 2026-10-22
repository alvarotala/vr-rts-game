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
    public static class LightingPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        [MenuItem("Frontier/Validate and preview experimental lighting")]
        public static void Render()
        {
            const string output = "TestResults/Lighting"; const int size = 1000;
            Directory.CreateDirectory(output);
            var preview = new PreviewRenderUtility(); var fixture = new GameObject("Lighting session");
            var boardObject = new GameObject("Lighting board");
            var oldBounds = Shader.GetGlobalVector("_FrontierBounds"); var oldMatrix = Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            float oldLighting = Shader.GetGlobalFloat("_FrontierEnhancedLighting"); var oldLightColor = Shader.GetGlobalColor("_FrontierLightColor");
            bool hadLighting = PlayerPrefs.HasKey(BoardView.LightingKey), hadFog = PlayerPrefs.HasKey(BoardView.FogDensityKey);
            int savedLighting = PlayerPrefs.GetInt(BoardView.LightingKey); float savedFog = PlayerPrefs.GetFloat(BoardView.FogDensityKey, 1);
            int checks = 0, captures = 0;
            try
            {
                PlayerPrefs.DeleteKey(BoardView.LightingKey);
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Call(session, "Awake");
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; board.Viewer = preview.camera; board.Width = 1.6f;
                Call(board, "Start"); preview.AddSingleGO(boardObject);
                Check(!board.EnhancedLighting && Shader.GetGlobalFloat("_FrontierEnhancedLighting") == 0,
                    "Fresh preferences retain Current lighting");
                Check(Field<ContactShadows>(board, "contactShadows") == null, "Current lighting does not allocate contact-shadow geometry");
                foreach (string shaderName in new[] { "Frontier/Board", "Frontier/ContactShadows" })
                {
                    var shader = Shader.Find(shaderName);
                    Check(shader != null && !ShaderUtil.ShaderHasError(shader), shaderName + ": no reported shader compilation errors");
                }
                foreach (string id in new[] { "training", "rift", "desert" })
                {
                    session.LoadMap(id); session.StartMatch(); session.Pause("lighting-preview", true); board.SetFogDensity(0);
                    // Editor-only survey of an actual starting match. Runtime lighting
                    // must continue to consume filtered PlayerView, never Match.Entities.
                    var survey = new PlayerView { player = 0, faction = session.Faction, tick = session.Match.Tick,
                        visibility = Enumerable.Repeat((byte)2, session.Map.width * session.Map.height).ToArray(),
                        entities = session.Match.Entities.Values.Select(e => new VisibleEntity { id = e.id, owner = e.owner,
                            definition = e.definition, position = e.position, health = e.health, complete = e.complete, order = CommandKind.Stop }).ToArray(),
                        resources = session.Match.Resources.Select(r => new ResourceState { id = r.id, definition = r.definition,
                            kind = r.kind, remaining = r.remaining, position = r.position }).ToArray() };
                    SetView(survey); board.Rebuild(); Refresh();
                    string snapshot = JsonUtility.ToJson(session.Match.Snapshot());
                    Check(survey.entities.Any(e => session.Catalog[e.definition].faction == "industrial")
                        && survey.entities.Any(e => session.Catalog[e.definition].faction == "biological"), id + ": real match includes both material families");
                    Compare(id + "-overview", new Point(session.Map.width * .5f, session.Map.height * .5f), session.Map.width * .58f);
                    foreach (int player in new[] { 0, 1 })
                    {
                        var hq = survey.entities.First(e => e.owner == player && session.Catalog[e.definition].role == "hq");
                        float half = session.Catalog[hq.definition].footprint * .5f;
                        Compare(id + "-" + session.Catalog[hq.definition].faction + "-detail",
                            new Point(hq.position.x + half + 2, hq.position.y + half + 3), 9);
                    }
                    CheckStateAndPersistence();
                    CheckShadows();
                    Check(JsonUtility.ToJson(session.Match.Snapshot()) == snapshot, id + ": previews and lighting switches preserve authoritative state");
                    session.LeaveMatch(); Refresh();
                    var afterMatch = Field<ContactShadows>(board, "contactShadows");
                    Check(afterMatch == null || !afterMatch.Root.gameObject.activeSelf, id + ": closing a match removes contact shadows");

                    void Compare(string name, Point center, float radius)
                    {
                        board.SetEnhancedLighting(false); CameraAt(center, radius); Refresh(); var current = Snap(name + "-current");
                        board.SetEnhancedLighting(true);
                        Check(Field<ContactShadows>(board, "contactShadows").DynamicCasterCount > 0, name + ": activation produces shadows immediately");
                        Refresh(); var enhanced = Snap(name + "-enhanced");
                        double difference = Difference(current, enhanced);
                        Check(difference > .001, name + ": enhanced lighting changes rendered pixels; difference=" + difference.ToString("0.0000"));
                        board.SetEnhancedLighting(false); Refresh(); var restored = Snap();
                        Check(Difference(current, restored) < .0002, name + ": returning to Current restores the original image");
                        Check(!Field<ContactShadows>(board, "contactShadows").Root.gameObject.activeSelf, name + ": Current hides all contact shadows");
                    }
                    void CheckStateAndPersistence()
                    {
                        int worker = survey.entities.First(e => e.owner == 0 && session.Catalog[e.definition].role == "worker").id;
                        session.Selection.Add(worker); session.ChooseBuild(session.Catalog.Role(session.Faction, "supply").id);
                        board.Placed = true; board.Zoom = 1.15f; board.Pan = new Vector2(.4f, -.2f); board.SetFogDensity(.5f);
                        var pose = board.transform.localToWorldMatrix; var selection = session.Selection.ToArray();
                        var visibility = (byte[])survey.visibility.Clone(); string build = session.BuildDefinition;
                        board.SetEnhancedLighting(true);
                        Check(PlayerPrefs.GetInt(BoardView.LightingKey) == 1 && Shader.GetGlobalFloat("_FrontierEnhancedLighting") == 1,
                            id + ": setting persists and updates the shader immediately");
                        Check(session.IsPaused && session.Selection.SetEquals(selection) && session.BuildDefinition == build
                            && board.Placed && board.Zoom == 1.15f && board.Pan == new Vector2(.4f, -.2f) && board.transform.localToWorldMatrix == pose
                            && board.FogDensity == .5f && survey.visibility.SequenceEqual(visibility),
                            id + ": toggling preserves pause, selection, placement, navigation and fog settings");
                        var restoredObject = new GameObject("Restored lighting board");
                        try
                        {
                            var restored = restoredObject.AddComponent<BoardView>(); restored.Session = session; restored.Viewer = preview.camera;
                            Call(restored, "Start"); Check(restored.EnhancedLighting, id + ": a new board restores the selected mode");
                        }
                        finally { Object.DestroyImmediate(restoredObject); }
                        session.Selection.Clear(); typeof(GameSession).GetProperty("BuildDefinition").SetValue(session, null);
                        board.Zoom = 1; board.Pan = Vector2.zero; board.SetFogDensity(0); Refresh();
                    }
                    void CheckShadows()
                    {
                        board.SetEnhancedLighting(true); Refresh(); var shadows = Field<ContactShadows>(board, "contactShadows");
                        var root = shadows.Root; var meshes = root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh).ToArray();
                        var material = root.GetComponentInChildren<MeshRenderer>(true).sharedMaterial;
                        Check(root.parent == board.Content && root.GetComponentsInChildren<Renderer>(true).Length == 2
                            && meshes.All(m => m.vertexCount <= 65532), id + ": shadows use two bounded meshes in map coordinates");
                        Check(root.GetComponentsInChildren<Collider>(true).Length == 0 && root.GetComponentsInChildren<Light>(true).Length == 0,
                            id + ": shadows add no input colliders or real-time shadow lights");
                        Check(material.GetTexture("_FogTex") == Field<Texture2D>(board, "fog"), id + ": shadow shader uses the current player visibility texture");
                        var background = preview.camera.backgroundColor; preview.camera.backgroundColor = Color.clear;
                        root.gameObject.SetActive(false); var unshadowed = SnapRgba();
                        root.gameObject.SetActive(true); var shadowed = SnapRgba(id + "-shadow-coverage");
                        var opaque = Enumerable.Range(0, unshadowed.Length).Where(i => unshadowed[i].a == 255).ToArray();
                        Check(opaque.Length > size * size / 10 && opaque.All(i => shadowed[i].a >= 254),
                            id + ": contact shadows preserve opaque terrain coverage for passthrough");
                        Check(Difference(unshadowed, shadowed) > .000001, id + ": contact shadows visibly darken the real terrain");
                        preview.camera.backgroundColor = background;
                        CheckConformity(meshes);
                        var ramp = session.Map.terrain.FirstOrDefault(t => t.ramp);
                        if (ramp != null)
                        {
                            shadows.SetStaticCasters(new[] { new Vector4(ramp.x + ramp.width * .5f, ramp.y + ramp.height * .5f, 3, .2f) });
                            CheckConformity(meshes);
                            var rampVertices = meshes[0].vertices;
                            Check(rampVertices.Length > 0 && rampVertices.Max(v => v.y) - rampVertices.Min(v => v.y) > .1f,
                                id + ": contact geometry follows the ramp instead of floating on a flat plane");
                        }
                        shadows.SetStaticCasters(Array.Empty<Vector4>());
                        var enemy = survey.entities.First(e => e.owner == 1 && session.Catalog[e.definition].building);
                        var probe = new PlayerView { player = 0, faction = session.Faction, tick = survey.tick,
                            visibility = (byte[])survey.visibility.Clone(), entities = new[] { enemy } };
                        SetView(probe); Refresh();
                        Check(shadows.DynamicCasterCount == 1, id + ": a currently visible enemy casts one contact shadow");
                        enemy.remembered = true; Array.Fill(probe.visibility, (byte)1); Refresh();
                        Check(shadows.CasterCount == 0 && meshes.All(m => m.vertexCount == 0), id + ": remembered enemies cannot cast shadows with mist disabled");
                        enemy.remembered = false; Array.Fill(probe.visibility, (byte)0); Refresh();
                        Check(shadows.DynamicCasterCount == 0, id + ": unseen cells suppress dynamic shadows even if an entity is still in the view");
                        probe.entities = Array.Empty<VisibleEntity>(); Array.Fill(probe.visibility, (byte)2); Refresh();
                        Check(shadows.DynamicCasterCount == 0, id + ": omitted enemies leave no stale contact shadow");
                        SetView(survey); Refresh();
                        var oldMeshes = meshes.ToArray(); var oldRoot = root; var oldMaterial = material;
                        board.SetVisible(false); Check(!root.gameObject.activeInHierarchy, id + ": hiding the board also hides its shadows");
                        board.SetVisible(true); Refresh();
                        board.Rebuild(); Refresh();
                        Check(oldRoot == null && oldMaterial == null && oldMeshes.All(m => m == null)
                            && board.Content.Cast<Transform>().Count(t => t.name == "Contact shadows") == 1,
                            id + ": rebuild releases old shadow geometry and creates exactly one replacement");

                        void CheckConformity(Mesh[] candidates)
                        {
                            bool conforming = true;
                            foreach (var mesh in candidates)
                            {
                                var vertices = mesh.vertices;
                                for (int q = 0; q < vertices.Length; q += 4)
                                {
                                    int cx = Mathf.FloorToInt((vertices[q].x + vertices[q + 2].x) * .5f);
                                    int cz = Mathf.FloorToInt((vertices[q].z + vertices[q + 2].z) * .5f);
                                    for (int v = q; v < q + 4; v++)
                                    {
                                        var p = vertices[v];
                                        float y = board.HeightAt(new Point(Mathf.Clamp(p.x, cx + .0001f, cx + .9999f), Mathf.Clamp(p.z, cz + .0001f, cz + .9999f)));
                                        conforming &= Mathf.Abs(p.y - y - .018f) < .001f;
                                    }
                                }
                            }
                            Check(conforming, id + ": every contact-shadow quad conforms to its terrain cell and cliff side");
                        }
                    }
                }
                Debug.Log($"FRONTIER_LIGHTING passed: {checks} checks, {captures} renders; output {output}; no physical Quest performance validation");

                void SetView(PlayerView view) => typeof(GameSession).GetProperty("View").SetValue(session, view);
                void Refresh() { typeof(BoardView).GetField("fogTick", Private).SetValue(board, -1); Call(board, "LateUpdate"); }
                void CameraAt(Point center, float radius)
                {
                    Refresh(); var point = board.Content.TransformPoint(new Vector3(center.x, board.HeightAt(center), center.y));
                    preview.camera.orthographic = true; preview.camera.orthographicSize = board.CellScale * radius;
                    preview.camera.nearClipPlane = .001f; preview.camera.farClipPlane = 10;
                    preview.camera.transform.position = point + new Vector3(.18f, 1.15f, -1.15f); preview.camera.transform.LookAt(point);
                    preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.018f, .025f, .035f);
                }
                Color32[] Snap(string name = null)
                {
                    preview.BeginStaticPreview(new Rect(0, 0, size, size)); preview.Render(true); var image = preview.EndStaticPreview();
                    try { if (name != null) File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG()); captures++; return image.GetPixels32(); }
                    finally { Object.DestroyImmediate(image); }
                }
                Color32[] SnapRgba(string name = null)
                {
                    preview.BeginPreview(new Rect(0, 0, size, size), GUIStyle.none); RenderTexture source;
                    try { preview.Render(true); } finally { source = (RenderTexture)preview.EndPreview(); }
                    var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
                    var previous = RenderTexture.active; var image = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                    try
                    {
                        Graphics.Blit(source, target); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, size, size), 0, 0); image.Apply();
                        if (name != null) File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG());
                        captures++; return image.GetPixels32();
                    }
                    finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image); }
                }
                double Difference(Color32[] a, Color32[] b)
                {
                    double difference = 0;
                    for (int i = 0; i < a.Length; i++) difference += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
                    return difference / (a.Length * 765.0);
                }
                void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Debug.Log("LIGHTING_PASS: " + message); }
            }
            finally
            {
                Object.DestroyImmediate(boardObject); Object.DestroyImmediate(fixture); preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds", oldBounds); Shader.SetGlobalMatrix("_FrontierWorldToBoard", oldMatrix);
                Shader.SetGlobalFloat("_FrontierEnhancedLighting", oldLighting); Shader.SetGlobalColor("_FrontierLightColor", oldLightColor);
                if (hadLighting) PlayerPrefs.SetInt(BoardView.LightingKey, savedLighting); else PlayerPrefs.DeleteKey(BoardView.LightingKey);
                if (hadFog) PlayerPrefs.SetFloat(BoardView.FogDensityKey, savedFog); else PlayerPrefs.DeleteKey(BoardView.FogDensityKey);
                PlayerPrefs.Save();
            }
        }
    }
}
