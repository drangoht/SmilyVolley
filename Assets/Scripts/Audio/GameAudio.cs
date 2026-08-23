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

        [Header("Musique")]
        public AudioClip musicClip;
        [Tooltip("Le morceau a un niveau proche de celui des effets : le baisser est ce qui " +
                 "le place derrière l'action au lieu de la concurrencer.")]
        [Range(0f, 1f)] public float musicVolume = 0.25f;
        public float musicFadeInSeconds = 1.5f;

        [Header("Volumes")]
        [Tooltip("Multiplie tous les effets. C'est le curseur exposé dans le menu ; les " +
                 "volumes par événement ci-dessous restent l'équilibre interne du mixage.")]
        [Range(0f, 1f)] public float sfxVolume = 1f;
        [Range(0f, 1f)] public float blobHitVolume = 0.80f;
        [Range(0f, 1f)] public float bounceVolume = 0.45f;
        [Range(0f, 1f)] public float ballLandVolume = 0.70f;
        [Range(0f, 1f)] public float blobLandVolume = 0.22f;
        [Range(0f, 1f)] public float pointVolume = 0.65f;
        [Tooltip("Appui du saut. Les blobs sautent sans arrêt : ce son doit rester en retrait " +
                 "de l'atterrissage, sinon il occupe tout le mixage.")]
        [Range(0f, 1f)] public float jumpVolume = 0.14f;
        [Tooltip("Le saut réutilise le son de pas dans le sable, monté en hauteur : un appui " +
                 "est un frottement plus vif et plus léger qu'une réception.")]
        public float jumpPitch = 1.25f;

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
        AudioSource music;
        int nextVoice;
        int victoryNote;
        float musicFadeStart;

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

            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.clip = musicClip;
        }

        void Start()
        {
            if (musicClip == null || music == null) return;

            // Démarrage en fondu : la musique attaque à plein volume dès l'image 1 sinon,
            // juste au moment où le joueur découvre l'écran.
            musicFadeStart = Time.time;
            music.volume = musicFadeInSeconds > 0f ? 0f : musicVolume;
            music.Play();
        }

        void Update()
        {
            if (music == null || !music.isPlaying) return;

            float target = musicVolume;
            if (musicFadeInSeconds > 0f)
            {
                float t = Mathf.Clamp01((Time.time - musicFadeStart) / musicFadeInSeconds);
                target *= t;
            }

            if (!Mathf.Approximately(music.volume, target)) music.volume = target;
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

            if (leftBlob != null)
            {
                leftBlob.Landed += OnBlobLanded;
                leftBlob.Jumped += OnBlobJumped;
            }

            if (rightBlob != null)
            {
                rightBlob.Landed += OnBlobLanded;
                rightBlob.Jumped += OnBlobJumped;
            }
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

            if (leftBlob != null)
            {
                leftBlob.Landed -= OnBlobLanded;
                leftBlob.Jumped -= OnBlobJumped;
            }

            if (rightBlob != null)
            {
                rightBlob.Landed -= OnBlobLanded;
                rightBlob.Jumped -= OnBlobJumped;
            }

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

        /// <summary>
        /// Appui du saut. Faute d'un son dédié convaincant — les banques d'effets de saut
        /// libres tiennent du ressort de dessin animé, ou durent deux secondes là où toute
        /// la palette du jeu vit sous une demi-seconde — on reprend le pas dans le sable,
        /// monté en hauteur et nettement plus discret. C'est aussi le geste réel : quitter
        /// le sable et y retomber font le même bruit, en plus vif à l'appui.
        /// </summary>
        void OnBlobJumped(Vector2 position) => Play(blobLandClips, jumpVolume, jumpPitch);

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
            volume *= sfxVolume;
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
