// Land of Fire · Estado lógico del luchador, independiente de Unity.
// Un solo reloj controla Bunny, golpe y Hurt; el motor y Animator obedecen sus fases.
using System;

namespace LandOfFire.BunnyStep
{
    // Mantener los cuatro primeros índices para compatibilidad.
    public enum FighterState { Idle, Guard, BunnyForward, BunnyBackward, BunnyForwardLoop, Hurt, Attack }
    public enum BunnyPhase { None, Preparation, Flight, Recovery }
    public enum AttackPhase { None, Anticipation, Smear, Pose, Recovery }

    // Máquina finita independiente de Unity. Un único reloj modifica su estado.
    public sealed class BunnyStateMachine
    {
        public FighterState State { get; private set; }
        public int Elapsed { get; private set; }
        public int Duration { get; private set; }
        public int StepDirection { get; private set; }
        public float Distance { get; private set; }
        public float Height { get; private set; }
        public BunnyPhase Phase { get; private set; }
        public int PhaseElapsed { get; private set; }
        public int PhaseDuration { get; private set; }
        public AttackPhase CurrentAttackPhase { get; private set; }
        public AttackMoveData CurrentAttack { get; private set; }
        public float PhaseProgress => PhaseDuration <= 1 ? 0 :
            (float)Math.Max(0, PhaseElapsed - 1) / (PhaseDuration - 1);
        public bool IsStepping => State == FighterState.BunnyForward || State == FighterState.BunnyBackward || State == FighterState.BunnyForwardLoop;
        public bool IsHurt => State == FighterState.Hurt;
        public bool IsAttacking => State == FighterState.Attack;
        public bool IsTimed => IsStepping || IsHurt || IsAttacking;
        public float Progress => IsTimed ? (float)Elapsed / Duration : 0;
        public event Action<FighterState, FighterState> StateChanged;
        private long lastBack = long.MinValue;
        private int facing = 1;
        private int loopTicks = 12;
        private float loopDistance = 1.2f, loopHeight = .3f, hurtStartHeight;
        private bool explicitTimings;
        private BunnyTiming forwardTiming, backwardTiming, loopTiming, activeTiming;
        private float flightProgress;
        private int preparationTicks;
        private int attackAnticipation, attackSmear, attackPose, attackRecovery;
        private float attackStartHeight;

        public void ConfigurePhases(BunnyTiming forward, BunnyTiming backward, BunnyTiming loop)
        {
            forwardTiming = forward;
            backwardTiming = backward;
            loopTiming = loop;
            explicitTimings = true;
        }

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
            attackStartHeight = 0;
            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;
            CurrentAttackPhase = AttackPhase.None;
            CurrentAttack = null;
            Duration = 0;
            flightProgress = 0;
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
                Phase = BunnyPhase.None;
                CurrentAttackPhase = AttackPhase.None;
                CurrentAttack = null;
            }
            if (!IsTimed)
            {
                Phase = BunnyPhase.None; PhaseElapsed = 0; PhaseDuration = 0;
                Change(heldDirection == -facing ? FighterState.Guard : FighterState.Idle);
            }
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
            CurrentAttack = null;
            CurrentAttackPhase = AttackPhase.None;
            attackStartHeight = 0;
            Duration = Math.Max(1, duration);
            if (state != FighterState.Hurt)
            {
                activeTiming = explicitTimings ?
                    (state == FighterState.BunnyBackward ? backwardTiming :
                     state == FighterState.BunnyForwardLoop ? loopTiming : forwardTiming) :
                    BunnyTiming.FromTotal(duration);
                // Recovery ya terminó: el ciclo sostenido entra directamente a Flight.
                preparationTicks = state == FighterState.BunnyForwardLoop ? 0 : activeTiming.Preparation;
                Duration = preparationTicks + activeTiming.Flight + activeTiming.Recovery;
            }
            Distance = Math.Max(0, distance);
            Height = Math.Max(0, height);
            StepDirection = direction;
            Elapsed = 0;
            Phase = state == FighterState.Hurt ? BunnyPhase.None :
                preparationTicks == 0 ? BunnyPhase.Flight : BunnyPhase.Preparation;
            PhaseElapsed = 0;
            PhaseDuration = state == FighterState.Hurt ? 0 :
                preparationTicks == 0 ? activeTiming.Flight : preparationTicks;
            flightProgress = 0;
            Change(state);
        }

        // Los datos se copian al comenzar: editar el asset afecta al próximo golpe, no al actual.
        public bool TryStartAttack(AttackMoveData move)
        {
            if (move == null || IsHurt || IsAttacking || State == FighterState.Guard) return false;
            if (IsStepping && !move.allowBunnyCancel) return false;
            if (State != FighterState.Idle && !IsStepping) return false;
            attackStartHeight = IsStepping ? VisualHeight : 0;
            attackAnticipation = move.AnticipationTicks;
            attackSmear = move.SmearTicks;
            attackPose = move.PoseTicks;
            attackRecovery = move.RecoveryTicks;
            Duration = attackAnticipation + attackSmear + attackPose + attackRecovery;
            CurrentAttack = move;
            CurrentAttackPhase = AttackPhase.Anticipation;
            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;
            flightProgress = 0;
            Elapsed = 0;
            ForgetTap();
            Change(FighterState.Attack);
            return true;
        }

        public float Advance()
        {
            if (!IsTimed) return 0;
            if (Elapsed >= Duration) return 0;
            int index = Elapsed++;
            if (IsHurt) return 0;
            if (IsAttacking)
            {
                if (index < attackAnticipation)
                    SetAttackPhase(AttackPhase.Anticipation, index, attackAnticipation);
                else if (index < attackAnticipation + attackSmear)
                    SetAttackPhase(AttackPhase.Smear, index - attackAnticipation, attackSmear);
                else if (index < attackAnticipation + attackSmear + attackPose)
                    SetAttackPhase(AttackPhase.Pose, index - attackAnticipation - attackSmear, attackPose);
                else
                    SetAttackPhase(AttackPhase.Recovery, index - attackAnticipation - attackSmear - attackPose, attackRecovery);
                return 0;
            }
            int takeoff = preparationTicks;
            int landing = takeoff + activeTiming.Flight;
            if (index < takeoff)
            {
                Phase = BunnyPhase.Preparation;
                PhaseDuration = preparationTicks;
                PhaseElapsed = index + 1;
                flightProgress = 0;
                return 0;
            }
            if (index < landing)
            {
                Phase = BunnyPhase.Flight;
                PhaseDuration = activeTiming.Flight;
                PhaseElapsed = index - takeoff + 1;
                float before = (float)(PhaseElapsed - 1) / activeTiming.Flight;
                flightProgress = (float)PhaseElapsed / activeTiming.Flight;
                return (Ease(flightProgress) - Ease(before)) * Distance * StepDirection;
            }
            Phase = BunnyPhase.Recovery;
            PhaseDuration = activeTiming.Recovery;
            PhaseElapsed = index - landing + 1;
            flightProgress = 1;
            return 0;
        }

        private void SetAttackPhase(AttackPhase next, int localIndex, int duration)
        {
            CurrentAttackPhase = next;
            PhaseElapsed = localIndex + 1;
            PhaseDuration = duration;
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
            IsAttacking ? attackStartHeight *
                (1 - Math.Min(1f, (float)Elapsed / Math.Max(1, attackAnticipation))) :
            IsStepping && Phase == BunnyPhase.Flight ? 4 * Height * flightProgress * (1 - flightProgress) : 0;
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
