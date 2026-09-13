using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public sealed class BunnyFighter : MonoBehaviour
    {
        public FighterInput input;
        public FighterPresentation presentation;
        [Min(0.01f)] public float halfWidth = 0.4f;
        [Header("Adelante: valores provisionales")]
        [Min(1)] public int forwardTicks = 12;
        [Min(0)] public float forwardDistance = 1.2f;
        [Min(0)] public float forwardHeight = 0.3f;
        [Header("Atrás: valores provisionales")]
        [Min(1)] public int backwardTicks = 14;
        [Min(0)] public float backwardDistance = 0.9f;
        [Min(0)] public float backwardHeight = 0.25f;
        [Min(1)] public int doubleTapTicks = 12;
        public BunnyStateMachine Machine { get; } = new BunnyStateMachine();
        public float X => transform.position.x;
        public float Width => Mathf.Max(0.01f, halfWidth);
        public bool GuardRequested => Machine.State == FighterState.Guard;
        public int Facing { get; private set; } = 1;
        private int poseTick;

        public void ResetFighter(int facing)
        {
            Facing = facing;
            Machine.Reset();
            poseTick = 0;
            if (input != null) input.ClearPresses();
            Render();
        }

        public float Propose(long tick, int facing, bool frozen)
        {
            Facing = facing;
            if (frozen)
            {
                Machine.ForgetTap();
                if (input != null) input.ClearPresses();
                return X;
            }
            FighterState before = Machine.State;
            Machine.BeginTick(facing, input != null ? input.Direction : 0);
            if (input != null)
                while (input.TryReadPress(out int press))
                    Machine.Press(press, tick, doubleTapTicks, forwardTicks, forwardDistance, forwardHeight,
                        backwardTicks, backwardDistance, backwardHeight);
            poseTick = before == Machine.State ? poseTick + 1 : 0;
            return X + Machine.Advance();
        }

        public void ApplyPosition(float x)
        {
            Vector3 position = transform.position;
            position.x = x;
            transform.position = position;
            Render();
        }

        private void Render()
        {
            if (presentation != null)
                presentation.Render(Machine.State, Machine.Progress, Machine.VisualHeight, Facing, poseTick);
        }

        private void OnDisable()
        {
            Machine.Reset();
            if (input != null) input.ClearPresses();
            if (presentation != null) presentation.ResetHeight();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.8f, new Vector3(Width * 2, 1.6f, 0.1f));
        }
    }
}
