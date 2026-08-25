using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SmilyVolley
{
    /// <summary>
    /// Le <b>seul</b> fichier du jeu qui lise une dalle tactile. Il tient l'état des doigts et le
    /// traduit en commandes par camp ; la géométrie, elle, vit dans <see cref="TouchZones"/>.
    ///
    /// <para><b>Pourquoi une classe statique hors des scènes.</b> Le pompage est installé en
    /// <c>BeforeSceneLoad</c> sur un objet <c>DontDestroyOnLoad</c>. Quand un invariant est porté
    /// par le cycle de vie d'un écran, un tiers peut l'annuler sans qu'aucune erreur ne le dise :
    /// un stick qui cesse d'être lu au chargement produirait un joueur qui ne bouge plus et une
    /// console vide. Le jeu n'a aujourd'hui qu'une scène — la garantie tient donc sans effort, et
    /// continuera de tenir si une seconde apparaît.</para>
    ///
    /// <para><b>Les boutons de ce jeu sont FIXES, et cela change tout.</b> Un joystick flottant
    /// n'existe que par la mémoire de l'endroit où le doigt s'est posé ; il faut alors suivre
    /// chaque doigt par son identifiant, d'une image à l'autre. Ici, un doigt commande ce qu'il
    /// recouvre <i>en ce moment</i> : l'état se recalcule entièrement à chaque image, sans aucune
    /// mémoire. C'est plus court, insensible aux doigts perdus — et cela offre gratuitement le
    /// geste qu'on attend d'un pavé directionnel : glisser de « gauche » à « droite » sans lever
    /// le pouce.</para>
    ///
    /// <para><b>Repère</b> : pixels écran, origine en bas à gauche.</para>
    /// </summary>
    public static class TouchInput
    {
        // ─── Ce que le reste du jeu consulte ──────────────────────────────────

        /// <summary>
        /// Le joueur se sert-il de ses doigts ? <b>Latché au premier vrai contact</b>, relâché dès
        /// qu'une touche du clavier arrive.
        /// </summary>
        /// <remarks>
        /// <para>⚠ <c>Touchscreen.current != null</c> ne répond <b>pas</b> à cette question : un
        /// portable Windows à écran tactile en déclare une alors que son propriétaire joue au
        /// clavier. S'y fier afficherait un pavé directionnel par-dessus le terrain sur une machine
        /// de bureau.</para>
        ///
        /// <para>La bascule est <b>réversible</b> : sur une tablette avec clavier, le joueur passe
        /// de l'un à l'autre en cours de partie et l'affichage doit suivre.</para>
        /// </remarks>
        public static bool Active => active || Forced;
        static bool active;

        /// <summary>
        /// Cet appareil a-t-il une dalle tactile qui a <b>déjà servi</b> ? <b>Jamais relâché.</b>
        /// </summary>
        /// <remarks>
        /// <para><b>Pourquoi une seconde notion, et pas <see cref="Active"/>.</b> Les deux répondent
        /// à des questions différentes : « le joueur se sert-il de ses doigts en ce moment ? »
        /// décide de ce qu'on <b>affiche</b>, et doit basculer dans les deux sens ; « cet appareil
        /// est-il tactile ? » décide de ce qui est <b>possible</b>, et la réponse ne redevient
        /// jamais non.</para>
        ///
        /// <para>C'est celle-ci que lit la garde d'orientation. <b>Tourner son téléphone ne cesse
        /// pas d'en faire un téléphone</b> — et un contact produit aussi un clic de compatibilité,
        /// qui ferait retomber <see cref="Active"/> au moment précis où le joueur touche l'écran.
        /// </para>
        /// </remarks>
        public static bool TouchCapable => touchCapable || Forced;
        static bool touchCapable;

        /// <summary>
        /// Les contrôles de jeu doivent-ils capter les doigts ?
        /// </summary>
        /// <remarks>
        /// <b>Fermé par défaut</b>, et c'est délibéré : hors d'un match, tout doigt appartient à
        /// uGUI. Un pavé qui resterait actif dans les menus volerait les appuis destinés aux
        /// boutons, et un menu qui ne répond pas est le pire symptôme possible sur mobile — le
        /// joueur n'a alors aucun recours. C'est <see cref="TouchHud"/>, l'objet qui <i>dessine</i>
        /// ces contrôles, qui ouvre et referme cette porte, si bien que les deux ne peuvent pas
        /// diverger sur la géométrie ni sur le mode.
        /// </remarks>
        public static bool GameControlsEnabled { get; private set; }

        /// <summary>
        /// Un seul camp joue-t-il au doigt ? Décide de l'agencement des boutons — voir
        /// <see cref="TouchZones"/>.
        /// </summary>
        /// <remarks>
        /// Posé par le HUD tactile depuis le mode du <see cref="GameManager"/> : contre
        /// l'ordinateur, le joueur unique étale ses commandes sur toute la largeur ; à deux, chacun
        /// se replie sur son bord. Le même drapeau sert au dessin et à la lecture.
        /// </remarks>
        public static bool Solo { get; private set; } = true;

        /// <summary>Ouvre ou referme la capture des doigts, et fixe l'agencement.</summary>
        /// <remarks>
        /// La refermer <b>relâche immédiatement</b> les commandes en cours : sans cela, un pavé
        /// tenu au moment où la pause s'ouvre resterait tenu, et le joueur repartirait dans cette
        /// direction à la reprise, sans avoir rien touché.
        /// </remarks>
        public static void SetGameControls(bool enabled, bool solo)
        {
            GameControlsEnabled = enabled;
            Solo = solo;
            if (!enabled) ReleaseAll();
        }

        /// <summary>Déplacement demandé au doigt pour ce camp : -1, 0 ou 1.</summary>
        /// <remarks>
        /// Dérivé des deux appuis, et non l'inverse. Les stocker séparément coûte un booléen et
        /// évite un défaut d'affichage : deux doigts posés en même temps sur « gauche » et
        /// « droite » donnent un déplacement nul — correct — mais un état déduit de ce zéro
        /// éteindrait <b>les deux</b> boutons alors que le joueur les touche.
        /// </remarks>
        public static float Horizontal(Side side)
        {
            SideState s = State(side);
            return (s.Right ? 1f : 0f) - (s.Left ? 1f : 0f);
        }

        /// <summary>Le bouton de saut de ce camp est-il tenu ?</summary>
        /// <remarks>
        /// ⚠ Vrai aussi pendant les quelques images qui suivent un <b>tapotement</b> relevé entre
        /// deux images. Sans cette rémanence, le geste le plus naturel sur un bouton — poser et
        /// lever aussitôt — ne produirait un saut qu'une fois sur deux : le blob ne saute que s'il
        /// touche le sol à l'image où la demande est vraie, et cette image-là peut tomber en plein
        /// vol. Le défaut se signale « le saut ne répond pas toujours », c'est-à-dire de la façon la
        /// plus coûteuse à instruire.
        /// </remarks>
        public static bool JumpHeld(Side side)
        {
            SideState s = State(side);
            return s.Jump || Fresh(s.JumpFrame);
        }

        /// <summary>La pause vient-elle d'être demandée ? <b>Consommé à la lecture.</b></summary>
        /// <remarks>
        /// <para>⚠ <b>Un appui ne peut PAS être publié comme « cette image-ci ».</b> Le pompage vit
        /// sur un objet créé en <c>BeforeSceneLoad</c>, et Unity ne garantit pas l'ordre des
        /// <c>Update</c> entre objets : le menu qui interroge la pause s'exécute donc, une fois sur
        /// deux, <b>avant</b> le pompage. Il lirait une image trop tôt, et à l'image suivante
        /// l'événement serait déjà périmé — l'appui disparaît, bouton parfaitement placé, zone
        /// parfaitement calculée, aucune erreur.</para>
        ///
        /// <para>La parade tient en deux parties : l'événement <b>survit</b> quelques images, et il
        /// est <b>consommé</b> par son lecteur pour ne pas se déclencher deux fois.</para>
        /// </remarks>
        public static bool PausePressedThisFrame()
        {
            if (pausePressedFrame < 0) return false;

            bool fresh = Fresh(pausePressedFrame);
            pausePressedFrame = -1;
            return fresh;
        }

        // ─── Ce que le HUD consulte pour dessiner ─────────────────────────────

        /// <summary>Le pavé de ce camp est-il tenu du côté indiqué ? (pour l'enfoncer visuellement)</summary>
        public static bool PadHeld(Side side, bool right)
        {
            SideState s = State(side);
            return right ? s.Right : s.Left;
        }

        /// <summary>Le bouton de saut est-il tenu, sans rémanence ? (pour l'enfoncer visuellement)</summary>
        /// <remarks>
        /// Sans la rémanence de <see cref="JumpHeld"/> : elle sert à ne pas perdre un saut, pas à
        /// laisser un bouton allumé sous un doigt qui n'est plus là.
        /// </remarks>
        public static bool JumpDrawnHeld(Side side) => State(side).Jump;

        /// <summary>Le bouton de pause est-il tenu ?</summary>
        public static bool PauseDrawnHeld => pauseHeld;

        // ─── État ─────────────────────────────────────────────────────────────

        /// <summary>Ce qu'un camp a sous les doigts. Recalculé en entier à chaque image.</summary>
        sealed class SideState
        {
            public bool Left, Right, Jump;

            /// <summary>Image du dernier appui ARRIVÉ sur le saut — voir <see cref="JumpHeld"/>.</summary>
            public int JumpFrame = -1;

            public void Clear()
            {
                Left = Right = Jump = false;
                JumpFrame = -1;
            }
        }

        static readonly SideState leftState = new SideState();
        static readonly SideState rightState = new SideState();
        static bool pauseHeld;
        static int pausePressedFrame = -1;

        static SideState State(Side side) => side == Side.Left ? leftState : rightState;

        /// <summary>
        /// Nombre d'images pendant lesquelles un appui reste relevable. Trois : de quoi couvrir
        /// n'importe quel ordre d'exécution et un tapotement bref, sans jamais retarder une action
        /// d'assez pour que ça se sente.
        /// </summary>
        const int EventLifetime = 3;

        static bool Fresh(int frame) => frame >= 0 && Time.frameCount - frame <= EventLifetime;

        // ─── Installation ─────────────────────────────────────────────────────

        static GameObject host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            // ⚠ Rejoué à chaque entrée en mode Play dans l'éditeur, où les statiques survivent d'une
            // session à l'autre : sans cette remise à zéro, un bouton tenu à l'arrêt du jeu resterait
            // tenu au lancement suivant.
            ReleaseAll();
            active = false;
            touchCapable = false;
            lastTouchFrame = int.MinValue / 2;
            pausePressedFrame = -1;
            GameControlsEnabled = false;
            Solo = true;

            if (host != null) return;

            host = new GameObject("[TouchInput]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<TouchInputPump>();

            EnableSimulationIfForced();
        }

        // ─── Mode forcé ───────────────────────────────────────────────────────

        /// <summary>
        /// Le mode tactile est-il forcé ? <c>--touch</c> en ligne de commande, <c>?touch</c> dans
        /// l'URL de la version web.
        /// </summary>
        /// <remarks>
        /// <b>C'est ce qui rend les contrôles vérifiables sans téléphone.</b> Sans lui, il n'y a
        /// rien à regarder sur la machine où l'on développe — et une interface qu'on ne peut pas
        /// afficher est une interface qu'on juge sur son code.
        /// </remarks>
        public static bool Forced
        {
            get
            {
                if (!forcedResolved)
                {
                    forcedResolved = true;
                    forced = ReadForcedFlag();
                }
                return forced;
            }
        }

        static bool forced;
        static bool forcedResolved;

        static bool ReadForcedFlag()
        {
            // En web, la ligne de commande n'existe pas : l'URL en tient lieu. Le séparateur fait
            // partie du motif — sans lui, n'importe quel chemin d'hébergement contenant « touch »
            // basculerait le jeu en tactile chez un joueur, ce qu'aucun journal ne dirait.
            string url = Application.absoluteURL;
            if (!string.IsNullOrEmpty(url) && (url.Contains("?touch") || url.Contains("&touch"))) return true;

            foreach (string argument in System.Environment.GetCommandLineArgs())
            {
                if (argument == "--touch") return true;
            }

            return false;
        }

        /// <summary>
        /// Sous le mode forcé, fait passer la souris pour un doigt — un vrai <c>Touchscreen</c>,
        /// alimenté par le paquet Input System lui-même.
        /// </summary>
        /// <remarks>
        /// <para>La solution facile aurait été de dessiner des boutons de démonstration : elle
        /// aurait montré une image et validé <i>autre chose</i> que le code du jeu. Ici la souris
        /// crée un doigt <b>réel</b> — le chemin parcouru est exactement celui d'un joueur.</para>
        ///
        /// <para>Ce que cela ne couvre pas : le multi-touch. Tenir le pavé <i>et</i> presser le saut
        /// demande deux doigts, donc un vrai écran ou l'émulation du navigateur — et à deux joueurs,
        /// quatre doigts à la fois.</para>
        /// </remarks>
        static void EnableSimulationIfForced()
        {
            if (!Forced) return;

            UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Enable();
            Debug.Log("[TOUCH] Mode tactile forcé : la souris est simulée en doigt.");
        }

        // ─── Pompage ──────────────────────────────────────────────────────────

        /// <summary>
        /// Une image de lecture de la dalle. Appelée par <see cref="TouchInputPump"/>, jamais
        /// ailleurs.
        /// </summary>
        internal static void Poll()
        {
            Touchscreen screen = Touchscreen.current;

            // Pas de dalle : l'état pendant doit être relâché, sinon un débranchement — ou le
            // passage de l'émulateur mobile du navigateur à la souris — laisserait un blob courir
            // tout seul.
            if (screen == null)
            {
                ReleaseAll();
                UpdateActiveLatch(sawTouch: false);
                return;
            }

            float w = Screen.width;
            float h = Screen.height;
            bool solo = Solo;

            // Les boutons de ce jeu sont FIXES : un doigt commande ce qu'il recouvre en ce moment.
            // On repart donc de rien à chaque image, sans suivre aucun doigt d'une image à l'autre —
            // ce qui offre au passage le geste qu'on attend d'un pavé : glisser de « gauche » à
            // « droite » sans lever le pouce. Seule l'image du dernier appui sur le saut survit,
            // parce qu'elle date un événement et non un état.
            int leftJumpFrame = leftState.JumpFrame;
            int rightJumpFrame = rightState.JumpFrame;
            leftState.Clear();
            rightState.Clear();
            leftState.JumpFrame = leftJumpFrame;
            rightState.JumpFrame = rightJumpFrame;

            bool sawTouch = false;
            bool nextPause = false;

            foreach (TouchControl touch in screen.touches)
            {
                // ⚠ L'ARRIVÉE se teste EN PLUS du maintien, jamais à sa place. Un appui posé et
                // relevé entre deux images se présente ici avec `isPressed` déjà à false : filtrer
                // sur le seul maintien **avale le tapotement**, qui est pourtant le geste le plus
                // naturel sur un bouton.
                bool arrived = touch.press.wasPressedThisFrame;
                bool held = touch.press.isPressed;
                if (!arrived && !held) continue;

                sawTouch = true;
                if (!GameControlsEnabled) continue;

                Vector2 position = touch.position.ReadValue();

                // La pause d'abord : elle n'appartient à aucun camp, et un bouton commun testé après
                // les camps serait avalé par celui dans la moitié duquel il se trouve.
                if (TouchZones.IsPause(position.x, position.y, w, h))
                {
                    nextPause = true;
                    if (arrived) pausePressedFrame = Time.frameCount;
                    continue;
                }

                if (Apply(Side.Left, position, solo, w, h, arrived)) continue;

                // En solo, tous les boutons appartiennent au camp de gauche : interroger celui de
                // droite y reviendrait à répondre pour un blob que l'ordinateur pilote.
                if (!solo) Apply(Side.Right, position, solo, w, h, arrived);
            }

            pauseHeld = nextPause;

            UpdateActiveLatch(sawTouch);
        }

        /// <summary>
        /// Range un doigt dans les commandes d'un camp. Faux s'il ne touche aucun de ses boutons —
        /// l'appelant essaie alors l'autre camp.
        /// </summary>
        static bool Apply(Side side, Vector2 position, bool solo, float w, float h, bool arrived)
        {
            SideState state = State(side);

            switch (TouchZones.Hit(position.x, position.y, side, solo, w, h))
            {
                case TouchTarget.Left:
                    state.Left = true;
                    return true;

                case TouchTarget.Right:
                    state.Right = true;
                    return true;

                case TouchTarget.Jump:
                    state.Jump = true;
                    if (arrived) state.JumpFrame = Time.frameCount;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Bascule entre « le joueur a des doigts » et « le joueur a un clavier ».</summary>
        static void UpdateActiveLatch(bool sawTouch)
        {
            if (sawTouch)
            {
                if (!active) WidenDragThreshold();
                active = true;
                touchCapable = true;
                lastTouchFrame = Time.frameCount;
                return;
            }

            if (!active) return;

            // ⚠ **Un appui du doigt produit AUSSI un clic de souris** sur la plupart des navigateurs
            // : c'est l'événement de compatibilité, hérité du web d'avant le tactile. Relâcher sur un
            // clic ferait donc disparaître les contrôles au moment même où le joueur les touche. On
            // ne tranche que sur le clavier, qui n'a aucun équivalent tactile — et la fenêtre
            // ci-dessous garde la trace du dernier vrai contact au cas où un clic viendrait à
            // compter un jour.
            if (Time.frameCount - lastTouchFrame <= CompatibilityClickFrames) return;

            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) active = false;
        }

        /// <summary>
        /// Fenêtre, en images, pendant laquelle un événement de souris est tenu pour l'écho d'un
        /// contact tactile. Une vingtaine d'images, soit un tiers de seconde : les navigateurs
        /// émettent leur clic de compatibilité jusqu'à ~300 ms après le relâchement du doigt.
        /// </summary>
        const int CompatibilityClickFrames = 20;

        static int lastTouchFrame = int.MinValue / 2;

        /// <summary>
        /// Élargit le seuil au-delà duquel uGUI requalifie un appui en glissement.
        /// </summary>
        /// <remarks>
        /// <para>⚠ <b>Le défaut classique du tactile sur uGUI, et il se signale « les boutons ne
        /// marchent pas ».</b> Le seuil par défaut est de 10 pixels, calibré pour une souris — qui
        /// ne bouge pas quand on clique. Un doigt, lui, roule de deux ou trois millimètres pendant
        /// l'appui : sur une dalle où un pixel logique vaut 0,2 mm, le seuil est franchi presque à
        /// chaque fois. uGUI conclut alors à un glissement et <b>le bouton ne reçoit jamais son
        /// clic</b>. Aucune erreur, aucun symptôme dans un journal : le menu paraît simplement
        /// mort.</para>
        ///
        /// <para>24 pixels, soit environ 4 mm, laisse passer le tremblement d'un pouce. Posé une
        /// seule fois, au premier contact : sur une machine sans dalle, le seuil de la souris reste
        /// intact.</para>
        /// </remarks>
        static void WidenDragThreshold()
        {
            EventSystem events = EventSystem.current;
            if (events != null && events.pixelDragThreshold < TouchDragThreshold)
                events.pixelDragThreshold = TouchDragThreshold;
        }

        const int TouchDragThreshold = 24;

        static void ReleaseAll()
        {
            leftState.Clear();
            rightState.Clear();
            pauseHeld = false;
        }

        /// <summary>
        /// L'objet qui appelle <see cref="Poll"/> une fois par image, hors de toute scène.
        /// </summary>
        /// <remarks>
        /// <c>Update</c> et non <c>FixedUpdate</c> : les entrées se lisent au rythme de l'affichage,
        /// et un <c>wasPressedThisFrame</c> lu à une autre cadence manque les appuis brefs — un
        /// tapotement de saut dure moins qu'un pas de physique.
        /// </remarks>
        sealed class TouchInputPump : MonoBehaviour
        {
            void Update() => Poll();
        }
    }
}
