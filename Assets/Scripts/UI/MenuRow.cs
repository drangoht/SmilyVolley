using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmilyVolley
{
    /// <summary>
    /// Une ligne de menu : un libellé à gauche, une valeur à droite et deux boutons − et +
    /// pour la régler à la souris. Les lignes sont créées une fois par <c>SceneBuilder</c>
    /// puis réutilisées d'un écran à l'autre — un menu qui reconstruit son interface à
    /// chaque ouverture génère des déchets pour rien.
    ///
    /// La ligne choisie ne porte plus de bandeau : c'est le blob de <see cref="MenuCursor"/>
    /// qui la désigne, et le libellé s'écarte pour lui faire place. Le rectangle de couleur
    /// disait « cette ligne » sans rien dire du jeu.
    /// </summary>
    public class MenuRow : MonoBehaviour, IPointerEnterHandler
    {
        public RectTransform rect;
        [Tooltip("Reste transparent : il ne sert plus qu'à recevoir le clic et le survol.")]
        public Image highlight;
        public Text label;
        public RectTransform labelRect;
        public Text value;
        public RectTransform valueRect;
        public Button button;

        [Tooltip("Boutons − et + : montrés sur les seules lignes réglables.")]
        public Button decrease;
        public Button increase;

        [Header("Animation")]
        [Tooltip("De combien le libellé s'écarte pour laisser passer le blob.")]
        public float selectedShift = 26f;
        public float shiftTime = 0.11f;
        [Tooltip("Sursaut de la valeur quand elle change : c'est ce qui se voit quand on " +
                 "règle une ligne dont le libellé, lui, ne bouge pas.")]
        public float popScale = 0.22f;
        public float popTime = 0.16f;

        /// <summary>Prévenu quand le curseur entre sur la ligne. Posé par <c>MenuController</c>.</summary>
        [System.NonSerialized] public System.Action hovered;

        // Palette de plage, accordée à l'affiche et au terrain : le bleu profond du logo
        // pour les libellés, l'ambre du sable pour les valeurs. Tout se lit sur la carte
        // crème du menu.
        static readonly Color LabelPlain = new Color(0.10f, 0.28f, 0.45f);
        static readonly Color LabelSelected = new Color(0.06f, 0.42f, 0.20f);
        static readonly Color LabelHeader = new Color(0.16f, 0.52f, 0.80f);
        static readonly Color ValuePlain = new Color(0.72f, 0.42f, 0.06f);
        static readonly Color Waiting = new Color(0.12f, 0.52f, 0.24f);

        bool selectedNow;
        float labelHome;
        float shift;
        float shiftVelocity;
        float pop;

        void Awake()
        {
            if (labelRect != null) labelHome = labelRect.anchoredPosition.x;
        }

        /// <summary>Affiche une entrée ordinaire. Les boutons − et + ne servent qu'aux réglages.</summary>
        public void Show(string labelText, string valueText, bool selected, bool waiting, bool adjustable)
        {
            gameObject.SetActive(true);

            label.text = labelText;
            // Le vert du blob sur la ligne qu'il désigne : le curseur et son libellé sont
            // alors une seule marque, là où deux couleurs sans rapport en feraient deux.
            label.color = selected ? LabelSelected : LabelPlain;
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;

            value.text = valueText ?? string.Empty;
            value.color = waiting ? Waiting : ValuePlain;

            highlight.color = Color.clear;
            if (button != null) button.interactable = true;
            ShowSteppers(adjustable);

            selectedNow = selected;
        }

        /// <summary>Affiche un intertitre : pas de valeur, pas de sélection possible.</summary>
        public void ShowHeader(string labelText)
        {
            gameObject.SetActive(true);

            label.text = labelText;
            label.color = LabelHeader;
            label.fontStyle = FontStyle.Bold;

            value.text = string.Empty;
            highlight.color = Color.clear;
            if (button != null) button.interactable = false;
            ShowSteppers(false);

            selectedNow = false;
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Fait sursauter la valeur : la ligne vient d'être réglée.</summary>
        public void Pop() => pop = 1f;

        /// <summary>
        /// Le curseur entre sur la ligne — ou sur son − ou son +, l'événement remontant
        /// jusqu'ici. La sélection le suit : pointer une entrée et la choisir deviennent
        /// le même geste, comme au clavier où l'on descend sur la ligne avant de valider.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData) => hovered?.Invoke();

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (labelRect != null)
            {
                shift = Mathf.SmoothDamp(shift, selectedNow ? selectedShift : 0f,
                    ref shiftVelocity, shiftTime, Mathf.Infinity, dt);
                labelRect.anchoredPosition = new Vector2(labelHome + shift,
                    labelRect.anchoredPosition.y);
            }

            if (valueRect != null)
            {
                // Une cloche qui monte et redescend : le sursaut se voit sans laisser la
                // valeur figée à une taille qui ne serait plus la bonne.
                if (pop > 0f) pop = Mathf.Max(0f, pop - dt / Mathf.Max(0.01f, popTime));
                float bump = 1f + popScale * Mathf.Sin(pop * Mathf.PI);
                valueRect.localScale = new Vector3(bump, bump, 1f);
            }
        }

        void ShowSteppers(bool visible)
        {
            if (decrease != null) decrease.gameObject.SetActive(visible);
            if (increase != null) increase.gameObject.SetActive(visible);
        }
    }
}
