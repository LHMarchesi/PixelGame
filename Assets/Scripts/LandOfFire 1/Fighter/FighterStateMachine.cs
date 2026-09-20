// Land of Fire · Máquina lógica común de un luchador.
// Controla estados, ticks, Bunny Step, Hurt y ataques.
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
        Attack
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

        public bool IsHurt =>
            State == FighterState.Hurt;

        public bool IsAttacking =>
            State == FighterState.Attack;

        public bool IsTimed =>
            IsStepping ||
            IsHurt ||
            IsAttacking;

        public float Progress =>
            IsTimed && Duration > 0
                ? (float)Elapsed / Duration
                : 0;

        // ================================================================
        // ATTACK CANCEL
        // ================================================================

        /// <summary>
        /// Indica si el ataque actual está dentro de su ventana
        /// configurable de cancel durante Pose.
        /// </summary>
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

                return CurrentAttack.IsCancelWindow(PhaseElapsed);
            }
        }

        public event Action<FighterState, FighterState> StateChanged;

        private int attackAnticipation;
        private int attackSmear;
        private int attackPose;
        private int attackRecovery;

        private float attackStartHeight;
        private float hurtStartHeight;

        private float flightProgress;

        private int configuredForwardTotal;
        private int configuredBackwardTotal;
        private int configuredLoopTotal;

        private float configuredLoopDistance;
        private float configuredLoopHeight;

        private int tapDirection;
        private long tapTick = long.MinValue;

        // ================================================================
        // CONFIGURATION
        // ================================================================

        public void ConfigureForwardLoop(
            int total,
            float distance,
            float height)
        {
            configuredLoopTotal = Mathf.Max(1, total);
            configuredLoopDistance = distance;
            configuredLoopHeight = height;
        }

        // ================================================================
        // TICK
        // ================================================================

        public void BeginTick(int facing, int direction)
        {
            if (IsTimed && State != FighterState.BunnyForwardLoop)
                return;

            if (State == FighterState.BunnyForwardLoop)
                return;

            if (direction == 0)
            {
                if (State == FighterState.Guard)
                    Change(FighterState.Idle);

                return;
            }

            if (State == FighterState.Idle)
                Change(FighterState.Guard);
        }

        public float Advance()
        {
            if (!IsTimed)
                return 0;

            if (Duration <= 0)
                return 0;

            if (Elapsed >= Duration)
            {
                FinishTimedState();
                return 0;
            }

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
                    attackAnticipation + attackSmear)
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
                        ? 1
                        : (float)Elapsed / Duration);

                return 0;
            }

            // ============================================================
            // BUNNY
            // ============================================================

            if (IsStepping)
            {
                if (Duration <= 1)
                {
                    flightProgress = 1;
                }
                else
                {
                    flightProgress =
                        (float)Elapsed / (Duration - 1);
                }

                Height = BunnyHeight(flightProgress);
                Distance = BunnyDistance(flightProgress);

                return Distance * StepDirection;
            }

            return 0;
        }

        // ================================================================
        // ATTACK START
        // ================================================================

        public bool TryStartAttack(AttackMoveData move)
        {
            if (move == null)
                return false;

            if (IsHurt)
                return false;

            if (IsAttacking)
                return false;

            if (State == FighterState.Guard)
                return false;

            if (IsStepping && !move.allowBunnyCancel)
                return false;

            if (State != FighterState.Idle && !IsStepping)
                return false;

            return StartAttack(move);
        }

        /// <summary>
        /// Intenta cancelar el ataque actual por otro ataque.
        /// Todos los ataques pueden cancelar a cualquier otro.
        /// La ventana solamente existe durante Pose.
        /// </summary>
        public bool TryCancelAttack(AttackMoveData move)
        {
            if (move == null)
                return false;

            if (!IsAttacking)
                return false;

            if (!IsAttackCancelWindow)
                return false;

            return StartAttack(move);
        }

        private bool StartAttack(AttackMoveData move)
        {
            attackStartHeight = IsStepping
                ? VisualHeight
                : 0;

            attackAnticipation = move.AnticipationTicks;
            attackSmear = move.SmearTicks;
            attackPose = move.PoseTicks;
            attackRecovery = move.RecoveryTicks;

            Duration =
                attackAnticipation +
                attackSmear +
                attackPose +
                attackRecovery;

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

        // ================================================================
        // BUNNY INPUT
        // ================================================================

        public void Press(
            int direction,
            long currentTick,
            int doubleTapTicks,
            int forwardDuration,
            float forwardDistance,
            float forwardHeight,
            int backwardDuration,
            float backwardDistance,
            float backwardHeight)
        {
            if (direction == 0)
                return;

            if (IsHurt || IsAttacking)
                return;

            if (State != FighterState.Idle &&
                State != FighterState.Guard)
                return;

            bool doubleTap =
                tapDirection == direction &&
                currentTick - tapTick <= doubleTapTicks;

            tapDirection = direction;
            tapTick = currentTick;

            if (doubleTap)
            {
                Start(
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
                        : backwardHeight);

                return;
            }

            Start(
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
                    : backwardHeight);
        }

        // ================================================================
        // HURT
        // ================================================================

        public void EnterHurt(int duration)
        {
            float currentHeight = VisualHeight;

            ForgetTap();

            Start(
                FighterState.Hurt,
                0,
                duration,
                0,
                0);

            hurtStartHeight = currentHeight;
        }

        // ================================================================
        // RESET
        // ================================================================

        public void Reset()
        {
            State = FighterState.Idle;

            Elapsed = 0;
            Duration = 0;

            StepDirection = 0;

            Distance = 0;
            Height = 0;

            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;

            CurrentAttackPhase = AttackPhase.None;
            CurrentAttack = null;

            attackAnticipation = 0;
            attackSmear = 0;
            attackPose = 0;
            attackRecovery = 0;

            attackStartHeight = 0;
            hurtStartHeight = 0;

            flightProgress = 0;

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
                if (IsHurt)
                    return Height;

                if (IsAttacking)
                    return attackStartHeight;

                return Height;
            }
        }

        private void Start(
            FighterState state,
            int direction,
            int duration,
            float distance,
            float height)
        {
            CurrentAttack = null;
            CurrentAttackPhase = AttackPhase.None;

            State = state;

            Elapsed = 0;
            Duration = Mathf.Max(1, duration);

            StepDirection = direction;

            Distance = distance;
            Height = height;

            Phase = BunnyPhase.Preparation;
            PhaseElapsed = 0;
            PhaseDuration = Duration;

            flightProgress = 0;

            Change(state);
        }

        private void SetAttackPhase(
            AttackPhase next,
            int localIndex,
            int duration)
        {
            CurrentAttackPhase = next;

            PhaseElapsed = localIndex + 1;
            PhaseDuration = duration;
        }

        private void FinishTimedState()
        {
            if (State == FighterState.BunnyForwardLoop)
            {
                Change(FighterState.Idle);
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
            CurrentAttackPhase = AttackPhase.None;
        }

        private void Change(FighterState next)
        {
            if (State == next)
                return;

            FighterState previous = State;
            State = next;

            StateChanged?.Invoke(previous, next);
        }

        private float BunnyHeight(float t)
        {
            t = Mathf.Clamp01(t);

            // Arco simple.
            return 4f * Height * t * (1f - t);
        }

        private float BunnyDistance(float t)
        {
            return Distance * Mathf.Clamp01(t);
        }
    }
}