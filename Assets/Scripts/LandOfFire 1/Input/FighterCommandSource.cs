// Land of Fire · Fuente abstracta de comandos de un Fighter.

using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public abstract class FighterCommandSource :
        MonoBehaviour
    {
        public abstract int Direction { get; }

        public abstract void ProcessInput(
            int facing,
            long tick);

        public abstract bool TryReadCommand(
            out FighterCommand command);

        public abstract void ClearCommands();
    }
}