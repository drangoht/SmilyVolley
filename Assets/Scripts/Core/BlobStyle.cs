namespace SmilyVolley
{
    /// <summary>
    /// Nature de la gelée dont un blob est fait. Le style ne change pas la taille du corps ni
    /// rien de la physique du jeu : il change la façon dont la gelée réagit — son contour au
    /// repos, sa raideur, la durée de son ballottement. La différence se voit donc surtout en
    /// mouvement, à l'atterrissage et sous la balle.
    ///
    /// Les réglages mécaniques de chaque style sont dans <see cref="BlobJelly"/>.
    /// </summary>
    public enum BlobStyle
    {
        /// <summary>Ferme : contour rond, revient vite, ne déborde qu'un peu.</summary>
        Round = 0,

        /// <summary>Molle : grande amplitude, flancs qui débordent, ballotte plusieurs fois.</summary>
        Soft = 1,

        /// <summary>Moulée : contour à dix faces, très raide, les arêtes se redressent aussitôt.</summary>
        Angular = 2,
    }
}
