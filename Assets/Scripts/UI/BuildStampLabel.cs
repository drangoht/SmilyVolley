using UnityEngine;
using UnityEngine.UI;

namespace SmilyVolley
{
    /// <summary>
    /// Écrit le tampon de build dans son texte, au lancement.
    ///
    /// <para>Il vit sur son propre canevas plutôt que dans le HUD : le HUD s'éteint dès qu'un menu
    /// s'ouvre, et c'est justement sur le menu que la plupart des captures d'écran sont prises.</para>
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class BuildStampLabel : MonoBehaviour
    {
        void Awake() => GetComponent<Text>().text = BuildInfo.Label;
    }
}
