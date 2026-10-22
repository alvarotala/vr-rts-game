using System;
using System.IO;
using System.Linq;
using Frontier.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Editor
{
    public static class TextEdgesPreview
    {
        [MenuItem("Frontier/Preview SDF text edges")]
        public static void Render()
        {
            const string output = "TestResults/TextEdges";
            Directory.CreateDirectory(output);
            var preview = new PreviewRenderUtility();
            var root = new GameObject("SDF edge preview", typeof(RectTransform), typeof(Canvas));
            int checks = 0, captures = 0;
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var label = new GameObject("Glyph", typeof(RectTransform), typeof(LocalizedText)).GetComponent<LocalizedText>();
                label.transform.SetParent(root.transform, false);
                label.rectTransform.sizeDelta = new Vector2(100, 100);
                label.transform.localScale = Vector3.one * .01f;
                label.text = "O"; label.fontSize = 80; label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
                preview.camera.orthographic = true; preview.camera.orthographicSize = .7f;
                preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 10;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = Color.black;
                preview.camera.transform.SetPositionAndRotation(new Vector3(0, 0, -2), Quaternion.identity);
                foreach (float angle in new[] { 0f, 40f, 60f })
                foreach (int size in new[] { 48, 192 })
                {
                    label.fontSharedMaterial = UiTypography.Font.material;
                    label.transform.localRotation = Quaternion.Euler(0, angle, 0);
                    label.ForceMeshUpdate();
                    var material = label.materialForRendering;
                    Check(material.mainTexture == UiTypography.Font.atlasTexture, "Renderer keeps its actual SDF atlas");
                    preview.BeginStaticPreview(new Rect(0, 0, size, size));
                    preview.DrawMesh(label.mesh, label.transform.localToWorldMatrix, material, 0);
                    preview.Render(true);
                    var image = preview.EndStaticPreview();
                    try
                    {
                        var pixels = image.GetPixels32();
                        Check(pixels.Count(p => p.r > 32) > 10, "Glyph remains visible at reduced resolution and angle");
                        Check(pixels.Count(p => p.r > 20 && p.r < 235) > 10, "Contours include antialiased coverage");
                        Check(image.GetPixel(size / 2, size / 2).r < .1f, "O retains its open center instead of a filled glyph quad");
                        File.WriteAllBytes($"{output}/plain-{angle:0}-{size}.png", image.EncodeToPNG());
                        captures++;
                    }
                    finally { Object.DestroyImmediate(image); }
                }
                Debug.Log($"FRONTIER_TEXT_EDGES passed: {checks} checks, {captures} renders; no physical Quest validation");
                void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; }
            }
            finally { Object.DestroyImmediate(root); preview.Cleanup(); }
        }
    }
}
