using System;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [RequireComponent(typeof(FighterInput))]
    public sealed class PlayerCommandSource : FighterCommandSource
    {
        public FighterInput input;
        private void Awake() { if (input == null) input = GetComponent<FighterInput>(); }
        public override int Direction => input != null && input.isActiveAndEnabled ? input.Direction : 0;
        public override bool TryReadPress(out int direction)
        {
            direction = 0;
            return input != null && input.isActiveAndEnabled && input.TryReadPress(out direction);
        }
        public override void ClearPresses() { if (input != null) input.ClearPresses(); }
    }
}


