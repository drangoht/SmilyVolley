using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SmilyVolley.EditorTools
{
    /// <summary>
    /// Active le Universal Render Pipeline sur tous les niveaux de qualité.
    /// Le Built-in Render Pipeline est déprécié depuis Unity 6 ; le jeu passe par le
    /// Renderer 2D d'URP, qui rend les sprites et ouvre l'accès à l'éclairage 2D.
    ///
    /// Unity stocke le pipeline actif dans QualitySettings, niveau par niveau : ne renseigner
    /// que le pipeline par défaut de GraphicsSettings laisserait les autres niveaux en Built-in.
    /// </summary>
    public static class RenderPipelineSetup
    {
        public const string PipelineAssetPath = "Assets/Settings/UniversalRP.asset";
        public const string GlobalSettingsPath = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";

        [MenuItem("Smily Volley/Activer le pipeline URP")]
        public static void Apply()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                Debug.LogError("Pipeline URP introuvable : " + PipelineAssetPath);
                return;
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;

            int previousLevel = QualitySettings.GetQualityLevel();
            int levelCount = QualitySettings.names.Length;
            for (int level = 0; level < levelCount; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(previousLevel, false);

            // Les réglages globaux portent notamment le volume profile par défaut. Sans cette
            // affectation explicite, l'éditeur en fabriquerait un au premier lancement.
            // UniversalRenderPipelineGlobalSettings est internal : on passe par la classe de base.
            var globalSettings = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(GlobalSettingsPath);
            if (globalSettings != null)
            {
                EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(globalSettings);
            }
            else
            {
                Debug.LogWarning("Réglages globaux URP introuvables : " + GlobalSettingsPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"URP actif sur {levelCount} niveau(x) de qualité : {PipelineAssetPath}");
        }
    }
}
