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
            public Vector3[] collapsedVertices, collapsedNormals;
            public Color[] colors;
            public int[] triangles;
            public Bounds bounds;
        }
        private struct Pose
        {
            public Vector3 position, bodyPosition;
            public Quaternion rotation, bodyRotation;
            public int tick;
        }
        private readonly Dictionary<string, Model> cache = new Dictionary<string, Model>();
        private readonly Dictionary<int, Pose> poses = new Dictionary<int, Pose>();
        private Vector3[] posedVertices = System.Array.Empty<Vector3>();
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
                    pose = new Pose { tick = death.tick, bodyRotation = Quaternion.identity };
                    if (!board.TryUnitDeathPose(death.unit, out pose.position, out pose.rotation, out pose.bodyPosition, out pose.bodyRotation))
                    {
                        pose.position = new Vector3(death.position.x, board.HeightAt(death.position), death.position.y);
                        pose.rotation = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : Quaternion.Euler(0, death.unit * 137.5f % 360, 0);
                        pose.bodyPosition = Vector3.zero; pose.bodyRotation = Quaternion.identity;
                    }
                    poses[death.unit] = pose;
                }
                ActiveCount++;
                AddBody(board, death, type, model, pose, age);
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
            model = new Model { vertices = source.vertices, normals = source.normals, colors = source.colors, triangles = source.triangles, bounds = source.bounds };
            if (type.faction == "biological")
            {
                var collapsed = BiologicalVisuals.Unit(type, false, false, collapsed: true);
                model.collapsedVertices = collapsed.vertices; model.collapsedNormals = collapsed.normals;
                float scale = UnitVisuals.ModelScale(type);
                for (int i = 0; i < model.collapsedVertices.Length; i++) model.collapsedVertices[i] *= scale;
                Release(collapsed);
            }
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

        private void AddBody(BoardView board, DeathEvent death, Archetype type, Model model, Pose pose, float age)
        {
            bool biological = type.faction == "biological";
            bool wreck = type.faction == "industrial" && (type.role == "worker" || type.role == "scout" || type.role == "siege");
            float fall = Mathf.SmoothStep(0, 1, age / FallSeconds);
            float fade = 1 - Mathf.SmoothStep(0, 1, (age - FadeStart) / (Lifetime - FadeStart));
            float side = (death.unit & 1) == 0 ? 1 : -1;
            var liveRotation = pose.rotation * pose.bodyRotation;
            var direction = liveRotation * Vector3.forward; direction.y = 0;
            var facing = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : pose.rotation;
            var origin = Vector3.Lerp(pose.position, new Vector3(death.position.x, board.HeightAt(death.position), death.position.y), fall)
                + pose.rotation * pose.bodyPosition * (1 - fall);
            var rotation = Quaternion.Slerp(liveRotation, GroundTilt(board, death.position, UnitVisuals.ModelScale(type)) * facing, fall);
            // Vehicles settle on their chassis. Upright soldiers tip from the rear
            // ground contact, with no sideways slide or arbitrary diagonal spin.
            var tumble = biological ? Quaternion.identity : Quaternion.Euler((wreck ? -4 : -83) * fall, 0, side * (wreck ? 5 : 6) * fall);
            var contact = wreck || biological ? Vector3.zero : new Vector3(0, model.bounds.min.y, model.bounds.min.z);
            if (posedVertices.Length < model.vertices.Length) posedVertices = new Vector3[model.vertices.Length];
            float support = float.NegativeInfinity;
            for (int i = 0; i < model.vertices.Length; i++)
            {
                var vertex = biological ? Vector3.Lerp(model.vertices[i], model.collapsedVertices[i], fall) : model.vertices[i];
                var point = rotation * (contact + tumble * (vertex - contact)); posedVertices[i] = point;
                support = Mathf.Max(support, board.HeightAt(new Point(origin.x + point.x, origin.z + point.z)) - origin.y - point.y);
            }
            // Allow a hovering body to settle down as well as lifting away from a
            // slope. Every pose has an actual contact instead of floating on a pivot.
            origin.y += Mathf.Lerp(Mathf.Max(0, support), support, fall) + .012f * fall;
            int start = vertices.Count;
            for (int i = 0; i < model.vertices.Length; i++)
            {
                var normal = biological ? Vector3.Lerp(model.normals[i], model.collapsedNormals[i], fall).normalized : model.normals[i];
                vertices.Add(origin + posedVertices[i]); normals.Add(rotation * tumble * normal);
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

        private static Quaternion GroundTilt(BoardView board, Point position, float scale)
        {
            float radius = Mathf.Max(.5f, scale * .35f);
            float left = board.HeightAt(new Point(position.x - radius, position.y)), right = board.HeightAt(new Point(position.x + radius, position.y));
            float back = board.HeightAt(new Point(position.x, position.y - radius)), front = board.HeightAt(new Point(position.x, position.y + radius));
            var normal = new Vector3(left - right, 2 * radius, back - front).normalized;
            // A cliff edge is not a surface on which to stand a corpse vertically.
            return Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), 35);
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
