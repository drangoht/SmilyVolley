using UnityEngine;
using UnityEngine.UI;

namespace SmilyVolley
{
    /// <summary>
    /// Dessine les commandes tactiles — un pavé directionnel et un bouton de saut par camp, plus le
    /// bouton de pause — et ouvre la capture des doigts tant qu'elles sont à l'écran.
    ///
    /// <para><b>Celui qui montre est celui qui écoute.</b> C'est ce composant, et lui seul, qui
    /// appelle <see cref="TouchInput.SetGameControls"/>. Séparer les deux responsabilités laisserait
    /// exister un état où des boutons répondent sans se voir — un doigt posé pendant un menu
    /// ferait alors courir un blob derrière le panneau — ou l'inverse, des boutons qui se voient et
    /// ne répondent pas, qui est le pire symptôme du tactile parce qu'il ne ressemble à rien
    /// d'autre qu'à un jeu cassé.</para>
    ///
    /// <para><b>Les positions ne sont pas décidées ici</b> : elles viennent toutes de
    /// <see cref="TouchZones"/>, qui sert aussi à la lecture. Le canevas est en
    /// <c>ConstantPixelSize</c> à l'échelle 1 et ses ancres sont au coin bas-gauche, si bien que les
    /// coordonnées écran de la dalle s'y posent <b>sans aucune conversion</b> — une mise à
    /// l'échelle, ici, serait une occasion silencieuse de désaccord entre le dessin et le
    /// toucher.</para>
    ///
    /// <para>Les images sont construites à l'exécution plutôt qu'enregistrées dans la scène : leur
    /// nombre dépend du mode de jeu, et leur taille de la dalle du joueur.</para>
    /// </summary>
    public class TouchHud : MonoBehaviour
    {
        [Header("Références")]
        public GameManager manager;
        public MenuController menu;

        [Header("Sprites")]
        [Tooltip("Découpé en neuf tranches : les touches du pavé s'étirent sans déformer leurs coins.")]
        public Sprite roundedSprite;
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

        /// <summary>Les trois boutons d'un camp.</summary>
        class SideControls
        {
            public Image PadLeft, PadRight, PadLeftGlyph, PadRightGlyph;
            public Image Jump, JumpGlyph;
        }

        Canvas canvas;
        SideControls left, right;
        Image pauseDisc, pauseBarA, pauseBarB;

        // L'agencement ne se recalcule qu'au changement : rotation de l'appareil, barre d'URL qui
        // se rétracte, passage d'un mode à l'autre. Le reste du temps, seules les teintes bougent.
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
            // boutons invisibles qui répondent encore.
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
            // joueur qui pose son tout premier doigt le fait forcément alors qu'aucun contrôle n'est
            // encore dessiné : c'est ce contact-là qui apprend au jeu qu'il y a des doigts. Fermer
            // la porte tant que rien n'est visible **avalerait donc systématiquement le premier
            // appui de la partie** — le joueur touche, rien ne bouge, il recommence. La condition
            // qui compte est celle qui protège vraiment quelque chose : hors match, tout doigt
            // appartient à uGUI, et un pavé actif y volerait les appuis destinés aux boutons du
            // menu. Toujours appelé, y compris quand rien n'est visible : c'est cet appel-là qui
            // referme la porte, et il ne doit pas dépendre d'une branche.
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

            // Contre l'ordinateur, le blob de droite n'a pas de mains : ses boutons n'ont rien à
            // commander, et les laisser à l'écran inviterait à presser ce qui ne répond pas.
            SetActive(right, !solo);
        }

        void Place(SideControls controls, Side side, bool solo, float w, float h)
        {
            Rect pad = TouchZones.PadRect(side, solo, w, h);
            float key = pad.width * 0.5f;

            // Les deux touches sont jointives : la frontière du dessin est celle de la lecture.
            Move(controls.PadLeft, new Vector2(pad.xMin + key * 0.5f, pad.center.y), new Vector2(key, pad.height));
            Move(controls.PadRight, new Vector2(pad.xMax - key * 0.5f, pad.center.y), new Vector2(key, pad.height));

            float glyph = Mathf.Min(key, pad.height) * 0.42f;
            Move(controls.PadLeftGlyph, ((RectTransform)controls.PadLeft.transform).anchoredPosition,
                 new Vector2(glyph, glyph));
            Move(controls.PadRightGlyph, ((RectTransform)controls.PadRight.transform).anchoredPosition,
                 new Vector2(glyph, glyph));

            Vector2 jump = TouchZones.JumpCenter(side, solo, w, h);
            float diameter = TouchZones.JumpRadius(solo, w, h) * 2f;
            Move(controls.Jump, jump, new Vector2(diameter, diameter));
            Move(controls.JumpGlyph, jump, new Vector2(diameter * 0.40f, diameter * 0.40f));
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

        // ------------------------------------------------------------------ teintes

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
            bool holdLeft = TouchInput.PadHeld(side, right: false);
            bool holdRight = TouchInput.PadHeld(side, right: true);
            bool holdJump = TouchInput.JumpDrawnHeld(side);

            controls.PadLeft.color = holdLeft ? heldColor : idleColor;
            controls.PadRight.color = holdRight ? heldColor : idleColor;
            controls.Jump.color = holdJump ? heldColor : idleColor;

            controls.PadLeftGlyph.color = holdLeft ? glyphHeldColor : glyphColor;
            controls.PadRightGlyph.color = holdRight ? glyphHeldColor : glyphColor;
            controls.JumpGlyph.color = holdJump ? glyphHeldColor : glyphColor;
        }

        // ------------------------------------------------------------------ construction

        SideControls BuildSide()
        {
            return new SideControls
            {
                PadLeft = CreateImage("PadLeft", roundedSprite, Image.Type.Sliced),
                PadRight = CreateImage("PadRight", roundedSprite, Image.Type.Sliced),
                // Le triangle du projet pointe vers le haut : les flèches horizontales sont le même
                // sprite tourné d'un quart de tour. La police du jeu, elle, n'a aucun glyphe de
                // flèche, et un navigateur n'a pas de police système pour l'y suppléer.
                PadLeftGlyph = CreateImage("PadLeftGlyph", triangleSprite, Image.Type.Simple, 90f),
                PadRightGlyph = CreateImage("PadRightGlyph", triangleSprite, Image.Type.Simple, -90f),
                Jump = CreateImage("Jump", discSprite, Image.Type.Simple),
                JumpGlyph = CreateImage("JumpGlyph", triangleSprite, Image.Type.Simple)
            };
        }

        void BuildPause()
        {
            pauseDisc = CreateImage("PauseDisc", discSprite, Image.Type.Simple);
            pauseBarA = CreateImage("PauseBarA", squareSprite, Image.Type.Simple);
            pauseBarB = CreateImage("PauseBarB", squareSprite, Image.Type.Simple);
        }

        Image CreateImage(string name, Sprite sprite, Image.Type type, float rotation = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var rect = (RectTransform)go.transform;
            // Ancres au coin bas-gauche : la position posée est alors exactement celle que
            // TouchZones a calculée, dans le repère de la dalle.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = type;
            image.color = idleColor;

            // ⚠ Sans cela, ces images captent les clics destinés à uGUI. Elles ne sont pas des
            // boutons : c'est TouchInput qui lit la dalle, elles ne font que montrer où viser.
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
            controls.PadLeft.gameObject.SetActive(active);
            controls.PadRight.gameObject.SetActive(active);
            controls.PadLeftGlyph.gameObject.SetActive(active);
            controls.PadRightGlyph.gameObject.SetActive(active);
            controls.Jump.gameObject.SetActive(active);
            controls.JumpGlyph.gameObject.SetActive(active);
        }
    }
}
