using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SmilyVolley.EditorTools
{
    /// <summary>Génération du build Windows, utilisable depuis le menu ou en ligne de commande.</summary>
    public static class BuildTools
    {
        const string OutputDirectory = "Build/Windows";
        const string ExecutableName = "SmilyVolley.exe";

        /// <summary>Reconstruit la scène puis compile le build : point d'entrée pour la ligne de commande.</summary>
        public static void RebuildEverything()
        {
            RenderPipelineSetup.Apply();
            SceneBuilder.Build();
            BuildWindows();
        }

        [MenuItem("Smily Volley/Compiler le build Windows")]
        public static void BuildWindows()
        {
            ConfigurePlayerSettings();
            Directory.CreateDirectory(OutputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                locationPathName = OutputDirectory + "/" + ExecutableName,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"Build réussi : {summary.outputPath} ({summary.totalSize / 1024 / 1024} Mo)");
            }
            else
            {
                Debug.LogError($"Build en échec : {summary.result} ({summary.totalErrors} erreurs)");
            }
        }

        [MenuItem("Smily Volley/Appliquer les réglages du projet")]
        public static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Smily";
            PlayerSettings.productName = "Smily Volley";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultIsNativeResolution = false;
            AssetDatabase.SaveAssets();
        }
    }
}