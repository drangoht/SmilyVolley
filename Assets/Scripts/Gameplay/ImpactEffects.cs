using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Particules d'impact : éclat à la frappe, gerbe de sable à la retombée, étincelle
    /// sur les murs et le filet.
    ///
    /// Un seul système par type d'effet, réutilisé pour toutes les émissions. Émettre via
    /// <see cref="ParticleSystem.EmitParams"/> permet de placer la bouffée n'importe où
    /// sans déplacer le Transform du système : pas de va-et-vient de Transform à chaque
    /// impact, et deux bouffées peuvent coexister à deux endroits du terrain.
    /// </summary>
    public class ImpactEffects : MonoBehaviour
    {
        [Header("Références")]
        public BallController ball;
        public BlobController leftBlob;
        public BlobController rightBlob;

        [Header("Systèmes")]
        public ParticleSystem hitBurst;
        public ParticleSystem sandBurst;
        public ParticleSystem bounceBurst;

        [Header("Quantités")]
        public int hitParticles = 10;
        public int ballLandParticles = 18;
        public int blobLandParticles = 8;
        public int bounceParticles = 6;

        ParticleSystem.EmitParams emitParams;

        void OnEnable()
        {
            if (ball != null)
            {
                ball.BlobHit += OnBlobHit;
                ball.GroundHit += OnBallLanded;
                ball.BounceHit += OnBounce;
            }

            if (leftBlob != null) leftBlob.Landed += OnBlobLanded;
            if (rightBlob != null) rightBlob.Landed += OnBlobLanded;
        }

        void OnDisable()
        {
            if (ball != null)
            {
                ball.BlobHit -= OnBlobHit;
                ball.GroundHit -= OnBallLanded;
                ball.BounceHit -= OnBounce;
            }

            if (leftBlob != null) leftBlob.Landed -= OnBlobLanded;
            if (rightBlob != null) rightBlob.Landed -= OnBlobLanded;
        }

        void OnBlobHit(BlobController blob)
        {
            if (ball == null) return;

            // La balle touche encore le blob : sa position est le point de contact,
            // à un rayon près qui ne se voit pas sur une bouffée de cette taille.
            Emit(hitBurst, ball.Body.position, hitParticles);
        }

        void OnBallLanded(Vector2 position) => Emit(sandBurst, position, ballLandParticles);

        void OnBounce(Vector2 position, float speed) => Emit(bounceBurst, position, bounceParticles);

        void OnBlobLanded(Vector2 position, float fallSpeed)
        {
            // Une simple retombée de saut soulève moins de sable qu'une chute de haut.
            float force = Mathf.Clamp01(fallSpeed / 9f);
            if (force < 0.15f) return;

            Emit(sandBurst, position, Mathf.RoundToInt(blobLandParticles * force));
        }

        void Emit(ParticleSystem system, Vector2 position, int count)
        {
            if (system == null || count <= 0) return;

            emitParams.position = position;
            emitParams.applyShapeToPosition = true;
            system.Emit(emitParams, count);
        }
    }
}
