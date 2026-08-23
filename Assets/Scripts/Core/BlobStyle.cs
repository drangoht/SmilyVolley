namespace SmilyVolley
{
    /// <summary>
    /// Interprétation graphique des blobs. Les trois styles partagent la même silhouette
    /// et la même amplitude de déformation : seule change la façon dont la gelée réagit,
    /// ce qui permet de les comparer sans rien modifier au jeu.
    /// </summary>
    public enum BlobStyle
    {
        /// <summary>Dôme ferme. La déformation reste proche d'un simple écrasement.</summary>
        Round = 0,

        /// <summary>Gelée liquide : les flancs gonflent à l'écrasement, se creusent à l'étirement.</summary>
        Soft = 1,

        /// <summary>Gelée moulée, à facettes. Ferme, avec des faces planes qui accrochent la lumière.</summary>
        Angular = 2,
    }
}
