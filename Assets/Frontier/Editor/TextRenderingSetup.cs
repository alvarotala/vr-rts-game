using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Frontier.Editor
{
    public static class TextRenderingSetup
    {
        private const string Folder = "Assets/Frontier/Resources/UI";

        [MenuItem("Frontier/Prepare SDF typography")]
        public static void Prepare()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            const string settingsPath = "Assets/Frontier/Resources/TMP Settings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TMP_Settings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
                AssetDatabase.SaveAssets();
            }
            string path = Folder + "/LegionSansSDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Frontier/Fonts/LiberationSans.ttf");
                if (source == null) throw new Exception("The bundled Liberation Sans source font is missing.");
                font = TMP_FontAsset.CreateFontAsset(source, 90, 12, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
                font.name = "Legion Sans SDF";
                // Bake both supported languages and the symbols used by controls.
                string characters = new string(Enumerable.Range(32, 95).Concat(Enumerable.Range(160, 96))
                    .Select(c => (char)c).ToArray()) + "\u2013\u2014\u2018\u2019\u201c\u201d\u2022\u2026\u2190\u2191\u2192\u2193\u2212";
                if (!font.TryAddCharacters(characters, out string missing))
                    throw new Exception("SDF atlas is missing characters: " + missing);
                font.atlasPopulationMode = AtlasPopulationMode.Static;
                font.atlasTextures[0].name = "Legion Sans SDF Atlas";
                font.atlasTextures[0].filterMode = FilterMode.Bilinear;
                font.atlasTextures[0].wrapMode = TextureWrapMode.Clamp;
                var shader = Shader.Find("TextMeshPro/Mobile/Distance Field SSD");
                if (shader == null) throw new Exception("The TMP screen-space derivative shader is missing.");
                font.material.shader = shader;
                font.material.name = "Legion Sans SDF";
                font.material.SetFloat("_Sharpness", 0);
                font.material.SetFloat("_OutlineWidth", 0);
                font.material.SetFloat("_OutlineSoftness", 0);
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            }
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            // All labels, including floating controller hints, use one plain
            // material. Apply this to existing assets as well as fresh atlases.
            font.material.DisableKeyword("OUTLINE_ON");
            font.material.SetFloat("_OutlineWidth", 0);
            font.material.SetFloat("_OutlineSoftness", 0);
            EditorUtility.SetDirty(font.material);
            AssetDatabase.DeleteAsset(Folder + "/LegionSansHint.mat");
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            serialized.FindProperty("m_defaultFontSize").floatValue = 18;
            serialized.FindProperty("m_defaultAutoSizeMinRatio").floatValue = .5f;
            serialized.FindProperty("m_defaultAutoSizeMaxRatio").floatValue = 2;
            serialized.FindProperty("m_EnableRaycastTarget").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(font); AssetDatabase.SaveAssets();
            Debug.Log("FRONTIER_SDF_ASSETS prepared: static Latin atlas, shared material without text outlines");
        }

        public static void Validate()
        {
            Prepare();
            MenuStylePreview.Render();
            LocalizationPreview.Run();
            UnifiedWheelPreview.Render();
            HarvestingMarkersPreview.Render();
        }

        public static void ValidatePlainText()
        {
            Prepare();
            LocalizationPreview.Run();
            TextEdgesPreview.Render();
        }
    }
}
