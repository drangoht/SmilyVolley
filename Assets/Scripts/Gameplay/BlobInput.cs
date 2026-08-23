using UnityEngine;

namespace SmilyVolley
{
    /// <summary>
    /// Source de commandes d'un blob. Le BlobController ne connaît que cette abstraction,
    /// ce qui permet d'échanger clavier et IA sans toucher au déplacement.
    /// </summary>
    public abstract class BlobInput : MonoBehaviour
    {
        /// <summary>-1 (gauche) a 1 (droite).</summary>
        public abstract float Horizontal { get; }

        /// <summary>Vrai tant que le saut est demande : le blob saute dès qu'il touche le sol.</summary>
        public abstract bool JumpHeld { get; }

        /// <summary>Appelé au début de chaque service pour remettre l'état interne à zéro.</summary>
        public virtual void OnServeStart() { }
    }
}
