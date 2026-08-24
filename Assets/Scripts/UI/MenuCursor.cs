using UnityEngine;
using UnityEngine.UI;

namespace SmilyVolley
{
    /// <summary>
    /// Le blob qui montre la ligne choisie. Il remplace le bandeau de sélection : un
    /// rectangle de couleur dit « cette ligne » sans rien dire du jeu, là où un blob qui
    /// saute d'une entrée à l'autre est déjà le personnage qu'on va conduire.
    ///
    /// Tout se calcule en temps non affecté par l'échelle : le menu fige le jeu à
    /// <c>timeScale</c> zéro, une animation qui s'appuierait sur <c>Time.deltaTime</c> ne
    /// bougerait jamais.
    /// </summary>
    public class MenuCursor : MonoBehaviour
    {
        public RectTransform rect;
        public Image image;

        [Header("Déplacement")]
        [Tooltip("Temps mis pour couvrir l'essentiel du trajet vers la ligne visée.")]
        public float travelTime = 0.12f;

        [Header("Gelée")]
        [Tooltip("Écrasement maximal pendant le saut, en proportion de la taille.")]
        public float squash = 0.28f;
        [Tooltip("Vitesse au-delà de laquelle l'écrasement est à son maximum, en pixels par seconde.")]
        public float squashSpeed = 1400f;
        [Tooltip("Respiration au repos : amplitude et vitesse du souffle de la gelée.")]
        public float breath = 0.035f;
        public float breathSpeed = 2.6f;

        float targetY;
        float currentY;
        float velocity;
        bool placed;

        /// <summary>Vise une ligne. Le premier appel y pose le blob sans le faire voyager.</summary>
        public void Follow(float y)
        {
            targetY = y;

            if (!placed)
            {
                currentY = y;
                velocity = 0f;
                placed = true;
                Apply();
            }
        }

        public void SetVisible(bool visible)
        {
            if (image != null) image.enabled = visible;
            // Un curseur caché puis remontré doit reparaître là où on le rappelle, pas
            // traverser l'écran depuis l'écran précédent.
            if (!visible) placed = false;
        }

        void Update()
        {
            if (rect == null || !placed) return;

            float dt = Time.unscaledDeltaTime;
            currentY = Mathf.SmoothDamp(currentY, targetY, ref velocity, travelTime, Mathf.Infinity, dt);
            Apply();
        }

        /// <summary>
        /// Position et déformation. Le blob s'aplatit dans le sens de son déplacement puis
        /// s'étire à l'arrêt : c'est la même gelée que sur le terrain, et c'est elle qui
        /// fait lire le saut comme un mouvement plutôt que comme un clignotement.
        /// </summary>
        void Apply()
        {
            Vector2 position = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(position.x, currentY);

            float rush = Mathf.Clamp01(Mathf.Abs(velocity) / Mathf.Max(1f, squashSpeed));
            float souffle = Mathf.Sin(Time.unscaledTime * breathSpeed) * breath;

            // Volume à peu près constant : ce qu'on perd en hauteur, on le prend en largeur.
            float y = 1f - squash * rush + souffle;
            float x = 1f + squash * rush - souffle;
            rect.localScale = new Vector3(x, y, 1f);
        }
    }
}
