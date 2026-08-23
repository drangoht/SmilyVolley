using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Choisit l'image du blob dans la planche du style actif, selon sa déformation
    /// courante. Chaque image porte une silhouette redessinée — flancs qui gonflent,
    /// facettes qui s'aplatissent — là où une simple mise à l'échelle ne donnerait
    /// qu'un dôme étiré.
    /// </summary>
    public class BlobAnimator : MonoBehaviour
    {
        /// <summary>Les neuf images d'un style, de la plus écrasée à la plus étirée.</summary>
        [System.Serializable]
        public class StyleFrames
        {
            public BlobStyle style;
            public Sprite[] frames;
        }

        [Header("Références")]
        public BlobController blob;
        public SpriteRenderer target;

        [Header("Planches")]
        public StyleFrames[] styles;

        [Header("Plage couverte par les images")]
        public float minSquash = BlobController.MinSquash;
        public float maxSquash = BlobController.MaxSquash;

        Sprite[] active;
        int shownIndex = -1;

        void Awake()
        {
            // Le composant porte la déformation : sans cela le blob la subirait deux fois,
            // une fois par l'image et une fois par l'échelle du BlobController.
            if (blob != null) blob.useProceduralSquash = false;
            if (active == null) SetStyle(BlobStyle.Round);
        }

        /// <summary>Bascule sur une autre planche. Sans effet si le style n'est pas fourni.</summary>
        public void SetStyle(BlobStyle style)
        {
            if (styles == null) return;

            for (int i = 0; i < styles.Length; i++)
            {
                if (styles[i] == null || styles[i].style != style) continue;
                if (styles[i].frames == null || styles[i].frames.Length == 0) continue;

                active = styles[i].frames;
                shownIndex = -1;   // force la réaffectation du sprite à la prochaine image
                return;
            }
        }

        void LateUpdate()
        {
            if (blob == null || target == null || active == null || active.Length == 0) return;

            float squash = blob.Squash;
            float t = Mathf.InverseLerp(minSquash, maxSquash, squash);
            int index = Mathf.Clamp(Mathf.RoundToInt(t * (active.Length - 1)), 0, active.Length - 1);

            if (index != shownIndex)
            {
                shownIndex = index;
                target.sprite = active[index];
            }

            // Le pas entre deux images vaut 7 % d'écrasement : sans rattrapage, la
            // déformation avancerait par saccades. L'image donne la forme, ce résidu
            // — 3,5 % au pire — donne la quantité exacte.
            float frameSquash = Mathf.Lerp(minSquash, maxSquash, index / (float)(active.Length - 1));
            float residual = frameSquash > 0.001f ? squash / frameSquash : 1f;
            target.transform.localScale = new Vector3(1f / residual, residual, 1f);
        }
    }
}
