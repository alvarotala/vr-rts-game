using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    // Two small transparent batches ground silhouettes without shadow maps or a
    // screen-space pass. Dynamic casters only come from the filtered PlayerView.
    public sealed class ContactShadows : System.IDisposable
    {
        private const int MaxStaticCasters = 1024, MaxDynamicCasters = 256, MaxVertices = 65532;
        private readonly BoardView board;
        private readonly GameObject root;
        private readonly Material material;
        private readonly Batch scenery, dynamic;
        private readonly int width, height;
        private bool visible;

        public Transform Root => root.transform;
        public int StaticCasterCount { get; private set; }
        public int DynamicCasterCount { get; private set; }
        public int CasterCount => StaticCasterCount + DynamicCasterCount;

        public ContactShadows(BoardView board, Texture2D visibility)
        {
            this.board = board; width = board.Session.Map.width; height = board.Session.Map.height;
            root = new GameObject("Contact shadows"); root.transform.SetParent(board.Content, false);
            material = new Material(Shader.Find("Frontier/ContactShadows"));
            material.SetTexture("_FogTex", visibility); material.SetVector("_MapSize", new Vector4(width, height, 0, 0));
            scenery = new Batch("Scenery contact shadows", root.transform, material, false);
            dynamic = new Batch("Visible contact shadows", root.transform, material, true);
            root.SetActive(false);
        }

        // Authored scenery only: x/y are map x/z, z is radius, w is opacity.
        // Do not pass simulation objects here: these casters are always public.
        public void SetStaticCasters(IEnumerable<Vector4> casters)
        {
            scenery.Clear(); StaticCasterCount = 0;
            if (casters != null) foreach (var caster in casters)
            {
                if (StaticCasterCount >= MaxStaticCasters) break;
                if (Add(scenery, caster.x, caster.y, caster.z, caster.w, null)) StaticCasterCount++;
            }
            scenery.Upload();
        }

        public void SetVisible(bool value)
        {
            if (visible == value) return;
            visible = value; root.SetActive(value);
            if (!value) { dynamic.Clear(); dynamic.Upload(); DynamicCasterCount = 0; }
        }

        public void Update(PlayerView view, IReadOnlyDictionary<int, Transform> entities, IReadOnlyDictionary<int, Transform> resources)
        {
            if (!visible) return;
            dynamic.Clear(); DynamicCasterCount = 0;
            if (view != null)
            {
                foreach (var entity in view.entities)
                {
                    if (DynamicCasterCount >= MaxDynamicCasters) break;
                    if (entity.remembered || !entities.TryGetValue(entity.id, out var visual) || !visual.gameObject.activeSelf) continue;
                    var center = visual.localPosition;
                    if (!CurrentlyVisible(view, Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z))) continue;
                    var type = board.Session.Catalog[entity.definition];
                    float scale = Mathf.Max(Mathf.Abs(visual.localScale.x), Mathf.Abs(visual.localScale.z));
                    float radius = (type.building ? type.footprint * .68f : UnitVisuals.ModelScale(type) * .62f) * scale;
                    if (Add(dynamic, center.x, center.z, radius, type.building ? .2f : .22f, view)) DynamicCasterCount++;
                }
                foreach (var resource in view.resources)
                {
                    if (DynamicCasterCount >= MaxDynamicCasters) break;
                    if (resource.remaining == 0 || !resources.TryGetValue(resource.id, out var visual) || !visual.gameObject.activeSelf) continue;
                    var center = visual.localPosition;
                    if (!CurrentlyVisible(view, Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.z))) continue;
                    float radius = resource.kind == "mineral" ? 1.25f : 1.4f;
                    if (Add(dynamic, center.x, center.z, radius, .16f, view)) DynamicCasterCount++;
                }
            }
            dynamic.Upload();
        }

        private bool CurrentlyVisible(PlayerView view, int x, int z)
        {
            if (x < 0 || z < 0 || x >= width || z >= height) return false;
            int cell = z * width + x;
            return view.visibility != null && cell < view.visibility.Length && view.visibility[cell] == 2;
        }

        private bool Add(Batch batch, float x, float z, float radius, float opacity, PlayerView view)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z)
                || float.IsNaN(radius) || float.IsInfinity(radius) || float.IsNaN(opacity) || float.IsInfinity(opacity)
                || radius <= 0 || opacity <= 0 || x < 0 || z < 0 || x >= width || z >= height) return false;
            radius = Mathf.Clamp(radius, .12f, 4.5f);
            int left = Mathf.Max(0, Mathf.FloorToInt(x - radius)), right = Mathf.Min(width - 1, Mathf.CeilToInt(x + radius) - 1);
            int back = Mathf.Max(0, Mathf.FloorToInt(z - radius)), front = Mathf.Min(height - 1, Mathf.CeilToInt(z + radius) - 1);
            if (batch.vertices.Count + (right - left + 1) * (front - back + 1) * 4 > MaxVertices) return false;
            int initialCount = batch.vertices.Count;
            Color color = new Color(view == null ? 0 : 1, 0, 0, Mathf.Clamp(opacity, 0, .28f));
            for (int cz = back; cz <= front; cz++) for (int cx = left; cx <= right; cx++)
            {
                // Geometry is also filtered, so lowering fog density or a delayed
                // visibility texture cannot reveal a shadow in a hidden cell.
                if (view != null && !CurrentlyVisible(view, cx, cz)) continue;
                float x0 = Mathf.Max(cx, x - radius), x1 = Mathf.Min(cx + 1, x + radius);
                float z0 = Mathf.Max(cz, z - radius), z1 = Mathf.Min(cz + 1, z + radius);
                float dx = x - Mathf.Clamp(x, x0, x1), dz = z - Mathf.Clamp(z, z0, z1);
                if (dx * dx + dz * dz >= radius * radius) continue;
                int start = batch.vertices.Count;
                Vertex(x0, z0); Vertex(x0, z1); Vertex(x1, z1); Vertex(x1, z0);
                batch.triangles.Add(start); batch.triangles.Add(start + 1); batch.triangles.Add(start + 2);
                batch.triangles.Add(start); batch.triangles.Add(start + 2); batch.triangles.Add(start + 3);

                void Vertex(float vx, float vz)
                {
                    // Sample this cell's own surface at all four corners. Sharing
                    // vertices across cliffs would stretch a floating diagonal.
                    float ground = board.HeightAt(new Point(Mathf.Clamp(vx, cx + .0001f, cx + .9999f), Mathf.Clamp(vz, cz + .0001f, cz + .9999f)));
                    batch.vertices.Add(new Vector3(vx, ground + .018f, vz));
                    batch.uv.Add(new Vector2((vx - x) / radius, (vz - z) / radius)); batch.colors.Add(color);
                }
            }
            return batch.vertices.Count > initialCount;
        }

        public void Dispose()
        {
            root.SetActive(false); scenery.Dispose(); dynamic.Dispose(); Release(material); Release(root);
        }

        private sealed class Batch : System.IDisposable
        {
            public readonly List<Vector3> vertices = new List<Vector3>(4096);
            public readonly List<Vector2> uv = new List<Vector2>(4096);
            public readonly List<Color> colors = new List<Color>(4096);
            public readonly List<int> triangles = new List<int>(6144);
            private readonly Mesh mesh;
            public Batch(string name, Transform parent, Material material, bool moving)
            {
                mesh = new Mesh { name = name }; if (moving) mesh.MarkDynamic();
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            public void Clear() { vertices.Clear(); uv.Clear(); colors.Clear(); triangles.Clear(); }
            public void Upload()
            {
                mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            }
            public void Dispose() { Release(mesh); }
        }
        private static void Release(Object value) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}
