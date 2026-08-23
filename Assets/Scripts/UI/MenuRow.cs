using UnityEngine;
using UnityEngine.UI;

namespace SmilyVolley
{
    /// <summary>
    /// Une ligne de menu : un libellé à gauche, une valeur à droite, et un bandeau de
    /// surbrillance. Les lignes sont créées une fois par <c>MenuBuilder</c> puis
    /// réutilisées d'un écran à l'autre — un menu qui reconstruit son interface à chaque
    /// ouverture génère des déchets pour rien.
    /// </summary>
    public class MenuRow : MonoBehaviour
    {
        public RectTransform rect;
        public Image highlight;
        public Text label;
        public Text value;
        public Button button;

        static readonly Color Selected = new Color(1f, 1f, 1f, 0.16f);
        static readonly Color LabelPlain = new Color(0.90f, 0.93f, 0.96f);
        static readonly Color LabelHeader = new Color(0.55f, 0.78f, 1f);
        static readonly Color ValuePlain = new Color(1f, 0.86f, 0.45f);
        static readonly Color Waiting = new Color(0.55f, 1f, 0.65f);

        /// <summary>Affiche une entrée ordinaire.</summary>
        public void Show(string labelText, string valueText, bool selected, bool waiting)
        {
            gameObject.SetActive(true);

            label.text = labelText;
            label.color = LabelPlain;
            label.fontStyle = FontStyle.Normal;

            value.text = valueText ?? string.Empty;
            value.color = waiting ? Waiting : ValuePlain;

            highlight.color = selected ? Selected : Color.clear;
            if (button != null) button.interactable = true;
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
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
