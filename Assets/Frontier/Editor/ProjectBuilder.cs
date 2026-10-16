using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.OpenXR;
using UnityEditor.XR.OpenXR.Features;

namespace Frontier.Editor
{
    public static class ProjectBuilder
    {
        private const string ScenePath="Assets/Frontier/Scenes/Frontier.unity";
        [MenuItem("Frontier/Configure project")]
        public static void Configure()
        {
            Directory.CreateDirectory("Assets/Frontier/Settings");Directory.CreateDirectory("Assets/Frontier/Scenes");
            PlayerSettings.companyName="Frontier";PlayerSettings.productName="Frontier Command";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.alvaro.frontiercommand");
            PlayerSettings.bundleVersion="0.1.5";PlayerSettings.Android.bundleVersionCode=6;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
            PlayerSettings.runInBackground=false;
            PlayerSettings.enableFrameTimingStats=true;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=1000;
            // Meta's linked desktop assemblies reference capture APIs. The playtest never requests them.
            PlayerSettings.macOS.microphoneUsageDescription="Audio for local virtual reality testing.";
            PlayerSettings.macOS.cameraUsageDescription="Camera for local mixed reality testing.";
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=settings.FindProperty("activeInputHandler");if(input!=null)input.intValue=1;settings.ApplyModifiedPropertiesWithoutUndo();
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Frontier/Settings/MobileURP.asset");
            if(pipeline==null)
            {
                var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();AssetDatabase.CreateAsset(renderer,"Assets/Frontier/Settings/MobileRenderer.asset");
                pipeline=UniversalRenderPipelineAsset.Create(renderer);pipeline.name="Frontier Mobile";AssetDatabase.CreateAsset(pipeline,"Assets/Frontier/Settings/MobileURP.asset");
            }
            pipeline.msaaSampleCount=4;pipeline.renderScale=1;pipeline.shadowDistance=0;pipeline.supportsHDR=false;
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;QualitySettings.vSyncCount=0;
            var xr=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>("Assets/Frontier/Settings/XRSettings.asset");
            if(xr==null){xr=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();AssetDatabase.CreateAsset(xr,"Assets/Frontier/Settings/XRSettings.asset");}
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,xr,true);
            var android=xr.SettingsForBuildTarget(BuildTargetGroup.Android);
            if(android==null)
            {
                android=ScriptableObject.CreateInstance<XRGeneralSettings>();android.name="Quest XR";AssetDatabase.AddObjectToAsset(android,xr);
                var manager=ScriptableObject.CreateInstance<XRManagerSettings>();manager.name="Quest loaders";AssetDatabase.AddObjectToAsset(manager,xr);android.Manager=manager;
                xr.SetSettingsForBuildTarget(BuildTargetGroup.Android,android);
            }
            android.InitManagerOnStart=true;
            if(!XRPackageMetadataStore.AssignLoader(android.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",BuildTargetGroup.Android))
                throw new BuildFailedException("Could not configure the OpenXR loader.");
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var openxr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if(openxr==null)throw new BuildFailedException("OpenXR settings were not generated.");
            openxr.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;
            foreach(var feature in openxr.GetFeatures())
            {
                string type=feature.GetType().Name;
                if(type=="OculusTouchControllerProfile"||type=="MetaQuestFeature"||type=="MetaXRFeature"||type=="OpenXRCompositionLayersFeature")
                {feature.enabled=true;EditorUtility.SetDirty(feature);Debug.Log("FRONTIER_XR_ENABLED "+type);}
                else if(type=="DetachedOculusTouchControllerProfile"){feature.enabled=false;EditorUtility.SetDirty(feature);}
            }
            var config=OVRProjectConfig.CachedProjectConfig;
            config.targetDeviceTypes.Clear();config.targetDeviceTypes.Add(OVRProjectConfig.DeviceType.Quest3);
            config.handTrackingSupport=OVRProjectConfig.HandTrackingSupport.ControllersOnly;
            config.anchorSupport=OVRProjectConfig.AnchorSupport.Enabled;
            config.sceneSupport=OVRProjectConfig.FeatureSupport.Supported;
            config.insightPassthroughSupport=OVRProjectConfig.FeatureSupport.Required;
            config.boundaryVisibilitySupport=OVRProjectConfig.FeatureSupport.Supported;
            OVRProjectConfig.CommitProjectConfig(config);
            if(!File.Exists(ScenePath))
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            // Shader.Find assets must be explicitly retained in player builds.
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach(string name in new[]{"Frontier/Board","Frontier/Impact","Frontier/Weapons","Universal Render Pipeline/Unlit","UI/Default"})
            {
                var shader=Shader.Find(name);if(shader==null)throw new BuildFailedException("Shader not found: "+name);
                bool found=false;for(int i=0;i<shaders.arraySize;i++)if(shaders.GetArrayElementAtIndex(i).objectReferenceValue==shader)found=true;
                if(!found){int i=shaders.arraySize;shaders.InsertArrayElementAtIndex(i);shaders.GetArrayElementAtIndex(i).objectReferenceValue=shader;}
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(xr);EditorUtility.SetDirty(android);EditorUtility.SetDirty(android.Manager);EditorUtility.SetDirty(openxr);EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("FRONTIER_CONFIGURED");
        }
        [MenuItem("Frontier/Build Quest APK")]
        public static void BuildQuest()
        {
            Configure();
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))throw new BuildFailedException("Android Build Support is missing.");
            PrepareOutput("Builds/Quest");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Quest/FrontierCommand.apk",target=BuildTarget.Android,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Quest build failed: "+report.summary.result);
            Debug.Log("FRONTIER_APK_BUILT "+report.summary.totalSize);
        }
        [MenuItem("Frontier/Build desktop playtest")]
        public static void BuildDesktop()
        {
            Configure();PrepareOutput("Builds/Desktop");
            // MRUK 203 ships a standalone macOS library for Apple Silicon only.
            EditorUserBuildSettings.SetPlatformSettings(BuildPipeline.GetBuildTargetName(BuildTarget.StandaloneOSX),"Architecture","ARM64");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Desktop/FrontierCommand.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Desktop playtest failed.");
        }
        private static void PrepareOutput(string directory)
        {
            Directory.CreateDirectory(directory);
            // Meta 203 copies this generated sidecar without overwrite on repeated builds.
            string bindings=Path.Combine(directory,"RuntimeActionBindings.json");
            if(File.Exists(bindings))File.Delete(bindings);
        }
        public static void ValidateContent()
        {
            var catalog=new Core.Catalog(JsonUtility.FromJson<Core.CatalogDefinition>(File.ReadAllText("Assets/Frontier/Resources/catalog.json")));
            foreach(string path in Directory.GetFiles("Assets/Frontier/Resources/Maps","*.json"))
            {var map=JsonUtility.FromJson<Core.MapDefinition>(File.ReadAllText(path));_ = new Core.Simulation(map,catalog);Debug.Log("FRONTIER_MAP_VALID "+map.id);}
        }
    }

    // OpenXR adds settings even without an active loader. The desktop playtest has
    // no XR runtime, and Meta's Android feature assets cannot deserialize on macOS.
    public sealed class DesktopXRBuildFilter:IPreprocessBuildWithReport
    {
        public int callbackOrder=>10000;
        public void OnPreprocessBuild(BuildReport report)
        {
            PlayerSettings.SetPreloadedAssets(PlayerSettings.GetPreloadedAssets()
                .Where(asset=>asset!=null&&asset.GetType().FullName!="UnityEditor.XR.Simulation.XRSimulationSettings"
                    &&(report.summary.platform!=BuildTarget.StandaloneOSX||!(asset is OpenXRSettings)&&!(asset is XRGeneralSettings))).ToArray());
        }
    }
}
