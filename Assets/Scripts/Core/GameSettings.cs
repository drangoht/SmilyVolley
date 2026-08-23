using UnityEngine;
using UnityEngine.InputSystem;

namespace SmilyVolley
{
    /// <summary>
    /// Tous les réglages modifiables par le joueur, et leur persistance d'une partie à
    /// l'autre. C'est la seule source de vérité : les composants de jeu reçoivent ces
    /// valeurs via <see cref="ApplyTo"/> et ne les stockent jamais durablement.
    ///
    /// La sauvegarde passe par PlayerPrefs. C'est peu élégant pour de gros volumes, mais
    /// ici il s'agit d'une quinzaine de scalaires : un fichier de configuration maison
    /// coûterait plus de code qu'il n'en ferait gagner.
    /// </summary>
    [System.Serializable]
    public class GameSettings
    {
        const string Prefix = "smily.";
        const int CurrentVersion = 1;

        // ----- commandes -----
        public Key p1Left = Key.A;
        public Key p1Right = Key.D;
        public Key p1Jump = Key.W;
        public Key p2Left = Key.LeftArrow;
        public Key p2Right = Key.RightArrow;
        public Key p2Jump = Key.UpArrow;

        // ----- partie -----
        public bool rightPlayerIsAi = true;
        [Range(0f, 1f)] public float aiDifficulty = 0.65f;
        public int pointsToWin = 15;
        public bool requireTwoPointLead = true;
        public int maxTouchesPerSide = 0;
        public bool serveGoesToLoser = true;
        public bool sideOutScoring = false;

        // ----- audio -----
        [Range(0f, 1f)] public float musicVolume = 0.25f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        // ----- apparence -----
        public BlobStyle blobStyle = BlobStyle.Round;

        // ----- affichage -----
        public bool fullscreen = false;

        /// <summary>Remet toutes les valeurs à celles d'origine, sans toucher au disque.</summary>
        public void ResetToDefaults()
        {
            var defaults = new GameSettings();

            p1Left = defaults.p1Left; p1Right = defaults.p1Right; p1Jump = defaults.p1Jump;
            p2Left = defaults.p2Left; p2Right = defaults.p2Right; p2Jump = defaults.p2Jump;

            rightPlayerIsAi = defaults.rightPlayerIsAi;
            aiDifficulty = defaults.aiDifficulty;
            pointsToWin = defaults.pointsToWin;
            requireTwoPointLead = defaults.requireTwoPointLead;
            maxTouchesPerSide = defaults.maxTouchesPerSide;
            serveGoesToLoser = defaults.serveGoesToLoser;
            sideOutScoring = defaults.sideOutScoring;

            musicVolume = defaults.musicVolume;
            sfxVolume = defaults.sfxVolume;
            blobStyle = defaults.blobStyle;
            fullscreen = defaults.fullscreen;
        }

        /// <summary>Remet les seules commandes à leur disposition d'origine.</summary>
        public void ResetControls()
        {
            var defaults = new GameSettings();
            p1Left = defaults.p1Left; p1Right = defaults.p1Right; p1Jump = defaults.p1Jump;
            p2Left = defaults.p2Left; p2Right = defaults.p2Right; p2Jump = defaults.p2Jump;
        }

        // ------------------------------------------------------------------ persistance

        public void Load()
        {
            // Une version absente signale une première exécution : on garde les défauts.
            if (PlayerPrefs.GetInt(Prefix + "version", 0) < CurrentVersion) return;

            p1Left = LoadKey("p1Left", p1Left);
            p1Right = LoadKey("p1Right", p1Right);
            p1Jump = LoadKey("p1Jump", p1Jump);
            p2Left = LoadKey("p2Left", p2Left);
            p2Right = LoadKey("p2Right", p2Right);
            p2Jump = LoadKey("p2Jump", p2Jump);

            rightPlayerIsAi = LoadBool("rightPlayerIsAi", rightPlayerIsAi);
            aiDifficulty = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "aiDifficulty", aiDifficulty));
            pointsToWin = Mathf.Max(1, PlayerPrefs.GetInt(Prefix + "pointsToWin", pointsToWin));
            requireTwoPointLead = LoadBool("requireTwoPointLead", requireTwoPointLead);
            maxTouchesPerSide = Mathf.Max(0, PlayerPrefs.GetInt(Prefix + "maxTouches", maxTouchesPerSide));
            serveGoesToLoser = LoadBool("serveGoesToLoser", serveGoesToLoser);
            sideOutScoring = LoadBool("sideOutScoring", sideOutScoring);

            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "musicVolume", musicVolume));
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "sfxVolume", sfxVolume));

            int style = PlayerPrefs.GetInt(Prefix + "blobStyle", (int)blobStyle);
            if (System.Enum.IsDefined(typeof(BlobStyle), style)) blobStyle = (BlobStyle)style;

            fullscreen = LoadBool("fullscreen", fullscreen);
        }

        public void Save()
        {
            PlayerPrefs.SetInt(Prefix + "version", CurrentVersion);

            SaveKey("p1Left", p1Left); SaveKey("p1Right", p1Right); SaveKey("p1Jump", p1Jump);
            SaveKey("p2Left", p2Left); SaveKey("p2Right", p2Right); SaveKey("p2Jump", p2Jump);

            SaveBool("rightPlayerIsAi", rightPlayerIsAi);
            PlayerPrefs.SetFloat(Prefix + "aiDifficulty", aiDifficulty);
            PlayerPrefs.SetInt(Prefix + "pointsToWin", pointsToWin);
            SaveBool("requireTwoPointLead", requireTwoPointLead);
            PlayerPrefs.SetInt(Prefix + "maxTouches", maxTouchesPerSide);
            SaveBool("serveGoesToLoser", serveGoesToLoser);
            SaveBool("sideOutScoring", sideOutScoring);

            PlayerPrefs.SetFloat(Prefix + "musicVolume", musicVolume);
            PlayerPrefs.SetFloat(Prefix + "sfxVolume", sfxVolume);
            PlayerPrefs.SetInt(Prefix + "blobStyle", (int)blobStyle);
            SaveBool("fullscreen", fullscreen);

            PlayerPrefs.Save();
        }

        static bool LoadBool(string name, bool fallback) => PlayerPrefs.GetInt(Prefix + name, fallback ? 1 : 0) != 0;
        static void SaveBool(string name, bool value) => PlayerPrefs.SetInt(Prefix + name, value ? 1 : 0);

        static void SaveKey(string name, Key key) => PlayerPrefs.SetInt(Prefix + name, (int)key);

        static Key LoadKey(string name, Key fallback)
        {
            int stored = PlayerPrefs.GetInt(Prefix + name, (int)fallback);
            // Une valeur hors énumération viendrait d'une sauvegarde d'une autre version :
            // mieux vaut la disposition d'origine qu'une touche fantôme injouable.
            return System.Enum.IsDefined(typeof(Key), stored) ? (Key)stored : fallback;
        }

        // ------------------------------------------------------------------ application

        /// <summary>Pousse les réglages dans les composants de jeu. Sans effet sur ceux qui manquent.</summary>
        public void ApplyTo(GameManager manager, GameAudio audio, BlobController leftBlob, BlobController rightBlob)
        {
            ApplyControls(leftBlob, p1Left, p1Right, p1Jump);
            ApplyControls(rightBlob, p2Left, p2Right, p2Jump);
            ApplyStyle(leftBlob);
            ApplyStyle(rightBlob);

            if (manager != null)
            {
                manager.rightPlayerIsAi = rightPlayerIsAi;
                manager.aiDifficulty = aiDifficulty;
                manager.pointsToWin = pointsToWin;
                manager.requireTwoPointLead = requireTwoPointLead;
                manager.maxTouchesPerSide = maxTouchesPerSide;
                manager.serveGoesToLoser = serveGoesToLoser;
                manager.sideOutScoring = sideOutScoring;
                manager.ApplyMode();
            }

            if (audio != null)
            {
                audio.musicVolume = musicVolume;
                audio.sfxVolume = sfxVolume;
            }

            // Screen.fullScreen relance le mode d'affichage à chaque affectation, même
            // identique : on ne touche à rien tant que le réglage n'a pas changé.
            if (Screen.fullScreen != fullscreen) Screen.fullScreen = fullscreen;
        }

        void ApplyStyle(BlobController blob)
        {
            if (blob == null) return;

            var animator = blob.GetComponent<BlobAnimator>();
            if (animator != null) animator.SetStyle(blobStyle);
        }

        static void ApplyControls(BlobController blob, Key left, Key right, Key jump)
        {
            if (blob == null) return;

            var human = blob.GetComponent<HumanBlobInput>();
            if (human == null) return;

            human.leftKey = left;
            human.rightKey = right;
            human.jumpKey = jump;
            human.RebindKeys();
        }

        /// <summary>Les six touches, dans l'ordre d'affichage du menu.</summary>
        public Key GetKey(int index) => index switch
        {
            0 => p1Left, 1 => p1Right, 2 => p1Jump,
            3 => p2Left, 4 => p2Right, _ => p2Jump,
        };

        public void SetKey(int index, Key key)
        {
            switch (index)
            {
                case 0: p1Left = key; break;
                case 1: p1Right = key; break;
                case 2: p1Jump = key; break;
                case 3: p2Left = key; break;
                case 4: p2Right = key; break;
                default: p2Jump = key; break;
            }
        }
    }
}
