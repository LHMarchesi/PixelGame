using System;

namespace LandOfFire.BunnyStep
{
    // Mantener los cuatro primeros índices para compatibilidad.
    public enum FighterState { Idle, Guard, BunnyForward, BunnyBackward, BunnyForwardLoop, Hurt }

    // Máquina finita independiente de Unity. Un único reloj modifica su estado.
    public sealed class BunnyStateMachine
    {
        public FighterState State { get; private set; }
        public int Elapsed { get; private set; }
        public int Duration { get; private set; }
        public int StepDirection { get; private set; }
        public float Distance { get; private set; }
        public float Height { get; private set; }
        public bool IsStepping => State == FighterState.BunnyForward || State == FighterState.BunnyBackward || State == FighterState.BunnyForwardLoop;
        public bool IsHurt => State == FighterState.Hurt;
        public bool IsTimed => IsStepping || IsHurt;
        public float Progress => IsTimed ? (float)Elapsed / Duration : 0;
        public event Action<FighterState, FighterState> StateChanged;
        private long lastBack = long.MinValue;
        private int facing = 1;
        private int loopTicks = 12;
        private float loopDistance = 1.2f, loopHeight = .3f, hurtStartHeight;

        public void ConfigureForwardLoop(int ticks, float distance, float height)
        {
            loopTicks = Math.Max(1, ticks);
            loopDistance = Math.Max(0, distance);
            loopHeight = Math.Max(0, height);
        }

        public void Reset()
        {
            lastBack = long.MinValue;
            Elapsed = 0;
            hurtStartHeight = 0;
            Change(FighterState.Idle);
        }

        public void BeginTick(int newFacing, int heldDirection)
        {
            bool facingChanged = newFacing != facing;
            if (facingChanged) { facing = newFacing; ForgetTap(); }
            if (IsTimed && Elapsed >= Duration)
            {
                bool forward = State == FighterState.BunnyForward || State == FighterState.BunnyForwardLoop;
                if (forward && !facingChanged && heldDirection == facing)
                {
                    Start(FighterState.BunnyForwardLoop, facing, loopTicks, loopDistance, loopHeight);
                    ForgetTap();
                    return;
                }
                Change(FighterState.Idle);
            }
            if (!IsTimed) Change(heldDirection == -facing ? FighterState.Guard : FighterState.Idle);
        }

        public void ForgetTap() { lastBack = long.MinValue; }

        public void Press(int direction, long tick, int doubleTapWindow,
            int forwardTicks, float forwardDistance, float forwardHeight,
            int backTicks, float backDistance, float backHeight)
        {
            if (IsTimed || direction == 0) return;
            if (direction == facing)
            {
                Start(FighterState.BunnyForward, direction, forwardTicks, forwardDistance, forwardHeight);
                ForgetTap();
            }
            else if (lastBack != long.MinValue && tick > lastBack && tick - lastBack <= doubleTapWindow)
            {
                Start(FighterState.BunnyBackward, direction, backTicks, backDistance, backHeight);
                ForgetTap();
            }
            else lastBack = tick;
        }

        private void Start(FighterState state, int direction, int duration, float distance, float height)
        {
            Duration = Math.Max(1, duration);
            Distance = Math.Max(0, distance);
            Height = Math.Max(0, height);
            StepDirection = direction;
            Elapsed = 0;
            Change(state);
        }

        public float Advance()
        {
            if (!IsTimed) return 0;
            float before = Progress;
            Elapsed = Math.Min(Duration, Elapsed + 1);
            if (IsHurt) return 0;
            // Smoothstep acumulado: distancia total exacta sin movimiento residual.
            return (Ease(Progress) - Ease(before)) * Distance * StepDirection;
        }

        // Se llama SOLO después de que la resolución de combate acepte el hit.
        public void EnterHurt(int duration)
        {
            float currentHeight = VisualHeight;
            ForgetTap();
            Start(FighterState.Hurt, 0, duration, 0, 0);
            hurtStartHeight = currentHeight;
        }

        public float VisualHeight => IsHurt ? hurtStartHeight * (1 - Progress) :
            IsStepping ? 4 * Height * Progress * (1 - Progress) : 0;
        private static float Ease(float t) => t * t * (3 - 2 * t);
        private void Change(FighterState next)
        {
            if (State == next) return;
            FighterState previous = State;
            State = next;
            StateChanged?.Invoke(previous, next);
        }
    }

    public static class ArenaSeparation
    {
        public static void Resolve(float oldA, float oldB, float widthA, float widthB,
            float min, float max, ref float a, ref float b)
        {
            float gap = widthA + widthB;
            a = Clamp(a, min + widthA, max - widthA);
            b = Clamp(b, min + widthB, max - widthB);
            if (b - a >= gap) return;
            float overlap = gap - (b - a);
            float advanceA = Math.Max(0, a - oldA), advanceB = Math.Max(0, oldB - b);
            float total = advanceA + advanceB;
            a -= overlap * (total > 0 ? advanceA / total : 0.5f);
            a = Clamp(a, min + widthA, max - widthB - gap);
            b = a + gap;
        }
        private static float Clamp(float x, float lo, float hi) => Math.Max(lo, Math.Min(hi, x));
    }
}
