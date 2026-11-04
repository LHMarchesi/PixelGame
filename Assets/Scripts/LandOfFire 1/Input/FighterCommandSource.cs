// Land of Fire · Fuente abstracta de comandos de un Fighter.

using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public abstract class FighterCommandSource :
        MonoBehaviour
    {
        public abstract int Direction { get; }

        // Optional profile hook for command recognizers that need
        // movement-related settings such as the Bunny double-tap window.
        public virtual void ConfigureMovement(FighterMovementProfile profile) { }

        // Optional held-button query used by charge attacks. Custom command
        // sources can override this; older sources remain fully compatible.
        public virtual bool IsAttackHeld(AttackCommand attack) => false;

        public abstract void ProcessInput(
            int facing,
            long tick);

        public abstract bool TryReadCommand(
            out FighterCommand command);

        public abstract void ClearCommands();
    }
}