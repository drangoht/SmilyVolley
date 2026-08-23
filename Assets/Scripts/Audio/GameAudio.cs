using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Bande sonore du match. Elle s'abonne aux événements du jeu et ne le sonde jamais :
    /// la balle et l'arbitre ignorent totalement qu'on les écoute.
    ///
    /// Chaque son est tiré au hasard dans une petite banque et joué avec une légère
    /// variation de hauteur. Sans cela, un échange un peu long dégénère en cliquetis
    /// mécanique : c'est la répétition à l'identique que l'oreille repère, pas le son.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        [Header("Références")]
        public BallController ball;
        public GameManager manager;
        public BlobController leftBlob;
        public BlobController rightBlob;

        [Header("Banques de sons")]
        public AudioClip[] blobHitClips;
        public AudioClip[] bounceClips;
        public AudioClip[] ballLandClips;
        public AudioClip[] blobLandClips;
        public AudioClip[] pointClips;

        [Header("Volumes")]
        [Range(0f, 1f)] public float blobHitVolume = 0.80f;
        [Range(0f, 1f)] public float bounceVolume = 0.45f;
        [Range(0f, 1f)] public float ballLandVolume = 0.70f;
        [Range(0f, 1f)] public float blobLandVolume = 0.22f;
        [Range(0f, 1f)] public float pointVolume = 0.65f;

        [Header("Variation")]
        [Tooltip("Écart de hauteur appliqué à chaque son, en proportion (0,12 = ±12 %).")]
        [Range(0f, 0.5f)] public float pitchJitter = 0.12f;
        [Tooltip("Nombre de sons pouvant se superposer. Chacun a sa propre hauteur.")]
        [Range(1, 16)] public int voiceCount = 6;

        [Header("Jingle de victoire")]
        [Tooltip("Demi-tons successifs joués sur le son de point à la fin du match.")]
        public float[] victorySemitones = { 0f, 4f, 7f, 12f };
        public float victoryNoteSpacing = 0.16f;
        [Range(0f, 1f)] public float victoryVolume = 0.75f;

        // Un AudioSource ne porte qu'une hauteur à la fois : PlayOneShot sur une source
        // unique donnerait la même à tous les sons simultanés. D'où le tourniquet.
        AudioSource[] voices;
        int nextVoice;
        int victoryNote;

        void Awake()
        {
            voices = new AudioSource[Mathf.Max(1, voiceCount)];
            for (int i = 0; i < voices.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                // Son 2D : le terrain tient dans l'écran, une spatialisation n'apporterait
                // qu'un déséquilibre gauche/droite gênant pour le joueur de gauche.
                source.spatialBlend = 0f;
                voices[i] = source;
            }
        }

        void OnEnable()
        {
            if (ball != null)
            {
                ball.BlobHit += OnBlobHit;
                ball.GroundHit += OnBallLanded;
                ball.BounceHit += OnBounce;
            }

            if (manager != null)
            {
                manager.PointScored += OnPointScored;
                manager.MatchWon += OnMatchWon;
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

            if (manager != null)
            {
                manager.PointScored -= OnPointScored;
                manager.MatchWon -= OnMatchWon;
            }

            if (leftBlob != null) leftBlob.Landed -= OnBlobLanded;
            if (rightBlob != null) rightBlob.Landed -= OnBlobLanded;

            CancelInvoke();
        }

        // ------------------------------------------------------------------ réactions

        void OnBlobHit(BlobController blob) => Play(blobHitClips, blobHitVolume);

        void OnBallLanded(Vector2 position) => Play(ballLandClips, ballLandVolume);

        void OnBounce(Vector2 position, float speed)
        {
            // Un frôlement contre le filet et un boulet contre le mur ne sonnent pas pareil.
            float force = ball != null && ball.maxSpeed > 0f ? Mathf.Clamp01(speed / ball.maxSpeed) : 1f;
            Play(bounceClips, bounceVolume * Mathf.Lerp(0.35f, 1f, force));
        }

        void OnBlobLanded(Vector2 position, float fallSpeed)
        {
            // Les blobs sautent sans arrêt : un pas au volume plein saturerait le mixage.
            float force = Mathf.Clamp01(fallSpeed / 9f);
            if (force < 0.15f) return;
            Play(blobLandClips, blobLandVolume * force);
        }

        void OnPointScored(Side winner) => Play(pointClips, pointVolume);

        void OnMatchWon(Side winner)
        {
            // Le pack ne fournit pas de fanfare : on en compose une en rejouant la cloche
            // sur une montée d'accord parfait.
            if (victorySemitones == null) return;

            // Remis à zéro à chaque victoire : sinon la deuxième fanfare partirait
            // au milieu de la montée.
            victoryNote = 0;

            for (int i = 0; i < victorySemitones.Length; i++)
            {
                Invoke(nameof(PlayVictoryNote), victoryNoteSpacing * i);
            }
        }

        void PlayVictoryNote()
        {
            if (victorySemitones == null || victorySemitones.Length == 0) return;

            float semitone = victorySemitones[victoryNote % victorySemitones.Length];
            victoryNote++;
            // Un demi-ton est un rapport de fréquence de 2^(1/12).
            Play(pointClips, victoryVolume, Mathf.Pow(2f, semitone / 12f));
        }

        // ------------------------------------------------------------------ lecture

        void Play(AudioClip[] bank, float volume, float pitch = 1f)
        {
            if (bank == null || bank.Length == 0 || volume <= 0.001f) return;

            AudioClip clip = bank.Length == 1 ? bank[0] : bank[Random.Range(0, bank.Length)];
            if (clip == null) return;

            AudioSource source = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;

            source.pitch = pitch * (1f + Random.Range(-pitchJitter, pitchJitter));
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
