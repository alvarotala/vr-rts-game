using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Frontier.Runtime
{
    // Shared faceted deposits, with a single small batch for all visible gas plumes.
    public sealed class ResourceVisuals : System.IDisposable
    {
        private readonly Dictionary<string, Mesh> models = new Dictionary<string, Mesh>();
        private readonly List<Vector3> vaporVertices = new List<Vector3>();
        private readonly List<Vector3> vaporUv = new List<Vector3>();
        private readonly List<Color> vaporColors = new List<Color>();
        private readonly List<int> vaporTriangles = new List<int>();
        private GameObject vaporObject;
        private Mesh vaporMesh;
        private Material vaporMaterial;

        public Mesh Get(string kind, int id)
        {
            int variant = Mathf.Abs(id % 3); string key = kind + "/" + variant;
            if (models.TryGetValue(key, out var mesh)) return mesh;
            var b = new Builder(); var random = new System.Random(173 + variant * 71);
            float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
            if (kind == "mineral")
            {
                // A rocky seam with five hexagonal shards, never a single smooth block.
                b.Rock(Vector3.zero, new Vector3(1.7f, .3f, 1.45f), new Color(.22f, .26f, .3f), variant);
                var positions = new[] { new Vector3(0, .15f, .12f), new Vector3(-.48f, .12f, -.1f), new Vector3(.46f, .12f, -.16f), new Vector3(-.28f, .13f, .45f), new Vector3(.2f, .12f, -.52f) };
                for (int i = 0; i < positions.Length; i++)
                {
                    float height = (i == 0 ? 1.95f : i < 3 ? 1.3f : .85f) * Range(.9f, 1.1f);
                    b.Crystal(positions[i], Range(.22f, .31f), height,
                        new Vector3(positions[i].x * .36f, 0, positions[i].z * .3f), Range(0, 60));
                }
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * 2 * Mathf.PI / 5;
                    b.Rock(new Vector3(Mathf.Cos(angle) * .8f, .015f, Mathf.Sin(angle) * .64f),
                        new Vector3(.36f, Range(.12f, .25f), .3f), new Color(.29f, .32f, .35f), i + variant);
                }
            }
            else
            {
                // An open, dark vent surrounded by an uneven rock rim and mineral staining.
                const int sides = 12;
                var outer = new Vector3[sides]; var rim = new Vector3[sides]; var inner = new Vector3[sides];
                for (int i = 0; i < sides; i++)
                {
                    float a = i * 2 * Mathf.PI / sides; var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    outer[i] = direction * Range(1.02f, 1.2f) + Vector3.up * .025f;
                    rim[i] = direction * Range(.7f, .85f) + Vector3.up * Range(.3f, .57f);
                    inner[i] = direction * Range(.36f, .47f) + Vector3.up * .075f;
                }
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    var rock = Color.Lerp(new Color(.24f, .26f, .29f), new Color(.43f, .4f, .47f), Range(0, 1));
                    b.Quad(outer[i], rim[i], rim[j], outer[j], rock);
                    b.Quad(rim[i], inner[i], inner[j], rim[j], new Color(.29f, .19f, .37f));
                    b.Triangle(new Vector3(0, .07f, 0), inner[j], inner[i], new Color(.045f, .025f, .075f));
                    if (i % 2 == 0)
                    {
                        var stain = Color.Lerp(new Color(.5f, .29f, .7f), new Color(.74f, .55f, .87f), Range(0, 1));
                        b.Quad(Vector3.Lerp(rim[i], inner[i], .12f) + Vector3.up * .008f,
                            Vector3.Lerp(rim[i], inner[i], .45f) + Vector3.up * .008f,
                            Vector3.Lerp(rim[j], inner[j], .45f) + Vector3.up * .008f,
                            Vector3.Lerp(rim[j], inner[j], .12f) + Vector3.up * .008f, stain);
                    }
                }
            }
            mesh = b.Finish("Resource / " + key); models.Add(key, mesh); return mesh;
        }

        public void BindVapor(Transform parent, Texture2D fog, int width, int height)
        {
            ClearVapor();
            vaporObject = new GameObject("Gas vapor", typeof(MeshFilter), typeof(MeshRenderer)); vaporObject.transform.SetParent(parent, false);
            vaporMesh = new Mesh { name = "Gas vapor batch" }; vaporMesh.MarkDynamic();
            vaporMaterial = new Material(Shader.Find("Frontier/Weapons"));
            vaporMaterial.SetTexture("_FogTex", fog); vaporMaterial.SetVector("_MapSize", new Vector4(width, height, 0, 0));
            vaporObject.GetComponent<MeshFilter>().sharedMesh = vaporMesh;
            vaporObject.GetComponent<MeshRenderer>().sharedMaterial = vaporMaterial;
        }
        public void UpdateVapor(BoardView board, Camera viewer)
        {
            if (vaporObject == null) return;
            var view = board.Session.View;
            if (view == null || viewer == null || !board.Session.Running) { vaporObject.SetActive(false); return; }
            vaporVertices.Clear(); vaporUv.Clear(); vaporColors.Clear(); vaporTriangles.Clear();
            var right = board.Content.InverseTransformDirection(viewer.transform.right).normalized;
            var up = board.Content.InverseTransformDirection(viewer.transform.up).normalized;
            // Simulation time freezes on pause and never advances hidden resource information.
            float time = view.tick / (float)Frontier.Core.Simulation.TickRate;
            foreach (var resource in view.resources)
            {
                if (resource.kind != "catalyst" || resource.remaining == 0) continue;
                int cell = (int)resource.position.y * board.Session.Map.width + (int)resource.position.x;
                if (view.visibility[cell] != 2) continue;
                // The extractor covers the vent, including while it is being built.
                if (view.entities.Any(e =>
                {
                    var type = board.Session.Catalog[e.definition];
                    return type.role == "extractor" && resource.position.x >= e.position.x && resource.position.y >= e.position.y
                        && resource.position.x < e.position.x + type.footprint && resource.position.y < e.position.y + type.footprint;
                })) continue;
                for (int i = 0; i < 4; i++)
                {
                    float t = Mathf.Repeat(time * .23f + i * .25f + resource.id * .137f, 1);
                    float phase = t * 5 + resource.id;
                    float radius = Mathf.Lerp(.23f, .65f, t);
                    var center = new Vector3(resource.position.x + Mathf.Sin(phase) * t * .28f,
                        board.HeightAt(resource.position) + .35f + t * 1.85f, resource.position.y + Mathf.Cos(phase * 1.3f) * t * .2f);
                    var color = Color.Lerp(new Color(.59f, .34f, .85f), new Color(.83f, .77f, .95f), t);
                    color.a = Mathf.Sin(t * Mathf.PI) * .48f;
                    int n = vaporVertices.Count;
                    vaporVertices.Add(center - right * radius - up * radius); vaporVertices.Add(center - right * radius + up * radius);
                    vaporVertices.Add(center + right * radius + up * radius); vaporVertices.Add(center + right * radius - up * radius);
                    vaporUv.Add(new Vector3(0, 0, 1)); vaporUv.Add(new Vector3(0, 1, 1)); vaporUv.Add(new Vector3(1, 1, 1)); vaporUv.Add(new Vector3(1, 0, 1));
                    for (int k = 0; k < 4; k++) vaporColors.Add(color);
                    vaporTriangles.Add(n); vaporTriangles.Add(n + 1); vaporTriangles.Add(n + 2); vaporTriangles.Add(n); vaporTriangles.Add(n + 2); vaporTriangles.Add(n + 3);
                }
            }
            vaporObject.SetActive(vaporVertices.Count > 0); vaporMesh.Clear();
            if (vaporVertices.Count == 0) return;
            vaporMesh.SetVertices(vaporVertices); vaporMesh.SetUVs(0, vaporUv); vaporMesh.SetColors(vaporColors);
            vaporMesh.SetTriangles(vaporTriangles, 0); vaporMesh.RecalculateBounds();
        }
        public void ClearVapor() { Release(vaporObject); Release(vaporMesh); Release(vaporMaterial); vaporObject = null; }
        public void Dispose() { ClearVapor(); foreach (var mesh in models.Values) Release(mesh); models.Clear(); }
        private static void Release(Object item) { if (item == null) return; if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item); }

        private sealed class Builder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();
            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            { int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); colors.Add(color); colors.Add(color); colors.Add(color); triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2); }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
            { Triangle(a, b, c, color); Triangle(a, c, d, color); }
            public void Rock(Vector3 center, Vector3 size, Color color, int variant)
            {
                for (int i = 0; i < 7; i++)
                {
                    Vector3 V(int side, float height, float radius)
                    {
                        float angle = side * 2 * Mathf.PI / 7 + variant;
                        return center + Vector3.Scale(new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius), size);
                    }
                    var a = V(i, .35f, .5f); var b = V(i + 1, .35f, .5f);
                    Quad(V(i, 0, .45f), a, b, V(i + 1, 0, .45f), color * .8f);
                    Triangle(a, center + Vector3.up * size.y, b, color * (i % 2 == 0 ? 1 : .88f));
                }
            }
            public void Crystal(Vector3 basePoint, float radius, float height, Vector3 lean, float yaw)
            {
                Vector3 V(int side, float t, float r)
                {
                    float angle = (side * 60 + yaw) * Mathf.Deg2Rad;
                    return basePoint + lean * t + new Vector3(Mathf.Cos(angle) * r, height * t, Mathf.Sin(angle) * r);
                }
                for (int i = 0; i < 6; i++)
                {
                    var a = V(i, 0, radius * .75f); var b = V(i, .7f, radius);
                    var c = V(i + 1, .7f, radius); var d = V(i + 1, 0, radius * .75f);
                    var color = i % 3 == 0 ? new Color(.15f, .53f, .77f) : i % 3 == 1 ? new Color(.3f, .8f, .95f) : new Color(.52f, .93f, 1);
                    Quad(a, b, c, d, color);
                    Triangle(b, basePoint + lean + Vector3.up * height, c, Color.Lerp(color, new Color(.83f, 1, 1), .4f));
                }
            }
            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}
