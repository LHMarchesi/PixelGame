using UnityEngine;

namespace LandOfFire.BunnyStep
{
    // Implementar para teclado, IA o reproducción. Sin source = dummy.
    public abstract class FighterCommandSource : MonoBehaviour
    {
        public abstract int Direction { get; }
        public abstract bool TryReadPress(out int direction);
        public abstract void ClearPresses();
    }
}
