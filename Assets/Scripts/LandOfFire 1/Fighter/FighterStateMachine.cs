// Land of Fire · Máquina lógica común de un luchador.
// Controla estados, ticks, Bunny Step, Hurt, Flying Hurt,
// Knockdown, Reset y ataques.

using System;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public enum FighterState
    {
        Idle,
        Guard,
        BunnyForward,
        BunnyBackward,
        BunnyForwardLoop,
        Hurt,
        Attack,
        FlyingHurt,
        Knockdown,
        Reset
    }

    public enum BunnyPhase
    {
        None,
        Preparation,
        Flight,
        Recovery
    }

    public enum AttackPhase
    {
        None,
        Anticipation,
        Smear,
        Pose,
        Recovery
    }

    public sealed class FighterStateMachine
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

        public float PhaseProgress =>
            PhaseDuration <= 1
                ? 0
                : (float)Math.Max(0, PhaseElapsed - 1) /
                  (PhaseDuration - 1);

        public bool IsStepping =>
            State == FighterState.BunnyForward ||
            State == FighterState.BunnyBackward ||
            State == FighterState.BunnyForwardLoop;

        public BunnyTiming BunnyTiming => bunnyTiming;

        private BunnyTiming bunnyTiming;

        private BunnyTiming configuredLoopTiming;
        public bool IsHurt =>
            State == FighterState.Hurt;

        public bool IsFlyingHurt =>
            State == FighterState.FlyingHurt;

        public bool IsKnockdown =>
            State == FighterState.Knockdown;

        public bool IsResetting =>
            State == FighterState.Reset;

        public bool IsAttacking =>
            State == FighterState.Attack;

        public bool IsTimed =>
            IsStepping ||
            IsHurt ||
            IsFlyingHurt ||
            IsKnockdown ||
            IsResetting ||
            IsAttacking;

        public float Progress =>
            IsTimed && Duration > 0
                ? (float)Elapsed / Duration
                : 0;

        // ================================================================
        // ATTACK CANCEL
        // ================================================================

        public bool IsAttackCancelWindow
        {
            get
            {
                if (!IsAttacking)
                    return false;

                if (CurrentAttack == null)
                    return false;

                if (CurrentAttackPhase != AttackPhase.Pose)
                    return false;

                return CurrentAttack.IsCancelWindow(
                    PhaseElapsed);
            }
        }

        public event Action<FighterState, FighterState> StateChanged;

        // ================================================================
        // ATTACK
        // ================================================================

        private int attackAnticipation;
        private int attackSmear;
        private int attackPose;
        private int attackRecovery;

        private float attackStartHeight;

        // ================================================================
        // HURT
        // ================================================================

        private float hurtStartHeight;

        // ================================================================
        // BUNNY
        // ================================================================

        private float flightProgress;
        private float startHeight;

        private int configuredLoopTotal;
        private float configuredLoopDistance;
        private float configuredLoopHeight;

        // ================================================================
        // FLYING HURT
        // ================================================================

        private float flyingHurtDistance;
        private float flyingHurtMaxHeight;
        private int flyingHurtKnockdownTicks;
        private float flyingProgress;

        // ================================================================
        // KNOCKDOWN
        // ================================================================

        private bool autoGetUp = true;

        // ================================================================
        // INPUT
        // ================================================================

        private int tapDirection;
        private long tapTick = long.MinValue;

        // ================================================================
        // CONFIGURATION
        // ================================================================

        public void ConfigureForwardLoop(
     int total,
     float distance,
     float height,
     BunnyTiming timing)
        {
            configuredLoopTotal =
                Mathf.Max(1, total);

            configuredLoopDistance =
                Mathf.Max(0, distance);

            configuredLoopHeight =
                Mathf.Max(0, height);

            configuredLoopTiming =
                timing;
        }
        public void ConfigureKnockdown(
            bool shouldAutoGetUp)
        {
            autoGetUp = shouldAutoGetUp;
        }

        // ================================================================
        // TICK
        // ================================================================

        public void BeginTick(
            int newFacing,
            int heldDirection)
        {
            // ============================================================
            // KNOCKDOWN
            // ============================================================

            if (State == FighterState.Knockdown &&
                Elapsed >= Duration)
            {
                if (autoGetUp)
                {
                    EnterReset();
                }

                return;
            }

            // ============================================================
            // RESET
            // ============================================================

            if (State == FighterState.Reset &&
                Elapsed >= Duration)
            {
                FinishReset();
                return;
            }

            // ============================================================
            // FIN DE OTRO ESTADO TEMPORIZADO
            // ============================================================

            if (IsTimed &&
                State != FighterState.Knockdown &&
                State != FighterState.Reset &&
                Elapsed >= Duration)
            {
                bool forward =
                    State == FighterState.BunnyForward ||
                    State == FighterState.BunnyForwardLoop;

                if (forward &&
                    heldDirection == newFacing)
                {
                    StartBunny(
                        FighterState.BunnyForwardLoop,
                        newFacing,
                        configuredLoopTotal,
                        configuredLoopDistance,
                        configuredLoopHeight,
                        configuredLoopTiming);

                    ForgetTap();
                    return;
                }

                FinishTimedState();
            }

            if (!IsTimed)
            {
                Phase = BunnyPhase.None;
                PhaseElapsed = 0;
                PhaseDuration = 0;

                Change(
                    heldDirection == -newFacing
                        ? FighterState.Guard
                        : FighterState.Idle);
            }
        }

        // ================================================================
        // ADVANCE
        // ================================================================

        public float Advance()
        {
            if (!IsTimed)
                return 0;

            // Knockdown terminado sin GetUp.
            if (IsKnockdown &&
                Elapsed >= Duration)
            {
                return 0;
            }

            if (Duration <= 0)
                return 0;

            if (Elapsed >= Duration)
                return 0;

            int index = Elapsed;

            Elapsed++;

            // ============================================================
            // ATTACK
            // ============================================================

            if (IsAttacking)
            {
                if (index < attackAnticipation)
                {
                    SetAttackPhase(
                        AttackPhase.Anticipation,
                        index,
                        attackAnticipation);
                }
                else if (
                    index <
                    attackAnticipation +
                    attackSmear)
                {
                    SetAttackPhase(
                        AttackPhase.Smear,
                        index - attackAnticipation,
                        attackSmear);
                }
                else if (
                    index <
                    attackAnticipation +
                    attackSmear +
                    attackPose)
                {
                    SetAttackPhase(
                        AttackPhase.Pose,
                        index -
                        attackAnticipation -
                        attackSmear,
                        attackPose);
                }
                else
                {
                    SetAttackPhase(
                        AttackPhase.Recovery,
                        index -
                        attackAnticipation -
                        attackSmear -
                        attackPose,
                        attackRecovery);
                }

                return 0;
            }

            // ============================================================
            // HURT
            // ============================================================

            if (IsHurt)
            {
                Height = Mathf.Lerp(
                    hurtStartHeight,
                    0,
                    Duration <= 1
                        ? 1f
                        : (float)Elapsed / Duration);

                return 0;
            }

            // ============================================================
            // FLYING HURT
            // ============================================================

            if (IsFlyingHurt)
            {
                Phase = BunnyPhase.Flight;
                PhaseDuration = Duration;

                float currentProgress =
                    Duration <= 1
                        ? 1f
                        : Mathf.Clamp01(
                            (float)Elapsed /
                            (Duration - 1));

                float previousProgress =
                    Duration <= 1
                        ? 0f
                        : Mathf.Clamp01(
                            (float)(Elapsed - 1) /
                            (Duration - 1));

                flyingProgress = currentProgress;

                Height =
                    ArcHeight(
                        flyingHurtMaxHeight,
                        currentProgress);

                float previousDistance =
                    flyingHurtDistance *
                    previousProgress;

                float currentDistance =
                    flyingHurtDistance *
                    currentProgress;

                Distance =
                    flyingHurtDistance;

                PhaseElapsed = Elapsed;

                return
                    (currentDistance -
                     previousDistance) *
                    StepDirection;
            }

            // ============================================================
            // KNOCKDOWN
            // ============================================================

            if (IsKnockdown)
            {
                Phase = BunnyPhase.None;
                PhaseElapsed = Elapsed;
                PhaseDuration = Duration;
                Height = 0;

                return 0;
            }

            // ============================================================
            // RESET
            // ============================================================

            if (IsResetting)
            {
                Phase = BunnyPhase.None;
                PhaseElapsed = Elapsed;
                PhaseDuration = Duration;
                Height = 0;

                return 0;
            }

            // ============================================================
            // BUNNY
            // ============================================================

            if (IsStepping)
            {
                int takeoff =
                    bunnyTiming.preparation;

                int landing =
                    takeoff +
                    bunnyTiming.Flight;

                // --------------------------------------------------------
                // PREPARATION
                // --------------------------------------------------------

                if (index < takeoff)
                {
                    Phase =
                        BunnyPhase.Preparation;

                    PhaseDuration =
                        Mathf.Max(
                            1,
                            bunnyTiming.preparation);

                    PhaseElapsed =
                        index + 1;

                    flightProgress = 0f;

                    Height = 0f;

                    return 0f;
                }

                // --------------------------------------------------------
                // FLIGHT
                // --------------------------------------------------------

                if (index < landing)
                {
                    Phase =
                        BunnyPhase.Flight;

                    PhaseDuration =
                        Mathf.Max(
                            1,
                            bunnyTiming.Flight);

                    PhaseElapsed =
                        index -
                        takeoff +
                        1;

                    float before =
                        bunnyTiming.Flight <= 1
                            ? 0f
                            : (float)(PhaseElapsed - 1) /
                              bunnyTiming.Flight;

                    flightProgress =
                        bunnyTiming.Flight <= 1
                            ? 1f
                            : (float)PhaseElapsed /
                              bunnyTiming.Flight;

                    before =
                        Mathf.Clamp01(before);

                    flightProgress =
                        Mathf.Clamp01(flightProgress);

                    // ----------------------------------------------------
                    // ALTURA
                    // ----------------------------------------------------

                    Height =
                        ArcHeight(
                            startHeight,
                            flightProgress);

                    // ----------------------------------------------------
                    // MOVIMIENTO HORIZONTAL
                    // ----------------------------------------------------

                    return
                        (Ease(flightProgress) -
                         Ease(before)) *
                        Distance *
                        StepDirection;
                }

                // --------------------------------------------------------
                // RECOVERY
                // --------------------------------------------------------

                Phase =
                    BunnyPhase.Recovery;

                PhaseDuration =
                    Mathf.Max(
                        1,
                        bunnyTiming.Recovery);

                PhaseElapsed =
                    index -
                    landing +
                    1;

                flightProgress = 1f;

                Height = 0f;

                return 0f;
            }

            return 0;
        }
        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
        // ================================================================
        // ATTACK START
        // ================================================================

        public bool TryStartAttack(
            AttackMoveData move)
        {
            if (move == null)
                return false;

            if (IsHurt ||
                IsFlyingHurt ||
                IsKnockdown ||
                IsResetting)
            {
                return false;
            }

            if (IsAttacking)
                return false;

            if (State == FighterState.Guard)
                return false;

            if (IsStepping &&
                !move.allowBunnyCancel)
            {
                return false;
            }

            if (State != FighterState.Idle &&
                !IsStepping)
            {
                return false;
            }

            return StartAttack(move);
        }

        public bool TryCancelAttack(
            AttackMoveData move)
        {
            if (move == null)
                return false;

            if (!IsAttacking)
                return false;

            if (!IsAttackCancelWindow)
                return false;

            return StartAttack(move);
        }

        private bool StartAttack(
            AttackMoveData move)
        {
            attackStartHeight =
                IsStepping
                    ? VisualHeight
                    : 0;

            attackAnticipation =
                move.AnticipationTicks;

            attackSmear =
                move.SmearTicks;

            attackPose =
                move.PoseTicks;

            attackRecovery =
                move.RecoveryTicks;

            Duration =
                attackAnticipation +
                attackSmear +
                attackPose +
                attackRecovery;

            CurrentAttack = move;

            CurrentAttackPhase =
                AttackPhase.Anticipation;

            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;

            Elapsed = 0;

            flightProgress = 0;

            ForgetTap();

            Change(FighterState.Attack);

            return true;
        }

        // ================================================================
        // BUNNY
        // ================================================================

        public void Press(
     int direction,
     long currentTick,
     int doubleTapTicks,
     int forwardDuration,
     float forwardDistance,
     float forwardHeight,
     BunnyTiming forwardTiming,
     int backwardDuration,
     float backwardDistance,
     float backwardHeight,
     BunnyTiming backwardTiming,
     BunnyTiming forwardLoopTiming)
        {
            if (direction == 0)
                return;

            if (IsTimed)
                return;

            if (State != FighterState.Idle &&
                State != FighterState.Guard)
            {
                return;
            }

            bool doubleTap =
                tapDirection == direction &&
                currentTick - tapTick <=
                doubleTapTicks;

            tapDirection = direction;
            tapTick = currentTick;

            if (doubleTap)
            {
                StartBunny(
                    direction > 0
                        ? FighterState.BunnyForwardLoop
                        : FighterState.BunnyBackward,

                    direction,

                    direction > 0
                        ? configuredLoopTotal
                        : backwardDuration,

                    direction > 0
                        ? configuredLoopDistance
                        : backwardDistance,

                    direction > 0
                        ? configuredLoopHeight
                        : backwardHeight,

                    direction > 0
                        ? forwardLoopTiming
                        : backwardTiming);

                ForgetTap();
                return;
            }

            StartBunny(
                direction > 0
                    ? FighterState.BunnyForward
                    : FighterState.BunnyBackward,

                direction,

                direction > 0
                    ? forwardDuration
                    : backwardDuration,

                direction > 0
                    ? forwardDistance
                    : backwardDistance,

                direction > 0
                    ? forwardHeight
                    : backwardHeight,

                direction > 0
                    ? forwardTiming
                    : backwardTiming);

            ForgetTap();
        }

        // ================================================================
        // HURT
        // ================================================================

        public void EnterHurt(
            int duration)
        {
            float currentHeight =
                VisualHeight;

            ForgetTap();

            StartTimedState(
     FighterState.Hurt,
     duration);

            hurtStartHeight =
                currentHeight;
        }

        // ================================================================
        // FLYING HURT
        // ================================================================

        public void EnterFlyingHurt(
            int duration,
            float distance,
            float height,
            int direction,
            int knockdownTicks)
        {
            ForgetTap();

            CurrentAttack = null;
            CurrentAttackPhase =
                AttackPhase.None;

            Duration =
                Mathf.Max(1, duration);

            Elapsed = 0;

            StepDirection =
                direction >= 0
                    ? 1
                    : -1;

            Distance =
                Mathf.Max(0, distance);

            Height = 0;

            flyingHurtDistance =
                Mathf.Max(0, distance);

            flyingHurtMaxHeight =
                Mathf.Max(0, height);

            flyingHurtKnockdownTicks =
                Mathf.Max(1, knockdownTicks);

            Phase =
                BunnyPhase.Flight;

            PhaseElapsed = 0;
            PhaseDuration = Duration;

            flyingProgress = 0;

            // IMPORTANTE:
            // no asignamos State antes de Change().
            Change(FighterState.FlyingHurt);
        }

        private void StartTimedState(
    FighterState state,
    int duration)
        {
            CurrentAttack = null;
            CurrentAttackPhase =
                AttackPhase.None;

            Elapsed = 0;

            Duration =
                Mathf.Max(1, duration);

            StepDirection = 0;

            Distance = 0;
            Height = 0;

            Phase =
                BunnyPhase.None;

            PhaseElapsed = 0;
            PhaseDuration = Duration;

            flightProgress = 0;

            Change(state);
        }
        // ================================================================
        // KNOCKDOWN
        // ================================================================

        private void EnterKnockdown()
        {
            CurrentAttack = null;

            CurrentAttackPhase =
                AttackPhase.None;

            Elapsed = 0;

            Duration =
                Mathf.Max(
                    1,
                    flyingHurtKnockdownTicks);

            StepDirection = 0;
            Distance = 0;
            Height = 0;

            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = Duration;

            // IMPORTANTE:
            // no asignamos State antes de Change().
            Change(FighterState.Knockdown);
        }

        // ================================================================
        // RESET / GET UP
        // ================================================================

        private void EnterReset()
        {
            CurrentAttack = null;
            CurrentAttackPhase =
                AttackPhase.None;

            Elapsed = 0;

            // Por ahora 1 tick.
            // Más adelante esto puede salir del FighterMovementProfile.
            Duration = 1;

            StepDirection = 0;
            Distance = 0;
            Height = 0;

            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = Duration;

            Change(FighterState.Reset);
        }

        private void FinishReset()
        {
            Elapsed = 0;
            Duration = 0;

            StepDirection = 0;
            Distance = 0;
            Height = 0;

            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;

            CurrentAttack = null;
            CurrentAttackPhase =
                AttackPhase.None;

            Change(FighterState.Idle);
        }

        // ================================================================
        // RESET
        // ================================================================

        public void Reset()
        {
            State =
                FighterState.Idle;

            Elapsed = 0;
            Duration = 0;

            StepDirection = 0;

            Distance = 0;
            Height = 0;

            Phase =
                BunnyPhase.None;

            PhaseElapsed = 0;
            PhaseDuration = 0;

            CurrentAttackPhase =
                AttackPhase.None;

            CurrentAttack = null;

            attackAnticipation = 0;
            attackSmear = 0;
            attackPose = 0;
            attackRecovery = 0;

            attackStartHeight = 0;
            hurtStartHeight = 0;

            flightProgress = 0;
            startHeight = 0;

            flyingHurtDistance = 0;
            flyingHurtMaxHeight = 0;
            flyingHurtKnockdownTicks = 0;
            flyingProgress = 0;
            ForgetTap();
        }

        // ================================================================
        // HELPERS
        // ================================================================

        public void ForgetTap()
        {
            tapDirection = 0;
            tapTick = long.MinValue;
        }

        public float VisualHeight
        {
            get
            {
                if (IsStepping ||
                    IsHurt ||
                    IsFlyingHurt)
                {
                    return Height;
                }

                if (IsAttacking)
                    return attackStartHeight;

                return 0;
            }
        }

        private void StartBunny(
      FighterState state,
      int direction,
      int duration,
      float distance,
      float height,
      BunnyTiming timing)
        {
            CurrentAttack = null;
            CurrentAttackPhase =
                AttackPhase.None;

            Elapsed = 0;

            Duration =
                Mathf.Max(1, duration);

            StepDirection = direction;

            Distance =
                Mathf.Max(0, distance);

            startHeight =
                Mathf.Max(0, height);

            Height = 0;

            bunnyTiming = timing;

            Phase =
                BunnyPhase.Preparation;

            PhaseElapsed = 0;

            PhaseDuration =
                Mathf.Max(
                    1,
                    timing.preparation);

            flightProgress = 0;

            Change(state);
        }

        private void SetAttackPhase(
            AttackPhase next,
            int localIndex,
            int duration)
        {
            CurrentAttackPhase =
                next;

            PhaseElapsed =
                localIndex + 1;

            PhaseDuration =
                duration;
        }

        private void FinishTimedState()
        {
            if (State ==
                FighterState.FlyingHurt)
            {
                EnterKnockdown();
                return;
            }

            if (State ==
                FighterState.BunnyForwardLoop)
            {
                Change(FighterState.Idle);
                Elapsed = 0;
                Duration = 0;
                return;
            }

            Change(FighterState.Idle);

            Elapsed = 0;
            Duration = 0;

            Distance = 0;
            Height = 0;

            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;

            CurrentAttack = null;
            CurrentAttackPhase =
                AttackPhase.None;
        }

        private void Change(
            FighterState next)
        {
            if (State == next)
                return;

            FighterState previous =
                State;

            State = next;

            StateChanged?.Invoke(
                previous,
                next);
        }

        private float ArcHeight(
            float maxHeight,
            float t)
        {
            t = Mathf.Clamp01(t);

            return
                4f *
                maxHeight *
                t *
                (1f - t);
        }
    }
}