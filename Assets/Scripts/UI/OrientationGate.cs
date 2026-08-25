using UnityEngine;
using UnityEngine.UI;

namespace SmilyVolley
{
    /// <summary>
    /// Le jeu se joue en <b>paysage</b>. En portrait, un panneau plein écran le dit et met tout en
    /// attente.
    ///
    /// <para><b>Pourquoi refuser le portrait plutôt que s'y adapter.</b> Le terrain fait un peu plus
    /// de 16 unités de large ; <see cref="CameraFitter"/> garantit qu'on les voit toutes, en
    /// dézoomant autant qu'il faut. En portrait, ce dézoom réduirait les blobs à des pastilles au
    /// milieu de deux immenses bandes de ciel, et poserait les commandes tactiles par-dessus le
    /// terrain faute de place en bas. Ce n'est pas un défaut de mise en page qu'on corrige en
    /// réagençant des panneaux : c'est la taille de ce qu'on doit suivre des yeux, et elle décide de
    /// la jouabilité.</para>
    ///
    /// <para><b>Réservée au tactile.</b> Une fenêtre de bureau plus haute que large est un choix de
    /// l'utilisateur, pas une erreur de tenue de téléphone : la bloquer serait une régression sur la
    /// plateforme qui marche.</para>
    /// </summary>
    public class OrientationGate : MonoBehaviour
    {
        [Header("Références")]
        public MenuController menu;

        [Tooltip("Le panneau plein écran, éteint tant que l'appareil est tenu correctement.")]
        public GameObject panel;

        bool blocking;

        void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>
        /// La garde doit-elle s'interposer ?
        /// </summary>
        /// <remarks>
        /// <para>⚠ <see cref="TouchInput.TouchCapable"/> et non <see cref="TouchInput.Active"/>. Le
        /// second dit « le joueur se sert de ses doigts <i>en ce moment</i> » et bascule dans les
        /// deux sens — or un appui produit aussi un clic de compatibilité, qui le fait retomber.
        /// <b>Toucher l'écran refermerait la garde</b>, et le menu s'afficherait en portrait. Ce
        /// qu'il faut savoir ici n'est pas ce que le joueur fait, c'est ce que l'appareil
        /// <i>est</i>.</para>
        ///
        /// <para>La forme du canevas fait foi, et non <c>Screen.orientation</c> : voir
        /// <see cref="TouchZones.IsPortrait"/>.</para>
        /// </remarks>
        static bool ShouldBlock()
            => (Application.isMobilePlatform || TouchInput.TouchCapable) &&
               TouchZones.IsPortrait(Screen.width, Screen.height);

        void Update()
        {
            bool block = ShouldBlock();

            if (block != blocking)
            {
                blocking = block;
                if (panel != null) panel.SetActive(block);

                // ⚠ On ne restaure PAS une valeur mémorisée à l'entrée. Le joueur peut tourner son
                // téléphone pendant un menu, qui a lui aussi arrêté le temps ; rendre au jeu la
                // vitesse qu'il avait « avant » le relancerait derrière un panneau de pause. On
                // repose la question à qui détient la réponse.
                if (!block) Time.timeScale = menu != null && menu.IsOpen ? 0f : 1f;
            }

            // ⚠ Réaffirmé à CHAQUE image, et pas seulement à l'entrée : le menu remet le temps à 1
            // dès qu'il se ferme, et il se ferme sur une frappe clavier — que le panneau, qui ne
            // masque que la vue, n'empêche pas. Le jeu repartirait alors sans que personne ne le
            // voie, ce qui est exactement ce que cette garde existe pour empêcher.
            if (block) Time.timeScale = 0f;
        }

        void OnDestroy()
        {
            // Une garde active au moment où la scène se ferme laisserait le temps arrêté pour la
            // suivante.
            if (blocking) Time.timeScale = 1f;
        }
    }
}
