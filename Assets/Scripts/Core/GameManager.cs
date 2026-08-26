using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SmilyVolley
{
    public enum MatchState
    {
        Serving,
        Rally,
        PointScored,
        MatchOver
    }

    /// <summary>
    /// Arbitre du match : service, comptage des touches, attribution des points et fin de partie.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Références")]
        public BallController ball;
        public BlobController leftBlob;
        public BlobController rightBlob;
        public HudController hud;

        [Header("Mode de jeu")]
        [Tooltip("Décoché : deux joueurs sur le même clavier. La touche Tab bascule en cours de partie.")]
        public bool rightPlayerIsAi = true;
        [Tooltip("Contre l'ordinateur : coché, le joueur tient le camp de DROITE et l'IA celui de gauche. " +
                 "Au doigt, c'est le côté de l'écran où l'on glisse ; au clavier, les touches du joueur 2.")]
        public bool soloPlayerOnRight = false;
        [Range(0f, 1f)] public float aiDifficulty = 0.65f;

        [Header("Règles")]
        public int pointsToWin = 15;
        public bool requireTwoPointLead = true;
        [Tooltip("Coché : seul le serveur peut marquer (règle historique de Blobby Volley).")]
        public bool sideOutScoring = false;
        [Tooltip("Le camp qui vient de perdre le point engage. Ignoré si Side Out Scoring est coché : " +
                 "cette règle-là suppose que le gagnant garde le service, sinon le score ne monterait jamais.")]
        public bool serveGoesToLoser = true;
        [Tooltip("Nombre de touches consécutives autorisées par camp. 0 ou moins : aucune limite.")]
        public int maxTouchesPerSide = 0;

        [Header("Service")]
        public float groundY = -4f;
        public float serveHeight = 3.6f;
        [Tooltip("Décalage de la balle vers le filet au service. À zéro, elle tombe pile sur " +
                 "le sommet du blob et n'en repart qu'à la verticale.")]
        public float serveOffsetX = 0.4f;
        public float serveDelay = 1.1f;
        public float pointPause = 1.8f;

        [Header("Commandes globales")]
        public Key restartKey = Key.R;
        // Tab occupe la même position sur tous les claviers, contrairement aux lettres.
        public Key toggleModeKey = Key.Tab;

        MatchState state = MatchState.Serving;
        Side server = Side.Left;
        int leftScore;
        int rightScore;
        int touchCount;
        Side? lastTouchSide;
        Coroutine pendingRoutine;

        // Les raccourcis globaux sont sondés à chaque image : on résout les contrôles une
        // fois plutôt que de repasser par l'indexeur du Keyboard 60 fois par seconde.
        Keyboard boundKeyboard;
        KeyControl restartControl;
        KeyControl toggleModeControl;

        HumanBlobInput rightHuman;
        AiBlobInput rightAi;
        HumanBlobInput leftHuman;
        AiBlobInput leftAi;

        /// <summary>
        /// Le camp que tient le joueur unique. Vaut le camp gauche hors solo, où il ne sert à rien.
        /// </summary>
        /// <remarks>
        /// Lu par le HUD tactile, qui en déduit la moitié d'écran sensible et le bord où poser le
        /// bouton de saut. Ces deux-là ne se choisissent pas séparément : le doigt <b>pointe</b> le
        /// terrain, donc la moitié d'écran où l'on glisse est celle du blob qu'on déplace.
        /// </remarks>
        public Side SoloSide => rightPlayerIsAi && soloPlayerOnRight ? Side.Right : Side.Left;

        // Périphérique auquel le HUD s'adresse en ce moment. Voir Update.
        bool hintIsTouch;

        /// <summary>Un point vient d'être attribué au camp indiqué.</summary>
        public event System.Action<Side> PointScored;

        /// <summary>Le camp indiqué remporte le match.</summary>
        public event System.Action<Side> MatchWon;

        public MatchState State => state;

        /// <summary>
        /// Coupe les raccourcis globaux pendant qu'un menu est ouvert. Sans cela, régler
        /// une touche sur « R » relancerait le match dans la foulée.
        /// </summary>
        public bool InputLocked { get; set; }

        void OnEnable()
        {
            if (ball != null)
            {
                ball.BlobHit += OnBlobHit;
                ball.GroundHit += OnGroundHit;
            }
        }

        void OnDisable()
        {
            if (ball != null)
            {
                ball.BlobHit -= OnBlobHit;
                ball.GroundHit -= OnGroundHit;
            }
        }

        void Start()
        {
            ApplyMode();
            ResetMatch();
        }

        void Update()
        {
            // Le passage du clavier au doigt, ou l'inverse, réécrit tout ce que le HUD annonce.
            // Suivi ici plutôt qu'au moment du contact : le joueur peut brancher un clavier, ou
            // poser son premier doigt, à n'importe quelle image — y compris pendant un échange,
            // où rien d'autre ne redessine le bandeau.
            if (TouchInput.Active != hintIsTouch) ApplyMode();

            if (InputLocked) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                boundKeyboard = null;
                return;
            }

            if (!ReferenceEquals(keyboard, boundKeyboard))
            {
                boundKeyboard = keyboard;
                restartControl = restartKey == Key.None ? null : keyboard[restartKey];
                toggleModeControl = toggleModeKey == Key.None ? null : keyboard[toggleModeKey];
            }

            if (restartControl != null && restartControl.wasPressedThisFrame) ResetMatch();

            if (toggleModeControl != null && toggleModeControl.wasPressedThisFrame)
            {
                rightPlayerIsAi = !rightPlayerIsAi;
                ApplyMode();
                ResetMatch();
            }
        }

        /// <summary>Donne chaque blob à sa source de commandes : un humain, ou l'ordinateur.</summary>
        /// <remarks>
        /// Les <b>deux</b> camps sont réaffectés à chaque appel, et non le seul camp de droite : en
        /// solo, l'ordinateur prend celui que le joueur ne tient pas, et ce peut être l'un ou
        /// l'autre. Ne toucher qu'à un camp laisserait, le temps d'une bascule, soit deux humains
        /// soit deux ordinateurs sur le terrain — le premier cas passe inaperçu jusqu'à ce qu'un
        /// blob cesse de bouger, le second jusqu'à ce que la partie se joue toute seule.
        /// </remarks>
        public void ApplyMode()
        {
            if (rightBlob == null) return;

            if (rightHuman == null) rightHuman = rightBlob.GetComponent<HumanBlobInput>();
            if (rightAi == null) rightAi = rightBlob.GetComponent<AiBlobInput>();
            if (leftBlob != null)
            {
                if (leftHuman == null) leftHuman = leftBlob.GetComponent<HumanBlobInput>();
                if (leftAi == null) leftAi = leftBlob.GetComponent<AiBlobInput>();
            }

            Bind(rightHuman, rightAi, rightPlayerIsAi && !soloPlayerOnRight);
            Bind(leftHuman, leftAi, rightPlayerIsAi && soloPlayerOnRight);

            rightBlob.RefreshInput();
            if (leftBlob != null) leftBlob.RefreshInput();

            if (hud != null)
            {
                // « Même clavier » cesse d'être vrai dès qu'il n'y en a pas : au doigt, les deux
                // joueurs se partagent l'écran, chacun ses boutons de son côté.
                string duo = TouchInput.Active ? "2 joueurs — même écran" : "2 joueurs — même clavier";
                hud.SetMode(rightPlayerIsAi ? "1 joueur — contre l'ordinateur" : duo);
                hud.SetHint(BuildHint());
            }

            hintIsTouch = TouchInput.Active;
        }

        /// <summary>Donne un blob à l'ordinateur, ou le rend à son joueur.</summary>
        /// <remarks>
        /// La difficulté est posée même sur l'IA qu'on éteint : le réglage doit valoir pour le camp
        /// qu'elle reprendra, sans quoi un joueur qui change de côté au milieu d'un réglage
        /// affronterait la difficulté d'avant.
        /// </remarks>
        void Bind(HumanBlobInput human, AiBlobInput ai, bool aiPlays)
        {
            if (human != null) human.enabled = !aiPlays;
            if (ai == null) return;

            ai.enabled = aiPlays;
            ai.difficulty = aiDifficulty;
        }

        /// <summary>
        /// Compose l'aide à l'écran avec les caractères réellement imprimés sur les touches :
        /// « Q / D » sur un clavier AZERTY, « A / D » sur un QWERTY, sans rien coder en dur.
        /// </summary>
        /// <remarks>
        /// <para>⚠ <b>Au doigt, ce bandeau est faux mot pour mot</b> : il nomme des touches que le
        /// joueur n'a pas. Il dit alors autre chose — et ce qu'il dit compte, parce que le
        /// déplacement au doigt est le seul geste du jeu qui <b>ne se voie pas</b> : il n'y a pas
        /// de bouton à repérer, juste une moitié d'écran où glisser. Un joueur qui l'ignore croit
        /// que son blob ne répond pas.</para>
        ///
        /// <para>Le bandeau a pu le dire parce que le bas de l'écran s'est libéré : il portait un
        /// pavé directionnel, qui le recouvrait autant qu'il recouvrait les blobs.</para>
        /// </remarks>
        string BuildHint()
        {
            if (TouchInput.Active)
            {
                // « Même tout en bas » n'est pas une politesse : SEULE L'ABSCISSE du doigt est lue.
                // Le joueur peut donc glisser au ras du sable, loin des blobs et de la balle, et
                // rendre à sa propre vue l'écran que sa main couvre. La propriété existe depuis le
                // premier jour du tactile ; rien ne la disait, et personne ne la découvre seul —
                // une main se pose là où l'on regarde, c'est-à-dire en plein milieu du jeu.
                string half = SoloSide == Side.Right ? "droite" : "gauche";

                return rightPlayerIsAi
                    ? $"Glissez le doigt dans la moitié {half} de l'écran, même tout en bas   —   le bouton pour sauter"
                    : "Chacun glisse de son côté de l'écran, même tout en bas   —   son bouton pour sauter";
            }

            string move = "Q / D";
            string jump = "Z";

            // Les touches annoncées sont celles du blob que le joueur tient RÉELLEMENT : en solo à
            // droite, ce sont les flèches et non Q / D. Nommer les autres enverrait le joueur
            // presser des touches qui pilotent le blob de l'ordinateur.
            HumanBlobInput soloHuman = SoloSide == Side.Right ? rightHuman : leftHuman;
            if (soloHuman == null)
            {
                BlobController blob = SoloSide == Side.Right ? rightBlob : leftBlob;
                if (blob != null) soloHuman = blob.GetComponent<HumanBlobInput>();
                if (SoloSide == Side.Right) rightHuman = soloHuman; else leftHuman = soloHuman;
            }

            if (soloHuman != null)
            {
                move = soloHuman.LeftLabel + " / " + soloHuman.RightLabel;
                jump = soloHuman.JumpLabel;
            }

            string toggle = HumanBlobInput.LabelOf(toggleModeKey);
            string restart = HumanBlobInput.LabelOf(restartKey);

            return rightPlayerIsAi
                ? $"{move} : se déplacer   —   {jump} : sauter   —   {toggle} : 2 joueurs   —   {restart} : rejouer"
                : $"J1 : {move} + {jump}      J2 : Gauche / Droite + Haut      {toggle} : contre l'ordinateur   —   {restart} : rejouer";
        }

        public void ResetMatch()
        {
            if (pendingRoutine != null)
            {
                StopCoroutine(pendingRoutine);
                pendingRoutine = null;
            }

            leftScore = 0;
            rightScore = 0;
            server = Side.Left;
            if (hud != null) hud.SetScore(0, 0);

            // Pas de dégel ici : StartServe est seul maître du blocage des blobs, et il
            // les fige de toute façon jusqu'au lâcher de balle — y compris en sortant
            // d'un match terminé, où ils étaient déjà bloqués.
            StartServe();
        }

        void StartServe()
        {
            state = MatchState.Serving;
            touchCount = 0;
            lastTouchSide = null;

            if (leftBlob != null) leftBlob.ResetToStart();
            if (rightBlob != null) rightBlob.ResetToStart();

            // Les deux camps restent bloqués jusqu'au lâcher de balle. Après un point ils
            // le sont déjà depuis AwardPoint ; l'appel compte pour le premier service et
            // pour la relance au clavier, qui ne passent pas par là.
            SetBlobsFrozen(true);

            PlaceBallForServe();

            if (hud != null) hud.ShowMessage("Service : " + server.Label());

            pendingRoutine = StartCoroutine(ReleaseBallRoutine());
        }

        /// <summary>
        /// Fige la balle au-dessus du camp qui engage, hors simulation.
        /// Appelée dès le point marqué et non au début du service : sinon la balle
        /// continuerait de rebondir pendant toute la pause, et le joueur ne verrait
        /// qu'à la fin de celle-ci à qui revient l'engagement.
        /// </summary>
        void PlaceBallForServe()
        {
            if (ball == null) return;

            BlobController serving = server == Side.Left ? leftBlob : rightBlob;
            float x = serving != null ? serving.StartPosition.x : 0f;

            // Décalée vers le filet plutôt que pile au-dessus du blob : la balle attaque
            // alors le flanc du blob et repart naturellement en biais, sans dépendre du
            // garde-fou de BallController.
            x -= serveOffsetX * server.Sign();

            ball.Freeze(new Vector2(x, groundY + serveHeight));
        }

        IEnumerator ReleaseBallRoutine()
        {
            yield return new WaitForSeconds(serveDelay);
            if (hud != null) hud.ClearMessage();

            // Rendre la main exactement au moment où la balle part : les deux joueurs
            // partent de la même ligne, personne n'a d'avance sur l'autre.
            SetBlobsFrozen(false);
            if (ball != null) ball.Release();

            state = MatchState.Rally;
            pendingRoutine = null;
        }

        void OnBlobHit(BlobController blob)
        {
            if (state != MatchState.Rally || blob == null) return;

            if (lastTouchSide.HasValue && lastTouchSide.Value == blob.side)
            {
                touchCount++;
            }
            else
            {
                lastTouchSide = blob.side;
                touchCount = 1;
            }

            if (maxTouchesPerSide > 0 && touchCount > maxTouchesPerSide)
            {
                AwardPoint(blob.side.Opposite(), maxTouchesPerSide + " touches pour " + blob.side.Label());
            }
        }

        void OnGroundHit(Vector2 position)
        {
            if (state != MatchState.Rally) return;

            Side landingSide = position.x < 0f ? Side.Left : Side.Right;
            Side winner = landingSide.Opposite();
            AwardPoint(winner, "Point pour " + winner.Label());
        }

        void AwardPoint(Side winner, string reason)
        {
            if (state != MatchState.Rally) return;
            state = MatchState.PointScored;

            bool winnerScores = !sideOutScoring || winner == server;
            if (winnerScores)
            {
                if (winner == Side.Left) leftScore++;
                else rightScore++;
            }

            // Le perdant engage : le camp qui vient de marquer n'enchaîne pas deux services.
            server = serveGoesToLoser && !sideOutScoring ? winner.Opposite() : winner;

            // L'échange est terminé : tout s'arrête, la balle comme les blobs, et plus rien
            // ne bouge jusqu'au lâcher de balle suivant. La balle se replace côté serveur
            // plutôt que de continuer à rebondir — au risque de retoucher le sol et de
            // brouiller la lecture du point — et les joueurs lâchent la partie le temps
            // d'afficher le score : s'agiter pendant le message n'avancerait à rien, ils
            // sont replacés sur leur ligne au service.
            PlaceBallForServe();
            SetBlobsFrozen(true);

            if (hud != null)
            {
                hud.SetScore(leftScore, rightScore);
                hud.ShowMessage(winnerScores ? reason : "Changement de service : " + winner.Label());
            }

            PointScored?.Invoke(winner);

            if (HasWon(winner))
            {
                EndMatch(winner);
                return;
            }

            pendingRoutine = StartCoroutine(NextPointRoutine());
        }

        bool HasWon(Side side)
        {
            int score = side == Side.Left ? leftScore : rightScore;
            int other = side == Side.Left ? rightScore : leftScore;
            if (score < pointsToWin) return false;
            return !requireTwoPointLead || score - other >= 2;
        }

        void EndMatch(Side winner)
        {
            state = MatchState.MatchOver;
            SetBlobsFrozen(true);
            if (hud != null)
            {
                // ⚠ Sans clavier, « Appuyez sur R » enferme le joueur sur l'écran de fin : c'est la
                // dernière phrase d'un match, et elle désignerait une touche qui n'existe pas. La
                // relance passe alors par le bouton de pause, seul endroit où elle est atteignable.
                string relance = TouchInput.Active
                    ? "Touchez Pause, en haut à droite, pour rejouer"
                    : "Appuyez sur " + HumanBlobInput.LabelOf(restartKey) + " pour rejouer";

                hud.ShowMessage(winner.Label() + " gagne le match !\n" + relance);
            }

            MatchWon?.Invoke(winner);
        }

        IEnumerator NextPointRoutine()
        {
            yield return new WaitForSeconds(pointPause);
            pendingRoutine = null;
            StartServe();
        }

        void SetBlobsFrozen(bool frozen)
        {
            if (leftBlob != null) leftBlob.Frozen = frozen;
            if (rightBlob != null) rightBlob.Frozen = frozen;
        }
    }
}
