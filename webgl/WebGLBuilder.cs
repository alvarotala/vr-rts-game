using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Frontier.Browser.Editor
{
    // Only copied into the generated browser project. Never configures Quest/XR.
    public static class WebGLBuilder
    {
        [MenuItem("Frontier/Build Web player")]
        public static void Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new BuildFailedException("Install Web Build Support for this Unity Editor.");
            string[] args = Environment.GetCommandLineArgs();
            string expectedVersion = Argument(args, "-frontier-editor-version", Application.unityVersion);
            if (Application.unityVersion != expectedVersion)
                throw new BuildFailedException("Use Unity " + expectedVersion + " for this browser build.");
            string buildId = Argument(args, "-frontier-build-id", Guid.NewGuid().ToString("N"));
            int index = Array.IndexOf(args, "-frontier-webgl-output");
            string output = index >= 0 && index + 1 < args.Length ? args[index + 1] : Path.GetFullPath("Builds/WebGL");
            if (!Path.IsPathRooted(output)) throw new BuildFailedException("Use an absolute Web output path.");
            PlayerSettings.companyName = "Frontier";
            PlayerSettings.productName = "Little Legion";
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.WebGL, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, "FRONTIER_BROWSER");
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.SetPreloadedAssets(Array.Empty<UnityEngine.Object>());
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.WebGL.maximumMemorySize = 1024;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:LittleLegion";
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Frontier/Settings/MobileURP.asset");
            if (pipeline == null) throw new BuildFailedException("The shared rendering pipeline is missing.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int quality = 0; quality < QualitySettings.names.Length; quality++)
            {
                QualitySettings.SetQualityLevel(quality, false);
                QualitySettings.renderPipeline = pipeline;
                QualitySettings.vSyncCount = 0;
            }
            RetainShaders();
            const string scenePath = "Assets/Frontier/Browser.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new BuildFailedException("Could not create the browser scene.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath }, locationPathName = output,
                target = BuildTarget.WebGL, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Web build failed: " + report.summary.result);
            File.WriteAllText(Path.Combine(output, "build-receipt.json"),
                JsonUtility.ToJson(new BuildReceipt { buildId = buildId, unityVersion = Application.unityVersion }, true));
            Debug.Log("FRONTIER_WEBGL_BUILT target=WebGL backend=IL2CPP path=" + output);
        }

        [Serializable] private sealed class BuildReceipt { public string buildId, unityVersion; }
        private static string Argument(string[] args, string key, string fallback)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        private static void RetainShaders()
        {
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (string name in new[] { "Frontier/Board", "Frontier/Impact", "Frontier/Weapons", "Frontier/Remains", "Frontier/Fog", "Frontier/ContactShadows", "Universal Render Pipeline/Unlit", "UI/Default" })
            {
                var shader = Shader.Find(name);
                if (shader == null) throw new BuildFailedException("Shader not found: " + name);
                bool found = false;
                for (int i = 0; i < shaders.arraySize; i++)
                    if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader) found = true;
                if (found) continue;
                int slot = shaders.arraySize;
                shaders.InsertArrayElementAtIndex(slot);
                shaders.GetArrayElementAtIndex(slot).objectReferenceValue = shader;
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
