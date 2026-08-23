using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Balle du jeu. Les rebonds sur les murs, le filet et le sol sont laissés à la physique 2D ;
    /// en revanche la frappe sur un blob est calculée à la main : la balle repart radialement
    /// depuis le centre du blob à vitesse constante. C'est ce qui permet de viser en frappant
    /// avec le côté du blob, et de smasher en le percutant par le dessus.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class BallController : MonoBehaviour
    {
        [Header("Frappe")]
        public float hitSpeed = 12f;
        [Tooltip("Part de la vitesse du blob transmise à la balle.")]
        public float blobVelocityInfluence = 0.32f;
        public float maxSpeed = 20f;
        [Tooltip("Délai avant de pouvoir ré-appliquer une frappe pendant un contact continu.")]
        public float stickyRehitDelay = 0.05f;
        [Tooltip("Angle minimal entre le renvoi et la verticale, en degrés. À zéro, une balle " +
                 "retombant pile sur le sommet d'un blob immobile rebondit indéfiniment.")]
        [Range(0f, 45f)] public float minVerticalAngle = 12f;

        [Header("Rendu")]
        public Transform visual;
        public float spinDegreesPerUnit = -28f;

        /// <summary>Déclenché quand la balle est frappée par un blob (une fois par contact).</summary>
        public event Action<BlobController> BlobHit;

        /// <summary>Déclenché au premier contact avec le sol depuis le service.</summary>
        public event Action<Vector2> GroundHit;

        /// <summary>
        /// Rebond sur un mur, le filet ou le plafond : position du contact et vitesse d'impact.
        /// Purement décoratif — l'arbitrage ne s'y intéresse pas — mais le son et les
        /// particules ont besoin de savoir *où* et *avec quelle force*.
        /// </summary>
        public event Action<Vector2, float> BounceHit;

        /// <summary>Nature d'un collider rencontré, résolue une seule fois puis mémorisée.</summary>
        readonly struct Contact
        {
            public readonly BlobController Blob;
            public readonly bool IsGround;

            public Contact(BlobController blob, bool isGround)
            {
                Blob = blob;
                IsGround = isGround;
            }
        }

        // OnCollisionStay2D se déclenche à chaque pas de physique tant que le contact dure :
        // y refaire un GetComponentInParent remonterait la hiérarchie 50 fois par seconde et
        // par contact. La scène ne contient qu'une poignée de colliders, on les mémorise.
        readonly Dictionary<Collider2D, Contact> contacts = new Dictionary<Collider2D, Contact>(16);

        Transform cachedTransform;
        Rigidbody2D body;
        BlobController currentContact;
        float lastHitTime = float.NegativeInfinity;
        float maxSpeedSqr;
        bool groundReported;

        public Rigidbody2D Body => body;
        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;
        public bool InPlay => body != null && body.simulated;

        void Awake()
        {
            cachedTransform = transform;
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.freezeRotation = true;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            maxSpeedSqr = maxSpeed * maxSpeed;
        }

        void OnValidate() => maxSpeedSqr = maxSpeed * maxSpeed;

        void FixedUpdate()
        {
            if (!body.simulated) return;

            Vector2 v = body.linearVelocity;
            if (v.sqrMagnitude > maxSpeedSqr)
            {
                body.linearVelocity = v.normalized * maxSpeed;
            }
        }

        void Update()
        {
            if (visual == null || !body.simulated) return;

            float spin = body.linearVelocity.x * spinDegreesPerUnit * Time.deltaTime;
            // Écrire une rotation nulle marque quand même le Transform sale côté moteur.
            if (spin != 0f) visual.Rotate(0f, 0f, spin);
        }

        /// <summary>Place la balle en attente de service : figée, hors simulation.</summary>
        public void Freeze(Vector2 position)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            cachedTransform.position = position;
            body.position = position;
            currentContact = null;
            groundReported = false;
            lastHitTime = float.NegativeInfinity;
        }

        /// <summary>Lâche la balle : le rally commence.</summary>
        public void Release()
        {
            body.simulated = true;
            body.linearVelocity = Vector2.zero;
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            Contact contact = Resolve(collision.collider);

            if (contact.Blob != null)
            {
                if (contact.Blob != currentContact)
                {
                    currentContact = contact.Blob;
                    ApplyBlobHit(contact.Blob, true);
                }
                return;
            }

            if (contact.IsGround)
            {
                if (groundReported) return;
                groundReported = true;
                GroundHit?.Invoke(body.position);
                return;
            }

            // Ni blob ni sol : mur, filet ou plafond.
            if (BounceHit != null)
            {
                BounceHit(ContactPoint(collision), collision.relativeVelocity.magnitude);
            }
        }

        /// <summary>
        /// Point de contact réel du choc, ou le centre de la balle si la physique n'en
        /// rapporte aucun. C'est là que se placent l'étincelle et le son.
        /// GetContact évite le tableau que <c>Collision2D.contacts</c> alloue à chaque appel.
        /// </summary>
        Vector2 ContactPoint(Collision2D collision)
            => collision.contactCount > 0 ? collision.GetContact(0).point : body.position;

        void OnCollisionStay2D(Collision2D collision)
        {
            BlobController blob = Resolve(collision.collider).Blob;
            if (blob == null) return;

            if (blob != currentContact)
            {
                currentContact = blob;
                ApplyBlobHit(blob, true);
            }
            else if (Time.time - lastHitTime >= stickyRehitDelay)
            {
                // Contact prolongé (blob qui monte sous la balle) : on relance sans compter de touche.
                ApplyBlobHit(blob, false);
            }
        }

        void OnCollisionExit2D(Collision2D collision)
        {
            BlobController blob = Resolve(collision.collider).Blob;
            if (blob != null && blob == currentContact) currentContact = null;
        }

        /// <summary>Nature d'un collider, calculée au premier contact puis relue depuis le cache.</summary>
        Contact Resolve(Collider2D collider)
        {
            if (collider == null) return default;

            if (contacts.TryGetValue(collider, out Contact known)) return known;

            var blob = collider.GetComponentInParent<BlobController>();
            var contact = new Contact(blob, blob == null && collider.GetComponent<GroundSurface>() != null);
            contacts.Add(collider, contact);
            return contact;
        }

        void ApplyBlobHit(BlobController blob, bool countAsTouch)
        {
            lastHitTime = Time.time;

            Vector2 direction = body.position - blob.Center;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
            direction.Normalize();
            direction = TiltAwayFromVertical(direction, blob);

            Vector2 velocity = direction * hitSpeed + blob.Velocity * blobVelocityInfluence;
            body.linearVelocity = Vector2.ClampMagnitude(velocity, maxSpeed);

            if (countAsTouch) BlobHit?.Invoke(blob);
        }

        /// <summary>
        /// Écarte le renvoi de la verticale d'au moins <see cref="minVerticalAngle"/>.
        ///
        /// Le renvoi radial à vitesse constante crée un point fixe : une balle qui retombe
        /// exactement sur le sommet d'un blob immobile repart exactement à la verticale,
        /// retombe au même endroit, et repart à l'identique. Rien ne s'amortit puisque la
        /// vitesse de renvoi est imposée, pas conservée — l'échange se fige donc pour de
        /// bon. Ce n'est pas un cas limite : au service, la balle est lâchée pile au-dessus
        /// du blob, et l'égalité est exacte.
        ///
        /// L'inclinaison conserve le côté déjà pris par la balle et ne tranche vers le camp
        /// adverse que sur une frappe rigoureusement centrée. Les chandelles restent hautes :
        /// à 12°, la composante verticale vaut encore 98 % de la vitesse de renvoi.
        /// </summary>
        Vector2 TiltAwayFromVertical(Vector2 direction, BlobController blob)
        {
            // Une balle renvoyée vers le bas (smash) retrouve le sol : aucun blocage possible.
            if (minVerticalAngle <= 0f || direction.y <= 0f) return direction;

            float minHorizontal = Mathf.Sin(minVerticalAngle * Mathf.Deg2Rad);
            if (Mathf.Abs(direction.x) >= minHorizontal) return direction;

            float sign = direction.x != 0f ? Mathf.Sign(direction.x) : -blob.side.Sign();
            return new Vector2(sign * minHorizontal, Mathf.Cos(minVerticalAngle * Mathf.Deg2Rad));
        }
    }
}
