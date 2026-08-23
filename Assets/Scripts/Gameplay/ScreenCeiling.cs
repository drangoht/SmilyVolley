using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Mur invisible collé au bord haut du champ visible. Sans lui, une balle bien frappée
    /// sort du cadre et le joueur attend plusieurs secondes sans rien voir avant qu'elle ne
    /// retombe. La position est recalculée en continu pour suivre le cadrage du
    /// <see cref="CameraFitter"/>, qui dépend du format de l'écran.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider2D))]
    public class ScreenCeiling : MonoBehaviour
    {
        public Camera targetCamera;

        [Tooltip("Épaisseur du collider : sa face inférieure est calée sur le haut de l'écran.")]
        public float thickness = 2f;

        Transform cachedTransform;

        void OnEnable() => cachedTransform = transform;

        void LateUpdate()
        {
            // Camera.main parcourt les caméras taguées : on préfère la référence directe
            // posée par SceneBuilder, et on ne retombe sur la recherche qu'en secours.
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || !cam.orthographic) return;
            if (cachedTransform == null) cachedTransform = transform;

            Vector3 current = cachedTransform.position;
            float top = cam.transform.position.y + cam.orthographicSize;
            var target = new Vector3(0f, top + thickness * 0.5f, current.z);

            // Déplacer un collider statique à chaque image coûte cher pour rien : on ne
            // bouge que lorsque le cadrage a réellement changé (redimensionnement de fenêtre).
            if ((current - target).sqrMagnitude > 0.000001f)
            {
                cachedTransform.position = target;
            }
        }
    }
}
