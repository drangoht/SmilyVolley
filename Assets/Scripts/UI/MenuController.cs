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

        public bool IsOpen => current != Screen.None;

        // ------------------------------------------------------------------ cycle de vie

        void Awake()
        {
            settings.Load();
            settings.ApplyTo(manager, gameAudio, leftBlob, rightBlob);

            if (rows == null) return;
            for (int i = 0; i < rows.Length; i++)
            {
                int rowIndex = i;
                if (rows[i].button != null) rows[i].button.onClick.AddListener(() => OnRowClicked(rowIndex));
                if (rows[i].decrease != null) rows[i].decrease.onClick.AddListener(() => OnStepClicked(rowIndex, -1));
                if (rows[i].increase != null) rows[i].increase.onClick.AddListener(() => OnStepClicked(rowIndex, 1));
            }
        }

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
            SelectFirstSelectable();
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
        }

        // ------------------------------------------------------------------ boucle

        void Update()
        {
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
            // ce qu'ils font, là où « ← → » demandent d'avoir lu le bandeau d'aide.
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
            entries.Add(Action("Jouer à deux sur le même clavier", () => StartMatch(false)));
            entries.Add(Action("Options", () => Open(Screen.Options)));
            entries.Add(Action("Quitter", Quit));
        }

        void BuildPause()
        {
            if (titleText != null) titleText.text = "PAUSE";

            entries.Add(Action("Reprendre", Close));
            entries.Add(Action("Rejouer le match", () => { if (manager != null) manager.ResetMatch(); Close(); }));
            entries.Add(Action("Options", () => Open(Screen.Options)));
            entries.Add(Action("Menu principal", () => Open(Screen.Main)));
            entries.Add(Action("Quitter", Quit));
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

        void Refresh()
        {
            if (rows == null || rows.Length == 0) return;

            // La sélection reste dans la fenêtre visible : le menu d'options est plus long
            // que l'écran, il faut le faire défiler sous le curseur.
            if (selected < scroll) scroll = selected;
            else if (selected >= scroll + rows.Length) scroll = selected - rows.Length + 1;
            scroll = Mathf.Clamp(scroll, 0, Mathf.Max(0, entries.Count - rows.Length));

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

            if (footerText != null) footerText.text = BuildFooter();
        }

        string BuildFooter()
        {
            if (awaitingKey >= 0) return "Appuyez sur la touche à affecter   —   Échap : annuler";

            return current switch
            {
                Screen.Options =>
                    "↑ ↓ molette : naviguer   —   ← → + − : régler   —   Entrée : valider   —   Échap : retour",
                Screen.Pause =>
                    "↑ ↓ molette : naviguer   —   Entrée : valider   —   Échap : reprendre",
                _ =>
                    "↑ ↓ molette : naviguer   —   Entrée : valider",
            };
        }
    }
}
