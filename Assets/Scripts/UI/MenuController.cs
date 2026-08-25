using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace SmilyVolley
{
    /// <summary>
    /// Menu principal, options et pause. Le menu se superpose au terrain figé plutôt que
    /// d'occuper une scène à part : le joueur voit ce qu'il règle, et il n'y a ni
    /// chargement ni duplication de la mise en place.
    ///
    /// La navigation est au clavier, comme le jeu — souris acceptée en complément. Le
    /// menu lit le clavier directement, sans passer par le système d'événements de l'UI :
    /// c'est déjà ainsi que le reste du jeu fonctionne, et cela évite d'avoir à faire
    /// coexister deux façons de lire les touches.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        public enum Screen
        {
            None,
            Main,
            Options,
            Pause
        }

        [Header("Références de jeu")]
        public GameManager manager;
        public GameAudio gameAudio;
        public BlobController leftBlob;
        public BlobController rightBlob;
        public HudController hud;
        [Tooltip("Masqué pendant qu'un menu est ouvert : score et aide traversaient le voile " +
                 "et se mêlaient au texte du menu.")]
        public Canvas hudCanvas;

        [Header("Interface")]
        public GameObject root;
        public Text titleText;
        public Text footerText;
        public MenuRow[] rows;

        [Tooltip("L'affiche du jeu, montrée derrière le menu principal seulement : sur la " +
                 "pause et les options, c'est le terrain qu'il faut voir.")]
        public Image splash;
        [Tooltip("Voile posé sur ce qui est derrière. Il s'efface presque devant l'affiche, " +
                 "qui n'a rien à cacher, et se referme sur le terrain pour porter le texte.")]
        public Image veil;
        [Tooltip("Carte de sable qui porte les lignes. Sa hauteur suit le nombre d'entrées " +
                 "affichées : un menu de quatre lignes ne doit pas traîner un panneau vide.")]
        public RectTransform card;
        public float rowHeight = 52f;
        public float cardPadding = 26f;
        [Tooltip("Largeur des lignes quand l'écran porte des réglages : le libellé à gauche, " +
                 "la valeur et ses boutons à droite.")]
        public float wideWidth = 1120f;
        [Tooltip("Largeur des lignes quand l'écran n'a que des entrées à choisir.")]
        public float narrowWidth = 720f;
        [Tooltip("Flèches montrées dans la marge de la carte quand la liste déborde d'un côté " +
                 "ou de l'autre. Des images, non du texte : la police du jeu n'a aucun glyphe de " +
                 "flèche, et un navigateur n'a pas de police système pour l'y suppléer.")]
        public Graphic scrollUp;
        public Graphic scrollDown;
        [Tooltip("Zones touchables des deux flèches. Au doigt, une indication qui ne se touche " +
                 "pas ne fait qu'annoncer ce qu'on ne peut pas atteindre.")]
        public Button scrollUpButton;
        public Button scrollDownButton;
        [Tooltip("Le blob qui montre la ligne choisie, à la place d'un bandeau.")]
        public MenuCursor cursor;
        [Tooltip("Porte le fondu d'ouverture de la carte.")]
        public CanvasGroup cardGroup;

        [Header("Animation")]
        [Tooltip("Durée du fondu à l'ouverture d'un écran.")]
        public float openTime = 0.20f;
        [Tooltip("De combien la carte monte pendant ce fondu.")]
        public float openRise = 34f;

        [Header("Touches du menu")]
        public Key upKey = Key.UpArrow;
        public Key downKey = Key.DownArrow;
        public Key prevKey = Key.LeftArrow;
        public Key nextKey = Key.RightArrow;
        public Key acceptKey = Key.Enter;
        public Key altAcceptKey = Key.Space;
        public Key backKey = Key.Escape;

        [Header("Répétition auto")]
        public float repeatDelay = 0.42f;
        public float repeatInterval = 0.07f;

        [Header("Souris")]
        [Tooltip("Temps minimal entre deux crans de molette. Une molette libre en envoie " +
                 "une dizaine par seconde : sans ce délai, la sélection traverserait l'écran.")]
        public float wheelInterval = 0.05f;

        enum EntryKind { Header, Action, Value, KeyBind }

        readonly struct Entry
        {
            public readonly EntryKind Kind;
            public readonly string Label;
            public readonly System.Func<string> Value;
            public readonly System.Action Activate;
            public readonly System.Action<int> Adjust;

            /// <summary>
            /// L'appui prolongé fait-il défiler ce réglage ? Vrai pour une échelle — vingt
            /// frappes pour monter un volume, sinon. Faux pour un choix : la liste bouclant,
            /// le maintien la ferait tourner sans qu'on puisse s'arrêter dessus.
            /// </summary>
            public readonly bool Repeats;

            public Entry(EntryKind kind, string label, System.Func<string> value,
                System.Action activate, System.Action<int> adjust, bool repeats = false)
            {
                Kind = kind; Label = label; Value = value; Activate = activate; Adjust = adjust;
                Repeats = repeats;
            }

            public bool Selectable => Kind != EntryKind.Header;
        }

        // Opacité du voile. Devant l'affiche il ne sert qu'à asseoir la carte ; devant le
        // terrain il doit délaver assez pour que le texte porte, sans éteindre la plage.
        const float VeilOverSplash = 0.10f;
        const float VeilOverField = 0.38f;

        static readonly float[] DifficultyValues = { 0.15f, 0.40f, 0.65f, 0.85f, 1.00f };
        static readonly string[] DifficultyNames =
            { "Tranquille", "Facile", "Normale", "Redoutable", "Implacable" };
        static readonly int[] PointOptions = { 5, 7, 11, 15, 21 };
        // Dans l'ordre de l'énumération BlobStyle.
        static readonly string[] BlobStyleNames = { "Ferme", "Molle", "Moulée" };

        readonly List<Entry> entries = new List<Entry>(32);
        readonly GameSettings settings = new GameSettings();

        Screen current = Screen.None;
        Screen previous = Screen.Main;
        int selected;
        int scroll;
        int awaitingKey = -1;
        Key heldDirection = Key.None;
        float nextRepeat;
        float nextWheel;
        float opening;
        float cardHome;

        public bool IsOpen => current != Screen.None;

        // ------------------------------------------------------------------ cycle de vie

        void Awake()
        {
            settings.Load();
            settings.ApplyTo(manager, gameAudio, leftBlob, rightBlob);

            if (card != null) cardHome = card.anchoredPosition.y;

            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                int rowIndex = i;
                if (rows[i].button != null) rows[i].button.onClick.AddListener(() => OnRowClicked(rowIndex));
                if (rows[i].decrease != null) rows[i].decrease.onClick.AddListener(() => OnStepClicked(rowIndex, -1));
                if (rows[i].increase != null) rows[i].increase.onClick.AddListener(() => OnStepClicked(rowIndex, 1));
                rows[i].hovered = () => OnRowHovered(rowIndex);
            }

            // Un appui déplace de plusieurs lignes : à une par appui, parcourir l'écran d'options
            // en demanderait une vingtaine, ce qui n'est pas une façon de faire défiler une liste.
            if (scrollUpButton != null) scrollUpButton.onClick.AddListener(() => ScrollBy(-ArrowStep));
            if (scrollDownButton != null) scrollDownButton.onClick.AddListener(() => ScrollBy(ArrowStep));
        }

        /// <summary>Nombre de lignes qu'un appui sur une flèche fait parcourir.</summary>
        const int ArrowStep = 3;

        void Start() => Open(Screen.Main);

        void OnDestroy()
        {
            // Un menu ouvert au moment où la scène se ferme laisserait le temps arrêté
            // pour la scène suivante.
            Time.timeScale = 1f;
        }

        // ------------------------------------------------------------------ ouverture

        public void Open(Screen screen)
        {
            if (screen == Screen.None) { Close(); return; }

            if (current != Screen.Options) previous = current == Screen.None ? Screen.Main : current;

            current = screen;
            awaitingKey = -1;
            selected = 0;
            scroll = 0;

            Time.timeScale = 0f;
            if (manager != null) manager.InputLocked = true;
            if (root != null) root.SetActive(true);
            if (hudCanvas != null) hudCanvas.enabled = false;

            Build();
            Dress();
            SelectFirstSelectable();
            ApplyMusicTheme();

            // Le curseur se repose sur la nouvelle ligne au lieu d'y glisser depuis
            // l'écran précédent, dont les lignes n'ont rien à voir.
            if (cursor != null) cursor.SetVisible(false);
            opening = 1f;

            Refresh();
        }

        public void Close()
        {
            current = Screen.None;
            awaitingKey = -1;

            settings.Save();

            if (root != null) root.SetActive(false);
            if (hudCanvas != null) hudCanvas.enabled = true;
            if (manager != null) manager.InputLocked = false;
            Time.timeScale = 1f;
            ApplyMusicTheme();
        }

        /// <summary>
        /// Choisit le morceau qui accompagne l'écran courant. La musique du menu appartient
        /// à l'affiche, pas au menu en général : sur la pause, on est encore dans le match
        /// — le couper de sa bande sonore le temps de régler un volume ferait deux fondus
        /// enchaînés pour rien. Les options héritent de l'écran d'où on les a ouvertes.
        /// </summary>
        void ApplyMusicTheme()
        {
            if (gameAudio == null) return;

            bool fromMain = current == Screen.Main
                || (current == Screen.Options && previous == Screen.Main);
            gameAudio.SetMenuTheme(fromMain);
        }

        // ------------------------------------------------------------------ boucle

        void Update()
        {
            if (IsOpen)
            {
                AnimateOpening();
                RefreshFooterOnInputSwitch();
            }

            // ⚠ Sur mobile, **Échap n'existe pas**. Sans ce bouton, une partie ne serait ni
            // interruptible ni quittable, et le joueur n'aurait d'autre issue que de fermer
            // l'onglet. Lu avant le clavier — et hors de la garde ci-dessous, qui rendrait la
            // pause tactile impossible sur un appareil sans clavier.
            if (!IsOpen && TouchInput.PausePressedThisFrame())
            {
                Open(Screen.Pause);
                return;
            }

            // ⚠ AVANT la sortie sur clavier absent : un téléphone n'en a pas, et tout ce qui suit
            // ce garde-là n'existerait jamais pour lui. C'est ainsi que le défilement au doigt
            // aurait pu être écrit, compilé, câblé — et rester mort sur le seul appareil qui en a
            // besoin.
            if (IsOpen && awaitingKey < 0) HandleTouchDrag();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (!IsOpen)
            {
                // Échap ouvre la pause en cours de partie.
                if (keyboard[backKey].wasPressedThisFrame) Open(Screen.Pause);
                return;
            }

            if (awaitingKey >= 0)
            {
                CaptureKey(keyboard);
                return;
            }

            if (keyboard[backKey].wasPressedThisFrame) { Back(); return; }

            if (keyboard[acceptKey].wasPressedThisFrame
                || keyboard[Key.NumpadEnter].wasPressedThisFrame
                || keyboard[altAcceptKey].wasPressedThisFrame)
            {
                Activate();
                return;
            }

            HandleDirection(keyboard);
            HandleWheel();
        }

        /// <summary>
        /// Le glissement du doigt fait défiler la liste, comme la molette.
        /// </summary>
        /// <remarks>
        /// <para>⚠ <b>Sans lui, la moitié des options est hors d'atteinte sur téléphone.</b> Le
        /// défilement de ce menu n'existe pas séparément : la fenêtre visible se déduit de la ligne
        /// courante, que seuls le clavier et la molette déplaçaient. Au doigt il n'y a ni l'un ni
        /// l'autre — le joueur touchait les lignes affichées et <b>rien</b> ne lui donnait accès aux
        /// suivantes. Signalé en jouant.</para>
        ///
        /// <para>La distance parcourue est convertie en lignes par le facteur d'échelle du canevas :
        /// <see cref="rowHeight"/> est en unités d'interface, le doigt en pixels d'écran. Les
        /// comparer directement rendrait le défilement quatre fois trop lent sur un téléphone —
        /// c'est-à-dire faux partout sauf à la résolution de référence, la seule où l'on
        /// développe.</para>
        ///
        /// <para>Le reliquat est conservé d'une image à l'autre : un glissement lent avance de moins
        /// d'un pixel par image, et comparer chaque déplacement isolé à la hauteur d'une ligne ne
        /// franchirait jamais le seuil — la liste refuserait de bouger sous un doigt qui bouge.</para>
        /// </remarks>
        void HandleTouchDrag()
        {
            float travelled = TouchInput.ConsumeMenuDragY();
            if (!CanScroll)
            {
                // Consommé quand même : ce qui a été parcouru pendant qu'on ne pouvait pas défiler
                // ne doit pas s'appliquer d'un coup au premier écran qui le peut.
                touchDragPending = 0f;
                return;
            }

            touchDragPending += travelled;

            if (dragCanvas == null) dragCanvas = GetComponent<Canvas>();
            float scale = dragCanvas != null ? Mathf.Max(0.01f, dragCanvas.scaleFactor) : 1f;
            float step = Mathf.Max(1f, rowHeight * scale);

            // Le doigt monte, le contenu monte avec lui : ce qui était en dessous apparaît. C'est
            // le sens du défilement dit naturel, et l'inverse d'une barre de défilement.
            int steps = (int)(touchDragPending / step);
            if (steps == 0) return;

            touchDragPending -= steps * step;
            ScrollBy(steps);
        }

        float touchDragPending;
        Canvas dragCanvas;

        /// <summary>
        /// La molette déplace la sélection, comme les flèches haut et bas.
        ///
        /// Un cran de molette n'arrive que sur une image, mais une molette libre en envoie
        /// une rafale : le délai minimal entre deux pas évite que la sélection traverse
        /// l'écran d'un coup de doigt. Le pas ne dépend pas de l'amplitude rapportée, qui
        /// vaut 120 sur une souris et une fraction sur un pavé tactile.
        /// </summary>
        void HandleWheel()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;
            if (Time.unscaledTime < nextWheel) return;

            nextWheel = Time.unscaledTime + wheelInterval;
            Move(scroll > 0f ? -1 : 1);
        }

        /// <summary>
        /// Déplacement et réglage, avec répétition à l'appui prolongé — sans elle, régler
        /// un volume de 0 à 100 % demanderait vingt frappes.
        /// </summary>
        void HandleDirection(Keyboard keyboard)
        {
            // Le pavé numérique double les flèches de réglage : « + » et « − » disent
            // ce qu'ils font, là où les flèches demandent d'avoir lu le bandeau d'aide.
            Key pressed = Key.None;
            if (keyboard[upKey].isPressed) pressed = upKey;
            else if (keyboard[downKey].isPressed) pressed = downKey;
            else if (keyboard[prevKey].isPressed || keyboard[Key.NumpadMinus].isPressed) pressed = prevKey;
            else if (keyboard[nextKey].isPressed || keyboard[Key.NumpadPlus].isPressed) pressed = nextKey;

            if (pressed == Key.None) { heldDirection = Key.None; return; }

            if (pressed != heldDirection)
            {
                heldDirection = pressed;
                nextRepeat = Time.unscaledTime + repeatDelay;
            }
            else
            {
                bool adjusting = pressed == prevKey || pressed == nextKey;
                if (adjusting && !RepeatsOnHold()) return;

                if (Time.unscaledTime < nextRepeat) return;
                nextRepeat = Time.unscaledTime + repeatInterval;
            }

            if (pressed == upKey) Move(-1);
            else if (pressed == downKey) Move(1);
            else if (pressed == prevKey) Adjust(-1);
            else Adjust(1);
        }

        /// <summary>
        /// La liste déborde-t-elle de ce qu'on en voit, et peut-on la faire défiler ?
        /// </summary>
        /// <remarks>
        /// Faux sur un écran qui tient tout entier : un glissement y déplacerait la sélection sans
        /// rien faire défiler, et le joueur verrait sa ligne courante sauter sous un geste dont il
        /// attendait qu'il ne fasse rien.
        /// </remarks>
        public bool CanScroll => IsOpen && awaitingKey < 0 && rows != null && entries.Count > rows.Length;

        /// <summary>
        /// Fait défiler la liste du nombre de lignes indiqué — positif vers le bas.
        /// </summary>
        /// <remarks>
        /// <para>Passe par <see cref="Move"/>, donc par la <b>sélection</b> : le défilement de ce
        /// menu n'existe pas séparément, la fenêtre visible se déduit de la ligne courante (voir
        /// <see cref="Refresh"/>). Le geste a donc exactement l'effet de plusieurs crans de
        /// molette, boucle comprise — le même geste ne doit pas se comporter de deux façons selon
        /// le périphérique qui l'a produit.</para>
        ///
        /// <para>Appelé par <see cref="HandleTouchDrag"/>, sans quoi l'écran d'options serait
        /// <b>partiellement hors d'atteinte au doigt</b> : il est plus long que l'écran, et ni le
        /// clavier ni la molette n'existent sur un téléphone.</para>
        /// </remarks>
        public void ScrollBy(int steps)
        {
            if (!CanScroll || steps == 0) return;

            int direction = steps > 0 ? 1 : -1;
            for (int i = 0; i < Mathf.Abs(steps); i++) Move(direction);
        }

        void Move(int direction)
        {
            if (entries.Count == 0) return;

            int index = selected;
            for (int step = 0; step < entries.Count; step++)
            {
                index += direction;
                if (index < 0) index = entries.Count - 1;
                if (index >= entries.Count) index = 0;

                if (entries[index].Selectable)
                {
                    selected = index;
                    Refresh();
                    return;
                }
            }
        }

        /// <summary>Le réglage sélectionné se laisse-t-il faire défiler à l'appui prolongé ?</summary>
        bool RepeatsOnHold() => IsValid(selected) && entries[selected].Repeats;

        void Adjust(int direction)
        {
            if (!IsValid(selected)) return;

            entries[selected].Adjust?.Invoke(direction);
            ApplyAndRefresh();

            // Après le rafraîchissement : la ligne montre alors la valeur qui vient de
            // changer, et c'est elle qu'on fait sursauter.
            MenuRow row = RowOf(selected);
            if (row != null) row.Pop();
        }

        void Activate()
        {
            if (!IsValid(selected)) return;

            Entry entry = entries[selected];
            if (entry.Activate != null) entry.Activate();
            else if (entry.Adjust != null) entry.Adjust(1);

            if (IsOpen) ApplyAndRefresh();
        }

        void Back()
        {
            switch (current)
            {
                case Screen.Options: Open(previous); break;
                case Screen.Pause: Close(); break;
                default: break; // le menu principal n'a pas d'écran parent
            }
        }

        /// <summary>Clic sur le − ou le + d'une ligne : la ligne devient courante, puis se règle.</summary>
        void OnStepClicked(int rowIndex, int direction)
        {
            int index = scroll + rowIndex;
            if (!IsValid(index)) return;

            selected = index;
            Adjust(direction);
        }

        /// <summary>
        /// Le curseur survole une ligne : la surbrillance vient l'y trouver. Sans cela, le
        /// joueur clique sur une ligne alors que la sélection en éclaire une autre, et rien
        /// ne lui dit lequel des deux repères le jeu écoute.
        ///
        /// Le survol est ignoré pendant qu'on attend une touche à affecter : la sélection
        /// désigne alors la commande en cours de réaffectation, pas un endroit où aller.
        /// </summary>
        void OnRowHovered(int rowIndex)
        {
            if (!IsOpen || awaitingKey >= 0) return;

            int index = scroll + rowIndex;
            if (index == selected || !IsValid(index)) return;

            selected = index;
            Refresh();
        }

        void OnRowClicked(int rowIndex)
        {
            int index = scroll + rowIndex;
            if (!IsValid(index)) return;

            selected = index;
            Activate();
            if (IsOpen) Refresh();
        }

        bool IsValid(int index) => index >= 0 && index < entries.Count && entries[index].Selectable;

        void ApplyAndRefresh()
        {
            settings.ApplyTo(manager, gameAudio, leftBlob, rightBlob);
            Refresh();
        }

        void SelectFirstSelectable()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Selectable) { selected = i; return; }
            }
            selected = 0;
        }

        // ------------------------------------------------------------------ réaffectation

        void CaptureKey(Keyboard keyboard)
        {
            if (keyboard[backKey].wasPressedThisFrame)
            {
                awaitingKey = -1;
                Refresh();
                return;
            }

            foreach (KeyControl control in keyboard.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;

                Key key = control.keyCode;
                if (key == Key.None) continue;

                settings.SetKey(awaitingKey, key);
                awaitingKey = -1;
                ApplyAndRefresh();
                return;
            }
        }

        // ------------------------------------------------------------------ contenu

        void Build()
        {
            entries.Clear();

            switch (current)
            {
                case Screen.Main: BuildMain(); break;
                case Screen.Pause: BuildPause(); break;
                case Screen.Options: BuildOptions(); break;
            }
        }

        void BuildMain()
        {
            if (titleText != null) titleText.text = "SMILY VOLLEY";

            entries.Add(Action("Jouer contre l'ordinateur", () => StartMatch(true)));
            // « Même clavier » devient faux dès qu'il n'y en a pas : au doigt, les deux joueurs se
            // partagent l'écran, chacun ses trois boutons de son côté.
            entries.Add(Action(TouchInput.Active
                ? "Jouer à deux sur le même écran"
                : "Jouer à deux sur le même clavier", () => StartMatch(false)));
            entries.Add(Action("Options", () => Open(Screen.Options)));
            AddQuit();
        }

        void BuildPause()
        {
            if (titleText != null) titleText.text = "PAUSE";

            entries.Add(Action("Reprendre", Close));
            entries.Add(Action("Rejouer le match", () => { if (manager != null) manager.ResetMatch(); Close(); }));
            entries.Add(Action("Options", () => Open(Screen.Options)));
            entries.Add(Action("Menu principal", () => Open(Screen.Main)));
            AddQuit();
        }

        /// <summary>
        /// Ajoute « Quitter », sauf là où quitter n'existe pas.
        /// </summary>
        /// <remarks>
        /// ⚠ En WebGL, <c>Application.Quit</c> ne fait <b>rien</b> : un onglet ne se ferme pas
        /// lui-même. La ligne s'affichait donc, se sélectionnait, se validait — et il ne se passait
        /// rien. Une commande qui ne répond pas est indiscernable d'un jeu bloqué, et c'est
        /// précisément l'écran où un joueur mobile a le moins de recours.
        /// </remarks>
        void AddQuit()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            entries.Add(Action("Quitter", Quit));
#endif
        }

        void BuildOptions()
        {
            if (titleText != null) titleText.text = "OPTIONS";

            entries.Add(Header("Commandes — joueur 1"));
            AddKeyBind("Gauche", 0);
            AddKeyBind("Droite", 1);
            AddKeyBind("Sauter", 2);

            entries.Add(Header("Commandes — joueur 2"));
            AddKeyBind("Gauche", 3);
            AddKeyBind("Droite", 4);
            AddKeyBind("Sauter", 5);

            entries.Add(Action("Rétablir les commandes d'origine",
                () => { settings.ResetControls(); ApplyAndRefresh(); }));

            entries.Add(Header("Adversaire"));
            entries.Add(Value("Deuxième joueur",
                () => settings.rightPlayerIsAi ? "Ordinateur" : "Humain",
                d => settings.rightPlayerIsAi = !settings.rightPlayerIsAi));
            entries.Add(Value("Difficulté",
                () => settings.rightPlayerIsAi ? DifficultyNames[DifficultyIndex()] : "—",
                d => settings.aiDifficulty = DifficultyValues[
                    Cycle(DifficultyIndex(), d, DifficultyValues.Length)]));

            entries.Add(Header("Règles"));
            entries.Add(Value("Points pour gagner",
                () => settings.pointsToWin.ToString(),
                d => settings.pointsToWin = PointOptions[
                    Cycle(PointIndex(), d, PointOptions.Length)]));
            entries.Add(Value("Écart de deux points",
                () => settings.requireTwoPointLead ? "Exigé" : "Non",
                d => settings.requireTwoPointLead = !settings.requireTwoPointLead));
            entries.Add(Value("Touches par camp",
                () => settings.maxTouchesPerSide <= 0 ? "Illimité" : settings.maxTouchesPerSide + " maximum",
                d => settings.maxTouchesPerSide = settings.maxTouchesPerSide <= 0 ? 3 : 0));
            entries.Add(Value("Comptage",
                () => settings.sideOutScoring ? "Au service" : "Chaque échange",
                d => settings.sideOutScoring = !settings.sideOutScoring));
            entries.Add(Value("Service",
                () => settings.sideOutScoring ? "Au gagnant"
                    : settings.serveGoesToLoser ? "Au perdant du point" : "Au gagnant du point",
                d => settings.serveGoesToLoser = !settings.serveGoesToLoser));

            entries.Add(Header("Son"));
            entries.Add(Scale("Musique",
                () => Percent(settings.musicVolume),
                d => settings.musicVolume = Step(settings.musicVolume, d)));
            entries.Add(Scale("Effets",
                () => Percent(settings.sfxVolume),
                d => settings.sfxVolume = Step(settings.sfxVolume, d)));

            entries.Add(Header("Apparence"));
            entries.Add(Value("Style des blobs",
                () => BlobStyleNames[(int)settings.blobStyle],
                d => settings.blobStyle = (BlobStyle)Cycle(
                    (int)settings.blobStyle, d, BlobStyleNames.Length)));

            entries.Add(Header("Affichage"));
            entries.Add(Value("Plein écran",
                () => settings.fullscreen ? "Oui" : "Non",
                d => settings.fullscreen = !settings.fullscreen));

            entries.Add(Header(string.Empty));
            entries.Add(Action("Tout remettre par défaut",
                () => { settings.ResetToDefaults(); ApplyAndRefresh(); }));
            entries.Add(Action("Retour", () => Open(previous)));
        }

        void AddKeyBind(string label, int index)
        {
            entries.Add(new Entry(EntryKind.KeyBind, label,
                () => awaitingKey == index ? "appuyez sur une touche…" : HumanBlobInput.LabelOf(settings.GetKey(index)),
                () => { awaitingKey = index; Refresh(); },
                null));
        }

        static Entry Header(string label) => new Entry(EntryKind.Header, label, null, null, null);

        static Entry Action(string label, System.Action activate)
            => new Entry(EntryKind.Action, label, null, activate, null);

        static Entry Value(string label, System.Func<string> value, System.Action<int> adjust)
            => new Entry(EntryKind.Value, label, value, null, adjust);

        /// <summary>Réglage continu : l'appui prolongé le fait défiler.</summary>
        static Entry Scale(string label, System.Func<string> value, System.Action<int> adjust)
            => new Entry(EntryKind.Value, label, value, null, adjust, true);

        /// <summary>
        /// Choix suivant dans une liste fermée : après le dernier revient le premier.
        ///
        /// Une liste qui bute à son extrémité ne dit pas au joueur si elle est finie ou si le
        /// menu a cessé de lire le clavier — c'est ce doute qu'on lève en bouclant. Le double
        /// modulo tient le pas négatif : en C#, -1 % 5 vaut -1.
        /// </summary>
        static int Cycle(int index, int direction, int count)
            => count <= 0 ? 0 : ((index + direction) % count + count) % count;

        int DifficultyIndex()
        {
            int best = 0;
            for (int i = 1; i < DifficultyValues.Length; i++)
            {
                if (Mathf.Abs(DifficultyValues[i] - settings.aiDifficulty)
                    < Mathf.Abs(DifficultyValues[best] - settings.aiDifficulty)) best = i;
            }
            return best;
        }

        int PointIndex()
        {
            int best = 0;
            for (int i = 1; i < PointOptions.Length; i++)
            {
                if (Mathf.Abs(PointOptions[i] - settings.pointsToWin)
                    < Mathf.Abs(PointOptions[best] - settings.pointsToWin)) best = i;
            }
            return best;
        }

        static float Step(float value, int direction) => Mathf.Clamp01(Mathf.Round((value + direction * 0.05f) * 100f) / 100f);
        static string Percent(float value) => Mathf.RoundToInt(value * 100f) + " %";

        void StartMatch(bool againstAi)
        {
            settings.rightPlayerIsAi = againstAi;
            settings.ApplyTo(manager, gameAudio, leftBlob, rightBlob);
            if (manager != null) manager.ResetMatch();
            Close();
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ affichage

        /// <summary>
        /// Fondu d'ouverture : la carte monte de quelques pixels en apparaissant. Un écran
        /// qui surgit d'un coup se lit comme un défaut d'affichage ; ce court trajet dit
        /// que le menu arrive par-dessus le jeu.
        /// </summary>
        void AnimateOpening()
        {
            if (opening <= 0f) return;

            opening = Mathf.Max(0f, opening - Time.unscaledDeltaTime / Mathf.Max(0.01f, openTime));
            float eased = opening * opening;

            if (cardGroup != null) cardGroup.alpha = 1f - eased;
            if (card != null)
            {
                card.anchoredPosition = new Vector2(card.anchoredPosition.x,
                    cardHome - openRise * eased);
            }
        }

        /// <summary>
        /// Pose le blob sur la ligne choisie. Sa hauteur est celle de la ligne dans la
        /// carte, et il se cache dès que la sélection sort de ce qui est affiché — sur un
        /// intertitre, par exemple, où il n'y a rien à désigner.
        /// </summary>
        void PlaceCursor()
        {
            if (cursor == null) return;

            int row = selected - scroll;
            bool visible = IsOpen && row >= 0 && row < rows.Length && IsValid(selected);
            cursor.SetVisible(visible);
            if (!visible) return;

            // Les lignes descendent depuis le haut de la liste, elle-même sous la marge
            // haute de la carte : le blob suit la même règle pour rester à leur niveau.
            cursor.Follow(-cardPadding - (row + 0.5f) * rowHeight);
        }

        /// <summary>La ligne affichée pour une entrée, ou <c>null</c> si elle a défilé hors du cadre.</summary>
        MenuRow RowOf(int index)
        {
            int row = index - scroll;
            return rows != null && row >= 0 && row < rows.Length ? rows[row] : null;
        }

        /// <summary>
        /// Habille l'écran courant : l'affiche derrière le menu principal, le terrain
        /// derrière la pause et les options.
        ///
        /// Le menu principal n'a pas de partie à montrer, et l'affiche dit le jeu mieux
        /// qu'un terrain vide ; son logo rend alors le titre écrit inutile. Les deux autres
        /// écrans arrivent au-dessus d'un match, que le joueur doit continuer à voir — un
        /// réglage se juge sur ce qu'il change.
        /// </summary>
        void Dress()
        {
            bool onSplash = current == Screen.Main && splash != null && splash.sprite != null;

            if (splash != null) splash.enabled = onSplash;
            if (veil != null)
            {
                Color color = veil.color;
                color.a = onSplash ? VeilOverSplash : VeilOverField;
                veil.color = color;
            }
            if (titleText != null) titleText.enabled = !onSplash;
        }

        /// <summary>
        /// Ajuste la carte au nombre de lignes réellement affichées : quatre entrées ne
        /// doivent pas traîner le panneau d'un écran d'options. Elle est ancrée en bas et
        /// grandit vers le haut, si bien qu'un menu court se pose sous le logo de l'affiche.
        /// </summary>
        void FitCard()
        {
            if (card == null || rows == null) return;

            int visible = Mathf.Clamp(entries.Count, 1, rows.Length);
            float width = HasValues() ? wideWidth : narrowWidth;

            card.sizeDelta = new Vector2(width + cardPadding * 2f, visible * rowHeight + cardPadding * 2f);
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null && rows[i].rect != null)
                    rows[i].rect.sizeDelta = new Vector2(width, rowHeight);
            }
        }

        /// <summary>L'écran courant affiche-t-il une valeur à droite d'un libellé ?</summary>
        bool HasValues()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Value != null) return true;
            }
            return false;
        }

        void Refresh()
        {
            if (rows == null || rows.Length == 0) return;

            FitCard();

            // La sélection reste dans la fenêtre visible : le menu d'options est plus long
            // que l'écran, il faut le faire défiler sous le curseur.
            if (selected < scroll) scroll = selected;
            else if (selected >= scroll + rows.Length) scroll = selected - rows.Length + 1;
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, entries.Count - rows.Length));

            bool canGoUp = scroll > 0;
            bool canGoDown = scroll + rows.Length < entries.Count;
            if (scrollUp != null) scrollUp.enabled = canGoUp;
            if (scrollDown != null) scrollDown.enabled = canGoDown;

            // La cible se retire avec sa flèche : un bouton invisible qui répond encore là où plus
            // rien ne s'affiche avalerait les appuis destinés à la ligne qui se trouve dessous.
            if (scrollUpButton != null) scrollUpButton.gameObject.SetActive(canGoUp);
            if (scrollDownButton != null) scrollDownButton.gameObject.SetActive(canGoDown);

            for (int i = 0; i < rows.Length; i++)
            {
                int index = scroll + i;
                if (index >= entries.Count) { rows[i].Hide(); continue; }

                Entry entry = entries[index];
                if (entry.Kind == EntryKind.Header)
                {
                    rows[i].ShowHeader(entry.Label);
                    continue;
                }

                rows[i].Show(entry.Label, entry.Value != null ? entry.Value() : string.Empty,
                    index == selected,
                    entry.Kind == EntryKind.KeyBind && awaitingKey >= 0 && index == selected,
                    entry.Adjust != null);
            }

            PlaceCursor();

            if (footerText != null) footerText.text = BuildFooter();
        }

        /// <summary>
        /// Réécrit le pied de page quand le joueur passe du clavier au doigt, ou l'inverse.
        /// </summary>
        /// <remarks>
        /// Sans cela, le rappel resterait celui du périphérique en usage à l'<b>ouverture</b> du
        /// menu — c'est-à-dire, au tout premier écran d'une partie mobile, le clavier : aucun doigt
        /// ne s'est encore posé, et le menu accueille donc le joueur en lui parlant d'une touche
        /// Entrée qu'il n'a pas. Le reste de la carte n'est redessiné que sur un changement de
        /// sélection, ce que le premier contact ne provoque pas toujours.
        /// </remarks>
        void RefreshFooterOnInputSwitch()
        {
            bool touch = TouchInput.Active;
            if (touch == footerIsTouch) return;

            footerIsTouch = touch;
            if (footerText != null) footerText.text = BuildFooter();
        }

        bool footerIsTouch;

        /// <summary>
        /// Le rappel des commandes du menu, en bas de la carte.
        /// </summary>
        /// <remarks>
        /// ⚠ <b>Au doigt, chacune de ces phrases est un mensonge</b> — et ce sont les premières que
        /// lit un joueur mobile. Il n'a ni molette, ni Entrée, ni Échap : il touche la ligne qu'il
        /// veut, et les <c>−</c> / <c>+</c> pour régler. La règle « un menu annonce ses touches »
        /// dit en fait « annonce comment on s'en sert » ; sans clavier, la réponse est le doigt.
        /// Un texte peut être <b>correct et faux</b>.
        /// </remarks>
        string BuildFooter()
        {
            if (awaitingKey >= 0)
            {
                // Réaffecter une touche demande un clavier : sur mobile, cette ligne ne s'affiche
                // que si le joueur en a branché un, puisque c'est lui qui a ouvert la capture.
                return "Appuyez sur la touche à affecter   —   Échap : annuler";
            }

            if (TouchInput.Active)
            {
                return current switch
                {
                    // ⚠ Le glissement s'annonce, sinon il n'existe pas : la liste d'options est plus
                    // longue que l'écran et rien, au doigt, ne suggère qu'on peut la faire défiler.
                    Screen.Options => "Glissez pour faire défiler   —   touchez une ligne   —   − et + pour régler",
                    Screen.Pause => "Touchez « Reprendre » pour retourner au match",
                    _ => "Touchez une ligne pour la choisir",
                };
            }

            return current switch
            {
                Screen.Options =>
                    "Haut/Bas ou molette : naviguer   —   Gauche/Droite ou + − : régler   —   Entrée : valider   —   Échap : retour",
                Screen.Pause =>
                    "Haut/Bas ou molette : naviguer   —   Entrée : valider   —   Échap : reprendre",
                _ =>
                    "Haut/Bas ou molette : naviguer   —   Entrée : valider",
            };
        }
    }
}
