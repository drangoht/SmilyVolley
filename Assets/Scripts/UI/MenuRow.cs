using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmilyVolley
{
    /// <summary>
    /// Une ligne de menu : un libellé à gauche, une valeur à droite, deux boutons − et +
    /// pour la régler à la souris, et un bandeau de surbrillance. Les lignes sont créées
    /// une fois par <c>SceneBuilder</c> puis réutilisées d'un écran à l'autre — un menu
    /// qui reconstruit son interface à chaque ouverture génère des déchets pour rien.
    /// </summary>
    public class MenuRow : MonoBehaviour, IPointerEnterHandler
    {
        public RectTransform rect;
        public Image highlight;
        public Text label;
        public Text value;
        public Button button;

        [Tooltip("Boutons − et + : montrés sur les seules lignes réglables.")]
        public Button decrease;
        public Button increase;

        /// <summary>Prévenu quand le curseur entre sur la ligne. Posé par <c>MenuController</c>.</summary>
        [System.NonSerialized] public System.Action hovered;

        // Palette de plage, accordée à l'affiche et au terrain : le bleu profond du logo
        // pour les libellés, le bleu du ciel pour la surbrillance, l'ambre du sable pour
        // les valeurs. Tout se lit sur la carte crème du menu.
        static readonly Color Selected = new Color(0.42f, 0.72f, 0.93f, 0.85f);
        static readonly Color LabelPlain = new Color(0.10f, 0.28f, 0.45f);
        static readonly Color LabelOnSelected = new Color(0.04f, 0.16f, 0.30f);
        static readonly Color LabelHeader = new Color(0.16f, 0.52f, 0.80f);
        static readonly Color ValuePlain = new Color(0.72f, 0.42f, 0.06f);
        static readonly Color Waiting = new Color(0.12f, 0.52f, 0.24f);

        /// <summary>Affiche une entrée ordinaire. Les boutons − et + ne servent qu'aux réglages.</summary>
        public void Show(string labelText, string valueText, bool selected, bool waiting, bool adjustable)
        {
            gameObject.SetActive(true);

            label.text = labelText;
            // Le bandeau bleu assombrit ce qu'il porte : sans ce cran de plus, le libellé
            // sélectionné se lit moins bien que ses voisins, ce qui inverse le repère.
            label.color = selected ? LabelOnSelected : LabelPlain;
            label.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;

            value.text = valueText ?? string.Empty;
            value.color = waiting ? Waiting : ValuePlain;

            highlight.color = selected ? Selected : Color.clear;
            if (button != null) button.interactable = true;
            ShowSteppers(adjustable);
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
        }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>
        /// Le curseur entre sur la ligne — ou sur son − ou son +, l'événement remontant
        /// jusqu'ici. La sélection le suit : pointer une entrée et la choisir deviennent
        /// le même geste, comme au clavier où l'on descend sur la ligne avant de valider.
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData) => hovered?.Invoke();

        void ShowSteppers(bool visible)
        {
            if (decrease != null) decrease.gameObject.SetActive(visible);
            if (increase != null) increase.gameObject.SetActive(visible);
        }
    }
}
