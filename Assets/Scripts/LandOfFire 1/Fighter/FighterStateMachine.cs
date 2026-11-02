// Land of Fire · Máquina lógica común de un luchador.
// Controla estados, ticks, Bunny Step, Hurt, Flying Hurt,
// Knockdown, Reset y ataques.
//
// La máquina NO interpreta input crudo.
// Recibe comandos ya reconocidos desde FighterCommandRecognizer.

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
        AirAttack,
        FlyingHurt,
        Knockdown,
        Reset,
        Jump,
        Walk
    }

    public enum BunnyPhase
    {
        None,
        Preparation,
        Flight,
        Recovery
    }

    public enum JumpPhase
    {
        None,
        Anticipation,
        Air,
        Descending,
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
        // ================================================================
        // STATE
        // ================================================================

        public FighterState State { get; private set; }

        public int Elapsed { get; private set; }

        public int Duration { get; private set; }

        public int StepDirection { get; private set; }

        public float Distance { get; private set; }

        public float Height { get; private set; }

        public BunnyPhase Phase { get; private set; }

        public JumpPhase CurrentJumpPhase { get; private set; }

        public int PhaseElapsed { get; private set; }

        public int PhaseDuration { get; private set; }

        public AttackPhase CurrentAttackPhase { get; private set; }

        public AttackMoveData CurrentAttack { get; private set; }

        public float PhaseProgress =>
            PhaseDuration <= 1
                ? 0
                : (float)Math.Max(
                    0,
                    PhaseElapsed - 1) /
                  (PhaseDuration - 1);

        public bool IsWalking =>
            State == FighterState.Walk;

        public bool IsStepping =>
            State == FighterState.BunnyForward ||
            State == FighterState.BunnyBackward ||
            State == FighterState.BunnyForwardLoop;

        public BunnyTiming BunnyTiming =>
            bunnyTiming;

        private BunnyTiming bunnyTiming;

        private BunnyTiming configuredLoopTiming;

        // Ground walk is evaluated in deterministic 60 Hz ticks.
        private float configuredWalkSpeed;
        private bool allowBackwardWalk;
        private int walkDirection;

        public bool IsHurt =>
            State == FighterState.Hurt;

        public bool IsFlyingHurt =>
            State == FighterState.FlyingHurt;

        public bool IsKnockdown =>
            State == FighterState.Knockdown;

        public bool IsResetting =>
            State == FighterState.Reset;

        public bool IsAttacking =>
            State == FighterState.Attack ||
            State == FighterState.AirAttack;

        public bool IsAirAttacking =>
            State == FighterState.AirAttack;

        public bool IsJumping =>
            State == FighterState.Jump;

        public bool IsTimed =>
            IsStepping ||
            IsJumping ||
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

                if (CurrentAttackPhase !=
                    AttackPhase.Pose)
                {
                    return false;
                }

                return CurrentAttack.IsCancelWindow(
                    PhaseElapsed);
            }
        }

        public event Action<
            FighterState,
            FighterState> StateChanged;

        // ================================================================
        // ATTACK DATA
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
        private int configuredJumpAnticipation;
        private int configuredJumpAir;
        private int configuredJumpDescending;
        private int configuredJumpRecovery;
        private float configuredJumpHeight;
        private float configuredJumpAirControl;
        private int jumpAirDirection;
        private float jumpHeightOffset;

        // Jump context suspended while an AirAttack runs.
        private bool hasSuspendedJump;
        private int suspendedJumpElapsed;
        private int suspendedJumpDuration;
        private int suspendedJumpAirDirection;
        private JumpPhase suspendedJumpPhase;
        private int suspendedJumpPhaseElapsed;
        private int suspendedJumpPhaseDuration;
        private float suspendedJumpHeightOffset;
        private float suspendedJumpStartHeight;

        // AirAttack displacement is accumulated phase by phase.
        private int airAttackFacing = 1;
        private Vector2 airAttackLastOffset;

        private int configuredForwardTotal;
        private float configuredForwardDistance;
        private float configuredForwardHeight;
        private BunnyTiming configuredForwardTiming;

        private int configuredBackwardTotal;
        private float configuredBackwardDistance;
        private float configuredBackwardHeight;
        private BunnyTiming configuredBackwardTiming;

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
        // RESET
        // ================================================================

        private int configuredResetTicks = 20;

        // ================================================================
        // CONFIGURATION
        // ================================================================

        public void ConfigureBunny(
            int forwardTotal,
            float forwardDistance,
            float forwardHeight,
            BunnyTiming forwardTiming,

            int backwardTotal,
            float backwardDistance,
            float backwardHeight,
            BunnyTiming backwardTiming,

            int loopTotal,
            float loopDistance,
            float loopHeight,
            BunnyTiming loopTiming)
        {
            configuredForwardTotal =
                Mathf.Max(
                    1,
                    forwardTotal);

            configuredForwardDistance =
                Mathf.Max(
                    0,
                    forwardDistance);

            configuredForwardHeight =
                Mathf.Max(
                    0,
                    forwardHeight);

            configuredForwardTiming =
                forwardTiming;


            configuredBackwardTotal =
                Mathf.Max(
                    1,
                    backwardTotal);

            configuredBackwardDistance =
                Mathf.Max(
                    0,
                    backwardDistance);

            configuredBackwardHeight =
                Mathf.Max(
                    0,
                    backwardHeight);

            configuredBackwardTiming =
                backwardTiming;


            configuredLoopTotal =
                Mathf.Max(
                    1,
                    loopTotal);

            configuredLoopDistance =
                Mathf.Max(
                    0,
                    loopDistance);

            configuredLoopHeight =
                Mathf.Max(
                    0,
                    loopHeight);

            configuredLoopTiming =
                loopTiming;
        }

        public void ConfigureWalk(
            float speedUnitsPerSecond,
            bool canWalkBackward)
        {
            configuredWalkSpeed =
                Mathf.Max(0f, speedUnitsPerSecond);

            allowBackwardWalk =
                canWalkBackward;
        }

        public void ConfigureJump(
            int anticipationTicks,
            int airTicks,
            int descendingTicks,
            int recoveryTicks,
            float height,
            float airControl)
        {
            configuredJumpAnticipation = Mathf.Max(1, anticipationTicks);
            configuredJumpAir = Mathf.Max(1, airTicks);
            configuredJumpDescending = Mathf.Max(1, descendingTicks);
            configuredJumpRecovery = Mathf.Max(1, recoveryTicks);
            configuredJumpHeight = Mathf.Max(0, height);
            configuredJumpAirControl = Mathf.Max(0, airControl);
        }

        public bool ExecuteJump()
        {
            if (IsTimed)
                return false;

            if (State != FighterState.Idle &&
                State != FighterState.Guard &&
                State != FighterState.Walk)
                return false;

            Elapsed = 0;
            Duration =
                configuredJumpAnticipation +
                configuredJumpAir +
                configuredJumpDescending +
                configuredJumpRecovery;

            StepDirection = 0;
            Distance = 0;
            Height = 0;
            jumpAirDirection = 0;
            jumpHeightOffset = 0f;
            hasSuspendedJump = false;
            CurrentJumpPhase = JumpPhase.Anticipation;
            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = configuredJumpAnticipation;
            CurrentAttack = null;
            CurrentAttackPhase = AttackPhase.None;
            flightProgress = 0;

            Change(FighterState.Jump);
            return true;
        }

        public void ConfigureKnockdown(
            bool shouldAutoGetUp)
        {
            autoGetUp =
                shouldAutoGetUp;
        }

        public void ConfigureReset(
            int resetTicks)
        {
            configuredResetTicks =
                Mathf.Max(
                    1,
                    resetTicks);
        }

        // ================================================================
        // COMMANDS
        // ================================================================

        public bool ExecuteBunnyForward(
            int facing)
        {
            if (!CanStartBunny())
                return false;

            StartBunny(
                FighterState.BunnyForward,
                facing,
                configuredForwardTotal,
                configuredForwardDistance,
                configuredForwardHeight,
                configuredForwardTiming);

            return true;
        }

        public bool ExecuteBunnyBackward(
            int facing)
        {
            if (!CanStartBunny())
                return false;

            StartBunny(
                FighterState.BunnyBackward,
                -facing,
                configuredBackwardTotal,
                configuredBackwardDistance,
                configuredBackwardHeight,
                configuredBackwardTiming);

            return true;
        }

        public bool ExecuteBunnyForwardLoop(
            int facing)
        {
            if (!CanStartBunny())
                return false;

            StartBunny(
                FighterState.BunnyForwardLoop,
                facing,
                configuredLoopTotal,
                configuredLoopDistance,
                configuredLoopHeight,
                configuredLoopTiming);

            return true;
        }

        private bool CanStartBunny()
        {
            if (IsTimed)
                return false;

            return
                State == FighterState.Idle ||
                State == FighterState.Guard ||
                State == FighterState.Walk;
        }

        // ================================================================
        // TICK
        // ================================================================

        public void BeginTick(
            int newFacing,
            int heldDirection)
        {
            if (State == FighterState.Jump)
            {
                jumpAirDirection = heldDirection;
            }
            else if (IsAirAttacking &&
                     CurrentAttack != null &&
                     !CurrentAttack.useCustomAirMovement)
            {
                // When the attack follows the jump arc, keep its normal
                // horizontal air control responsive to directional input.
                suspendedJumpAirDirection = heldDirection;
            }

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
                FinishTimedState();
            }

            // ============================================================
            // GROUNDED STANCE: IDLE / WALK / GUARD
            // ============================================================

            if (!IsTimed)
            {
                Phase = BunnyPhase.None;
                PhaseElapsed = 0;
                PhaseDuration = 0;
                walkDirection = heldDirection;

                if (heldDirection == -newFacing && !allowBackwardWalk)
                {
                    Change(FighterState.Guard);
                }
                else if (heldDirection != 0)
                {
                    Change(FighterState.Walk);
                }
                else
                {
                    Change(FighterState.Idle);
                }
            }
        }

        // ================================================================
        // ADVANCE
        // ================================================================

        public float Advance()
        {
            if (State == FighterState.Walk)
            {
                const float TickSeconds = 1f / 60f;
                return configuredWalkSpeed * TickSeconds * walkDirection;
            }

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

            int index =
                Elapsed;

            Elapsed++;

            // ============================================================
            // JUMP
            // ============================================================

            if (IsJumping)
            {
                int anticipationEnd = configuredJumpAnticipation;
                int airEnd = anticipationEnd + configuredJumpAir;
                int descendingEnd = airEnd + configuredJumpDescending;

                if (index < anticipationEnd)
                {
                    CurrentJumpPhase = JumpPhase.Anticipation;
                    PhaseElapsed = index + 1;
                    PhaseDuration = configuredJumpAnticipation;
                    Height = 0f;
                    return 0f;
                }

                if (index < airEnd)
                {
                    CurrentJumpPhase = JumpPhase.Air;
                    PhaseElapsed = index - anticipationEnd + 1;
                    PhaseDuration = configuredJumpAir;

                    float progress = configuredJumpAir <= 1
                        ? 1f
                        : Mathf.Clamp01((float)(PhaseElapsed - 1) / (configuredJumpAir - 1));

                    Height = Mathf.Max(0f, configuredJumpHeight * Ease(progress) + jumpHeightOffset);
                    return configuredJumpAirControl * jumpAirDirection;
                }

                if (index < descendingEnd)
                {
                    CurrentJumpPhase = JumpPhase.Descending;
                    PhaseElapsed = index - airEnd + 1;
                    PhaseDuration = configuredJumpDescending;

                    float progress = configuredJumpDescending <= 1
                        ? 1f
                        : Mathf.Clamp01((float)(PhaseElapsed - 1) / (configuredJumpDescending - 1));

                    Height = Mathf.Max(0f, configuredJumpHeight * (1f - Ease(progress)) + jumpHeightOffset);
                    return configuredJumpAirControl * jumpAirDirection;
                }

                CurrentJumpPhase = JumpPhase.Recovery;
                PhaseElapsed = index - descendingEnd + 1;
                PhaseDuration = configuredJumpRecovery;
                Height = 0f;
                return 0f;
            }

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
                        index -
                        attackAnticipation,
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

                return IsAirAttacking
                    ? AdvanceAirAttackMovement()
                    : 0f;
            }

            // ============================================================
            // HURT
            // ============================================================

            if (IsHurt)
            {
                Height =
                    Mathf.Lerp(
                        hurtStartHeight,
                        0,
                        Duration <= 1
                            ? 1f
                            : (float)Elapsed /
                              Duration);

                return 0;
            }

            // ============================================================
            // FLYING HURT
            // ============================================================

            if (IsFlyingHurt)
            {
                Phase =
                    BunnyPhase.Flight;

                PhaseDuration =
                    Duration;

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

                flyingProgress =
                    currentProgress;

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

                PhaseElapsed =
                    Elapsed;

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
                Phase =
                    BunnyPhase.None;

                PhaseElapsed =
                    Elapsed;

                PhaseDuration =
                    Duration;

                Height = 0;

                return 0;
            }

            // ============================================================
            // RESET
            // ============================================================

            if (IsResetting)
            {
                Phase =
                    BunnyPhase.None;

                PhaseElapsed =
                    Elapsed;

                PhaseDuration =
                    Duration;

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
                        Mathf.Clamp01(
                            before);

                    flightProgress =
                        Mathf.Clamp01(
                            flightProgress);

                    Height =
                        ArcHeight(
                            startHeight,
                            flightProgress);

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

        private static float Ease(
            float t)
        {
            t =
                Mathf.Clamp01(t);

            return
                t *
                t *
                (3f - 2f * t);
        }

        // ================================================================
        // ATTACK START
        // ================================================================

        public bool TryStartAttack(
            AttackMoveData move)
        {
            if (move == null || IsAttacking)
                return false;

            if (IsHurt || IsFlyingHurt || IsKnockdown || IsResetting)
                return false;

            if (State == FighterState.Guard)
                return false;

            if (IsStepping && !move.allowBunnyCancel)
                return false;

            if (State != FighterState.Idle &&
                State != FighterState.Walk &&
                !IsStepping)
            {
                return false;
            }

            return StartAttack(move, false, 1);
        }

        public bool TryStartAirAttack(
            AttackMoveData move,
            int facing)
        {
            if (move == null || !IsJumping || Height <= 0.001f)
                return false;

            if (CurrentJumpPhase != JumpPhase.Air &&
                CurrentJumpPhase != JumpPhase.Descending)
            {
                return false;
            }

            return StartAttack(move, true, facing);
        }

        public bool TryCancelAttack(AttackMoveData move)
        {
            return TryCancelAttack(move, 1);
        }

        public bool TryCancelAttack(
            AttackMoveData move,
            int facing)
        {
            if (move == null || !IsAttacking || !IsAttackCancelWindow)
                return false;

            return StartAttack(move, IsAirAttacking, facing);
        }

        private bool StartAttack(
            AttackMoveData move,
            bool aerial,
            int facing)
        {
            if (aerial)
            {
                // Capture the jump only on the first AirAttack. Air cancels
                // keep the original suspended context intact.
                if (IsJumping)
                {
                    hasSuspendedJump = true;
                    suspendedJumpElapsed = Elapsed;
                    suspendedJumpDuration = Duration;
                    suspendedJumpAirDirection = jumpAirDirection;
                    suspendedJumpPhase = CurrentJumpPhase;
                    suspendedJumpPhaseElapsed = PhaseElapsed;
                    suspendedJumpPhaseDuration = PhaseDuration;
                    suspendedJumpHeightOffset = jumpHeightOffset;
                }
                else if (IsAirAttacking &&
                         hasSuspendedJump &&
                         CurrentAttack != null &&
                         CurrentAttack.useCustomAirMovement)
                {
                    // Commit the vertical displacement of the current
                    // custom attack before starting a cancel. This keeps
                    // both custom->custom and custom->jump-arc cancels
                    // continuous instead of snapping to the old curve.
                    float jumpBaseHeight =
                        JumpHeightAtProcessedElapsed(
                            suspendedJumpElapsed,
                            suspendedJumpHeightOffset);

                    suspendedJumpHeightOffset +=
                        Height - jumpBaseHeight;
                }

                attackStartHeight = Height;
                if (hasSuspendedJump)
                    suspendedJumpStartHeight = Height;

                airAttackFacing = facing >= 0 ? 1 : -1;
                airAttackLastOffset = Vector2.zero;
            }
            else
            {
                attackStartHeight = IsStepping ? VisualHeight : 0f;
                hasSuspendedJump = false;
                jumpHeightOffset = 0f;
            }

            attackAnticipation = move.AnticipationTicks;
            attackSmear = move.SmearTicks;
            attackPose = move.PoseTicks;
            attackRecovery = move.RecoveryTicks;

            Duration = attackAnticipation + attackSmear + attackPose + attackRecovery;
            CurrentAttack = move;
            CurrentAttackPhase = AttackPhase.Anticipation;
            Phase = BunnyPhase.None;
            CurrentJumpPhase = JumpPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;
            Elapsed = 0;
            flightProgress = 0;

            Change(aerial ? FighterState.AirAttack : FighterState.Attack);
            return true;
        }

        private float AdvanceAirAttackMovement()
        {
            if (CurrentAttack == null)
                return 0f;

            if (!CurrentAttack.useCustomAirMovement)
                return AdvanceSuspendedJumpTrajectory();

            float phaseProgress = PhaseDuration <= 1
                ? 1f
                : Mathf.Clamp01((float)(PhaseElapsed - 1) / (PhaseDuration - 1));

            Vector2 offset = Vector2.zero;

            switch (CurrentAttackPhase)
            {
                case AttackPhase.Anticipation:
                    offset = CurrentAttack.airAnticipationMovement * phaseProgress;
                    break;

                case AttackPhase.Smear:
                    offset = CurrentAttack.airAnticipationMovement +
                             CurrentAttack.airSmearMovement * phaseProgress;
                    break;

                case AttackPhase.Pose:
                    offset = CurrentAttack.airAnticipationMovement +
                             CurrentAttack.airSmearMovement +
                             CurrentAttack.airPoseMovement * phaseProgress;
                    break;

                case AttackPhase.Recovery:
                    offset = CurrentAttack.airAnticipationMovement +
                             CurrentAttack.airSmearMovement +
                             CurrentAttack.airPoseMovement +
                             CurrentAttack.airRecoveryMovement * phaseProgress;
                    break;
            }

            float horizontalDelta = offset.x - airAttackLastOffset.x;
            airAttackLastOffset = offset;
            Height = Mathf.Max(0f, attackStartHeight + offset.y);

            return horizontalDelta * airAttackFacing;
        }

        private float AdvanceSuspendedJumpTrajectory()
        {
            if (!hasSuspendedJump)
                return 0f;

            int index = suspendedJumpElapsed;

            int anticipationEnd = configuredJumpAnticipation;
            int airEnd = anticipationEnd + configuredJumpAir;
            int descendingEnd = airEnd + configuredJumpDescending;

            if (index >= suspendedJumpDuration)
            {
                suspendedJumpElapsed = suspendedJumpDuration;
                suspendedJumpPhase = JumpPhase.Recovery;
                suspendedJumpPhaseElapsed = configuredJumpRecovery;
                suspendedJumpPhaseDuration = configuredJumpRecovery;
                Height = 0f;
                return 0f;
            }

            suspendedJumpElapsed++;

            if (index < anticipationEnd)
            {
                suspendedJumpPhase = JumpPhase.Anticipation;
                suspendedJumpPhaseElapsed = index + 1;
                suspendedJumpPhaseDuration = configuredJumpAnticipation;
                Height = 0f;
                return 0f;
            }

            if (index < airEnd)
            {
                suspendedJumpPhase = JumpPhase.Air;
                suspendedJumpPhaseElapsed = index - anticipationEnd + 1;
                suspendedJumpPhaseDuration = configuredJumpAir;

                float progress = configuredJumpAir <= 1
                    ? 1f
                    : Mathf.Clamp01(
                        (float)(suspendedJumpPhaseElapsed - 1) /
                        (configuredJumpAir - 1));

                Height = Mathf.Max(
                    0f,
                    configuredJumpHeight * Ease(progress) +
                    suspendedJumpHeightOffset);

                return configuredJumpAirControl *
                       suspendedJumpAirDirection;
            }

            if (index < descendingEnd)
            {
                suspendedJumpPhase = JumpPhase.Descending;
                suspendedJumpPhaseElapsed = index - airEnd + 1;
                suspendedJumpPhaseDuration = configuredJumpDescending;

                float progress = configuredJumpDescending <= 1
                    ? 1f
                    : Mathf.Clamp01(
                        (float)(suspendedJumpPhaseElapsed - 1) /
                        (configuredJumpDescending - 1));

                Height = Mathf.Max(
                    0f,
                    configuredJumpHeight * (1f - Ease(progress)) +
                    suspendedJumpHeightOffset);

                return configuredJumpAirControl *
                       suspendedJumpAirDirection;
            }

            suspendedJumpPhase = JumpPhase.Recovery;
            suspendedJumpPhaseElapsed = index - descendingEnd + 1;
            suspendedJumpPhaseDuration = configuredJumpRecovery;
            Height = 0f;
            return 0f;
        }

        private float JumpHeightAtProcessedElapsed(
            int elapsed,
            float heightOffset)
        {
            int index = Mathf.Max(0, elapsed - 1);

            int anticipationEnd = configuredJumpAnticipation;
            int airEnd = anticipationEnd + configuredJumpAir;
            int descendingEnd = airEnd + configuredJumpDescending;

            if (index < anticipationEnd || index >= descendingEnd)
                return 0f;

            if (index < airEnd)
            {
                int phaseElapsed = index - anticipationEnd + 1;
                float progress = configuredJumpAir <= 1
                    ? 1f
                    : Mathf.Clamp01(
                        (float)(phaseElapsed - 1) /
                        (configuredJumpAir - 1));

                return Mathf.Max(
                    0f,
                    configuredJumpHeight * Ease(progress) +
                    heightOffset);
            }

            int descendingElapsed = index - airEnd + 1;
            float descendingProgress = configuredJumpDescending <= 1
                ? 1f
                : Mathf.Clamp01(
                    (float)(descendingElapsed - 1) /
                    (configuredJumpDescending - 1));

            return Mathf.Max(
                0f,
                configuredJumpHeight * (1f - Ease(descendingProgress)) +
                heightOffset);
        }

        private void FinishAirAttack()
        {
            bool usedCustomAirMovement =
                CurrentAttack != null &&
                CurrentAttack.useCustomAirMovement;

            CurrentAttack = null;
            CurrentAttackPhase = AttackPhase.None;
            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;
            airAttackLastOffset = Vector2.zero;

            if (!hasSuspendedJump)
            {
                FinishGroundedAttack();
                return;
            }

            // Custom air movement modifies the vertical offset of the
            // remaining jump arc. When following the jump trajectory, the
            // jump context has already advanced naturally during the attack.
            jumpHeightOffset = usedCustomAirMovement
                ? suspendedJumpHeightOffset +
                  (Height - suspendedJumpStartHeight)
                : suspendedJumpHeightOffset;

            hasSuspendedJump = false;

            if (Height <= 0.001f)
            {
                FinishGroundedAttack();
                return;
            }

            Elapsed = suspendedJumpElapsed;
            Duration = suspendedJumpDuration;
            jumpAirDirection = suspendedJumpAirDirection;
            CurrentJumpPhase = suspendedJumpPhase;
            PhaseElapsed = suspendedJumpPhaseElapsed;
            PhaseDuration = suspendedJumpPhaseDuration;
            Change(FighterState.Jump);
        }

        private void FinishGroundedAttack()
        {
            Elapsed = 0;
            Duration = 0;
            Height = 0f;
            Distance = 0f;
            StepDirection = 0;
            jumpHeightOffset = 0f;
            hasSuspendedJump = false;
            CurrentJumpPhase = JumpPhase.None;
            CurrentAttack = null;
            CurrentAttackPhase = AttackPhase.None;
            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;
            Change(FighterState.Idle);
        }

        // ================================================================
        // HURT
        // ================================================================

        public void EnterHurt(
            int duration)
        {
            float currentHeight =
                VisualHeight;

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
            hasSuspendedJump = false;
            jumpHeightOffset = 0f;
            CurrentAttack = null;

            CurrentAttackPhase =
                AttackPhase.None;

            Duration =
                Mathf.Max(
                    1,
                    duration);

            Elapsed = 0;

            StepDirection =
                direction >= 0
                    ? 1
                    : -1;

            Distance =
                Mathf.Max(
                    0,
                    distance);

            Height = 0;

            flyingHurtDistance =
                Mathf.Max(
                    0,
                    distance);

            flyingHurtMaxHeight =
                Mathf.Max(
                    0,
                    height);

            flyingHurtKnockdownTicks =
                Mathf.Max(
                    1,
                    knockdownTicks);

            Phase =
                BunnyPhase.Flight;

            PhaseElapsed = 0;

            PhaseDuration =
                Duration;

            flyingProgress = 0;

            Change(
                FighterState.FlyingHurt);
        }

        // ================================================================
        // TIMED STATE
        // ================================================================

        private void StartTimedState(
            FighterState state,
            int duration)
        {
            hasSuspendedJump = false;
            jumpHeightOffset = 0f;
            CurrentAttack = null;

            CurrentAttackPhase =
                AttackPhase.None;

            Elapsed = 0;

            Duration =
                Mathf.Max(
                    1,
                    duration);

            StepDirection = 0;

            Distance = 0;
            Height = 0;

            Phase =
                BunnyPhase.None;

            PhaseElapsed = 0;
            PhaseDuration =
                Duration;

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

            Phase =
                BunnyPhase.None;

            PhaseElapsed = 0;
            PhaseDuration =
                Duration;

            Change(
                FighterState.Knockdown);
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

            Duration =
                Mathf.Max(
                    1,
                    configuredResetTicks);

            StepDirection = 0;

            Distance = 0;
            Height = 0;

            Phase =
                BunnyPhase.None;

            PhaseElapsed = 0;
            PhaseDuration =
                Duration;

            Change(
                FighterState.Reset);
        }

        private void FinishReset()
        {
            Elapsed = 0;
            Duration = 0;

            StepDirection = 0;

            Distance = 0;
            Height = 0;
            jumpHeightOffset = 0f;
            hasSuspendedJump = false;

            Phase =
                BunnyPhase.None;

            CurrentJumpPhase = JumpPhase.None;

            PhaseElapsed = 0;
            PhaseDuration = 0;

            CurrentAttack = null;

            CurrentAttackPhase =
                AttackPhase.None;

            Change(
                FighterState.Idle);
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

            configuredJumpAnticipation = 0;
            configuredJumpAir = 0;
            configuredJumpDescending = 0;
            configuredJumpRecovery = 0;
            configuredJumpHeight = 0;
            configuredJumpAirControl = 0;
            jumpAirDirection = 0;
            jumpHeightOffset = 0f;
            hasSuspendedJump = false;
            suspendedJumpElapsed = 0;
            suspendedJumpDuration = 0;
            suspendedJumpAirDirection = 0;
            suspendedJumpPhase = JumpPhase.None;
            suspendedJumpPhaseElapsed = 0;
            suspendedJumpPhaseDuration = 0;
            suspendedJumpHeightOffset = 0f;
            suspendedJumpStartHeight = 0f;
            airAttackFacing = 1;
            airAttackLastOffset = Vector2.zero;

            CurrentJumpPhase = JumpPhase.None;

            configuredForwardTotal = 0;
            configuredForwardDistance = 0;
            configuredForwardHeight = 0;

            configuredBackwardTotal = 0;
            configuredBackwardDistance = 0;
            configuredBackwardHeight = 0;

            configuredLoopTotal = 0;
            configuredLoopDistance = 0;
            configuredLoopHeight = 0;

            configuredWalkSpeed = 0f;
            allowBackwardWalk = false;
            walkDirection = 0;

            flyingHurtDistance = 0;
            flyingHurtMaxHeight = 0;
            flyingHurtKnockdownTicks = 0;
            flyingProgress = 0;

            bunnyTiming =
                default;

            configuredForwardTiming =
                default;

            configuredBackwardTiming =
                default;

            configuredLoopTiming =
                default;

            autoGetUp = true;

            configuredResetTicks = 20;
        }

        // ================================================================
        // HELPERS
        // ================================================================

        public float VisualHeight
        {
            get
            {
                if (IsStepping ||
                    IsJumping ||
                    IsAirAttacking ||
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
            hasSuspendedJump = false;
            jumpHeightOffset = 0f;
            CurrentAttack = null;

            CurrentAttackPhase =
                AttackPhase.None;

            Elapsed = 0;

            Duration =
                Mathf.Max(
                    1,
                    duration);

            StepDirection =
                direction;

            Distance =
                Mathf.Max(
                    0,
                    distance);

            startHeight =
                Mathf.Max(
                    0,
                    height);

            Height = 0;

            bunnyTiming =
                timing;

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
            if (IsAirAttacking)
            {
                FinishAirAttack();
                return;
            }

            if (State ==
                FighterState.FlyingHurt)
            {
                EnterKnockdown();
                return;
            }

            if (State ==
                FighterState.BunnyForwardLoop)
            {
                Change(
                    FighterState.Idle);

                Elapsed = 0;
                Duration = 0;

                return;
            }

            Change(
                FighterState.Idle);

            Elapsed = 0;
            Duration = 0;

            Distance = 0;
            Height = 0;
            jumpHeightOffset = 0f;
            hasSuspendedJump = false;

            Phase =
                BunnyPhase.None;

            CurrentJumpPhase = JumpPhase.None;

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

            State =
                next;

            StateChanged?.Invoke(
                previous,
                next);
        }

        private float ArcHeight(
            float maxHeight,
            float t)
        {
            t =
                Mathf.Clamp01(t);

            return
                4f *
                maxHeight *
                t *
                (1f - t);
        }
    }
}