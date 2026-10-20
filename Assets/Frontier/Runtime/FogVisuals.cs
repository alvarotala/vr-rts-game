using System;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Frontier.Runtime
{
    // Three terrain-following sheets give fog some depth without particles,
    // per-cell GameObjects, depth textures or a volumetric raymarch.
    public sealed class FogVisuals : IDisposable
    {
        public const int LayerCount = 3;
        public const int MaxSegments = 64;
        public const int MaxVertices = LayerCount * (MaxSegments + 1) * (MaxSegments + 1);
        public Transform Root { get; private set; }
        private readonly BoardView board;
        private Mesh mesh;
        private Material material;
        private static readonly int TimeProperty = Shader.PropertyToID("_FogTime");

        public FogVisuals(BoardView board, Texture2D visibility, Color? tint = null)
        {
            this.board = board;
            var go = new GameObject("Fog of war", typeof(MeshFilter), typeof(MeshRenderer));
            Root = go.transform; Root.SetParent(board.Content, false); go.SetActive(false);
            material = new Material(Shader.Find("Frontier/Fog")) { name = "Fog of war layers", enableInstancing = true };
            material.SetTexture("_FogTex", visibility);
            material.SetVector("_MapSize", new Vector4(board.Session.Map.width, board.Session.Map.height, 0, 0));
            material.SetColor("_FogColor", tint ?? new Color(.22f, .29f, .34f));
            mesh = BuildMesh();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private Mesh BuildMesh()
        {
            int width = board.Session.Map.width, height = board.Session.Map.height;
            int columns = Mathf.Min(width, MaxSegments), rows = Mathf.Min(height, MaxSegments);
            int stride = columns + 1, perLayer = stride * (rows + 1);
            float stepX = width / (float)columns, stepY = height / (float)rows;
            var vertices = new Vector3[perLayer * LayerCount];
            var uv = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            var triangles = new int[columns * rows * 6 * LayerCount];
            var surface = new float[perLayer];
            // Conservatively sample the sheet's footprint around each vertex. This
            // prevents coarse triangles from cutting through plateau edges or ramps.
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            {
                float top = 0;
                for (int sy = -1; sy <= 1; sy++) for (int sx = -1; sx <= 1; sx++)
                {
                    float px = Mathf.Clamp((x + sx * .5f) * stepX, .001f, width - .001f);
                    float py = Mathf.Clamp((y + sy * .5f) * stepY, .001f, height - .001f);
                    top = Mathf.Max(top, board.HeightAt(new Point(px, py)));
                }
                surface[y * stride + x] = top;
            }
            int triangle = 0;
            for (int layer = 0; layer < LayerCount; layer++)
            {
                byte opacity = (byte)(layer == 0 ? 140 : layer == 1 ? 110 : 82);
                for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
                {
                    int index = layer * perLayer + y * stride + x;
                    float px = x * stepX, py = y * stepY;
                    float wave = Mathf.Sin(px * .24f + layer * 1.7f) * Mathf.Cos(py * .21f - layer) * .1f;
                    vertices[index] = new Vector3(px, surface[y * stride + x] + .45f + layer * .55f + wave, py);
                    uv[index] = new Vector2(layer, 0); colors[index] = new Color32(255, 255, 255, opacity);
                    if (x == columns || y == rows) continue;
                    triangles[triangle++] = index; triangles[triangle++] = index + stride; triangles[triangle++] = index + stride + 1;
                    triangles[triangle++] = index; triangles[triangle++] = index + stride + 1; triangles[triangle++] = index + 1;
                }
            }
            var result = new Mesh { name = "Fog of war / three terrain sheets" };
            result.vertices = vertices; result.uv = uv; result.colors32 = colors; result.triangles = triangles;
            result.RecalculateBounds();
            var bounds = result.bounds; bounds.Expand(new Vector3(0, .5f, 0)); result.bounds = bounds;
            return result;
        }

        public void Update(bool visible, float time)
        {
            if (Root == null) return;
            bool active = visible && board.Session.Running && board.Session.View != null && board.Content.gameObject.activeInHierarchy;
            Root.gameObject.SetActive(active);
            if (active) material.SetFloat(TimeProperty, time);
        }

        public void Hide() { if (Root != null) Root.gameObject.SetActive(false); }

        public void Dispose()
        {
            Hide(); if (Root != null) Release(Root.gameObject);
            Release(mesh); Release(material); Root = null; mesh = null; material = null;
        }

        private static void Release(Object value)
        { if (value != null) { if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value); } }
    }
}
