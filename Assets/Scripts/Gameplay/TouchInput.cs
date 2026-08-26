using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SmilyVolley
{
    /// <summary>
    /// Le <b>seul</b> fichier du jeu qui lise une dalle tactile. Il tient l'état des doigts ; la
    /// géométrie vit dans <see cref="TouchZones"/>, et la traduction en déplacement dans
    /// <see cref="HumanBlobInput"/>.
    ///
    /// <para><b>Pourquoi une classe statique hors des scènes.</b> Le pompage est installé en
    /// <c>BeforeSceneLoad</c> sur un objet <c>DontDestroyOnLoad</c>. Quand un invariant est porté
    /// par le cycle de vie d'un écran, un tiers peut l'annuler sans qu'aucune erreur ne le dise :
    /// un doigt qui cesse d'être lu au chargement produirait un joueur qui ne bouge plus et une
    /// console vide.</para>
    ///
    /// <para><b>Le déplacement a une mémoire, le saut n'en a pas.</b> Le bouton de saut est fixe :
    /// un doigt commande ce qu'il recouvre en ce moment, et son état se recalcule en entier à
    /// chaque image. Le déplacement, lui, est un <i>glissement</i> — et un glissement peut sortir
    /// de la moitié d'écran où il a commencé. ⚠ Sans mémoire, un doigt de gauche qui franchit le
    /// milieu se mettrait à piloter <b>le blob de l'autre joueur</b>, ce qui est exactement le
    /// geste qu'un joueur fait quand il court vers le filet. Chaque doigt appartient donc au camp
    /// où il s'est posé, jusqu'à ce qu'il se lève.</para>
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
        /// clavier. S'y fier afficherait des commandes tactiles sur une machine de bureau.</para>
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
        /// Les commandes de jeu doivent-elles capter les doigts ?
        /// </summary>
        /// <remarks>
        /// <b>Fermé hors d'un match</b>, et c'est délibéré : tout doigt appartient alors à uGUI. Une
        /// zone de glissement qui resterait active dans les menus volerait les appuis destinés aux
        /// boutons, et un menu qui ne répond pas est le pire symptôme possible sur mobile — le
        /// joueur n'a alors aucun recours. C'est <see cref="TouchHud"/> qui ouvre et referme cette
        /// porte, si bien que le dessin et la lecture ne peuvent pas diverger sur le mode.
        /// </remarks>
        public static bool GameControlsEnabled { get; private set; }

        /// <summary>
        /// Un seul camp joue-t-il au doigt ? Décide de l'agencement — voir <see cref="TouchZones"/>.
        /// </summary>
        public static bool Solo { get; private set; } = true;

        /// <summary>Le camp que tient le joueur unique. N'a de sens que si <see cref="Solo"/>.</summary>
        /// <remarks>
        /// <b>Il ne suffit pas de savoir qu'un seul camp joue, il faut savoir lequel.</b> Le doigt
        /// désigne un endroit du terrain en le pointant : la moitié d'écran où l'on glisse est
        /// <i>déjà</i> la moitié de terrain où le blob court, et les deux ne peuvent pas être
        /// choisies séparément. Un joueur qui prend le camp de droite glisse donc à droite, et son
        /// bouton de saut passe à gauche — voir <see cref="TouchZones.JumpCenter"/>.
        /// </remarks>
        public static Side SoloSide { get; private set; } = Side.Left;

        /// <summary>Ouvre ou referme la capture des doigts, et fixe l'agencement.</summary>
        /// <remarks>
        /// La refermer <b>relâche immédiatement</b> les commandes en cours : sans cela, un doigt
        /// posé au moment où la pause s'ouvre resterait posé, et le blob repartirait vers ce point
        /// à la reprise, sans que personne n'ait rien touché.
        /// </remarks>
        public static void SetGameControls(bool enabled, bool solo, Side soloSide)
        {
            GameControlsEnabled = enabled;
            Solo = solo;
            SoloSide = soloSide;
            if (!enabled) ReleaseAll();
        }

        /// <summary>Un doigt désigne-t-il en ce moment un endroit du terrain pour ce camp ?</summary>
        public static bool HasMoveTarget(Side side) => State(side).MoveFinger != NoFinger;

        /// <summary>
        /// Abscisse du doigt qui pilote ce camp, en pixels écran. N'a de sens que si
        /// <see cref="HasMoveTarget"/>.
        /// </summary>
        /// <remarks>
        /// Seule l'abscisse est publiée : le blob ne se déplace que sur un axe, et l'ordonnée du
        /// doigt ne veut rien dire. Le joueur peut donc glisser à la hauteur qui l'arrange — en bas
        /// de l'écran, loin des blobs qu'il regarde.
        /// </remarks>
        public static float MoveScreenX(Side side) => State(side).MoveScreenX;

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

        /// <summary>Le bouton de saut est-il tenu, sans rémanence ? (pour l'enfoncer visuellement)</summary>
        /// <remarks>
        /// Sans la rémanence de <see cref="JumpHeld"/> : elle sert à ne pas perdre un saut, pas à
        /// laisser un bouton allumé sous un doigt qui n'est plus là.
        /// </remarks>
        public static bool JumpDrawnHeld(Side side) => State(side).Jump;

        /// <summary>Le bouton de pause est-il tenu ?</summary>
        public static bool PauseDrawnHeld => pauseHeld;

        // ─── Le glissement dans les menus ─────────────────────────────────────

        /// <summary>
        /// Déplacement vertical du doigt depuis la dernière lecture, en pixels écran, <b>hors
        /// match</b>. Positif vers le haut. <b>Consommé à la lecture.</b>
        /// </summary>
        /// <remarks>
        /// <para><b>Pourquoi ce n'est pas uGUI qui s'en charge.</b> uGUI sait faire glisser
        /// (<c>IDragHandler</c>), et c'était la première version. Mais son glissement ne naît que si
        /// le pointeur <i>bouge après</i> avoir été enfoncé, sur des images distinctes : tout ce qui
        /// enfonce et déplace dans la même image ne franchit jamais son seuil, et l'événement
        /// n'arrive pas. Le composant vivait, ses nombres étaient bons, et il ne recevait rien —
        /// mesuré à l'écran, faute de pouvoir lire un journal dans un build de production.</para>
        ///
        /// <para>Le lire ici met le défilement <b>sur le même chemin que le déplacement des
        /// blobs</b>, qui lui fonctionne : la position brute de la dalle, image par image. Un
        /// mécanisme éprouvé vaut mieux qu'un mécanisme correct qu'on ne peut pas éprouver.</para>
        ///
        /// <para>Un <b>tapotement ne produit rien</b> : le doigt qui se pose n'accumule aucun
        /// déplacement, et c'est uGUI qui reçoit le clic sur la ligne — les deux ne se marchent pas
        /// dessus.</para>
        /// </remarks>
        public static float ConsumeMenuDragY()
        {
            float travelled = menuTravel;
            menuTravel = 0f;
            return travelled;
        }

        // ─── État ─────────────────────────────────────────────────────────────

        const int NoFinger = int.MinValue;

        /// <summary>Ce qu'un camp a sous les doigts.</summary>
        sealed class SideState
        {
            /// <summary>Identifiant du doigt qui pilote le déplacement, ou <see cref="NoFinger"/>.</summary>
            public int MoveFinger = NoFinger;

            /// <summary>Abscisse écran de ce doigt.</summary>
            public float MoveScreenX;

            public bool Jump;

            /// <summary>Image du dernier appui ARRIVÉ sur le saut — voir <see cref="JumpHeld"/>.</summary>
            public int JumpFrame = -1;

            public void Clear()
            {
                MoveFinger = NoFinger;
                MoveScreenX = 0f;
                Jump = false;
                JumpFrame = -1;
            }
        }

        static readonly SideState leftState = new SideState();
        static readonly SideState rightState = new SideState();
        static bool pauseHeld;
        static int pausePressedFrame = -1;

        // Le doigt qui glisse dans un menu, et ce qu'il a parcouru depuis la dernière lecture.
        static int menuFinger = NoFinger;
        static float menuLastY;
        static float menuTravel;

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
            // session à l'autre : sans cette remise à zéro, un doigt tenu à l'arrêt du jeu resterait
            // tenu au lancement suivant.
            ReleaseAll();
            ReleaseMenuDrag();
            active = false;
            touchCapable = false;
            lastTouchFrame = int.MinValue / 2;
            pausePressedFrame = -1;
            GameControlsEnabled = false;
            Solo = true;
            SoloSide = Side.Left;

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
        /// <b>C'est ce qui rend les commandes vérifiables sans téléphone.</b> Sans lui, il n'y a
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
        /// <para>La solution facile aurait été de dessiner des commandes de démonstration : elle
        /// aurait montré une image et validé <i>autre chose</i> que le code du jeu. Ici la souris
        /// crée un doigt <b>réel</b> — le chemin parcouru est exactement celui d'un joueur.</para>
        ///
        /// <para>Ce que cela ne couvre pas : le multi-touch. Glisser <i>et</i> presser le saut
        /// demande deux doigts, donc un vrai écran ou l'émulation du navigateur — et à deux
        /// joueurs, quatre doigts à la fois.</para>
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
                ReleaseMenuDrag();
                UpdateActiveLatch(sawTouch: false);
                return;
            }

            if (!GameControlsEnabled)
            {
                // Hors match, le seul geste qui compte est le glissement des menus.
                PollMenuDrag(screen);
                UpdateActiveLatch(AnyTouch(screen));
                return;
            }

            ReleaseMenuDrag();

            float w = Screen.width;
            float h = Screen.height;
            bool solo = Solo;

            // Le saut est fixe : son état se recalcule en entier. Seule l'image du dernier appui
            // survit, parce qu'elle date un événement et non un état.
            leftState.Jump = false;
            rightState.Jump = false;

            bool sawTouch = false;
            bool nextPause = false;

            // ─── Première passe : les doigts DÉJÀ attachés à un déplacement ───
            // Ils gardent leur camp où qu'ils aillent, y compris au-delà du milieu de l'écran ou
            // par-dessus un bouton. C'est toute la raison d'être de cette mémoire.
            bool leftSeen = false, rightSeen = false;

            foreach (TouchControl touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;

                int id = touch.touchId.ReadValue();
                if (id == leftState.MoveFinger)
                {
                    leftState.MoveScreenX = touch.position.ReadValue().x;
                    leftSeen = true;
                }
                else if (id == rightState.MoveFinger)
                {
                    rightState.MoveScreenX = touch.position.ReadValue().x;
                    rightSeen = true;
                }
            }

            if (!leftSeen) leftState.MoveFinger = NoFinger;
            if (!rightSeen) rightState.MoveFinger = NoFinger;

            // ─── Seconde passe : tous les autres doigts ───
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

                int id = touch.touchId.ReadValue();
                if (id == leftState.MoveFinger || id == rightState.MoveFinger) continue;

                Vector2 position = touch.position.ReadValue();

                // La pause d'abord : elle n'appartient à aucun camp, et un bouton commun testé après
                // les camps serait avalé par celui dans la moitié duquel il se trouve.
                if (TouchZones.IsPause(position.x, position.y, w, h))
                {
                    nextPause = true;
                    if (arrived) pausePressedFrame = Time.frameCount;
                    continue;
                }

                // En solo, un seul camp a un joueur : interroger l'autre reviendrait à répondre
                // pour un blob que l'ordinateur pilote.
                if (solo)
                {
                    Apply(SoloSide, id, position, solo, w, h, arrived);
                    continue;
                }

                if (Apply(Side.Left, id, position, solo, w, h, arrived)) continue;
                Apply(Side.Right, id, position, solo, w, h, arrived);
            }

            pauseHeld = nextPause;
            UpdateActiveLatch(sawTouch);
        }

        /// <summary>
        /// Suit le doigt qui glisse dans un menu et cumule son déplacement vertical.
        /// </summary>
        /// <remarks>
        /// Un seul doigt à la fois, celui qui s'est posé le premier : à deux doigts, la somme de
        /// deux gestes contraires ferait tressauter la liste. Le doigt qui se pose n'ajoute rien —
        /// seul ce qu'il parcourt <i>ensuite</i> compte, faute de quoi le simple fait de toucher
        /// une ligne la ferait défiler sous le doigt qui la choisit.
        /// </remarks>
        static void PollMenuDrag(Touchscreen screen)
        {
            // Le doigt déjà suivi garde la main, où qu'il soit dans la liste des contacts.
            foreach (TouchControl touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;
                if (touch.touchId.ReadValue() != menuFinger) continue;

                float y = touch.position.ReadValue().y;
                menuTravel += y - menuLastY;
                menuLastY = y;
                return;
            }

            // Sinon, le premier doigt posé devient celui qu'on suit.
            foreach (TouchControl touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;

                menuFinger = touch.touchId.ReadValue();
                menuLastY = touch.position.ReadValue().y;
                return;
            }

            ReleaseMenuDrag();
        }

        /// <summary>Oublie le doigt du menu, et ce qu'il avait parcouru sans être lu.</summary>
        static void ReleaseMenuDrag()
        {
            menuFinger = NoFinger;
            menuTravel = 0f;
        }

        /// <summary>Un doigt quelconque est-il posé ? Sert au seul latch quand la porte est fermée.</summary>
        static bool AnyTouch(Touchscreen screen)
        {
            foreach (TouchControl touch in screen.touches)
            {
                if (touch.press.wasPressedThisFrame || touch.press.isPressed) return true;
            }
            return false;
        }

        /// <summary>
        /// Range un doigt dans les commandes d'un camp. Faux s'il ne lui appartient pas —
        /// l'appelant essaie alors l'autre camp.
        /// </summary>
        static bool Apply(Side side, int id, Vector2 position, bool solo, float w, float h, bool arrived)
        {
            SideState state = State(side);

            switch (TouchZones.Hit(position.x, position.y, side, solo, w, h))
            {
                case TouchTarget.Jump:
                    state.Jump = true;
                    if (arrived) state.JumpFrame = Time.frameCount;
                    return true;

                case TouchTarget.Move:
                    // Un seul doigt pilote un camp : le second qui se pose ne vole pas la main au
                    // premier, sans quoi une paume posée à plat ferait tressauter le blob entre
                    // deux points au gré de l'ordre de lecture de la dalle.
                    if (state.MoveFinger == NoFinger)
                    {
                        state.MoveFinger = id;
                        state.MoveScreenX = position.x;
                    }
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
            // clic ferait donc disparaître les commandes au moment même où le joueur les touche. On
            // ne tranche que sur le clavier, qui n'a aucun équivalent tactile.
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
        /// <para>24 pixels, soit environ 4 mm, laisse passer le tremblement d'un pouce sans empêcher
        /// un vrai glissement — celui qui fait défiler la liste d'options parcourt bien davantage.
        /// Posé une seule fois, au premier contact : sur une machine sans dalle, le seuil de la
        /// souris reste intact.</para>
        /// </remarks>
        static void WidenDragThreshold()
        {
            EventSystem events = EventSystem.current;
            if (events != null && events.pixelDragThreshold < TouchDragThreshold)
                events.pixelDragThreshold = TouchDragThreshold;
        }

        const int TouchDragThreshold = 24;

        /// <summary>
        /// Relâche les commandes de JEU.
        /// </summary>
        /// <remarks>
        /// ⚠ Ne touche pas au glissement des menus, et c'est un défaut corrigé : le HUD appelle
        /// <see cref="SetGameControls"/>(false) à <b>chaque image</b> tant qu'un menu est ouvert —
        /// ce n'est pas une transition, c'est un état réaffirmé. Effacer le glissement ici le
        /// remettait donc à zéro juste avant que le menu ne le lise, et la liste ne bougeait pas
        /// d'un pixel sous un doigt qui la parcourait.
        /// </remarks>
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
