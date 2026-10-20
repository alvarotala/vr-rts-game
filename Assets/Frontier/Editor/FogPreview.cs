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
    public static class FogPreview
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);

        [MenuItem("Frontier/Preview and check fog relief")]
        public static void Run()
        {
            const string output = "TestResults/Fog"; const int size = 1000;
            Directory.CreateDirectory(output);
            var fixture = new GameObject("Fog preview session"); var boardObject = new GameObject("Fog preview board");
            var preview = new PreviewRenderUtility(); int checks = 0, captures = 0;
            var previousBounds = Shader.GetGlobalVector("_FrontierBounds"); var previousMatrix = Shader.GetGlobalMatrix("_FrontierWorldToBoard");
            try
            {
                var session = fixture.AddComponent<GameSession>(); if (session.Catalog == null) Call(session, "Awake");
                session.LoadMap("training");
                var board = boardObject.AddComponent<BoardView>(); board.Session = session; board.Viewer = preview.camera; Call(board, "Start");
                preview.AddSingleGO(boardObject);
                var shader = Shader.Find("Frontier/Fog");
                Check(shader != null && !ShaderUtil.ShaderHasError(shader), "Fog shader compiles with the URP stereo variants");
                var fog = Field<FogVisuals>(board, "fogVisuals");
                fog.Update(true, 0);
                Check(!fog.Root.gameObject.activeSelf, "A map preview before a match has no fog sheets");

                foreach (var asset in Resources.LoadAll<TextAsset>("Maps").OrderBy(a => a.name))
                {
                    session.LoadMap(asset.name); session.StartMatch();
                    var hiddenEnemy = session.Match.Entities.Values.First(e => e.owner == 1);
                    session.View.entities = Array.Empty<VisibleEntity>(); session.View.resources = Array.Empty<ResourceState>();
                    session.View.attacks = Array.Empty<AttackEvent>(); session.View.deaths = Array.Empty<DeathEvent>(); session.View.abilityEvents = Array.Empty<AbilityEvent>();
                    SetBands(); Refresh(); fog = Field<FogVisuals>(board, "fogVisuals");
                    var root = fog.Root; var mesh = root.GetComponent<MeshFilter>().sharedMesh; var material = root.GetComponent<MeshRenderer>().sharedMaterial;
                    Check(root.gameObject.activeInHierarchy && root.parent == board.Content, asset.name + ": active fog follows map coordinates");
                    Check(root.GetComponentsInChildren<Renderer>().Length == 1 && mesh.subMeshCount == 1 && mesh.vertexCount <= FogVisuals.MaxVertices,
                        asset.name + ": all three sheets share one bounded mesh and material");
                    Check(root.GetComponentsInChildren<Collider>().Length == 0 && root.GetComponentsInChildren<ParticleSystem>().Length == 0,
                        asset.name + ": fog adds no picking colliders or per-cell particles");
                    Check(mesh.uv.Select(p => p.x).Distinct().Count() == FogVisuals.LayerCount, asset.name + ": fog contains three separated depth layers");
                    var lower = mesh.vertices.Take(mesh.vertexCount / FogVisuals.LayerCount).ToArray();
                    Check(lower.All(p => p.y > board.HeightAt(new Point(Mathf.Clamp(p.x, .001f, session.Map.width - .001f), Mathf.Clamp(p.z, .001f, session.Map.height - .001f))) + .2f),
                        asset.name + ": the lowest sheet remains above the terrain and ramp samples");
                    if (session.Map.terrain.Any(t => t.elevation > 0))
                        Check(lower.Max(p => p.y) - lower.Min(p => p.y) > .75f, asset.name + ": mist follows raised terrain rather than forming a flat map-wide plane");
                    var tint = material.GetColor("_FogColor");
                    Check(session.Map.theme == "desert" ? tint.r > tint.b : tint.b > tint.r, asset.name + ": fog tint matches the battlefield theme");
                    var visibility = Field<Texture2D>(board, "fog");
                    Check(material.GetTexture("_FogTex") == visibility && visibility.filterMode == FilterMode.Point, "Fog uses the existing filtered player visibility texture");
                    Check(visibility.GetPixel(0, 0).r == 0 && Mathf.Abs(visibility.GetPixel(session.Map.width / 2, 0).r - 75 / 255f) < .001f
                        && visibility.GetPixel(session.Map.width - 1, 0).r == 1, "Unseen, remembered and visible cells retain distinct 0 / 75 / 255 values");
                    Check(Field<System.Collections.IDictionary>(board, "entities").Count == 0 && !session.View.entities.Any(e => e.id == hiddenEnemy.id),
                        "Fog presentation does not create an enemy omitted from PlayerView");
                    var point = new Point(session.Map.width * .84f, session.Map.height * .7f);
                    var ground = board.Content.TransformPoint(new Vector3(point.x, board.HeightAt(point), point.y));
                    board.ResolvePointerTarget(new Ray(ground + Vector3.up, Vector3.down), point, ground, out int target, out int resource);
                    Check(target == 0 && resource == 0, "Fog cannot become a unit or resource selection target");

                    CameraAt(false); fog.Update(true, 12); Snap(asset.name + "-relief");
                    CameraAt(true); fog.Update(true, 12); Snap(asset.name + "-overview");
                    var originalVertices = mesh.vertices; int originalObjects = root.GetComponentsInChildren<Transform>(true).Length;
                    fog.Update(true, 40);
                    Check(root.GetComponent<MeshFilter>().sharedMesh == mesh && root.GetComponent<MeshRenderer>().sharedMaterial == material
                        && originalVertices.SequenceEqual(mesh.vertices) && root.GetComponentsInChildren<Transform>(true).Length == originalObjects,
                        "Animation updates only the shader clock, without rebuilding meshes or adding objects");
                    Check(material.GetFloat("_FogTime") == 40, "The fog animation clock accepts a reproducible simulation time");

                    if (asset.name == "training") CheckPixels();
                    board.SetVisible(false); fog.Update(true, 60);
                    Check(!fog.Root.gameObject.activeSelf, "Hidden boards suppress the fog even if an update requests visibility");
                    board.SetVisible(true); Refresh();
                    session.Pause("fog-preview", true); Refresh(); float pausedTime = material.GetFloat("_FogTime");
                    session.Match.Step(); Refresh();
                    Check(material.GetFloat("_FogTime") == pausedTime, "Paused simulation does not advance the fog clock");
                    session.Pause("fog-preview", false);
                    board.Rebuild(); Refresh();
                    Check(root == null && mesh == null && material == null && board.Content.Cast<Transform>().Count(t => t.name == "Fog of war") == 1,
                        "Rebuild releases the old fog object, mesh and material before creating one replacement");
                    fog = Field<FogVisuals>(board, "fogVisuals"); root = fog.Root; mesh = root.GetComponent<MeshFilter>().sharedMesh;
                    session.StartMatch(); Refresh();
                    Check(root == null && mesh == null && Field<FogVisuals>(board, "fogVisuals").Root != null, "Rematch releases old fog geometry and visibility bindings");
                    session.LeaveMatch(); board.SetVisible(true); Refresh();
                    Check(!Field<FogVisuals>(board, "fogVisuals").Root.gameObject.activeSelf, "Leaving a match removes fog from the next setup preview");

                    void CheckPixels()
                    {
                        // Isolate fog from terrain color and decorations so opacity and
                        // exact visibility clipping can be checked from the rendered pixels.
                        var siblings = board.Content.Cast<Transform>().Where(t => t != root).Select(t => (t.gameObject, t.gameObject.activeSelf)).ToArray();
                        foreach (var entry in siblings) entry.gameObject.SetActive(false);
                        try
                        {
                            CameraAt(true); fog.Hide(); var baseline = Snap();
                            fog.Update(true, 0); var first = Snap("visibility-states");
                            double unseen = BandDifference(baseline, first, 0), explored = BandDifference(baseline, first, 1), visible = BandDifference(baseline, first, 2);
                            Check(unseen > .015 && explored > .005 && unseen > explored * 1.3, "Rendered unseen mist is denser than explored mist: " + unseen.ToString("0.000") + " / " + explored.ToString("0.000"));
                            Check(visible < .001, "Currently visible cells receive no fog, including beside the visibility boundary");
                            fog.Update(true, 40); var later = Snap();
                            Check(BandDifference(first, later, 0) > .001, "Slow cloud drift changes the rendered mist without moving terrain");
                            fog.Update(true, 0); var repeated = Snap();
                            Check(BandDifference(first, repeated, 0) < .0001, "The same fog time renders the same mist pattern");
                            Array.Fill(session.View.visibility, (byte)2); Refresh();
                            foreach (var entry in siblings) entry.gameObject.SetActive(false);
                            fog.Update(true, 0); var clear = Snap();
                            Check(BandDifference(baseline, clear, 0) < .001 && BandDifference(baseline, clear, 1) < .001,
                                "New visibility removes all fog from previously unseen and explored cells");
                            SetBands(); Refresh(); foreach (var entry in siblings) entry.gameObject.SetActive(false);
                            board.Zoom = 2; board.Pan = new Vector2(session.Map.width * .15f, 0); Refresh();
                            foreach (var entry in siblings) entry.gameObject.SetActive(false);
                            fog.Update(true, 0); var zoomed = Snap();
                            foreach (var local in new[] { new Vector3(-board.Width * .6f, 0, 0), new Vector3(board.Width * .6f, 0, 0), new Vector3(0, 0, -board.Depth * .6f), new Vector3(0, 0, board.Depth * .6f) })
                                Check(PixelDifference(baseline[Pixel(board.transform.TransformPoint(local))], zoomed[Pixel(board.transform.TransformPoint(local))]) < .001,
                                    "Zoom and pan keep fog clipped to the physical board bounds");
                        }
                        finally
                        {
                            board.Fit(); foreach (var entry in siblings) if (entry.gameObject != null) entry.gameObject.SetActive(entry.activeSelf);
                            SetBands(); Refresh();
                        }
                    }
                    void SetBands()
                    {
                        for (int y = 0; y < session.Map.height; y++) for (int x = 0; x < session.Map.width; x++)
                            session.View.visibility[y * session.Map.width + x] = (byte)Mathf.Min(2, x * 3 / session.Map.width);
                    }
                    void Refresh()
                    {
                        typeof(BoardView).GetField("fogTick", Private).SetValue(board, -1); Call(board, "LateUpdate");
                    }
                    void CameraAt(bool top)
                    {
                        preview.camera.orthographic = true; preview.camera.aspect = 1; preview.camera.orthographicSize = board.Width * .75f;
                        preview.camera.nearClipPlane = .001f; preview.camera.farClipPlane = 10;
                        preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(.018f, .03f, .045f);
                        var center = board.transform.position;
                        preview.camera.transform.position = center + (top ? Vector3.up * 2 : new Vector3(.4f, 1.3f, -1.6f));
                        preview.camera.transform.LookAt(center, top ? Vector3.forward : Vector3.up);
                    }
                    int Pixel(Vector3 world)
                    {
                        var viewport = preview.camera.WorldToViewportPoint(world);
                        int x = Mathf.Clamp(Mathf.FloorToInt(viewport.x * size), 0, size - 1), y = Mathf.Clamp(Mathf.FloorToInt(viewport.y * size), 0, size - 1);
                        return y * size + x;
                    }
                    double BandDifference(Color32[] a, Color32[] b, int band)
                    {
                        double total = 0; int count = 0;
                        int left = Mathf.CeilToInt(session.Map.width * band / 3f), right = Mathf.CeilToInt(session.Map.width * (band + 1) / 3f);
                        for (float x = left + .1f; x < right - .1f; x += .5f) for (float y = 3; y < session.Map.height - 3; y += 3)
                        {
                            int pixel = Pixel(board.Content.TransformPoint(new Vector3(x, 0, y)));
                            total += PixelDifference(a[pixel], b[pixel]); count++;
                        }
                        return total / count;
                    }
                }
                Debug.Log($"FRONTIER_FOG_VISUALS passed: {checks} checks, {captures} renders; no player build or physical Quest performance validation");

                Color32[] Snap(string name = null)
                {
                    preview.BeginStaticPreview(new Rect(0, 0, size, size)); preview.Render(true);
                    var image = preview.EndStaticPreview();
                    try
                    {
                        if (name != null) File.WriteAllBytes(output + "/" + name + ".png", image.EncodeToPNG());
                        captures++; return image.GetPixels32();
                    }
                    finally { Object.DestroyImmediate(image); }
                }
                double PixelDifference(Color32 a, Color32 b) => (Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b)) / 765.0;
                void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Debug.Log("FOG_PASS: " + message); }
            }
            finally
            {
                Object.DestroyImmediate(boardObject); Object.DestroyImmediate(fixture); preview.Cleanup();
                Shader.SetGlobalVector("_FrontierBounds", previousBounds); Shader.SetGlobalMatrix("_FrontierWorldToBoard", previousMatrix);
            }
        }
    }
}
