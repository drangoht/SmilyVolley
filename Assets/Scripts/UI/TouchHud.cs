using UnityEngine;
using UnityEngine.UI;

namespace SmilyVolley
{
    /// <summary>
    /// Dessine les commandes tactiles — un bouton de saut par camp, le bouton de pause, et le
    /// repère qui suit le doigt qui déplace un blob — et ouvre la capture des doigts pendant un
    /// match.
    ///
    /// <para><b>Celui qui montre est celui qui écoute.</b> C'est ce composant, et lui seul, qui
    /// appelle <see cref="TouchInput.SetGameControls"/>. Séparer les deux responsabilités laisserait
    /// exister un état où des commandes répondent sans se voir — un doigt posé pendant un menu
    /// ferait alors courir un blob derrière le panneau.</para>
    ///
    /// <para><b>Il n'y a presque plus rien à dessiner</b>, et c'est le but : le déplacement est un
    /// glissement libre dans sa moitié d'écran, pas un bouton à viser. Ce qui occupait le bas de
    /// l'écran — donc la bande où vivent les blobs — a disparu ; il ne reste qu'un bouton de saut
    /// au coin, et un repère sous le doigt pour dire au joueur où le jeu croit qu'il pointe.</para>
    ///
    /// <para><b>Les positions ne sont pas décidées ici</b> : elles viennent de
    /// <see cref="TouchZones"/>, qui sert aussi à la lecture. Le canevas est en
    /// <c>ConstantPixelSize</c> à l'échelle 1 et ses ancres sont au coin bas-gauche, si bien que les
    /// coordonnées écran de la dalle s'y posent <b>sans aucune conversion</b>.</para>
    /// </summary>
    public class TouchHud : MonoBehaviour
    {
        [Header("Références")]
        public GameManager manager;
        public MenuController menu;

        [Header("Sprites")]
        public Sprite discSprite;
        public Sprite triangleSprite;
        public Sprite squareSprite;

        // Très translucide au repos : ces boutons couvrent le sable, et le joueur regarde la balle,
        // pas ses pouces. Un contrôle tactile qu'on remarque est un contrôle tactile de trop.
        [Header("Teintes")]
        public Color idleColor = new Color(1f, 0.99f, 0.94f, 0.22f);
        public Color heldColor = new Color(1f, 0.99f, 0.94f, 0.62f);
        public Color glyphColor = new Color(0.16f, 0.26f, 0.36f, 0.62f);
        public Color glyphHeldColor = new Color(0.10f, 0.20f, 0.30f, 0.92f);

        [Tooltip("La colonne posée sous le doigt qui déplace un blob. Plus discrète encore que " +
                 "les boutons : elle traverse tout l'écran, y compris le ciel où vole la balle.")]
        public Color markerColor = new Color(1f, 0.99f, 0.94f, 0.16f);

        /// <summary>Ce qu'un camp affiche.</summary>
        class SideControls
        {
            public Image Jump, JumpGlyph;

            /// <summary>Le repère posé à l'abscisse du doigt, tant qu'il pilote le blob.</summary>
            public Image Marker;
        }

        Canvas canvas;
        SideControls left, right;
        Image pauseDisc, pauseBarA, pauseBarB;

        // L'agencement ne se recalcule qu'au changement : rotation de l'appareil, barre d'URL qui
        // se rétracte, passage d'un mode à l'autre. Le reste du temps, seuls les teintes et le
        // repère bougent.
        int laidOutWidth = -1;
        int laidOutHeight = -1;
        bool laidOutSolo;
        bool laidOutBuilt;

        void Awake()
        {
            canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = false;
        }

        void OnDisable()
        {
            // Un composant désactivé ne dessine plus : laisser la porte ouverte donnerait des
            // commandes invisibles qui répondent encore.
            TouchInput.SetGameControls(false, TouchInput.Solo);
        }

        /// <summary>
        /// <c>LateUpdate</c> et non <c>Update</c> : le mode du match peut changer dans l'image
        /// courante (la touche de bascule, un choix de menu), et l'agencement doit refléter ce qui
        /// sera vrai à l'affichage, pas ce qui l'était au début de l'image.
        /// </summary>
        void LateUpdate()
        {
            bool solo = manager == null || manager.rightPlayerIsAi;
            bool inMatch = menu == null || !menu.IsOpen;
            bool visible = inMatch && TouchInput.Active;

            // ⚠ La porte suit le MATCH, pas l'affichage — et la nuance est un défaut évité. Un
            // joueur qui pose son tout premier doigt le fait forcément alors qu'aucune commande
            // n'est encore dessinée : c'est ce contact-là qui apprend au jeu qu'il y a des doigts.
            // Fermer la porte tant que rien n'est visible **avalerait donc systématiquement le
            // premier geste de la partie**. La condition qui protège vraiment quelque chose est
            // l'autre : hors match, tout doigt appartient à uGUI.
            TouchInput.SetGameControls(inMatch, solo);

            if (canvas != null) canvas.enabled = visible;
            if (!visible) return;

            EnsureLayout(solo);
            Paint(solo);
        }

        // ------------------------------------------------------------------ agencement

        void EnsureLayout(bool solo)
        {
            int w = Screen.width;
            int h = Screen.height;

            if (laidOutBuilt && w == laidOutWidth && h == laidOutHeight && solo == laidOutSolo) return;

            laidOutWidth = w;
            laidOutHeight = h;
            laidOutSolo = solo;

            if (!laidOutBuilt)
            {
                left = BuildSide();
                right = BuildSide();
                BuildPause();
                laidOutBuilt = true;
            }

            Place(left, Side.Left, solo, w, h);
            Place(right, Side.Right, solo, w, h);
            PlacePause(w, h);

            // Contre l'ordinateur, le blob de droite n'a pas de mains : son bouton n'a rien à
            // commander, et le laisser à l'écran inviterait à presser ce qui ne répond pas.
            SetActive(right, !solo);
        }

        void Place(SideControls controls, Side side, bool solo, float w, float h)
        {
            Vector2 jump = TouchZones.JumpCenter(side, solo, w, h);
            float diameter = TouchZones.JumpRadius(solo, w, h) * 2f;

            Move(controls.Jump, jump, new Vector2(diameter, diameter));
            Move(controls.JumpGlyph, jump, new Vector2(diameter * 0.40f, diameter * 0.40f));

            // Le repère n'est placé qu'à l'usage : sa hauteur est fixe, son abscisse suit le doigt.
            Move(controls.Marker, new Vector2(0f, 0f), new Vector2(diameter * 0.10f, h));
        }

        void PlacePause(float w, float h)
        {
            Vector2 center = TouchZones.PauseCenter(w, h);
            float diameter = TouchZones.PauseRadius(w, h) * 2f;
            var bar = new Vector2(diameter * 0.11f, diameter * 0.36f);
            float offset = diameter * 0.11f;

            Move(pauseDisc, center, new Vector2(diameter, diameter));
            Move(pauseBarA, center + new Vector2(-offset, 0f), bar);
            Move(pauseBarB, center + new Vector2(offset, 0f), bar);
        }

        // ------------------------------------------------------------------ teintes et repère

        void Paint(bool solo)
        {
            PaintSide(left, Side.Left);
            if (!solo) PaintSide(right, Side.Right);

            bool pausePressed = TouchInput.PauseDrawnHeld;
            pauseDisc.color = pausePressed ? heldColor : idleColor;
            Color pauseInk = pausePressed ? glyphHeldColor : glyphColor;
            pauseBarA.color = pauseInk;
            pauseBarB.color = pauseInk;
        }

        void PaintSide(SideControls controls, Side side)
        {
            bool holdJump = TouchInput.JumpDrawnHeld(side);
            controls.Jump.color = holdJump ? heldColor : idleColor;
            controls.JumpGlyph.color = holdJump ? glyphHeldColor : glyphColor;

            // Le repère : une colonne claire à l'endroit que le doigt désigne.
            //
            // ⚠ Il ne double pas le doigt, il le CORRIGE. Le doigt cache le point qu'il touche, et
            // le blob met un instant à l'atteindre : sans repère, le joueur ne sait ni où il vient
            // de pointer, ni si le jeu l'a entendu. Une colonne pleine hauteur reste visible
            // au-dessus de la main.
            bool moving = TouchInput.HasMoveTarget(side);
            controls.Marker.enabled = moving;
            if (!moving) return;

            var rect = (RectTransform)controls.Marker.transform;
            rect.anchoredPosition = new Vector2(TouchInput.MoveScreenX(side), Screen.height * 0.5f);
        }

        // ------------------------------------------------------------------ construction

        SideControls BuildSide()
        {
            var controls = new SideControls
            {
                Jump = CreateImage("Jump", discSprite, Image.Type.Simple),
                // Le triangle du projet pointe vers le haut : c'est le glyphe du saut sans rotation.
                // La police du jeu, elle, n'a aucune flèche, et un navigateur n'a pas de police
                // système pour l'y suppléer.
                JumpGlyph = CreateImage("JumpGlyph", triangleSprite, Image.Type.Simple),
                Marker = CreateImage("Marker", squareSprite, Image.Type.Simple)
            };

            controls.Marker.color = markerColor;
            // Éteint jusqu'au premier doigt : posé à zéro par l'agencement, il barrerait sinon le
            // bord gauche de l'écran le temps d'une image.
            controls.Marker.enabled = false;

            return controls;
        }

        void BuildPause()
        {
            pauseDisc = CreateImage("PauseDisc", discSprite, Image.Type.Simple);
            pauseBarA = CreateImage("PauseBarA", squareSprite, Image.Type.Simple);
            pauseBarB = CreateImage("PauseBarB", squareSprite, Image.Type.Simple);
        }

        Image CreateImage(string name, Sprite sprite, Image.Type type)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rect = (RectTransform)go.transform;
            // Ancres au coin bas-gauche : la position posée est alors exactement celle que
            // TouchZones a calculée, dans le repère de la dalle.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = idleColor;

            // ⚠ Sans cela, ces images captent les clics destinés à uGUI. Elles ne sont pas des
            // boutons : c'est TouchInput qui lit la dalle, elles ne font que montrer.
            image.raycastTarget = false;

            return image;
        }

        static void Move(Image image, Vector2 center, Vector2 size)
        {
            var rect = (RectTransform)image.transform;
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }

        static void SetActive(SideControls controls, bool active)
        {
            controls.Jump.gameObject.SetActive(active);
            controls.JumpGlyph.gameObject.SetActive(active);
            controls.Marker.gameObject.SetActive(active);
        }
    }
}
