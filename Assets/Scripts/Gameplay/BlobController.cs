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

        /// <summary>
        /// Le blob vient de retoucher le sable : position du contact et vitesse de chute.
        /// Sert au bruit de pas et à la gerbe de sable.
        /// </summary>
        public event System.Action<Vector2, float> Landed;

        /// <summary>Le blob vient de quitter le sol : position de l'appui.</summary>
        public event System.Action<Vector2> Jumped;

        /// <summary>
        /// La balle vient de frapper le blob : direction du centre vers la balle, et vitesse
        /// d'arrivée. C'est ce que la gelée transforme en creux, du bon côté et à la bonne
        /// profondeur — d'où la direction plutôt qu'un simple signal de contact.
        /// </summary>
        public event System.Action<Vector2, float> BallStruck;

        /// <summary>Le blob vient d'être replacé pour un service : tout état visuel repart de zéro.</summary>
        public event System.Action Respawned;

        [Header("Déplacement")]
        public float moveSpeed = 6.5f;
        public float jumpSpeed = 9.7f;
        public float gravity = 15.7f;

        [Header("Terrain")]
        public float groundY = -4f;
        public float minX = -7.2f;
        public float maxX = -1.15f;
        public float radius = 1f;

        // Tampon partagé : GetComponents<T>() alloue un tableau à chaque appel, la surcharge
        // à liste réutilise le même buffer. Le changement de mode passe par ici.
        static readonly List<BlobInput> InputBuffer = new List<BlobInput>(4);

        Transform cachedTransform;
        Rigidbody2D body;
        BlobInput input;
        Vector2 velocity;
        Vector2 startPosition;
        bool grounded = true;

        /// <summary>Centre du cercle de collision : c'est le point de référence des rebonds de balle.</summary>
        public Vector2 Center => body != null ? body.position : (Vector2)cachedTransform.position;

        public Vector2 Velocity => velocity;
        public bool Grounded => grounded;

        /// <summary>
        /// Position de départ du blob, celle qu'il retrouve à chaque service. C'est au-dessus
        /// d'elle que la balle est engagée : s'appuyer dessus plutôt que sur la position
        /// courante permet de placer la balle avant que les blobs ne soient replacés.
        /// </summary>
        public Vector2 StartPosition => startPosition;

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
            cachedTransform.position = startPosition;
            if (body != null) body.position = startPosition;
            if (input != null) input.OnServeStart();
            Respawned?.Invoke();
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
                    Jumped?.Invoke(new Vector2(body.position.x, groundY));
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
                if (!grounded)
                {
                    // Vitesse de chute avant remise à zéro : elle dose l'impact sonore et visuel.
                    Landed?.Invoke(new Vector2(next.x, groundY), Mathf.Abs(velocity.y));
                }
                velocity.y = 0f;
                grounded = true;
            }

            next.x = Mathf.Clamp(next.x, minX, maxX);
            body.MovePosition(next);
        }

        /// <summary>
        /// Relaie la frappe de la balle. La <see cref="BallController"/> connaît la direction
        /// et la vitesse du contact ; le blob ne les utilise pas lui-même, mais tout ce qui
        /// l'habille — gelée, particules, son — s'y branche par cet événement.
        /// </summary>
        public void ReportBallImpact(Vector2 direction, float speed)
        {
            BallStruck?.Invoke(direction, speed);
        }
    }
}
