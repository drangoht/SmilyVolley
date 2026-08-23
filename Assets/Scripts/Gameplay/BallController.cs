using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Balle du jeu. Les rebonds sur les murs, le filet et le sol sont laissés à la physique 2D ;
    /// en revanche la frappe sur un blob est calculée à la main : la balle repart radialement
    /// depuis le centre du blob. C'est ce qui permet de viser en frappant avec le côté du
    /// blob, et de smasher en le percutant par le dessus.
    ///
    /// Le placement donne la direction, l'élan donne la vitesse. Un blob immobile renvoie
    /// toujours à la même vitesse plancher ; un blob qui se jette dans la balle lui ajoute
    /// son propre élan, et c'est de là que viennent le smash et la balle rapide.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class BallController : MonoBehaviour
    {
        [Header("Frappe")]
        [Tooltip("Vitesse de renvoi d'une frappe sans élan. C'est le plancher de tout échange : " +
                 "une balle mollement reprise repart toujours à cette vitesse-là.")]
        public float hitSpeed = 12f;

        [Tooltip("Élan du blob le long de l'axe de renvoi, ajouté à la vitesse. À 1, un blob " +
                 "qui retombe à 9 u/s sur la balle la renvoie 9 u/s plus vite : c'est le smash.")]
        public float blobDrive = 1f;

        [Tooltip("Part de l'excès de vitesse que la balle garde d'une frappe à l'autre. " +
                 "En dessous de 1, une balle rapide se calme d'elle-même en quelques échanges.")]
        [Range(0f, 0.95f)] public float speedCarry = 0.5f;

        [Tooltip("Part de la vitesse du blob transmise à la direction du renvoi. " +
                 "Ne joue que sur l'orientation : la vitesse, elle, vient de Blob Drive.")]
        public float blobVelocityInfluence = 0.32f;

        public float maxSpeed = 24f;

        [Tooltip("Vitesse de montée maximale au sortir d'une frappe. Une balle plus rapide " +
                 "monte plus haut : au-delà, elle quitte le cadre et le joueur la perd de vue. " +
                 "Ne plafonne que la montée — un smash et un renvoi rasant gardent tout leur élan.")]
        public float maxClimbSpeed = 13.5f;
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

        /// <summary>
        /// Vitesse courante. Lue juste après <see cref="BlobHit"/>, elle donne la force du
        /// renvoi : c'est ce qui dose le son et la bouffée de particules de la frappe.
        /// </summary>
        public float Speed => body != null ? body.linearVelocity.magnitude : 0f;
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

            // Relevée avant le renvoi : c'est la vitesse d'arrivée qui creuse la gelée,
            // pas celle du départ, qui est constante et ne dirait rien de la force du coup.
            float incoming = body.linearVelocity.magnitude;

            Vector2 direction = body.position - blob.Center;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
            direction.Normalize();

            // Le creux se forme là où la balle touche, avant que le renvoi ne soit écarté
            // de la verticale : la correction sert à la trajectoire, pas au point de contact.
            Vector2 contact = direction;

            direction = TiltAwayFromVertical(direction, blob);

            // Direction : le radial, infléchi par le déplacement du blob. C'est le placement
            // qui vise, et le mouvement ne fait qu'accompagner.
            Vector2 aim = direction * hitSpeed + blob.Velocity * blobVelocityInfluence;
            Vector2 heading = aim.sqrMagnitude > 1e-6f ? aim.normalized : direction;

            // Vitesse : le plancher, plus l'élan que le blob met dans l'axe du renvoi, plus
            // ce que la balle avait déjà de trop. Le blob qui retombe sur une balle basse
            // signe le smash ; celui qui monte sous une balle haute signe la chandelle tendue.
            float drive = Mathf.Max(0f, Vector2.Dot(blob.Velocity, contact));

            // L'excès ne survit qu'en partie : sans cet amortissement, chaque frappe
            // rendrait ce qu'elle a reçu et la balle ne redescendrait jamais au calme.
            float carried = Mathf.Max(0f, incoming - hitSpeed) * speedCarry;

            Vector2 launched = heading * Mathf.Min(hitSpeed + drive * blobDrive + carried, maxSpeed);

            // Un blob qui saute sous la balle lui met tout son élan dans la verticale, et la
            // balle sort du cadre par le haut : mesuré, elle touchait le plafond de l'écran.
            // Écrêter la seule montée aplatit ces chandelles-fusées sans rien retirer au
            // smash, qui va vers le bas, ni au renvoi rasant, qui va sur le côté.
            if (launched.y > maxClimbSpeed) launched.y = maxClimbSpeed;

            body.linearVelocity = launched;

            blob.ReportBallImpact(contact, incoming);

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
