using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Garantit que tout le terrain reste visible quel que soit le format d'écran :
    /// la largeur visible est prioritaire, et le bas de l'image reste calé sur le sol.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraFitter : MonoBehaviour
    {
        public float minVisibleHalfWidth = 8.9f;
        public float minSize = 5f;
        public float bottomY = -4.6f;

        Transform cachedTransform;
        Camera cam;
        float appliedAspect = float.NaN;

        void OnEnable()
        {
            cachedTransform = transform;
            cam = GetComponent<Camera>();
            appliedAspect = float.NaN;
        }

        /// <summary>Force un recadrage après édition des champs dans l'Inspector.</summary>
        void OnValidate() => appliedAspect = float.NaN;

        void LateUpdate()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cachedTransform == null) cachedTransform = transform;
            if (cam == null || !cam.orthographic) return;

            // Le cadrage ne dépend que du format de l'écran : il ne change qu'au
            // redimensionnement de la fenêtre, pas à chaque image.
            float aspect = Mathf.Max(cam.aspect, 0.01f);
            if (aspect == appliedAspect) return;
            appliedAspect = aspect;

            float size = Mathf.Max(minSize, minVisibleHalfWidth / aspect);
            cam.orthographicSize = size;

            Vector3 position = cachedTransform.position;
            position.y = bottomY + size;
            cachedTransform.position = position;
        }
    }
}
