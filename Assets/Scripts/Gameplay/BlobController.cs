using System.Collections.Generic;
using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Déplacement d'un blob. La physique est entièrement manuelle (Rigidbody2D kinematic) :
    /// c'est ce qui donne le contrôle « sec » de Blobby Volley, sans inertie ni rebond parasite
    /// quand la balle percute le blob.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BlobController : MonoBehaviour
    {
        [Header("Identité")]
        public Side side = Side.Left;

        [Header("Déplacement")]
        public float moveSpeed = 6.5f;
        public float jumpSpeed = 9.7f;
        public float gravity = 15.7f;

        [Header("Terrain")]
        public float groundY = -4f;
        public float minX = -7.2f;
        public float maxX = -1.15f;
        public float radius = 1f;

        [Header("Rendu")]
        [Tooltip("Transform du sprite, utilisé pour l'effet d'écrasement à l'atterrissage.")]
        public Transform visual;
        public float squashRecoverySpeed = 5f;

        // Tampon partagé : GetComponents<T>() alloue un tableau à chaque appel, la surcharge
        // à liste réutilise le même buffer. Le changement de mode passe par ici.
        static readonly List<BlobInput> InputBuffer = new List<BlobInput>(4);

        Transform cachedTransform;
        Rigidbody2D body;
        BlobInput input;
        Vector2 velocity;
        Vector2 startPosition;
        bool grounded = true;
        float squash = 1f;
        float appliedSquash = float.NaN;

        /// <summary>Centre du cercle de collision : c'est le point de référence des rebonds de balle.</summary>
        public Vector2 Center => body != null ? body.position : (Vector2)cachedTransform.position;

        public Vector2 Velocity => velocity;
        public bool Grounded => grounded;

        /// <summary>Bloque les commandes (fin de match, pause…) sans désactiver la gravité.</summary>
        public bool Frozen { get; set; }

        void Awake()
        {
            cachedTransform = transform;
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.freezeRotation = true;
            startPosition = cachedTransform.position;
            RefreshInput();
        }

        /// <summary>
        /// Sélectionne la première source de commandes activée du GameObject.
        /// Permet de basculer humain / IA en activant l'un ou l'autre composant.
        /// </summary>
        public void RefreshInput()
        {
            input = null;
            GetComponents(InputBuffer);

            for (int i = 0; i < InputBuffer.Count; i++)
            {
                if (InputBuffer[i].enabled)
                {
                    input = InputBuffer[i];
                    break;
                }
            }

            InputBuffer.Clear();
        }

        public void ResetToStart()
        {
            velocity = Vector2.zero;
            grounded = true;
            squash = 1f;
            cachedTransform.position = startPosition;
            if (body != null) body.position = startPosition;
            if (input != null) input.OnServeStart();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool canAct = input != null && !Frozen;

            velocity.x = canAct ? Mathf.Clamp(input.Horizontal, -1f, 1f) * moveSpeed : 0f;

            if (grounded)
            {
                if (canAct && input.JumpHeld)
                {
                    velocity.y = jumpSpeed;
                    grounded = false;
                    squash = 1.18f;
                }
            }
            else
            {
                velocity.y -= gravity * dt;
            }

            Vector2 next = body.position + velocity * dt;

            if (next.y <= groundY)
            {
                next.y = groundY;
                if (!grounded) squash = 0.78f;
                velocity.y = 0f;
                grounded = true;
            }

            next.x = Mathf.Clamp(next.x, minX, maxX);
            body.MovePosition(next);
        }

        void Update()
        {
            if (visual == null) return;

            squash = Mathf.MoveTowards(squash, 1f, squashRecoverySpeed * Time.deltaTime);

            // Le blob passe l'essentiel du match sans écrasement : réécrire la même échelle
            // à chaque image marquerait le Transform sale pour rien.
            if (squash == appliedSquash) return;

            appliedSquash = squash;
            visual.localScale = new Vector3(1f / squash, squash, 1f);
        }
    }
}
