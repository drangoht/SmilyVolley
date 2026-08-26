using UnityEngine;

namespace SmilyVolley
{
    /// <summary>Ce qu'un doigt posé à un endroit donné commande.</summary>
    public enum TouchTarget
    {
        None,

        /// <summary>Le doigt désigne l'endroit du terrain où le blob doit aller.</summary>
        Move,

        Jump
    }

    /// <summary>
    /// Découpage de l'écran tactile : ce qu'un doigt commande selon l'endroit où il se pose.
    ///
    /// <para><b>Le déplacement n'est pas un bouton.</b> Chaque joueur pilote son blob en
    /// <b>glissant le doigt</b> dans sa moitié d'écran : le point touché désigne l'endroit du
    /// terrain où il veut être, et le blob y court. La conversion de l'écran vers le monde passe
    /// par la caméra (voir <see cref="HumanBlobInput"/>), si bien que le doigt pointe
    /// <i>littéralement</i> l'endroit visé — aucune correspondance à inventer, aucun réglage de
    /// sensibilité à trouver.</para>
    ///
    /// <para><b>Ce que ce choix a supprimé.</b> Un pavé directionnel occupait le bas de l'écran,
    /// c'est-à-dire la bande où vivent les blobs : le joueur perdait de vue le personnage qu'il
    /// déplaçait, au moment précis où il le déplaçait. Il ne reste en bas qu'un bouton de saut, au
    /// coin.</para>
    ///
    /// <para><b>Repère : pixels écran, origine en bas à gauche</b> — celui de
    /// <c>Touchscreen.position</c>, et celui d'un canevas uGUI en <c>ConstantPixelSize</c> dont les
    /// ancres sont au coin bas-gauche. Le HUD tactile pose donc ses images aux coordonnées rendues
    /// ici sans aucune conversion.</para>
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

        /// <summary>Rayon du bouton de pause.</summary>
        /// <remarks>
        /// ⚠ Ce bouton n'est pas un confort : <b>sur mobile, il n'y a pas d'Échap</b>. Sans lui, une
        /// partie ne peut être ni interrompue ni quittée, et le joueur n'a d'autre issue que de
        /// fermer l'onglet. Petit et à l'opposé des commandes : on le presse entre deux échanges,
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
        /// Débord de la zone SENSIBLE du bouton de saut par rapport à son dessin.
        /// </summary>
        /// <remarks>
        /// Le doigt masque ce qu'il touche : le joueur vise le bouton qu'il a vu il y a une
        /// demi-seconde, pas celui qu'il voit. Une cible sensible plus large que le dessin absorbe
        /// cette erreur, et c'est la correction la plus rentable du tactile. Elle ne vole rien : ce
        /// qui l'entoure est la zone de déplacement, où un doigt de trop n'a aucune conséquence
        /// puisqu'il aurait de toute façon désigné un point tout proche.
        /// </remarks>
        public const float TouchSlop = 0.3f;

        /// <summary>
        /// Hauteur du coin de repos, en fraction de l'unité — et <see cref="MinPalmBandPx"/> au
        /// moins.
        /// </summary>
        /// <remarks>
        /// <para><b>Ce n'est pas un doigt, c'est une main.</b> En paysage, l'appareil est tenu par
        /// ses deux bords, et ce qui touche la dalle près du coin bas extérieur n'est pas le pouce
        /// qui joue mais sa base. Lue comme une désignation, elle envoie le blob au mur et l'y
        /// retient — le joueur voit un blob qui « ne répond plus » alors que le jeu lui obéit
        /// parfaitement, à une main qu'il ne sait pas avoir posée.</para>
        ///
        /// <para>Petite exprès : elle n'a rien à interdire au-delà de ce que la main occupe. Le
        /// bandeau d'aide invite justement à glisser <b>en bas</b> de l'écran — c'est là que le
        /// doigt gêne le moins la vue — et une bande généreuse reprendrait d'une main ce que le
        /// conseil donne de l'autre.</para>
        /// </remarks>
        public const float PalmBandFraction = 0.03f;

        /// <summary>Hauteur plancher du coin de repos, sur les écrans très bas.</summary>
        /// <remarks>
        /// ⚠ <b>Ces deux mesures sont une estimation, pas un relevé.</b> Ce qu'il faudrait connaître
        /// est la position du <i>centre</i> du contact que produit la base d'un pouce, et elle ne
        /// s'obtient que sur un vrai appareil. Trop basse, la zone laisse passer la main qu'elle
        /// vise ; trop haute, elle refuse un doigt qui joue. Le premier défaut est celui d'avant,
        /// le second serait pire — d'où le choix d'une zone modeste.
        /// </remarks>
        public const float MinPalmBandPx = 14f;

        /// <summary>Hauteur du coin de repos sur cet écran.</summary>
        public static float PalmBand(bool solo, float w, float h)
            => Mathf.Max(MinPalmBandPx, PalmBandFraction * Unit(solo, w, h));

        // ------------------------------------------------------------------ unité de mesure

        /// <summary>Largeur minimale, en unités, pour qu'un camp loge son bouton dans SA MOITIÉ d'écran.</summary>
        const float DuoWidthNeeded = 2f * (MarginFraction + 2f * JumpRadiusFraction);

        /// <summary>Même calcul en solo, où le bouton et la pause se partagent la largeur entière.</summary>
        const float SoloWidthNeeded = 2f * MarginFraction + 2f * SoloJumpRadiusFraction
                                      + 2f * PauseRadiusFraction;

        /// <summary>
        /// L'unité sur laquelle toutes les tailles sont bâties.
        /// </summary>
        /// <remarks>
        /// <para><b>La hauteur, d'abord.</b> En paysage c'est la dimension courte : c'est elle qui
        /// décide de la place réellement disponible sous le pouce. Un bouton dimensionné sur la
        /// largeur deviendrait énorme sur une tablette et minuscule sur un téléphone — exactement
        /// l'inverse de ce qu'il faut.</para>
        ///
        /// <para><b>Mais bornée par la largeur</b>, pour qu'un bouton ne déborde jamais au-delà du
        /// milieu de l'écran sur une fenêtre presque carrée. Deux boutons superposés dont un seul
        /// répond serait le pire symptôme possible, et il n'apparaîtrait sur aucun journal.</para>
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

        public static float JumpRadius(bool solo, float w, float h)
            => Mathf.Max(MinTouchPx * 0.5f,
                         (solo ? SoloJumpRadiusFraction : JumpRadiusFraction) * Unit(solo, w, h));

        /// <summary>Rayon du bouton de pause. Mesuré comme en solo : il ne partage sa ligne avec rien.</summary>
        public static float PauseRadius(float w, float h)
            => Mathf.Max(MinTouchPx * 0.5f, PauseRadiusFraction * Unit(true, w, h));

        // ------------------------------------------------------------------ placement

        /// <summary>Centre du bouton de saut DESSINÉ d'un camp.</summary>
        /// <remarks>
        /// <para>Au coin <b>extérieur</b> de sa moitié — le bord de l'écran, là où tombe
        /// naturellement le pouce de la main qui tient l'appareil. Le milieu de l'écran reste ainsi
        /// entièrement dégagé, et rien ne se pose entre les deux joueurs.</para>
        ///
        /// <para><b>En solo, le saut part dans la moitié que le joueur ne tient pas</b>, et cette
        /// moitié dépend du camp qu'il joue : sa main de déplacement a besoin de toute la sienne,
        /// et l'autre ne commande rien d'autre. Le paramètre <paramref name="side"/> suffit à le
        /// dire, puisqu'en solo il ne vaut jamais que le camp du joueur.</para>
        /// </remarks>
        public static Vector2 JumpCenter(Side side, bool solo, float w, float h)
        {
            float margin = Margin(solo, w, h);
            float radius = JumpRadius(solo, w, h);

            bool atRightEdge = solo ? side == Side.Left : side == Side.Right;
            float x = atRightEdge ? w - margin - radius : margin + radius;

            return new Vector2(x, margin + radius);
        }

        /// <summary>Centre du bouton de pause — en haut à droite, loin des pouces de jeu.</summary>
        public static Vector2 PauseCenter(float w, float h)
        {
            float margin = Margin(true, w, h);
            float radius = PauseRadius(w, h);

            return new Vector2(w - margin - radius, h - margin - radius);
        }

        // ------------------------------------------------------------------ zones sensibles

        /// <summary>Rayon SENSIBLE du bouton de saut.</summary>
        public static float JumpTouchRadius(bool solo, float w, float h)
            => JumpRadius(solo, w, h) * (1f + TouchSlop);

        /// <summary>
        /// Le doigt tombe-t-il sur le bouton de pause ?
        /// </summary>
        /// <remarks>
        /// Sans marge, contrairement au saut : une pause déclenchée par erreur en plein échange
        /// coûte le point. Il vaut mieux la manquer une fois que la déclencher une fois.
        /// </remarks>
        public static bool IsPause(float x, float y, float w, float h)
        {
            Vector2 center = PauseCenter(w, h);
            float radius = PauseRadius(w, h);

            return (new Vector2(x, y) - center).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// La moitié d'écran d'un camp : toute la surface où son joueur peut glisser.
        /// </summary>
        /// <remarks>
        /// <para><b>Une moitié entière, et rien de moins.</b> Le joueur ne vise pas une piste : il
        /// désigne un endroit du terrain, et cet endroit est <i>déjà</i> à l'écran, dans sa moitié.
        /// Restreindre la zone à une bande obligerait à traduire un geste en un autre, ce qui est
        /// exactement ce que ce schéma évite.</para>
        ///
        /// <para>En solo, le joueur n'a que le camp gauche : sa moitié est la même qu'à deux, et
        /// la moitié droite ne commande rien — le blob de droite y est piloté par l'ordinateur.</para>
        /// </remarks>
        public static bool IsMoveZone(float x, float y, Side side, float w, float h)
        {
            float middle = w * 0.5f;
            return side == Side.Left ? x <= middle : x > middle;
        }

        /// <summary>
        /// Ce que commande un doigt posé en (x, y) POUR LE CAMP indiqué. <see cref="TouchTarget.None"/>
        /// s'il n'appartient pas à ce camp — le doigt est alors peut-être à l'autre, qu'il faut
        /// interroger séparément.
        /// </summary>
        /// <remarks>
        /// <para>Le saut est testé <b>avant</b> la zone de déplacement : il est posé dedans, et
        /// l'ordre inverse l'avalerait entièrement.</para>
        ///
        /// <para>La pause n'est pas testée ici : elle n'appartient à aucun camp. C'est
        /// <see cref="IsPause"/> qui la tranche, et le lecteur doit l'appeler <b>avant</b> — un
        /// bouton commun placé dans la moitié d'un camp serait sinon avalé par lui.</para>
        /// </remarks>
        public static TouchTarget Hit(float x, float y, Side side, bool solo, float w, float h)
        {
            Vector2 jump = JumpCenter(side, solo, w, h);
            float reach = JumpTouchRadius(solo, w, h);
            if ((new Vector2(x, y) - jump).sqrMagnitude <= reach * reach) return TouchTarget.Jump;

            if (IsPalmRest(x, y, side, solo, w, h)) return TouchTarget.None;

            return IsMoveZone(x, y, side, w, h) ? TouchTarget.Move : TouchTarget.None;
        }

        /// <summary>
        /// Le contact tombe-t-il là où la main qui TIENT l'appareil se pose ?
        /// </summary>
        /// <remarks>
        /// <para>Un coin, pas une bande : le bord bas <b>extérieur</b> du camp, sur la largeur que
        /// le bouton de saut occupe ailleurs. C'est l'endroit précis où la base du pouce touche la
        /// dalle quand on tient un téléphone en paysage.</para>
        ///
        /// <para>⚠ Ce test ne s'applique qu'aux doigts qui <b>se posent</b> : <see cref="Hit"/>
        /// n'est consulté que pour eux, un glissement déjà engagé gardant son camp où qu'il aille.
        /// La nuance est tout le rendement du dispositif — refuser un doigt qui arrive ne coûte
        /// rien, l'arracher en cours de course arrêterait le blob en plein échange.</para>
        ///
        /// <para>Le saut est testé <b>avant</b> : il vit dans ce coin-là, et l'ordre inverse le
        /// rendrait injouable exactement sur les appareils où il est déjà le plus à l'étroit.</para>
        /// </remarks>
        public static bool IsPalmRest(float x, float y, Side side, bool solo, float w, float h)
        {
            if (y > PalmBand(solo, w, h)) return false;

            // La largeur du coin : une marge et un RAYON de bouton de saut, soit une quinzaine de
            // millimètres sur un téléphone courant. Rien à régler séparément, et la zone suit les
            // mêmes écrans que le reste de l'agencement.
            //
            // ⚠ Un diamètre avait d'abord été pris : le coin couvrait alors un tiers de la moitié
            // du joueur, c'est-à-dire tout le bas du camp jusqu'au mur — et le bandeau d'aide
            // invite justement à glisser en bas. La zone reprenait d'une main ce que le conseil
            // donnait de l'autre.
            float corner = Margin(solo, w, h) + JumpRadius(solo, w, h);

            return side == Side.Left ? x <= corner : x >= w - corner;
        }
    }
}
