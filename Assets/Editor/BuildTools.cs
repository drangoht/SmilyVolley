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
        const string SplashPath = "Assets/Art/splash-screen.jpg";

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
            ConfigureSplashScreen();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Écran de lancement : l'affiche du jeu, là où Unity montre son gris par défaut.
        /// C'est la première image du jeu, autant que ce soit la bonne.
        ///
        /// <b>L'affiche est un logo, pas le fond.</b> Passée en <c>background</c>, elle
        /// s'affiche méconnaissable — Unity garde ce fond en très basse résolution pour
        /// pouvoir le montrer avant tout chargement, et le titre en devient illisible. Ni
        /// la taille maximale de la texture ni la compression n'y changent rien : essayées
        /// en 4096 non compressé, le flou est identique. En logo, la même image est nette.
        /// Le fond retombe donc sur un aplat de ciel, qui reste dans la palette du jeu.
        ///
        /// Le logo Unity est imposé par la licence Personal ; il passe sous l'affiche plutôt
        /// que de flotter seul, en version sombre puisque le fond est clair.
        /// </summary>
        static void ConfigureSplashScreen()
        {
            var affiche = AssetDatabase.LoadAssetAtPath<Sprite>(SplashPath);
            if (affiche == null)
            {
                Debug.LogWarning("Affiche introuvable, écran de lancement inchangé : " + SplashPath);
                return;
            }

            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.background = null;
            PlayerSettings.SplashScreen.backgroundColor = SplashFallback;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.UnityLogoBelow;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.DarkOnLight;
            PlayerSettings.SplashScreen.logos = new[]
            {
                PlayerSettings.SplashScreenLogo.Create(SplashSeconds, affiche)
            };
            // Voile noir derrière les logos : la licence Personal le bloque à 0,5. On
            // demande zéro et on note ce qu'Unity a retenu, pour qu'un écran assombri ne
            // passe pas pour un bug.
            PlayerSettings.SplashScreen.overlayOpacity = 0f;

            Debug.Log("Écran de lancement : voile à " + PlayerSettings.SplashScreen.overlayOpacity);
        }

        // Fond de l'écran de lancement : le bleu du ciel, plutôt que le gris d'usine.
        static readonly Color SplashFallback = new Color(0.62f, 0.82f, 0.95f);
        // Deux secondes : le minimum qu'Unity accepte pour un logo.
        const float SplashSeconds = 2f;
    }
}
