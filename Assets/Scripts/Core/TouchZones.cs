using UnityEngine;

namespace SmilyVolley
{
    /// <summary>Ce qu'un doigt posé à un endroit donné commande.</summary>
    public enum TouchTarget
    {
        None,
        Left,
        Right,
        Jump
    }

    /// <summary>
    /// Découpage de l'écran tactile : où sont les boutons de chaque camp, et lequel un doigt
    /// touche.
    ///
    /// <para><b>Pourquoi ces nombres vivent ici et pas dans le HUD.</b> Ils sont lus par deux
    /// couches qui ne se parlent pas : le <i>dessin</i> (où poser le bouton) et la <i>lecture</i>
    /// (ce doigt appuie-t-il dessus ?). Les laisser diverger produit le défaut le plus coûteux du
    /// tactile — un bouton qui se voit et ne répond pas, ou qui répond à côté, sans qu'aucune
    /// erreur ne soit levée. Un seul jeu de nombres, consommé des deux côtés, rend l'écart
    /// impossible.</para>
    ///
    /// <para><b>Repère : pixels écran, origine en bas à gauche</b> — celui de
    /// <c>Touchscreen.position</c>, et celui d'un canevas uGUI en <c>ConstantPixelSize</c> dont les
    /// ancres sont au coin bas-gauche. C'est ce qui permet au HUD tactile de poser ses images aux
    /// coordonnées rendues ici sans aucune conversion.</para>
    ///
    /// <para><b>Deux agencements, un seul jeu de formules.</b> À deux joueurs, chacun tient son
    /// bord de l'écran avec ses trois boutons ; en solo contre l'ordinateur, les mêmes boutons
    /// s'écartent aux deux bouts — déplacement sous le pouce gauche, saut sous le pouce droit,
    /// puisque la seconde main est libre. Le drapeau <c>solo</c> traverse donc toute l'API.</para>
    /// </summary>
    public static class TouchZones
    {
        // ----- proportions, en fraction de l'unité de mesure (voir Unit) -----

        /// <summary>
        /// Marge des boutons au bord de l'écran.
        /// </summary>
        /// <remarks>
        /// Un bouton collé au bord est en partie hors de portée sur une dalle à coins arrondis, et
        /// tombe dans la bande où le navigateur mobile capte ses propres gestes (retour, barre
        /// d'onglets). Une marge l'en sort.
        /// </remarks>
        public const float MarginFraction = 0.055f;

        /// <summary>Largeur d'UNE des deux touches du pavé directionnel.</summary>
        public const float PadKeyWidthFraction = 0.19f;

        /// <summary>Hauteur du pavé directionnel.</summary>
        /// <remarks>
        /// ⚠ Réduite de 0,24 après essai : le blob peut courir jusqu'à son mur, donc <b>jusque sous
        /// son propre pavé</b>, et à 0,24 le pavé était deux fois plus haut que lui — le joueur
        /// perdait de vue le personnage qu'il déplaçait, au moment précis où il le déplaçait. La
        /// superposition ne disparaît pas (le bas de l'écran appartient aux blobs comme aux pouces,
        /// c'est la nature de ce jeu au doigt), mais le sommet du blob dépasse maintenant du bouton.
        /// 0,20 vaut encore 72 px sur un téléphone bas de 360 px, bien au-delà de la cible
        /// confortable.
        /// </remarks>
        public const float PadHeightFraction = 0.20f;

        /// <summary>Rayon du bouton de saut à deux joueurs, où chacun n'a qu'une moitié d'écran.</summary>
        public const float JumpRadiusFraction = 0.135f;

        /// <summary>
        /// Rayon du bouton de saut en solo, où toute la largeur est disponible.
        /// </summary>
        /// <remarks>
        /// Plus gros qu'à deux : contre l'ordinateur le joueur a ses deux pouces, celui de droite ne
        /// sert qu'à ça, et le saut est ce qu'on presse le plus vite dans ce jeu.
        /// </remarks>
        public const float SoloJumpRadiusFraction = 0.155f;

        /// <summary>Écart entre le pavé directionnel et le bouton de saut d'un même camp.</summary>
        public const float GapFraction = 0.05f;

        /// <summary>Rayon du bouton de pause.</summary>
        /// <remarks>
        /// ⚠ Ce bouton n'est pas un confort : <b>sur mobile, il n'y a pas d'Échap</b>. Sans lui, une
        /// partie ne peut être ni interrompue ni quittée, et le joueur n'a d'autre issue que de
        /// fermer l'onglet. Petit et à l'opposé des contrôles : on le presse entre deux échanges,
        /// jamais pendant.
        /// </remarks>
        public const float PauseRadiusFraction = 0.075f;

        // ----- planchers et débords -----

        /// <summary>
        /// Côté minimal d'une cible, en pixels.
        /// </summary>
        /// <remarks>
        /// 44 px est la cible tactile confortable admise (environ 9 mm). Les fractions ci-dessus
        /// valent davantage sur toute dalle courante ; ce plancher ne sert que sur les écrans très
        /// bas, où une proportion seule produirait des boutons qu'on manque.
        /// </remarks>
        public const float MinTouchPx = 44f;

        /// <summary>
        /// Débord de la zone SENSIBLE d'un bouton par rapport à son dessin, en fraction de sa
        /// propre taille.
        /// </summary>
        /// <remarks>
        /// <para>Le doigt masque ce qu'il touche : le joueur vise le bouton qu'il a vu il y a une
        /// demi-seconde, pas celui qu'il voit. Une cible sensible plus large que le dessin absorbe
        /// cette erreur, et c'est la correction la plus rentable du tactile.</para>
        ///
        /// <para>⚠ Elle n'est PAS appliquée du côté où deux boutons se font face (voir
        /// <see cref="PadTouchRect"/>) : deux zones dilatées l'une vers l'autre se recouvrent, et
        /// l'une des deux cesse alors d'être atteignable près de sa bordure — le joueur qui vise
        /// « droite » saute. Chaque bouton gagne donc de la surface vers les bords de l'écran, là
        /// où il y a de la place et où le doigt tombe court, et n'en gagne aucune vers son
        /// voisin.</para>
        /// </remarks>
        public const float TouchSlop = 0.3f;

        // ------------------------------------------------------------------ unité de mesure

        /// <summary>
        /// Largeur minimale, en unités, pour qu'un camp loge ses trois boutons dans SA MOITIÉ
        /// d'écran : marge + pavé + écart + bouton de saut, plus une marge de sécurité au milieu.
        /// </summary>
        const float DuoWidthNeeded = 2f * (MarginFraction + 2f * PadKeyWidthFraction
                                           + GapFraction + 2f * JumpRadiusFraction);

        /// <summary>Même calcul en solo, où les boutons se partagent la largeur entière.</summary>
        const float SoloWidthNeeded = 2f * MarginFraction + 2f * PadKeyWidthFraction
                                      + 2f * SoloJumpRadiusFraction + GapFraction;

        /// <summary>
        /// L'unité sur laquelle toutes les tailles sont bâties.
        /// </summary>
        /// <remarks>
        /// <para><b>La hauteur, d'abord.</b> En paysage c'est la dimension courte : c'est elle qui
        /// décide de la place réellement disponible sous le pouce. Un bouton dimensionné sur la
        /// largeur deviendrait énorme sur une tablette et minuscule sur un téléphone — exactement
        /// l'inverse de ce qu'il faut.</para>
        ///
        /// <para><b>Mais bornée par la largeur.</b> Sur un écran presque carré — une fenêtre de
        /// navigateur qu'on a rétrécie, une tablette en 4/3 — la hauteur devient si proche de la
        /// largeur que les trois boutons d'un camp débordent au-delà du milieu de l'écran et
        /// viennent recouvrir ceux de l'autre joueur. Le symptôme serait le pire du tactile : deux
        /// boutons superposés dont un seul répond, sans la moindre erreur. Ce plafond-là ne mord
        /// jamais en 16/9, où le rapport vaut 1,78 pour un besoin d'environ 1,51.
        /// </para>
        /// </remarks>
        public static float Unit(bool solo, float screenWidth, float screenHeight)
        {
            float needed = solo ? SoloWidthNeeded : DuoWidthNeeded;
            return Mathf.Max(1f, Mathf.Min(screenHeight, screenWidth / needed));
        }

        // ------------------------------------------------------------------ orientation

        /// <summary>
        /// L'écran est-il en portrait ? Le jeu s'y refuse (voir <see cref="OrientationGate"/>).
        /// </summary>
        /// <remarks>
        /// Comparer les deux dimensions plutôt que d'interroger <c>Screen.orientation</c> : en
        /// WebGL, l'orientation rapportée suit le verrouillage de rotation du système et <b>ment</b>
        /// dès que l'utilisateur l'a bloqué, alors que la taille du canevas, elle, dit toujours la
        /// vérité sur ce que le joueur voit.
        /// </remarks>
        public static bool IsPortrait(float screenWidth, float screenHeight) => screenHeight > screenWidth;

        // ------------------------------------------------------------------ dimensions

        public static float Margin(bool solo, float w, float h) => MarginFraction * Unit(solo, w, h);

        public static float PadKeyWidth(bool solo, float w, float h)
            => Mathf.Max(MinTouchPx, PadKeyWidthFraction * Unit(solo, w, h));

        public static float PadHeight(bool solo, float w, float h)
            => Mathf.Max(MinTouchPx, PadHeightFraction * Unit(solo, w, h));

        public static float JumpRadius(bool solo, float w, float h)
            => Mathf.Max(MinTouchPx * 0.5f,
                         (solo ? SoloJumpRadiusFraction : JumpRadiusFraction) * Unit(solo, w, h));

        /// <summary>Rayon du bouton de pause. Mesuré comme en solo : il ne partage sa ligne avec rien.</summary>
        public static float PauseRadius(float w, float h)
            => Mathf.Max(MinTouchPx * 0.5f, PauseRadiusFraction * Unit(true, w, h));

        // ------------------------------------------------------------------ placement

        /// <summary>
        /// Pavé directionnel DESSINÉ d'un camp : les deux touches d'un seul tenant, gauche à gauche
        /// et droite à droite.
        /// </summary>
        /// <remarks>
        /// <para><b>D'un seul tenant, et non deux boutons séparés par un vide.</b> Deux cibles
        /// distinctes obligent le pouce à viser, et la zone entre elles ne fait rien — c'est
        /// précisément là que le doigt tombe quand on change de direction en pleine course. Un pavé
        /// continu n'a pas de trou : la frontière est au milieu, et glisser d'un côté à l'autre sans
        /// lever le doigt change de direction.</para>
        ///
        /// <para>Le camp de droite garde « gauche à gauche » : refléter les commandes parce que le
        /// joueur est assis à droite est un piège classique, et faux — le blob, lui, ne s'est pas
        /// retourné.</para>
        /// </remarks>
        public static Rect PadRect(Side side, bool solo, float w, float h)
        {
            float margin = Margin(solo, w, h);
            float width = PadKeyWidth(solo, w, h) * 2f;
            float height = PadHeight(solo, w, h);

            // En solo, le seul pavé du jeu est à gauche : le joueur a ses deux pouces, déplacement à
            // gauche et saut à droite. À deux, chacun tient son propre bord de l'écran.
            bool atLeftEdge = solo || side == Side.Left;
            float x = atLeftEdge ? margin : w - margin - width;

            return new Rect(x, margin, width, height);
        }

        /// <summary>Centre du bouton de saut DESSINÉ d'un camp.</summary>
        public static Vector2 JumpCenter(Side side, bool solo, float w, float h)
        {
            float margin = Margin(solo, w, h);
            float radius = JumpRadius(solo, w, h);

            // En solo le saut part à l'autre bout de l'écran, sous le pouce droit resté libre.
            if (solo) return new Vector2(w - margin - radius, margin + radius);

            Rect pad = PadRect(side, false, w, h);
            float gap = GapFraction * Unit(false, w, h);

            // À deux, il se range à l'intérieur du pavé de son camp — vers le filet, donc vers
            // l'autre joueur, mais sans jamais franchir le milieu de l'écran (voir Unit).
            float x = side == Side.Left ? pad.xMax + gap + radius : pad.xMin - gap - radius;

            return new Vector2(x, margin + radius);
        }

        /// <summary>Centre du bouton de pause — en haut à droite, loin des deux paires de pouces.</summary>
        public static Vector2 PauseCenter(float w, float h)
        {
            float margin = Margin(true, w, h);
            float radius = PauseRadius(w, h);

            return new Vector2(w - margin - radius, h - margin - radius);
        }

        // ------------------------------------------------------------------ zones sensibles

        /// <summary>
        /// Pavé SENSIBLE : le dessin élargi vers les bords de l'écran, jamais vers le bouton de
        /// saut voisin. Voir <see cref="TouchSlop"/>.
        /// </summary>
        public static Rect PadTouchRect(Side side, bool solo, float w, float h)
        {
            Rect drawn = PadRect(side, solo, w, h);
            float slop = PadKeyWidth(solo, w, h) * TouchSlop;

            // Le côté « intérieur » est celui où se trouve le bouton de saut : il ne gagne rien.
            bool jumpOnRight = solo || side == Side.Left;

            float xMin = drawn.xMin - (jumpOnRight ? slop : 0f);
            float xMax = drawn.xMax + (jumpOnRight ? 0f : slop);

            // Vers le bas jusqu'au bord de l'écran : un pouce qui vise le pavé tombe plus souvent
            // court que long, et il n'y a rien sous lui à voler.
            return Rect.MinMaxRect(xMin, 0f, xMax, drawn.yMax + slop);
        }

        /// <summary>Rayon SENSIBLE du bouton de saut.</summary>
        public static float JumpTouchRadius(bool solo, float w, float h)
            => JumpRadius(solo, w, h) * (1f + TouchSlop);

        /// <summary>
        /// Le doigt tombe-t-il sur le bouton de pause ?
        /// </summary>
        /// <remarks>
        /// Sans marge, contrairement aux autres : une pause déclenchée par erreur en plein échange
        /// coûte le point. Il vaut mieux la manquer une fois que la déclencher une fois.
        /// </remarks>
        public static bool IsPause(float x, float y, float w, float h)
        {
            Vector2 center = PauseCenter(w, h);
            float radius = PauseRadius(w, h);

            return (new Vector2(x, y) - center).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// Ce que commande un doigt posé en (x, y) POUR LE CAMP indiqué. <see cref="TouchTarget.None"/>
        /// s'il ne touche aucun de ses boutons — le doigt appartient alors peut-être à l'autre camp,
        /// qu'il faut interroger séparément.
        /// </summary>
        /// <remarks>
        /// La pause n'est pas testée ici : elle n'appartient à aucun camp. C'est <see cref="IsPause"/>
        /// qui la tranche, et le lecteur doit l'appeler <b>avant</b> — un bouton commun placé dans
        /// la moitié d'un camp serait sinon avalé par lui.
        /// </remarks>
        public static TouchTarget Hit(float x, float y, Side side, bool solo, float w, float h)
        {
            var point = new Vector2(x, y);

            Vector2 jump = JumpCenter(side, solo, w, h);
            float reach = JumpTouchRadius(solo, w, h);
            if ((point - jump).sqrMagnitude <= reach * reach) return TouchTarget.Jump;

            if (!PadTouchRect(side, solo, w, h).Contains(point)) return TouchTarget.None;

            // La frontière est au milieu du pavé DESSINÉ, pas du pavé sensible : le débord
            // n'appartient qu'à la touche du bord vers laquelle il s'étend.
            float middle = PadRect(side, solo, w, h).center.x;
            return x < middle ? TouchTarget.Left : TouchTarget.Right;
        }
    }
}
