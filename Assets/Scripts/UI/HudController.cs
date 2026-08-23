using UnityEngine;
using UnityEngine.UI;

namespace SmilyVolley
{
    /// <summary>Affichage du score, des messages de manche et du rappel des commandes.</summary>
    public class HudController : MonoBehaviour
    {
        public Text leftScoreText;
        public Text rightScoreText;
        public Text messageText;
        public Text modeText;
        public Text hintText;

        // int.ToString() alloue une chaîne à chaque appel. Les scores d'un match tiennent
        // dans une petite table : au-delà on retombe sur la conversion classique.
        const int CachedScoreCount = 64;
        static readonly string[] ScoreLabels = BuildScoreLabels();

        float messageExpiry = -1f;
        int shownLeftScore = -1;
        int shownRightScore = -1;

        static string[] BuildScoreLabels()
        {
            var labels = new string[CachedScoreCount];
            for (int i = 0; i < CachedScoreCount; i++) labels[i] = i.ToString();
            return labels;
        }

        static string Label(int score)
            => score >= 0 && score < CachedScoreCount ? ScoreLabels[score] : score.ToString();

        // Le HUD n'a besoin d'une boucle que pendant un message temporisé. Les autres
        // méthodes fonctionnent sur un composant désactivé : seul Update est suspendu.
        void Awake() => enabled = false;

        void Update()
        {
            if (Time.time < messageExpiry) return;

            messageExpiry = -1f;
            if (messageText != null) messageText.text = string.Empty;
            enabled = false;
        }

        public void SetScore(int left, int right)
        {
            if (left != shownLeftScore)
            {
                shownLeftScore = left;
                if (leftScoreText != null) leftScoreText.text = Label(left);
            }

            if (right != shownRightScore)
            {
                shownRightScore = right;
                if (rightScoreText != null) rightScoreText.text = Label(right);
            }
        }

        public void SetMode(string mode)
        {
            if (modeText != null) modeText.text = mode;
        }

        public void SetHint(string hint)
        {
            if (hintText != null) hintText.text = hint;
        }

        /// <summary>Affiche un message central. Une durée nulle ou négative le rend permanent.</summary>
        public void ShowMessage(string message, float duration = 0f)
        {
            if (messageText != null) messageText.text = message;

            bool timed = duration > 0f;
            messageExpiry = timed ? Time.time + duration : -1f;
            enabled = timed;
        }

        public void ClearMessage()
        {
            messageExpiry = -1f;
            enabled = false;
            if (messageText != null) messageText.text = string.Empty;
        }
    }
}
