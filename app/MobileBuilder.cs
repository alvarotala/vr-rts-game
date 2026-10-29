using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Frontier.Mobile.Editor
{
    // This file is copied only to app/.generated, never imported by Quest.
    public static class MobileBuilder
    {
        public static void Build()
        {
            var args = Environment.GetCommandLineArgs();
            string platform = Argument(args, "-frontier-mobile-platform", "");
            if (platform != "android" && platform != "ios")
                throw new BuildFailedException("Choose android or ios with app/build.py.");
            string configuration = Argument(args, "-frontier-mobile-configuration", "development");
            if (configuration != "development" && configuration != "release")
                throw new BuildFailedException("Mobile configuration must be development or release.");
            bool release = configuration == "release";
            string applicationVersion = Argument(args, "-frontier-app-version", "0.1.0");
            if (!Regex.IsMatch(applicationVersion, @"\A(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\z"))
                throw new BuildFailedException("App version must use the X.Y.Z format.");
            string applicationBuild = Argument(args, "-frontier-build-number", "1");
            if (!Regex.IsMatch(applicationBuild, @"\A[0-9]+\z") || !int.TryParse(applicationBuild, out int buildNumber) || buildNumber <= 0)
                throw new BuildFailedException("Build number must be a positive 32-bit integer.");
            bool android = platform == "android";
            var target = android ? BuildTarget.Android : BuildTarget.iOS;
            var group = android ? BuildTargetGroup.Android : BuildTargetGroup.iOS;
            var namedTarget = android ? NamedBuildTarget.Android : NamedBuildTarget.iOS;
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                throw new BuildFailedException("Install " + target + " Build Support for this Unity Editor.");
            string version = Argument(args, "-frontier-editor-version", "");
            if (version != Application.unityVersion)
                throw new BuildFailedException("Use Unity " + version + " for this mobile build.");
            string output = Argument(args, "-frontier-mobile-output", "");
            if (!Path.IsPathRooted(output)) throw new BuildFailedException("Use an absolute mobile output path.");
            string buildId = Argument(args, "-frontier-build-id", Guid.NewGuid().ToString("N"));
            PlayerSettings.companyName = "Frontier";
            PlayerSettings.productName = "Little Legion";
            PlayerSettings.bundleVersion = applicationVersion;
            // A phone build must coexist with the Quest APK and its preferences.
            PlayerSettings.SetApplicationIdentifier(namedTarget, "app.sadmonkey.littlelegion.mobile");
            PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(namedTarget, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(namedTarget, ManagedStrippingLevel.Low);
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, "FRONTIER_BROWSER;FRONTIER_MOBILE");
            PlayerSettings.SetPreloadedAssets(Array.Empty<UnityEngine.Object>());
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            // HTTP is only available for local development builds; public defaults use HTTPS.
            PlayerSettings.insecureHttpOption = release ? InsecureHttpOption.NotAllowed : InsecureHttpOption.DevelopmentOnly;
            if (android)
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
                PlayerSettings.Android.bundleVersionCode = buildNumber;
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
                PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.OpenGLES3 });
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            }
            else
            {
                PlayerSettings.iOS.targetOSVersionString = "15.0";
                PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
                PlayerSettings.iOS.buildNumber = buildNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
                PlayerSettings.iOS.requiresFullScreen = true;
                PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
                PlayerSettings.SetGraphicsAPIs(target, new[] { GraphicsDeviceType.Metal });
            }
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
            const string scenePath = "Assets/Frontier/Mobile.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new BuildFailedException("Could not create the mobile scene.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(android ? Path.GetDirectoryName(output) : output);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath }, locationPathName = output,
                target = target, options = release ? BuildOptions.None : BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Mobile build failed: " + report.summary.result);
            string receipt = android ? Path.ChangeExtension(output, ".receipt.json") : Path.Combine(output, "build-receipt.json");
            File.WriteAllText(receipt, JsonUtility.ToJson(new Receipt
            { buildId = buildId, unityVersion = Application.unityVersion, platform = platform,
                configuration = configuration, version = applicationVersion, buildNumber = buildNumber }, true));
            Debug.Log("FRONTIER_MOBILE_BUILT platform=" + platform + " backend=IL2CPP path=" + output);
        }

        [Serializable] private sealed class Receipt { public string buildId, unityVersion, platform, configuration, version; public int buildNumber; }
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
