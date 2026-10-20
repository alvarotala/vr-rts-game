using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.Runtime
{
    // Bodies are presentation only: one bounded mesh, no colliders or simulated units.
    public sealed class DeathEffects : System.IDisposable
    {
        public const int MaxBodies = 64, MaxVertices = 90000;
        public const float FallSeconds = .5f, FadeStart = 3.5f, Lifetime = 6f;
        public int ActiveCount { get; private set; }
        private readonly GameObject root;
        private readonly Mesh mesh = new Mesh { name = "Fallen units", indexFormat = IndexFormat.UInt32 };
        private readonly Material material = new Material(Shader.Find("Frontier/Remains"));
        private readonly UnitVisuals models = new UnitVisuals();
        private sealed class Model
        {
            public Vector3[] vertices, normals;
            public Color[] colors;
            public int[] triangles;
        }
        private readonly Dictionary<string, Model> cache = new Dictionary<string, Model>();
        private readonly Dictionary<int, (Quaternion rotation, int tick)> poses = new Dictionary<int, (Quaternion, int)>();
        private readonly List<int> expired = new List<int>();
        private readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private int finishTick = -1;
        private float finishClock;

        public DeathEffects(Transform parent, Texture fog, int width, int height)
        {
            root = new GameObject("Unit remains", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(parent, false); mesh.MarkDynamic();
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            material.SetTexture("_FogTex", fog); material.SetVector("_MapSize", new Vector4(width, height, 0, 0));
        }

        public void Update(BoardView board, float visualTick)
        {
            // Simulation stops at victory. Let the final casualties finish, unless paused.
            var view = board.Session.View;
            if (view != null && view.winner >= 0)
            {
                if (finishTick != view.tick) { finishTick = view.tick; finishClock = visualTick; }
                else if (!board.Session.IsPaused) finishClock += Time.unscaledDeltaTime * Simulation.TickRate;
                visualTick = finishClock;
            }
            else finishTick = -1;
            Render(board, visualTick);
        }

        public void Render(BoardView board, float visualTick)
        {
            vertices.Clear(); normals.Clear(); colors.Clear(); triangles.Clear(); ActiveCount = 0;
            var view = board.Session.View;
            if (view == null || !board.Content.gameObject.activeInHierarchy) { Clear(); return; }
            expired.Clear();
            foreach (var pose in poses) if (visualTick - pose.Value.tick >= Simulation.DeathHistoryTicks) expired.Add(pose.Key);
            foreach (int id in expired) poses.Remove(id);
            var events = view.deaths;
            if (events != null) for (int i = events.Length - 1; i >= 0 && ActiveCount < MaxBodies; i--)
            {
                var death = events[i];
                float age = (visualTick - death.tick) / Simulation.TickRate;
                if (age < 0 || age >= Lifetime || !Visible(board, death.position)) continue;
                if (!board.Session.Catalog.Has(death.definition)) continue;
                var type = board.Session.Catalog[death.definition]; if (type.building) continue;
                var model = GetModel(type, death.owner == view.player);
                if (vertices.Count + model.vertices.Length + 144 > MaxVertices) continue;
                if (!poses.TryGetValue(death.unit, out var pose))
                {
                    var direction = new Vector3(death.direction.x, 0, death.direction.y);
                    var facing = board.TryUnitRotation(death.unit, out var rotation) ? rotation :
                        direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : Quaternion.Euler(0, death.unit * 137.5f % 360, 0);
                    pose = (facing, death.tick); poses[death.unit] = pose;
                }
                ActiveCount++;
                AddBody(board, death, type, model, pose.rotation, age);
            }
            mesh.Clear();
            if (vertices.Count > 0)
            {
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            }
        }

        private Model GetModel(Archetype type, bool own)
        {
            string key = type.id + (own ? "/own" : "/enemy");
            if (cache.TryGetValue(key, out var model)) return model;
            var source = models.Get(type, remains: true);
            model = new Model { vertices = source.vertices, normals = source.normals, colors = source.colors, triangles = source.triangles };
            var palette = new[] { own ? new Color(.2f, .8f, 1) : new Color(1, .36f, .23f),
                type.faction == "biological" ? Color.white : new Color(.12f, .17f, .22f), UnitVisuals.Accent(type.role) };
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                var painted = new HashSet<int>();
                foreach (int vertex in source.GetTriangles(submesh))
                    if (painted.Add(vertex)) model.colors[vertex] *= palette[submesh];
            }
            cache.Add(key, model); return model;
        }

        private void AddBody(BoardView board, DeathEvent death, Archetype type, Model model, Quaternion facing, float age)
        {
            bool wreck = type.faction == "industrial" && (type.role == "worker" || type.role == "scout" || type.role == "siege");
            float fall = Mathf.SmoothStep(0, 1, age / FallSeconds);
            float fade = 1 - Mathf.SmoothStep(0, 1, (age - FadeStart) / (Lifetime - FadeStart));
            float side = (death.unit & 1) == 0 ? 1 : -1;
            var rotation = facing * Quaternion.AngleAxis((wreck ? 110 : 88) * fall, new Vector3(side, 0, .35f).normalized);
            var slide = facing * new Vector3(side * .12f, 0, .25f) * UnitVisuals.ModelScale(type) * fall;
            var origin = new Vector3(death.position.x, board.HeightAt(death.position), death.position.y) + slide;
            // Keep the entire tilted body above its local ground, including ramps.
            float lift = 0;
            foreach (var vertex in model.vertices)
            {
                var point = rotation * vertex;
                lift = Mathf.Max(lift, board.HeightAt(new Point(origin.x + point.x, origin.z + point.z)) - origin.y - point.y);
            }
            origin.y += lift + .025f;
            int start = vertices.Count;
            for (int i = 0; i < model.vertices.Length; i++)
            {
                vertices.Add(origin + rotation * model.vertices[i]); normals.Add(rotation * model.normals[i]);
                var color = model.colors[i];
                color = Color.Lerp(color, new Color(color.grayscale * .62f, color.grayscale * .62f, color.grayscale * .62f), fall * .55f);
                color.a = fade; colors.Add(color);
            }
            foreach (int index in model.triangles) triangles.Add(start + index);
            if (wreck && age < .65f)
            {
                // Short, local shards: visible destruction without a large flash over the battle.
                float progress = age / .65f, scale = UnitVisuals.ModelScale(type);
                var center = new Vector3(death.position.x, board.HeightAt(death.position) + .35f * scale, death.position.y);
                for (int shard = 0; shard < 6; shard++)
                {
                    float angle = shard * Mathf.PI / 3 + death.unit;
                    var travel = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * progress * .8f * scale;
                    travel.y = Mathf.Sin(progress * Mathf.PI) * (.45f + shard % 3 * .18f) * scale;
                    var color = Color.Lerp(new Color(1, .85f, .3f), new Color(.35f, .16f, .07f), progress); color.a = 1 - progress;
                    Shard(center + travel, .07f * scale * (1 - progress * .6f), color);
                }
            }
        }

        private void Shard(Vector3 center, float size, Color color)
        {
            // An octahedron remains visible from either eye and every table angle.
            for (int half = -1; half <= 1; half += 2) for (int side = 0; side < 4; side++)
            {
                float a = side * Mathf.PI * .5f, b = (side + half) * Mathf.PI * .5f;
                var top = Vector3.up * (size * half);
                var left = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * size;
                var right = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * size;
                var normal = Vector3.Cross(left - top, right - top).normalized;
                int start = vertices.Count;
                vertices.Add(center + top); vertices.Add(center + left); vertices.Add(center + right);
                for (int i = 0; i < 3; i++) { normals.Add(normal); colors.Add(color); triangles.Add(start + i); }
            }
        }

        private static bool Visible(BoardView board, Point point)
        {
            int x = Mathf.FloorToInt(point.x), y = Mathf.FloorToInt(point.y), width = board.Session.Map.width;
            return x >= 0 && y >= 0 && x < width && y < board.Session.Map.height && board.Session.View.visibility[y * width + x] == 2;
        }
        public void Clear() { mesh.Clear(); poses.Clear(); ActiveCount = 0; finishTick = -1; }
        public void Dispose() { root.SetActive(false); Release(root); Release(mesh); Release(material); models.Dispose(); }
        private static void Release(Object value) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); }
    }
}
