using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Carte d'identité du binaire : le numéro de version publié et le commit dont il est issu.
    ///
    /// <para>Le tampon affiché en bas de l'écran n'est pas là pour le joueur mais pour le
    /// <b>rapport de bug</b> : sans lui, une capture d'écran ne dit pas quelle version elle
    /// montre, et une partie de test peut porter sur un build périmé sans que personne ne s'en
    /// aperçoive — ce qui compte double pour une page web, où le navigateur sert volontiers un
    /// ancien fichier depuis son cache.</para>
    ///
    /// <para>La version vient des réglages du projet ; le SHA vit dans une ressource écrite par le
    /// build lui-même, et non compilée en dur : il doit désigner le commit dont ce binaire est
    /// issu, connu au dernier moment.</para>
    /// </summary>
    public static class BuildInfo
    {
        /// <summary>Ressource écrite par le build : une ligne, le SHA court.</summary>
        public const string ResourcePath = "build_sha";

        static string sha;

        /// <summary>Version du projet, telle qu'elle sera publiée.</summary>
        public static string Version => Application.version;

        /// <summary>
        /// SHA court du commit dont ce binaire est issu — suffixé <c>+</c> si l'arbre de travail
        /// portait des modifications, <c>dev</c> si git n'a rien pu dire.
        /// </summary>
        /// <remarks>
        /// Les trois cas ne disent pas la même chose : un SHA nu désigne un commit qu'on peut
        /// ressortir ; un SHA suffixé prévient que le binaire ne correspond à aucun commit ;
        /// « dev » avoue une ignorance, là où un SHA périmé prétendrait savoir.
        /// </remarks>
        public static string GitSha
        {
            get
            {
                if (sha != null) return sha;

                var asset = Resources.Load<TextAsset>(ResourcePath);
                sha = asset != null && asset.text.Trim().Length > 0 ? asset.text.Trim() : "dev";
                return sha;
            }
        }

        /// <summary>Libellé affiché en jeu : <c>v1.0.0-a1b2c3d</c>.</summary>
        public static string Label => $"v{Version}-{GitSha}";
    }
}
