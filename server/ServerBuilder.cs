using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Frontier.Dedicated.Editor
{
    public static class ServerBuilder
    {
        public static void BuildLinux()
        {
            string[] args = Environment.GetCommandLineArgs();
            string output = Argument(args, "-frontier-server-output", Argument(args, "-buildOutput", null));
            if (string.IsNullOrWhiteSpace(output) || !Path.IsPathRooted(output))
                throw new BuildFailedException("Pass an absolute -frontier-server-output path.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64))
                throw new BuildFailedException("Linux Dedicated Server build support is required for this Unity editor.");

            PlayerSettings.companyName = "Frontier";
            PlayerSettings.productName = "Little Legion Server";
            PlayerSettings.runInBackground = true;
            // The trusted control plane uses HTTP on the private Docker network.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            // Ahead-of-time compilation also avoids Mono JIT address assumptions
            // when the production Linux amd64 image is tested under emulation.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Server, ApiCompatibilityLevel.NET_Unity_4_8);
            EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
            const string scenePath = "Assets/Server.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new BuildFailedException("Could not create the server scene.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath }, locationPathName = output, target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Dedicated server build failed: " + report.summary.result);
            Debug.Log("FRONTIER_SERVER_BUILT target=StandaloneLinux64 subtarget=Server backend=IL2CPP path=" + output);
        }

        private static string Argument(string[] args, string name, string fallback)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
    }
}
