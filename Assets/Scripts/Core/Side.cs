namespace SmilyVolley
{
    /// <summary>Camp du terrain. La valeur numérique sert aussi de signe sur l'axe X.</summary>
    public enum Side
    {
        Left = -1,
        Right = 1
    }

    public static class SideExtensions
    {
        public static Side Opposite(this Side side) => side == Side.Left ? Side.Right : Side.Left;

        public static float Sign(this Side side) => side == Side.Left ? -1f : 1f;

        public static string Label(this Side side) => side == Side.Left ? "Joueur 1" : "Joueur 2";
    }
}
