using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SmilyVolley
{
    /// <summary>
    /// Commandes clavier, lues directement sur le clavier courant du package Input System.
    ///
    /// L'énumération <see cref="Key"/> désigne une POSITION physique sur un clavier QWERTY,
    /// jamais le caractère imprimé sur la touche. <c>Key.A</c> correspond donc à la touche
    /// marquée « Q » sur un clavier AZERTY, et <c>Key.W</c> à celle marquée « Z ».
    /// Le trio A / D / W ci-dessous se lit donc Q / D / Z en AZERTY et A / D / W en QWERTY :
    /// les mêmes touches sous les doigts dans les deux cas.
    ///
    /// Le caractère réellement imprimé est disponible via <see cref="LabelOf"/>, qui interroge
    /// la disposition active du système : c'est ce qui permet à l'aide à l'écran d'afficher
    /// les bonnes lettres sans rien coder en dur.
    ///
    /// <para><b>Le doigt entre ici, et pas ailleurs.</b> Sur mobile, les commandes viennent de
    /// <see cref="TouchInput"/> ; elles s'ajoutent au clavier au lieu de le remplacer, si bien
    /// qu'un joueur passe de l'un à l'autre en cours de partie sans qu'aucun code de bascule
    /// n'existe. C'est aussi ce qui rend le mode contre l'ordinateur correct sans effort : le blob
    /// de droite y a ce composant <b>désactivé</b> au profit de <see cref="AiBlobInput"/>, donc le
    /// tactile suit exactement les camps qu'un humain tient — un troisième fichier d'entrées aurait
    /// eu à redécouvrir cette règle, et à la maintenir en accord.</para>
    ///
    /// <para>La géométrie et la mémoire des doigts, elles, ne sont pas ici : ce composant ne fait
    /// que consulter un état déjà calculé.</para>
    /// </summary>
    public class HumanBlobInput : BlobInput
    {
        [Header("Identité")]
        [Tooltip("Le camp dont ce composant lit les commandes tactiles. Le clavier, lui, " +
                 "est identifié par ses seules touches.")]
        public Side side = Side.Left;

        [Header("Touches principales")]
        public Key leftKey = Key.A;
        public Key rightKey = Key.D;
        public Key jumpKey = Key.W;

        [Header("Touches alternatives (Key.None pour désactiver)")]
        public Key altLeftKey = Key.None;
        public Key altRightKey = Key.None;
        public Key altJumpKey = Key.None;

        // Résoudre une Key en KeyControl passe par l'indexeur du Keyboard à chaque appel.
        // Les commandes sont lues plusieurs fois par image : on garde les contrôles sous la main
        // et on ne les recalcule qu'au changement de périphérique (clavier débranché, rebranché).
        Keyboard boundKeyboard;
        KeyControl left, altLeft, right, altRight, jump, altJump;

        public string LeftLabel => LabelOf(leftKey);
        public string RightLabel => LabelOf(rightKey);
        public string JumpLabel => LabelOf(jumpKey);

        public override float Horizontal
        {
            get
            {
                // Le doigt est lu d'abord et rend la main s'il ne demande rien : sur une tablette
                // avec clavier, les deux périphériques cohabitent, et aucun ne doit annuler l'autre.
                float touch = TouchInput.Horizontal(side);
                if (touch != 0f) return Mathf.Clamp(touch, -1f, 1f);

                if (!EnsureBound()) return 0f;

                float h = 0f;
                if (IsPressed(left, altLeft)) h -= 1f;
                if (IsPressed(right, altRight)) h += 1f;
                return h;
            }
        }

        public override bool JumpHeld
            => TouchInput.JumpHeld(side) || (EnsureBound() && IsPressed(jump, altJump));

        /// <summary>Rebranche les contrôles si le clavier courant a changé. Faux s'il n'y en a aucun.</summary>
        bool EnsureBound()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                boundKeyboard = null;
                return false;
            }

            if (!ReferenceEquals(keyboard, boundKeyboard)) Bind(keyboard);
            return true;
        }

        void Bind(Keyboard keyboard)
        {
            boundKeyboard = keyboard;
            left = Control(keyboard, leftKey);
            altLeft = Control(keyboard, altLeftKey);
            right = Control(keyboard, rightKey);
            altRight = Control(keyboard, altRightKey);
            jump = Control(keyboard, jumpKey);
            altJump = Control(keyboard, altJumpKey);
        }

        /// <summary>
        /// À appeler après avoir changé une touche : les contrôles étant mis en cache,
        /// modifier les champs ne suffit pas à les faire prendre en compte.
        /// </summary>
        public void RebindKeys() => boundKeyboard = null;

        /// <summary>Force la relecture des touches après modification depuis l'Inspector.</summary>
        void OnValidate() => boundKeyboard = null;

        static KeyControl Control(Keyboard keyboard, Key key) => key == Key.None ? null : keyboard[key];

        static bool IsPressed(KeyControl main, KeyControl alt)
            => (main != null && main.isPressed) || (alt != null && alt.isPressed);

        /// <summary>Caractère imprimé sur la touche dans la disposition clavier active.</summary>
        public static string LabelOf(Key key)
        {
            if (key == Key.None) return string.Empty;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return key.ToString();

            string display = keyboard[key].displayName;
            if (string.IsNullOrEmpty(display)) return key.ToString();

            // Les lettres remontent en minuscule ; les noms de touches (« Tab », « Space »)
            // sont déjà correctement capitalisés et ne doivent pas être criés.
            return display.Length == 1 ? display.ToUpperInvariant() : display;
        }
    }
}
