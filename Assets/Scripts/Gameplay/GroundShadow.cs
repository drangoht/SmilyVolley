using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Projette une ombre au sol sous un objet. Elle rétrécit et s'éclaircit avec la hauteur,
    /// ce qui donne un repère de profondeur utile pour juger où la balle va retomber.
    /// </summary>
    public class GroundShadow : MonoBehaviour
    {
        public Transform target;
        public float groundY = -3.97f;
        public float baseScale = 1f;
        public float fadeHeight = 7f;
        public float minScale = 0.55f;
        public float minAlpha = 0.12f;
        public float maxAlpha = 0.55f;

        Transform cachedTransform;
        SpriteRenderer spriteRenderer;
        float appliedScale = float.NaN;
        float appliedAlpha = float.NaN;
        float appliedX = float.NaN;

        void Awake()
        {
            cachedTransform = transform;
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        void LateUpdate()
        {
            if (target == null) return;

            Vector3 targetPosition = target.position;
            float height = Mathf.Max(0f, targetPosition.y - groundY);
            float closeness = Mathf.Clamp01(1f - height / fadeHeight);

            if (targetPosition.x != appliedX)
            {
                appliedX = targetPosition.x;
                cachedTransform.position = new Vector3(targetPosition.x, groundY, cachedTransform.position.z);
            }

            // L'ombre d'un blob au sol ne bouge ni en taille ni en opacité : on n'écrit
            // l'échelle et la couleur que lorsqu'elles changent réellement.
            float scale = baseScale * Mathf.Lerp(minScale, 1f, closeness);
            if (scale != appliedScale)
            {
                appliedScale = scale;
                cachedTransform.localScale = new Vector3(scale, scale, 1f);
            }

            if (spriteRenderer == null) return;

            float alpha = Mathf.Lerp(minAlpha, maxAlpha, closeness);
            if (alpha != appliedAlpha)
            {
                appliedAlpha = alpha;
                Color c = spriteRenderer.color;
                c.a = alpha;
                spriteRenderer.color = c;
            }
        }
    }
}
