using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// IA de blob. Elle résout la trajectoire balistique de la balle pour trouver où
    /// celle-ci redescendra à hauteur de frappe, puis se place légèrement en retrait de ce
    /// point afin de renvoyer vers le camp adverse.
    /// </summary>
    public class AiBlobInput : BlobInput
    {
        [Header("Références")]
        public BallController ball;
        public BlobController blob;

        [Header("Difficulté")]
        [Range(0f, 1f)] public float difficulty = 0.65f;
        [Tooltip("Erreur de visée maximale en unités, à difficulté nulle.")]
        public float maxAimError = 1.8f;
        [Tooltip("Intervalle de décision maximal en secondes, à difficulté nulle.")]
        public float maxThinkInterval = 0.30f;

        [Header("Comportement")]
        [Tooltip("Décalage par rapport au point d'impact, pour renvoyer vers l'adversaire.")]
        public float aimOffset = 0.45f;
        public float idleX = 4.6f;
        public float deadZone = 0.12f;
        public float jumpTriggerDistance = 1.7f;
        public float jumpReach = 2.6f;

        [Header("Terrain (pour la prédiction des rebonds)")]
        public float wallMinX = -8.2f;
        public float wallMaxX = 8.2f;

        Transform ballTransform;
        float sideSign = 1f;
        float targetX;
        float nextThinkTime;
        bool wantJump;

        public override float Horizontal
        {
            get
            {
                if (blob == null) return 0f;
                float delta = targetX - blob.Center.x;
                if (Mathf.Abs(delta) < deadZone) return 0f;
                return delta < 0f ? -1f : 1f;
            }
        }

        public override bool JumpHeld => wantJump;

        void OnEnable() => CacheReferences();

        void CacheReferences()
        {
            ballTransform = ball != null ? ball.transform : null;
            if (blob != null) sideSign = blob.side.Sign();
        }

        public override void OnServeStart()
        {
            CacheReferences();
            targetX = idleX;
            wantJump = false;
            nextThinkTime = 0f;
        }

        void Update()
        {
            if (ball == null || blob == null) return;
            if (ballTransform == null) CacheReferences();

            if (Time.time >= nextThinkTime)
            {
                Think();
                // Une IA facile réfléchit lentement : c'est ce délai qui la rend prenable.
                nextThinkTime = Time.time + Mathf.Lerp(maxThinkInterval, 0.03f, difficulty);
            }

            UpdateJump();
        }

        void Think()
        {
            float aimError = Random.Range(-1f, 1f) * Mathf.Lerp(maxAimError, 0.05f, difficulty);

            if (!ball.InPlay)
            {
                // Balle figée en attente de service : viser sa position la collerait au filet
                // quand c'est l'adversaire qui engage. On attend en fond de court.
                targetX = idleX;
                return;
            }

            float strikeY = blob.groundY + blob.radius;
            float impactX = PredictImpactX(strikeY);

            if (!IsMySide(impactX) && !IsMySide(ballTransform.position.x))
            {
                targetX = idleX + aimError * 0.3f;
                return;
            }

            // Se placer côté ligne de fond : la balle repart alors vers le filet.
            targetX = Mathf.Clamp(impactX + aimOffset * sideSign + aimError, blob.minX, blob.maxX);
        }

        void UpdateJump()
        {
            wantJump = false;
            if (!ball.InPlay) return;

            Vector3 ballPos = ballTransform.position;
            if (!IsMySide(ballPos.x)) return;

            if (Mathf.Abs(ballPos.x - blob.Center.x) > jumpTriggerDistance) return;

            // Sauter sous une balle qui monte encore la manquerait : on attend qu'elle
            // redescende (vy <= 1) et qu'elle soit à portée de frappe.
            float height = ballPos.y - (blob.groundY + blob.radius);
            wantJump = height > 0.25f && height < jumpReach && ball.Velocity.y <= 1f;
        }

        /// <summary>Vrai si l'abscisse tombe dans le camp de ce blob. Le filet (x = 0) n'appartient à personne.</summary>
        bool IsMySide(float x) => x * sideSign > 0f;

        /// <summary>
        /// Résout y(t) = y0 + vy·t - ½·g·t² = targetY et renvoie l'abscisse correspondante,
        /// repliée sur le terrain pour tenir compte des rebonds contre les murs.
        /// </summary>
        float PredictImpactX(float targetY)
        {
            Vector3 p = ballTransform.position;
            Vector2 v = ball.Velocity;

            float g = Mathf.Abs(Physics2D.gravity.y) * ball.Body.gravityScale;
            if (g <= 0.0001f) return p.x;

            float a = -0.5f * g;
            float discriminant = v.y * v.y - 4f * a * (p.y - targetY);
            if (discriminant < 0f) return p.x;

            float root = Mathf.Sqrt(discriminant);
            float inverse = 1f / (2f * a);
            float t = Mathf.Max((-v.y + root) * inverse, (-v.y - root) * inverse);
            if (t <= 0f) return p.x;

            return FoldInsideCourt(p.x + v.x * t);
        }

        /// <summary>Replie une abscisse hors terrain en simulant les rebonds sur les murs.</summary>
        float FoldInsideCourt(float x)
        {
            float span = wallMaxX - wallMinX;
            if (span <= 0.01f) return x;

            float folded = Mathf.Repeat(x - wallMinX, span * 2f);
            if (folded > span) folded = span * 2f - folded;
            return wallMinX + folded;
        }
    }
}
