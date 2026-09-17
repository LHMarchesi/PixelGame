// Land of Fire · Entrada abstracta por luchador.
// Contrato para teclado, IA o reproducción; las órdenes se consumen una vez por tick.
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public enum AttackCommand { Light }

    // Implementar para teclado, IA o reproducción. Sin source = dummy.
    public abstract class FighterCommandSource : MonoBehaviour
    {
        public abstract int Direction { get; }
        public abstract bool TryReadPress(out int direction);
        // Virtual para que fuentes de comandos anteriores sigan siendo válidas.
        public virtual bool TryReadAttack(out AttackCommand command)
        {
            command = default(AttackCommand);
            return false;
        }
        public abstract void ClearPresses();
    }
}
