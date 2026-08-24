using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SmilyVolley.EditorTools
{
    /// <summary>Génération des builds Windows et web, utilisable depuis le menu ou en ligne de commande.</summary>
    public static class BuildTools
    {
        const string OutputDirectory = "Build/Windows";
        const string WebOutputDirectory = "Build/Web";
        const string ExecutableName = "SmilyVolley.exe";
        const string ShaAssetPath = "Assets/Resources/build_sha.txt";

        /// <summary>Reconstruit la scène puis compile le build Windows : point d'entrée en ligne de commande.</summary>
        public static void RebuildEverything()
        {
            RenderPipelineSetup.Apply();
            SceneBuilder.Build();
            BuildWindows();
        }

        /// <summary>Reconstruit la scène puis compile la version web : point d'entrée en ligne de commande.</summary>
        public static void RebuildWeb()
        {
            RenderPipelineSetup.Apply();
            SceneBuilder.Build();
            BuildWeb();
        }

        [MenuItem("Smily Volley/Compiler le build Windows")]
        public static void BuildWindows()
        {
            ConfigurePlayerSettings();
            StampGitSha();
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
                WriteBuildStamp(OutputDirectory);
            }
            else
            {
                Debug.LogError($"Build en échec : {summary.result} ({summary.totalErrors} erreurs)");
            }
        }

        // ------------------------------------------------------------------ web

        /// <summary>
        /// Compile la version jouable dans un navigateur. Sortie : <c>Build/Web</c>, à pousser telle
        /// quelle sur itch.io.
        /// </summary>
        /// <remarks>
        /// Les réglages web sont posés ici plutôt que laissés à l'éditeur : un réglage fait à la
        /// souris ne vaut que sur le poste où il a été fait, et se perd au premier clone du dépôt.
        /// </remarks>
        [MenuItem("Smily Volley/Compiler la version web")]
        public static void BuildWeb()
        {
            ConfigurePlayerSettings();
            StampGitSha();
            ApplyWebSettings();
            Directory.CreateDirectory(WebOutputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { SceneBuilder.ScenePath },
                // ⚠ En WebGL, Unity attend un DOSSIER et non un fichier : il y écrit index.html et Build/.
                locationPathName = WebOutputDirectory,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Build web en échec : {summary.result} ({summary.totalErrors} erreurs)");
                return;
            }

            Debug.Log($"Build web réussi : {summary.outputPath} ({summary.totalSize / 1024 / 1024} Mo)");
            WriteBuildStamp(WebOutputDirectory);
            StampWebCacheBuster(WebOutputDirectory);
        }

        /// <summary>
        /// Réglages du lecteur web. Chacun corrige un défaut qui ne se voit pas à la compilation :
        /// ils produisent un jeu qui démarre, puis se comporte mal.
        /// </summary>
        static void ApplyWebSettings()
        {
            NamedBuildTarget web = NamedBuildTarget.WebGL;

            // Brotli comprime nettement mieux que gzip sur du WebAssembly, mais le navigateur ne sait
            // le décompresser que si le serveur annonce l'encodage. Le repli JS rend le build
            // indépendant de cette configuration : il tourne sur itch.io comme sur n'importe quel
            // hébergement statique.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;

            // Les deux musiques pèsent l'essentiel du .data et ne changent pas d'une visite à
            // l'autre : sans ce cache, chaque partie les retélécharge.
            PlayerSettings.WebGL.dataCaching = true;

            // Un terrain, deux blobs, une balle : le jeu n'alloue presque rien une fois la mise en
            // place faite. 128 Mo évitent les paliers de croissance du tas sans peser au chargement.
            PlayerSettings.WebGL.initialMemorySize = 128;
            PlayerSettings.WebGL.maximumMemorySize = 512;

            // ⚠ WebGL est la seule plateforme dont le niveau de stripping par défaut est le plus
            // agressif. L'Input System résout ses couches de contrôle par réflexion : au niveau
            // élevé, le jeu démarre normalement et ne répond plus au clavier.
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.Low);

            // Les exceptions explicitement levées gardent leur pile dans la console du navigateur :
            // c'est le seul moyen d'instruire un défaut qu'on ne reproduit pas hors du navigateur.
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            // La toile par défaut d'Unity est en 960 x 600, soit du 16/10 : le jeu, composé pour du
            // 16/9, s'y retrouvait bordé de bandes sombres. Ces deux valeurs alimentent aussi les
            // dimensions écrites dans la page, dont le cadrage se déduit.
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;

            // Le gabarit du projet : cadre 16/9 centré sur fond de plage, confisque les touches que
            // le navigateur détourne (l'espace et les flèches font défiler la page), réveille le
            // contexte audio et porte la garde-cache.
            PlayerSettings.WebGL.template = "PROJECT:SmilyVolley";
            PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;

            Debug.Log($"Réglages web : tas {PlayerSettings.WebGL.initialMemorySize} Mo, " +
                      $"{PlayerSettings.WebGL.compressionFormat} (repli {PlayerSettings.WebGL.decompressionFallback}), " +
                      $"stripping Low, gabarit {PlayerSettings.WebGL.template}.");
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

        // ------------------------------------------------------------------ tampon de build

        /// <summary>
        /// Pose l'identité git du code qu'on s'apprête à construire, dans la ressource que le jeu lit
        /// pour afficher son tampon. Appelé <b>avant</b> le build, sans quoi le binaire embarquerait
        /// la valeur précédente.
        /// </summary>
        /// <remarks>
        /// Écrite ici et non par le script de publication : posée seulement au moment de publier,
        /// elle resterait ensuite en place, et tout build local suivant afficherait le SHA de la
        /// dernière release — un garde-fou de fraîcheur qui se trompe est pire que pas de garde-fou,
        /// puisqu'on lui fait confiance.
        /// </remarks>
        static void StampGitSha()
        {
            string sha = Git("rev-parse --short HEAD");

            if (sha.Length == 0)
            {
                // Pas de dépôt, ou pas de git dans le PATH : « dev » avoue l'ignorance, là où un SHA
                // périmé prétendrait savoir.
                sha = "dev";
            }
            else if (HasLocalChanges())
            {
                sha += "+";
            }

            string full = Path.GetFullPath(ShaAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));

            bool isNew = !File.Exists(full);
            File.WriteAllText(full, sha);

            // Le fichier est ignoré par git : sur un clone frais il n'existe pas encore, et la base
            // d'assets ne le connaît donc pas — un ImportAsset seul ne l'y ferait pas entrer.
            if (isNew) AssetDatabase.Refresh();

            // Sans réimport, le build embarquerait la valeur que la base d'assets a en mémoire.
            AssetDatabase.ImportAsset(ShaAssetPath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"Identité git : {sha}");
        }

        /// <summary>
        /// Le dépôt porte-t-il des modifications autres que celles qu'une publication pose
        /// elle-même ?
        /// </summary>
        static bool HasLocalChanges()
        {
            foreach (string line in Git("status --porcelain").Split('\n'))
            {
                string entry = line.Trim();
                if (entry.Length == 0) continue;

                // « XY chemin » : le statut tient sur les deux premières colonnes.
                string path = entry.Length > 2 ? entry.Substring(2).Trim().Replace('\\', '/') : "";

                if (path.EndsWith("Assets/Resources/build_sha.txt", StringComparison.Ordinal)) continue;
                if (path.EndsWith("ProjectSettings/ProjectSettings.asset", StringComparison.Ordinal)) continue;

                return true;
            }

            return false;
        }

        /// <summary>Écrit, à côté du build, la carte d'identité de ce qui vient d'être construit.</summary>
        /// <remarks>
        /// C'est le seul contrôle honnête de fraîcheur : les métadonnées d'un binaire Unity décrivent
        /// le <i>moteur</i> et non le jeu, et l'horodatage ne vaut pas mieux, le build étant
        /// incrémental — un fichier identique n'est pas réécrit. Ce tampon-ci est produit par le
        /// build : il ne peut pas annoncer une version que le build n'a pas posée. Le script de
        /// publication le lit avant de pousser.
        /// </remarks>
        static void WriteBuildStamp(string directory)
        {
            string sha = ReadSha();
            string date = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

            string json = "{\n" +
                          $"  \"version\": \"{PlayerSettings.bundleVersion}\",\n" +
                          $"  \"sha\": \"{sha}\",\n" +
                          $"  \"date\": \"{date}\",\n" +
                          $"  \"engine\": \"{Application.unityVersion}\"\n" +
                          "}\n";

            File.WriteAllText(Path.Combine(directory, "build_stamp.json"), json);
            Debug.Log($"Tampon de build : v{PlayerSettings.bundleVersion}-{sha}");
        }

        /// <summary>Remplace <c>__BUILD_ID__</c> dans la page par une empreinte propre à ce build.</summary>
        /// <remarks>
        /// Sans elle, un navigateur qui a déjà vu la page ressert le chargeur d'un build et le wasm
        /// d'un autre : le jeu ne démarre plus, et le seul indice est un message d'erreur qui ne
        /// change pas alors que le build, lui, a changé. L'horodatage s'ajoute au SHA parce que deux
        /// builds locaux d'affilée partagent le même commit et doivent quand même se distinguer ; il
        /// invalide aussi le cache IndexedDB d'Unity, qui indexe par URL.
        /// </remarks>
        static void StampWebCacheBuster(string directory)
        {
            string indexPath = Path.Combine(directory, "index.html");
            if (!File.Exists(indexPath))
            {
                Debug.LogWarning("index.html introuvable : pas de garde-cache posée.");
                return;
            }

            string buildId = ReadSha() + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            string html = File.ReadAllText(indexPath);

            if (!html.Contains("__BUILD_ID__"))
            {
                // Le gabarit a été modifié sans que le jeton y survive : le dire fort, sans quoi le
                // défaut ne se manifestera que chez un joueur, sous la forme d'un jeu qui ne démarre pas.
                Debug.LogWarning("__BUILD_ID__ absent du gabarit : le navigateur pourra mélanger deux builds.");
                return;
            }

            File.WriteAllText(indexPath, html.Replace("__BUILD_ID__", buildId));
            Debug.Log($"Garde-cache : {buildId}");
        }

        static string ReadSha()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(ShaAssetPath);
            return asset != null && asset.text.Trim().Length > 0 ? asset.text.Trim() : "dev";
        }

        /// <summary>Exécute une commande git à la racine du projet. Chaîne vide si git est indisponible.</summary>
        static string Git(string arguments)
        {
            try
            {
                var info = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = Path.GetDirectoryName(Application.dataPath),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = Process.Start(info);
                if (process == null) return string.Empty;

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return process.ExitCode == 0 ? output.Trim() : string.Empty;
            }
            catch (Exception error)
            {
                Debug.LogWarning($"git indisponible : {error.Message}");
                return string.Empty;
            }
        }
    }
}
