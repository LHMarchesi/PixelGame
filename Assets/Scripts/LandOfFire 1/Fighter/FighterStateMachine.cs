// Land of Fire · Máquina lógica común de un luchador.
// Controla estados, ticks, Bunny Step, Hurt, Flying Hurt,
// Knockdown, Reset y ataques.
//
// La máquina NO interpreta input crudo.
// Recibe comandos ya reconocidos desde FighterCommandRecognizer.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public enum FighterState
    {
        Idle,
        Guard,
        BlockRecovery,
        GuardBreak,
        OverheadCollision,
        OverheadImpact,
        OverheadRecovery,
        BodyCollision,
        BodyImpact,
        BodyRecovery,
        LegsCollision,
        LegsImpact,
        LegsRecovery,
        BunnyForward,
        BunnyBackward,
        BunnyForwardLoop,
        Hurt,
        Attack,
        AirAttack,
        FlyingHurt,
        ComboLimitKnockdown,
        Knockdown,
        Reset,
        Jump,
        Walk,
        Parry,
        ParryWhiff,
        ParryStun,
        ParryRecovery,
        AutoCombo,
        Rekka
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

        public AutoComboData CurrentAutoCombo { get; private set; }

        public AutoComboImpactData CurrentAutoComboAttack { get; private set; }

        public int CurrentAutoComboAttackIndex { get; private set; } = -1;

        public int CurrentAutoComboAttackElapsedTick { get; private set; }

        public bool IsAutoComboTransitioning { get; private set; }

        public PhaseAnimation CurrentAutoComboAnimation { get; private set; }

        public int CurrentAutoComboAnimationElapsedTick { get; private set; }

        public int CurrentAutoComboAnimationDurationTicks { get; private set; }

        public RekkaTreeData CurrentRekkaTree { get; private set; }

        public RekkaNodeData CurrentRekkaNode { get; private set; }

        public int CurrentRekkaNodeElapsedTick => IsRekka ? Elapsed : 0;

        public int CurrentRekkaNodeSerial { get; private set; }

        public int CurrentRekkaNodeIndex { get; private set; }

        public RekkaNodeOutcome CurrentRekkaNodeOutcome { get; private set; }

        public PhaseAnimation CurrentRekkaAnimation { get; private set; }

        public int CurrentRekkaAnimationElapsedTick { get; private set; }

        public int CurrentRekkaAnimationDurationTicks { get; private set; }

        public bool IsRekka =>
            State == FighterState.Rekka;

        public bool IsRekkaContinuationWindow =>
            IsRekka &&
            CurrentRekkaNode != null &&
            CurrentRekkaNode.attack != null &&
            CurrentRekkaNode.attack.IsRekkaContinuationWindowAtTick(Elapsed);

        public event Action RekkaNodeChanged;

        // Hold charge extends Anticipation without changing the shared tick rate.
        public int HoldChargeTicks { get; private set; }

        public int CombatAttackElapsedTick =>
            Mathf.Max(0, Elapsed - HoldChargeTicks);

        public bool IsHoldAttackCharged =>
            CurrentAttack != null &&
            CurrentAttack.allowHoldAttack &&
            HoldChargeTicks >= CurrentAttack.HoldMinChargeTicks;

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
        private int walkDirection;

        public FighterHurtType CurrentHurtType => currentHurtType;

        public FighterHurtPhase CurrentHurtPhase => currentHurtPhase;

        public int AccumulatedHurtHitstunTicks => hurtAccumulatedHitstunTicks;

        public bool IsHurt => IsHurtState(State);

        public bool IsHurtRecovery =>
            IsHurt && currentHurtPhase == FighterHurtPhase.Recovery;

        public bool IsHurtRecoveryParryWindowActive =>
            IsHurtRecovery &&
            hurtRecoveryParryWindowTicks > 0 &&
            Elapsed >= hurtRecoveryParryDelayTicks &&
            Elapsed < hurtRecoveryParryDelayTicks + hurtRecoveryParryWindowTicks;

        public bool IsFlyingHurt =>
            State == FighterState.FlyingHurt;

        public bool IsComboLimitKnockdown =>
            State == FighterState.ComboLimitKnockdown;

        public bool IsKnockdown =>
            State == FighterState.Knockdown;

        public bool IsResetting =>
            State == FighterState.Reset;

        public bool IsParrying =>
            State == FighterState.Parry;

        public bool IsParryWhiff =>
            State == FighterState.ParryWhiff;

        public bool IsParryStun =>
            State == FighterState.ParryStun;

        public bool IsParryRecovery =>
            State == FighterState.ParryRecovery;

        public bool IsParryState =>
            IsParrying || IsParryWhiff || IsParryStun || IsParryRecovery;

        public bool IsAttacking =>
            State == FighterState.Attack ||
            State == FighterState.AirAttack;

        public bool IsAutoCombo =>
            State == FighterState.AutoCombo;

        public AutoComboImpactData CurrentAutoComboCancelWindow =>
            IsAutoCombo && CurrentAutoCombo != null
                // Elapsed is the last fully simulated Auto Combo tick. A
                // cancel configured on an attack-local tick becomes
                // available only after that tick has been resolved.
                ? CurrentAutoCombo.FindCancelWindow(Elapsed)
                : null;

        public bool IsAutoComboCancelWindow =>
            CurrentAutoComboCancelWindow != null;

        public bool IsAirAttacking =>
            State == FighterState.AirAttack;

        public bool IsJumping =>
            State == FighterState.Jump;

        public bool IsTimed =>
            IsStepping ||
            State == FighterState.BlockRecovery ||
            State == FighterState.GuardBreak ||
            IsParryState ||
            IsJumping ||
            IsHurt ||
            IsFlyingHurt ||
            IsComboLimitKnockdown ||
            IsKnockdown ||
            IsResetting ||
            IsAttacking ||
            IsAutoCombo ||
            IsRekka;

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
        private bool holdChargeRequested;
        private bool holdChargeButtonHeld;

        private float attackStartHeight;
        private int groundAttackFacing = 1;
        private Vector2 groundAttackLastOffset;

        // Auto Combo displacement is accumulated independently for each
        // sequential attack, while the fighter's world position stays continuous.
        private int currentFacing = 1;
        private int autoComboMovementFacing = 1;
        private int autoComboMovementAttackIndex = -1;
        private float autoComboAttackStartHeight;
        private Vector2 autoComboAttackLastOffset;

        // Rekka nodes use the same local attack/animation data as Auto Combos,
        // but advance as a closed branching route.
        private RekkaBranchData pendingRekkaBranch;
        private readonly List<RekkaBranchData> availableRekkaBranches =
            new List<RekkaBranchData>(2);
        private int rekkaMovementFacing = 1;
        private int rekkaMovementNodeSerial = -1;
        private float rekkaNodeStartHeight;
        private Vector2 rekkaNodeLastOffset;
        private bool rekkaWindowObserved;

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
        private int configuredAirAttackInputStartTick = 12;
        private int configuredAirAttackInputEndTick = 24;
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

        // Combo Limit Knockdown is a separate, lighter fall. It deliberately
        // has its own movement values and state instead of reusing FlyingHurt.
        private float comboLimitKnockdownDistance;
        private float comboLimitKnockdownMaxHeight;
        private int comboLimitKnockdownGroundTicks;

        // Three-stage Hurt state. Attack hitstun is additional to the base
        // Impact duration and accumulates only while the fighter remains in Impact.
        private FighterHurtType currentHurtType = FighterHurtType.None;
        private FighterHurtPhase currentHurtPhase = FighterHurtPhase.None;
        private int hurtCollisionTicks;
        private int hurtBaseImpactTicks;
        private int hurtRecoveryTicks;
        private int hurtAccumulatedHitstunTicks;
        private int hurtMaxAccumulatedHitstunTicks;
        private int hurtRecoveryParryDelayTicks;
        private int hurtRecoveryParryWindowTicks;
        private int hurtSequenceElapsedTicks;
        private int hurtSequenceTotalTicks;

        // ================================================================
        // KNOCKDOWN
        // ================================================================

        private bool autoGetUp = true;

        // ================================================================
        // RESET
        // ================================================================

        private int configuredResetTicks = 20;

        // Parry attempt outcome is resolved by the shared combat resolver on
        // the exact input tick. The Parry state remains visible for its
        // animation before branching to ParryWhiff / ParryRecovery / Idle.
        private int configuredParryWhiffRecoveryTicks = 18;
        private bool parrySuccessResolved;
        private bool parrySuccessPerfect;
        private int parrySuccessRecoveryTicks;

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

            // Back is always a movement input now. Guard is evaluated
            // reactively by FighterCombat2D only on the impact tick.
            // Keep the parameter for compatibility with existing callers.
        }

        public void ConfigureJump(
            int anticipationTicks,
            int airTicks,
            int descendingTicks,
            int recoveryTicks,
            float height,
            float airControl,
            int airAttackInputStartTick,
            int airAttackInputEndTick)
        {
            configuredJumpAnticipation = Mathf.Max(1, anticipationTicks);
            configuredJumpAir = Mathf.Max(1, airTicks);
            configuredJumpDescending = Mathf.Max(1, descendingTicks);
            configuredJumpRecovery = Mathf.Max(1, recoveryTicks);

            configuredAirAttackInputStartTick =
                Mathf.Max(1, airAttackInputStartTick);

            configuredAirAttackInputEndTick =
                Mathf.Max(
                    configuredAirAttackInputStartTick,
                    airAttackInputEndTick);

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
            CurrentAutoCombo = null;
            ClearRekkaState();
            CurrentAttackPhase = AttackPhase.None;
            flightProgress = 0;

            Change(FighterState.Jump);
            return true;
        }

        public void ConfigureHurtRecoveryParryWindow(
            int delayTicks,
            int windowTicks)
        {
            hurtRecoveryParryDelayTicks = Mathf.Max(0, delayTicks);
            hurtRecoveryParryWindowTicks = Mathf.Max(0, windowTicks);
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
            currentFacing = newFacing >= 0 ? 1 : -1;
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

                // Holding Back means walking backwards by default.
                // Guard is no longer a persistent stance: the combat
                // resolver checks Back on the exact impact tick.
                if (heldDirection != 0)
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
            // REKKA
            // ============================================================

            if (IsRekka)
            {
                Phase = BunnyPhase.None;
                UpdateRekkaPresentation();
                return AdvanceRekkaMovement();
            }

            // ============================================================
            // AUTO COMBO
            // ============================================================

            if (IsAutoCombo)
            {
                Phase = BunnyPhase.None;
                UpdateAutoComboPresentation();
                return AdvanceAutoComboMovement();
            }

            // ============================================================
            // ATTACK
            // ============================================================

            if (IsAttacking)
            {
                // Hold charge starts only after the base Anticipation has
                // completed. Each additional held tick extends Anticipation
                // by one tick, so releasing advances into Smear on the next
                // simulation tick. The effective combat timeline subtracts
                // these extra ticks; Telegraph deliberately keeps absolute time.
                if (holdChargeRequested && holdChargeButtonHeld &&
                    CurrentAttack != null && CurrentAttack.allowHoldAttack &&
                    index >= attackAnticipation &&
                    HoldChargeTicks < CurrentAttack.HoldMaxChargeTicks)
                {
                    HoldChargeTicks++;
                    attackAnticipation++;
                    Duration++;
                }

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
                    : AdvanceGroundAttackMovement();
            }

            // ============================================================
            // BLOCK RECOVERY
            // ============================================================

            if (State == FighterState.BlockRecovery ||
                State == FighterState.GuardBreak)
            {
                Phase = BunnyPhase.None;
                PhaseElapsed = Elapsed;
                PhaseDuration = Duration;
                Height = 0f;
                return 0f;
            }

            // ============================================================
            // HURT
            // ============================================================

            if (IsHurt)
            {
                hurtSequenceElapsedTicks = Mathf.Min(
                    hurtSequenceTotalTicks,
                    hurtSequenceElapsedTicks + 1);
                Height = Mathf.Lerp(
                    hurtStartHeight,
                    0f,
                    hurtSequenceTotalTicks <= 1
                        ? 1f
                        : Mathf.Clamp01(
                            (float)hurtSequenceElapsedTicks / hurtSequenceTotalTicks));
                Phase = BunnyPhase.None;
                PhaseElapsed = Elapsed;
                PhaseDuration = Duration;
                return 0f;
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
            // COMBO LIMIT KNOCKDOWN (LIGHT FALL)
            // ============================================================

            if (IsComboLimitKnockdown)
            {
                Phase = BunnyPhase.Flight;
                PhaseDuration = Duration;

                float currentProgress = Duration <= 1
                    ? 1f
                    : Mathf.Clamp01((float)(Elapsed - 1) / (Duration - 1));

                float previousProgress = Duration <= 1
                    ? 0f
                    : Mathf.Clamp01((float)(Elapsed - 2) / (Duration - 1));

                Height = ArcHeight(
                    comboLimitKnockdownMaxHeight,
                    currentProgress);

                float previousDistance =
                    comboLimitKnockdownDistance * previousProgress;
                float currentDistance =
                    comboLimitKnockdownDistance * currentProgress;

                Distance = comboLimitKnockdownDistance;
                PhaseElapsed = Elapsed;

                return (currentDistance - previousDistance) * StepDirection;
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

        public bool TryStartAttack(AttackMoveData move)
        {
            return TryStartAttack(move, false);
        }

        public bool TryStartAttack(
            AttackMoveData move,
            bool requestHoldCharge)
        {
            if (move == null || IsAttacking)
                return false;

            if (IsHurt || IsFlyingHurt || IsComboLimitKnockdown ||
                IsKnockdown || IsResetting)
                return false;

            if (State == FighterState.Guard ||
                State == FighterState.GuardBreak)
                return false;

            if (IsStepping && !move.allowBunnyCancel)
                return false;

            if (State != FighterState.Idle &&
                State != FighterState.Walk &&
                !IsStepping)
            {
                return false;
            }

            return StartAttack(move, false, 1, requestHoldCharge);
        }

        public bool TryStartAutoCombo(AutoComboData combo)
        {
            if (combo == null || !combo.HasValidAttacks || IsTimed ||
                (State != FighterState.Idle && State != FighterState.Walk))
            {
                return false;
            }

            return StartAutoCombo(combo, 0f);
        }

        public bool TryCancelAttackToAutoCombo(AutoComboData combo)
        {
            if (combo == null || !IsAttacking || IsAirAttacking ||
                !IsAttackCancelWindow || CurrentAttack == null ||
                !CurrentAttack.allowAutoComboCancel)
            {
                return false;
            }

            return StartAutoCombo(
                combo,
                CurrentAttack.cancelStartupSkipPercent);
        }

        public bool TryStartRekka(RekkaTreeData tree, int facing)
        {
            if (tree == null || !tree.HasValidTree || IsTimed ||
                (State != FighterState.Idle && State != FighterState.Walk))
            {
                return false;
            }

            return StartRekka(tree, facing, 0f);
        }

        public bool TryCancelAttackToRekka(RekkaTreeData tree, int facing)
        {
            if (tree == null || !tree.HasValidTree ||
                !IsAttacking || IsAirAttacking ||
                !IsAttackCancelWindow || CurrentAttack == null ||
                !CurrentAttack.allowRekkaCancel)
            {
                return false;
            }

            return StartRekka(
                tree,
                facing,
                CurrentAttack.cancelStartupSkipPercent);
        }

        public bool TryContinueRekka(RekkaBranchData branch)
        {
            if (!IsRekka || branch == null || CurrentRekkaNode == null ||
                !IsRekkaContinuationWindow ||
                !CurrentRekkaNode.ContainsBranch(branch))
            {
                return false;
            }

            if (CurrentRekkaNodeOutcome == RekkaNodeOutcome.Unknown)
            {
                pendingRekkaBranch = branch;
                return true;
            }

            if (!branch.IsAvailableFor(CurrentRekkaNodeOutcome) ||
                (CurrentRekkaNodeOutcome == RekkaNodeOutcome.Hit &&
                 CurrentRekkaTree != null &&
                 CurrentRekkaTree.ShouldTriggerKnockdown(CurrentRekkaNodeIndex) &&
                 !CurrentRekkaNode.IsTerminal))
                return false;

            return StartRekkaNode(branch.targetNode);
        }

        public void GetAvailableRekkaBranches(List<RekkaBranchData> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            if (!IsRekka || !IsRekkaContinuationWindow ||
                CurrentRekkaNode == null)
                return;

            if (CurrentRekkaNodeOutcome == RekkaNodeOutcome.Hit &&
                CurrentRekkaTree != null &&
                CurrentRekkaTree.ShouldTriggerKnockdown(CurrentRekkaNodeIndex) &&
                !CurrentRekkaNode.IsTerminal)
                return;

            CurrentRekkaNode.GetBranchesForOutcome(
                CurrentRekkaNodeOutcome,
                destination);
        }

        public void ResolveRekkaNodeOutcome(
            int nodeSerial,
            RekkaNodeOutcome outcome)
        {
            if (!IsRekka || nodeSerial != CurrentRekkaNodeSerial ||
                CurrentRekkaNodeOutcome != RekkaNodeOutcome.Unknown ||
                outcome == RekkaNodeOutcome.Unknown)
                return;

            CurrentRekkaNodeOutcome = outcome;

            if (pendingRekkaBranch == null)
                return;

            RekkaBranchData pending = pendingRekkaBranch;
            pendingRekkaBranch = null;

            if (CurrentRekkaNode != null &&
                CurrentRekkaNode.ContainsBranch(pending) &&
                pending.IsAvailableFor(outcome) &&
                !(outcome == RekkaNodeOutcome.Hit &&
                  CurrentRekkaTree != null &&
                  CurrentRekkaTree.ShouldTriggerKnockdown(CurrentRekkaNodeIndex) &&
                  !CurrentRekkaNode.IsTerminal))
            {
                StartRekkaNode(pending.targetNode);
            }
        }

        public void FinalizeRekkaTick(bool hasPendingContact)
        {
            if (!IsRekka || CurrentRekkaNode == null ||
                CurrentRekkaNode.attack == null ||
                CurrentRekkaNodeOutcome != RekkaNodeOutcome.Unknown ||
                hasPendingContact)
                return;

            AutoComboImpactData attack = CurrentRekkaNode.attack;
            if (attack.attackMove == null)
            {
                ResolveRekkaNodeOutcome(
                    CurrentRekkaNodeSerial,
                    RekkaNodeOutcome.Miss);
                return;
            }

            if (!attack.attackMove.useHitbox)
            {
                if (IsRekkaContinuationWindow)
                    rekkaWindowObserved = true;
                else if (rekkaWindowObserved)
                {
                    ResolveRekkaNodeOutcome(
                        CurrentRekkaNodeSerial,
                        RekkaNodeOutcome.Miss);
                }

                return;
            }

            if (Elapsed >= attack.HitboxEndTick)
            {
                ResolveRekkaNodeOutcome(
                    CurrentRekkaNodeSerial,
                    RekkaNodeOutcome.Miss);
            }
        }

        public void EndRekkaRoute(int nodeSerial)
        {
            if (!IsRekka || nodeSerial != CurrentRekkaNodeSerial)
                return;

            FinishGroundedAttack();
        }

        public bool TryCancelAutoComboToAttack(
            AttackMoveData move,
            int facing,
            bool requestHoldCharge)
        {
            AutoComboImpactData cancel = CurrentAutoComboCancelWindow;
            if (move == null || cancel == null ||
                !cancel.AllowsCancelTo(AutoComboCancelTarget.SingleMove))
            {
                return false;
            }

            if (!StartAttack(move, false, facing, requestHoldCharge))
                return false;

            ApplyAttackStartupSkip(cancel.cancelStartupSkipPercent);
            return true;
        }

        public bool TryCancelAutoComboToAutoCombo(AutoComboData combo)
        {
            AutoComboImpactData cancel = CurrentAutoComboCancelWindow;
            if (combo == null || cancel == null ||
                !cancel.AllowsCancelTo(AutoComboCancelTarget.AutoCombo))
            {
                return false;
            }

            return StartAutoCombo(combo, cancel.cancelStartupSkipPercent);
        }

        public bool TryCancelAutoComboToRekka(
            RekkaTreeData tree,
            int facing)
        {
            AutoComboImpactData cancel = CurrentAutoComboCancelWindow;
            if (tree == null || !tree.HasValidTree || cancel == null ||
                !cancel.AllowsCancelTo(AutoComboCancelTarget.Rekka))
                return false;

            return StartRekka(tree, facing, cancel.cancelStartupSkipPercent);
        }

        public bool HasFutureAutoComboCancelWindow =>
            IsAutoCombo && CurrentAutoCombo != null &&
            CurrentAutoCombo.HasCancelWindowAfterTick(Elapsed + 1);

        private bool StartRekka(
            RekkaTreeData tree,
            int facing,
            float startupSkipPercent)
        {
            if (tree == null || !tree.HasValidTree)
                return false;

            CurrentAttack = null;
            CurrentAutoCombo = null;
            ResetAutoComboPresentation();
            holdChargeRequested = false;
            holdChargeButtonHeld = false;
            HoldChargeTicks = 0;
            hasSuspendedJump = false;
            jumpHeightOffset = 0f;
            CurrentJumpPhase = JumpPhase.None;
            CurrentRekkaTree = tree;
            CurrentRekkaNodeIndex = 0;
            rekkaMovementFacing = facing >= 0 ? 1 : -1;
            rekkaMovementNodeSerial = -1;
            rekkaNodeStartHeight = Height;
            rekkaNodeLastOffset = Vector2.zero;
            Phase = BunnyPhase.None;
            StepDirection = 0;
            Distance = 0f;

            if (!StartRekkaNode(tree.rootNode))
                return false;

            int skippedTicks = Mathf.RoundToInt(
                tree.StartupTicks *
                Mathf.Clamp(startupSkipPercent, 0f, 100f) / 100f);

            Elapsed = Mathf.Clamp(skippedTicks, 0, Mathf.Max(0, Duration - 1));
            UpdateRekkaPresentation();
            return true;
        }

        private bool StartRekkaNode(RekkaNodeData node)
        {
            if (node == null || !node.HasValidNode)
                return false;

            CurrentRekkaNode = node;
            CurrentRekkaNodeIndex++;
            CurrentRekkaNodeOutcome = RekkaNodeOutcome.Unknown;
            pendingRekkaBranch = null;
            CurrentAttack = null;
            CurrentAutoCombo = null;
            CurrentAttackPhase = AttackPhase.None;
            ResetAutoComboPresentation();
            CurrentRekkaAnimation = null;
            CurrentRekkaAnimationElapsedTick = 0;
            CurrentRekkaAnimationDurationTicks = 0;

            Elapsed = 0;
            Duration = Mathf.Max(1, node.DurationTicks);
            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = Duration;
            StepDirection = 0;
            Distance = 0f;

            CurrentRekkaNodeSerial++;
            rekkaMovementNodeSerial = CurrentRekkaNodeSerial;
            rekkaNodeStartHeight = Height;
            rekkaNodeLastOffset = Vector2.zero;
            rekkaWindowObserved = false;

            Change(FighterState.Rekka);
            RekkaNodeChanged?.Invoke();
            return true;
        }

        private void UpdateRekkaPresentation()
        {
            CurrentRekkaAnimation = null;
            CurrentRekkaAnimationElapsedTick = 0;
            CurrentRekkaAnimationDurationTicks = 0;
            CurrentAttackPhase = AttackPhase.None;

            if (CurrentRekkaNode == null || CurrentRekkaNode.attack == null)
                return;

            PhaseAnimation animation;
            int animationElapsed;
            int animationDuration;
            AttackPhase phase;

            if (!CurrentRekkaNode.attack.TryGetAnimationAtTick(
                Elapsed,
                out animation,
                out animationElapsed,
                out animationDuration,
                out phase))
                return;

            CurrentRekkaAnimation = animation;
            CurrentRekkaAnimationElapsedTick = animationElapsed;
            CurrentRekkaAnimationDurationTicks = animationDuration;
            CurrentAttackPhase = phase;
            PhaseElapsed = animationElapsed;
            PhaseDuration = animationDuration;
        }

        private float AdvanceRekkaMovement()
        {
            if (CurrentRekkaNode == null || CurrentRekkaNode.attack == null)
                return 0f;

            if (rekkaMovementNodeSerial != CurrentRekkaNodeSerial)
            {
                rekkaMovementNodeSerial = CurrentRekkaNodeSerial;
                rekkaNodeStartHeight = Height;
                rekkaNodeLastOffset = Vector2.zero;
            }

            Vector2 offset = CurrentRekkaNode.attack.MovementOffsetAtTick(Elapsed);
            float horizontalDelta =
                (offset.x - rekkaNodeLastOffset.x) * rekkaMovementFacing;

            rekkaNodeLastOffset = offset;
            Height = Mathf.Max(0f, rekkaNodeStartHeight + offset.y);
            return horizontalDelta;
        }

        private void ClearRekkaState()
        {
            CurrentRekkaTree = null;
            CurrentRekkaNode = null;
            CurrentRekkaNodeIndex = 0;
            CurrentRekkaNodeOutcome = RekkaNodeOutcome.Unknown;
            pendingRekkaBranch = null;
            CurrentRekkaAnimation = null;
            CurrentRekkaAnimationElapsedTick = 0;
            CurrentRekkaAnimationDurationTicks = 0;
            rekkaMovementNodeSerial = -1;
            rekkaNodeLastOffset = Vector2.zero;
            rekkaWindowObserved = false;
            availableRekkaBranches.Clear();
        }

        private bool StartAutoCombo(
            AutoComboData combo,
            float startupSkipPercent)
        {
            if (combo == null || !combo.HasValidAttacks)
                return false;

            CurrentAttack = null;
            ClearRekkaState();
            CurrentAutoCombo = combo;
            CurrentAttackPhase = AttackPhase.None;
            ResetAutoComboPresentation();
            holdChargeRequested = false;
            holdChargeButtonHeld = false;
            HoldChargeTicks = 0;
            hasSuspendedJump = false;
            jumpHeightOffset = 0f;
            CurrentJumpPhase = JumpPhase.None;
            autoComboMovementFacing = currentFacing;
            autoComboMovementAttackIndex = -1;
            autoComboAttackStartHeight = Height;
            autoComboAttackLastOffset = Vector2.zero;
            Phase = BunnyPhase.None;
            PhaseDuration = combo.DurationTicks;
            Duration = combo.DurationTicks;

            int skippedTicks = Mathf.RoundToInt(
                combo.StartupTicks * Mathf.Clamp(startupSkipPercent, 0f, 100f) / 100f);
            Elapsed = Mathf.Clamp(skippedTicks, 0, Duration - 1);
            PhaseElapsed = Elapsed;
            StepDirection = 0;
            Distance = 0f;

            Change(FighterState.AutoCombo);
            return true;
        }

        private void ApplyAttackStartupSkip(float startupSkipPercent)
        {
            if (CurrentAttack == null)
                return;

            int skippedTicks = Mathf.RoundToInt(
                attackAnticipation * Mathf.Clamp(startupSkipPercent, 0f, 100f) / 100f);
            Elapsed = Mathf.Clamp(skippedTicks, 0, Mathf.Max(0, Duration - 1));
        }

        public bool TryStartAirAttack(
            AttackMoveData move,
            int facing)
        {
            return TryStartAirAttack(move, facing, false);
        }

        public bool TryStartAirAttack(
            AttackMoveData move,
            int facing,
            bool requestHoldCharge)
        {
            if (move == null || !IsJumping || Height <= 0.001f)
                return false;

            if (!IsAirAttackInputWindowOpen())
                return false;

            return StartAttack(move, true, facing, requestHoldCharge);
        }

        private bool IsAirAttackInputWindowOpen()
        {
            if (!IsJumping)
                return false;

            // The input window uses one continuous tick timeline beginning
            // on the first tick of JumpPhase.Air. Descending continues that
            // timeline instead of restarting its counter.
            int airborneTick;

            switch (CurrentJumpPhase)
            {
                case JumpPhase.Air:
                    airborneTick = PhaseElapsed;
                    break;

                case JumpPhase.Descending:
                    airborneTick =
                        configuredJumpAir + PhaseElapsed;
                    break;

                default:
                    return false;
            }

            return
                airborneTick >= configuredAirAttackInputStartTick &&
                airborneTick <= configuredAirAttackInputEndTick;
        }

        public bool TryCancelAttack(AttackMoveData move)
        {
            return TryCancelAttack(move, 1, false);
        }

        public bool TryCancelAttack(
            AttackMoveData move,
            int facing)
        {
            return TryCancelAttack(move, facing, false);
        }

        public bool TryCancelAttack(
            AttackMoveData move,
            int facing,
            bool requestHoldCharge)
        {
            if (move == null || !IsAttacking || !IsAttackCancelWindow)
                return false;

            return StartAttack(move, IsAirAttacking, facing, requestHoldCharge);
        }

        public void UpdateHoldAttackInput(bool isButtonHeld)
        {
            holdChargeButtonHeld =
                isButtonHeld &&
                holdChargeRequested &&
                IsAttacking &&
                CurrentAttack != null &&
                CurrentAttack.allowHoldAttack;
        }

        private bool StartAttack(
            AttackMoveData move,
            bool aerial,
            int facing,
            bool requestHoldCharge)
        {
            // Preserve a custom Auto Combo height offset when a cancel enters
            // a regular grounded move. Otherwise clearing CurrentAutoCombo
            // would make VisualHeight resolve to zero and cause a visible snap.
            bool wasCancellingSequence = IsAutoCombo || IsAttacking;
            float inheritedVisualHeight = VisualHeight;

            CurrentAutoCombo = null;
            ResetAutoComboPresentation();
            ClearRekkaState();
            groundAttackFacing = facing >= 0 ? 1 : -1;
            groundAttackLastOffset = Vector2.zero;

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
                attackStartHeight = wasCancellingSequence
                    ? inheritedVisualHeight
                    : (IsStepping ? VisualHeight : 0f);
                hasSuspendedJump = false;
                jumpHeightOffset = 0f;
            }

            attackAnticipation = move.AnticipationTicks;
            holdChargeRequested = requestHoldCharge && move.allowHoldAttack;
            holdChargeButtonHeld = holdChargeRequested;
            HoldChargeTicks = 0;
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

        private float AdvanceGroundAttackMovement()
        {
            if (CurrentAttack == null || !CurrentAttack.useCustomGroundMovement)
            {
                groundAttackLastOffset = Vector2.zero;
                Height = attackStartHeight;
                return 0f;
            }

            Vector2 offset = CurrentAttack.GroundMovementOffset(
                CurrentAttackPhase,
                PhaseElapsed,
                PhaseDuration);

            float horizontalDelta =
                (offset.x - groundAttackLastOffset.x) * groundAttackFacing;

            groundAttackLastOffset = offset;
            Height = Mathf.Max(0f, attackStartHeight + offset.y);
            return horizontalDelta;
        }

        private float AdvanceAutoComboMovement()
        {
            AutoComboImpactData attack = CurrentAutoComboAttack;
            if (attack == null)
                return 0f;

            if (CurrentAutoComboAttackIndex != autoComboMovementAttackIndex)
            {
                autoComboMovementAttackIndex = CurrentAutoComboAttackIndex;
                autoComboAttackStartHeight = Height;
                autoComboAttackLastOffset = Vector2.zero;
            }

            // During an optional transition delay, the previous attack keeps
            // its final pose and displacement; no new movement is introduced.
            if (IsAutoComboTransitioning)
                return 0f;

            Vector2 offset = attack.MovementOffsetAtTick(
                CurrentAutoComboAttackElapsedTick);

            float horizontalDelta =
                (offset.x - autoComboAttackLastOffset.x) * autoComboMovementFacing;

            autoComboAttackLastOffset = offset;
            Height = Mathf.Max(0f, autoComboAttackStartHeight + offset.y);
            return horizontalDelta;
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
            CurrentAutoCombo = null;
            ClearRekkaState();
            CurrentAttackPhase = AttackPhase.None;
            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;
            holdChargeRequested = false;
            holdChargeButtonHeld = false;
            HoldChargeTicks = 0;
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

        /// <summary>
        /// Cancels the current attack immediately and returns to Idle.
        /// Used only when a shared-tick Clash has already been confirmed.
        /// </summary>
        public void CancelAttackToIdle()
        {
            if (!IsAttacking && !IsAutoCombo && !IsRekka)
                return;

            FinishGroundedAttack();
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
            CurrentAutoCombo = null;
            ClearRekkaState();
            CurrentAttackPhase = AttackPhase.None;
            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;
            holdChargeRequested = false;
            holdChargeButtonHeld = false;
            HoldChargeTicks = 0;
            groundAttackLastOffset = Vector2.zero;
            autoComboMovementAttackIndex = -1;
            autoComboAttackLastOffset = Vector2.zero;
            Change(FighterState.Idle);
        }

        // ================================================================
        // HURT
        // ================================================================

        public void EnterBlockRecovery(int duration)
        {
            if (duration <= 0)
                return;

            StartTimedState(
                FighterState.BlockRecovery,
                duration);
        }

        public void EnterGuardBreak(int duration)
        {
            StartTimedState(
                FighterState.GuardBreak,
                Mathf.Max(1, duration));
        }

        public void EnterParryAttempt(
            int attemptDurationTicks,
            int whiffRecoveryTicks)
        {
            configuredParryWhiffRecoveryTicks =
                Mathf.Max(1, whiffRecoveryTicks);

            parrySuccessResolved = false;
            parrySuccessPerfect = false;
            parrySuccessRecoveryTicks = 0;

            StartTimedState(
                FighterState.Parry,
                Mathf.Max(1, attemptDurationTicks));
        }

        /// <summary>
        /// Marks a successful Parry. The fighter remains in the Parry state
        /// for the configured Parry animation duration. When that animation
        /// ends, perfect Parry returns to Idle and imperfect Parry enters
        /// its configured recovery state.
        /// </summary>
        public void ResolveParrySuccess(bool perfect, int recoveryTicks)
        {
            if (State != FighterState.Parry)
                return;

            parrySuccessResolved = true;
            parrySuccessPerfect = perfect;
            parrySuccessRecoveryTicks = Mathf.Max(0, recoveryTicks);
            // Do not shorten Duration: the Parry animation must finish before
            // branching to perfect control or imperfect ParryRecovery.
        }

        public void EnterParryWhiff(int duration)
        {
            StartTimedState(FighterState.ParryWhiff, Mathf.Max(1, duration));
        }

        public void EnterParryStun(int duration)
        {
            StartTimedState(FighterState.ParryStun, Mathf.Max(1, duration));
        }

        public void EnterParryRecovery(int duration)
        {
            if (duration <= 0)
                return;

            StartTimedState(FighterState.ParryRecovery, duration);
        }

        public void EnterHurt(
            FighterHurtType hurtType,
            int collisionTicks,
            int impactTicks,
            int recoveryTicks,
            int attackHitstunTicks,
            int maxAccumulatedHitstunTicks)
        {
            float currentHeight = VisualHeight;

            currentHurtType = hurtType == FighterHurtType.None
                ? FighterHurtType.Body
                : hurtType;
            hurtCollisionTicks = Mathf.Max(1, collisionTicks);
            hurtBaseImpactTicks = Mathf.Max(1, impactTicks);
            hurtRecoveryTicks = Mathf.Max(1, recoveryTicks);
            hurtMaxAccumulatedHitstunTicks = Mathf.Max(0, maxAccumulatedHitstunTicks);
            hurtAccumulatedHitstunTicks = Mathf.Clamp(
                attackHitstunTicks,
                0,
                hurtMaxAccumulatedHitstunTicks);
            hurtSequenceElapsedTicks = 0;
            hurtSequenceTotalTicks = hurtCollisionTicks + hurtBaseImpactTicks +
                                     hurtRecoveryTicks + hurtAccumulatedHitstunTicks;
            hurtStartHeight = currentHeight;

            StartHurtPhase(FighterHurtPhase.Collision);
        }

        /// <summary>
        /// Adds the new move's hitstun to the current Impact. The state is not
        /// restarted; only the remaining Impact duration is extended, capped
        /// by the configured accumulated-hitstun limit.
        /// </summary>
        public int AddHurtHitstun(int additionalTicks)
        {
            if (!IsHurt || currentHurtPhase != FighterHurtPhase.Impact ||
                additionalTicks <= 0)
            {
                return 0;
            }

            int nextTotal = Mathf.Min(
                hurtMaxAccumulatedHitstunTicks,
                hurtAccumulatedHitstunTicks + additionalTicks);
            int added = Mathf.Max(0, nextTotal - hurtAccumulatedHitstunTicks);
            if (added <= 0)
                return 0;

            hurtAccumulatedHitstunTicks = nextTotal;
            hurtSequenceTotalTicks += added;
            Duration += added;
            PhaseDuration = Duration;
            return added;
        }

        private void StartHurtPhase(FighterHurtPhase phase)
        {
            currentHurtPhase = phase;
            hasSuspendedJump = false;
            jumpHeightOffset = 0f;
            CurrentJumpPhase = JumpPhase.None;
            Elapsed = 0;
            StepDirection = 0;
            Distance = 0f;
            Phase = BunnyPhase.None;
            PhaseElapsed = 0;
            PhaseDuration = 0;
            Height = 0f;
            CurrentAttack = null;
            CurrentAutoCombo = null;
            ClearRekkaState();
            CurrentAttackPhase = AttackPhase.None;
            HoldChargeTicks = 0;
            holdChargeRequested = false;
            holdChargeButtonHeld = false;

            switch (phase)
            {
                case FighterHurtPhase.Collision:
                    Duration = hurtCollisionTicks;
                    break;
                case FighterHurtPhase.Impact:
                    Duration = hurtBaseImpactTicks + hurtAccumulatedHitstunTicks;
                    break;
                case FighterHurtPhase.Recovery:
                    Duration = hurtRecoveryTicks;
                    break;
                default:
                    Duration = 0;
                    break;
            }

            Duration = Mathf.Max(1, Duration);
            PhaseDuration = Duration;
            Change(GetHurtState(currentHurtType, currentHurtPhase));
        }

        private void AdvanceHurtPhase()
        {
            switch (currentHurtPhase)
            {
                case FighterHurtPhase.Collision:
                    StartHurtPhase(FighterHurtPhase.Impact);
                    break;

                case FighterHurtPhase.Impact:
                    StartHurtPhase(FighterHurtPhase.Recovery);
                    break;

                case FighterHurtPhase.Recovery:
                    currentHurtType = FighterHurtType.None;
                    currentHurtPhase = FighterHurtPhase.None;
                    hurtAccumulatedHitstunTicks = 0;
                    hurtSequenceElapsedTicks = 0;
                    hurtSequenceTotalTicks = 0;
                    Change(FighterState.Idle);
                    Elapsed = 0;
                    Duration = 0;
                    Phase = BunnyPhase.None;
                    PhaseElapsed = 0;
                    PhaseDuration = 0;
                    Height = 0f;
                    break;

                default:
                    currentHurtType = FighterHurtType.None;
                    currentHurtPhase = FighterHurtPhase.None;
                    Change(FighterState.Idle);
                    Elapsed = 0;
                    Duration = 0;
                    break;
            }
        }

        private void ClearHurtContext()
        {
            currentHurtType = FighterHurtType.None;
            currentHurtPhase = FighterHurtPhase.None;
            hurtAccumulatedHitstunTicks = 0;
            hurtSequenceElapsedTicks = 0;
            hurtSequenceTotalTicks = 0;
            hurtCollisionTicks = 0;
            hurtBaseImpactTicks = 0;
            hurtRecoveryTicks = 0;
            hurtMaxAccumulatedHitstunTicks = 0;
        }

        private static bool IsHurtState(FighterState state)
        {
            return state == FighterState.Hurt ||
                   state == FighterState.OverheadCollision ||
                   state == FighterState.OverheadImpact ||
                   state == FighterState.OverheadRecovery ||
                   state == FighterState.BodyCollision ||
                   state == FighterState.BodyImpact ||
                   state == FighterState.BodyRecovery ||
                   state == FighterState.LegsCollision ||
                   state == FighterState.LegsImpact ||
                   state == FighterState.LegsRecovery;
        }

        private static FighterState GetHurtState(
            FighterHurtType type,
            FighterHurtPhase phase)
        {
            switch (type)
            {
                case FighterHurtType.Overhead:
                    switch (phase)
                    {
                        case FighterHurtPhase.Collision: return FighterState.OverheadCollision;
                        case FighterHurtPhase.Impact: return FighterState.OverheadImpact;
                        case FighterHurtPhase.Recovery: return FighterState.OverheadRecovery;
                    }
                    break;

                case FighterHurtType.Legs:
                    switch (phase)
                    {
                        case FighterHurtPhase.Collision: return FighterState.LegsCollision;
                        case FighterHurtPhase.Impact: return FighterState.LegsImpact;
                        case FighterHurtPhase.Recovery: return FighterState.LegsRecovery;
                    }
                    break;

                default:
                    switch (phase)
                    {
                        case FighterHurtPhase.Collision: return FighterState.BodyCollision;
                        case FighterHurtPhase.Impact: return FighterState.BodyImpact;
                        case FighterHurtPhase.Recovery: return FighterState.BodyRecovery;
                    }
                    break;
            }

            return FighterState.Hurt;
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
            ClearHurtContext();
            hasSuspendedJump = false;
            jumpHeightOffset = 0f;
            CurrentAttack = null;
            CurrentAutoCombo = null;
            ClearRekkaState();

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

        /// <summary>
        /// Starts the dedicated light fall used when the global combo-hit
        /// limit is reached. Its timeline and movement are independent from
        /// FlyingHurt, then it enters the standard grounded Knockdown state.
        /// </summary>
        public void EnterComboLimitKnockdown(
            int fallTicks,
            float horizontalDistance,
            float maxHeight,
            int groundedKnockdownTicks,
            int direction)
        {
            ClearHurtContext();
            hasSuspendedJump = false;
            jumpHeightOffset = 0f;
            CurrentJumpPhase = JumpPhase.None;
            CurrentAttack = null;
            CurrentAutoCombo = null;
            ClearRekkaState();
            CurrentAttackPhase = AttackPhase.None;
            holdChargeRequested = false;
            holdChargeButtonHeld = false;
            HoldChargeTicks = 0;

            Duration = Mathf.Max(1, fallTicks);
            Elapsed = 0;
            StepDirection = direction >= 0 ? 1 : -1;
            comboLimitKnockdownDistance = Mathf.Max(0f, horizontalDistance);
            comboLimitKnockdownMaxHeight = Mathf.Max(0f, maxHeight);
            comboLimitKnockdownGroundTicks = Mathf.Max(1, groundedKnockdownTicks);
            Distance = comboLimitKnockdownDistance;
            Height = 0f;
            Phase = BunnyPhase.Flight;
            PhaseElapsed = 0;
            PhaseDuration = Duration;
            Change(FighterState.ComboLimitKnockdown);
        }

        // ================================================================
        // TIMED STATE
        // ================================================================

        private void StartTimedState(
            FighterState state,
            int duration)
        {
            if (!IsHurtState(state))
                ClearHurtContext();

            holdChargeRequested = false;
            holdChargeButtonHeld = false;
            HoldChargeTicks = 0;
            hasSuspendedJump = false;
            jumpHeightOffset = 0f;
            CurrentAttack = null;
            CurrentAutoCombo = null;
            ClearRekkaState();

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

        private void EnterKnockdown(int durationOverride = -1)
        {
            ClearHurtContext();
            CurrentAttack = null;
            CurrentAutoCombo = null;
            ClearRekkaState();

            CurrentAttackPhase =
                AttackPhase.None;

            Elapsed = 0;

            Duration =
                Mathf.Max(
                    1,
                    durationOverride > 0
                        ? durationOverride
                        : flyingHurtKnockdownTicks);

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
            ClearHurtContext();
            CurrentAttack = null;
            CurrentAutoCombo = null;
            ClearRekkaState();

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
            ClearHurtContext();
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
            ClearHurtContext();
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
            CurrentAutoCombo = null;
            ClearRekkaState();

            attackAnticipation = 0;
            attackSmear = 0;
            attackPose = 0;
            attackRecovery = 0;
            holdChargeRequested = false;
            holdChargeButtonHeld = false;
            HoldChargeTicks = 0;

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
            walkDirection = 0;

            flyingHurtDistance = 0;
            flyingHurtMaxHeight = 0;
            flyingHurtKnockdownTicks = 0;
            flyingProgress = 0;
            comboLimitKnockdownDistance = 0f;
            comboLimitKnockdownMaxHeight = 0f;
            comboLimitKnockdownGroundTicks = 0;

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
            configuredParryWhiffRecoveryTicks = 18;
            parrySuccessResolved = false;
            parrySuccessPerfect = false;
            parrySuccessRecoveryTicks = 0;
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
                    IsAutoCombo ||
                    IsRekka ||
                    IsHurt ||
                    IsFlyingHurt ||
                    IsComboLimitKnockdown)
                {
                    return Height;
                }

                if (IsAttacking)
                    return CurrentAttack != null && CurrentAttack.useCustomGroundMovement
                        ? Height
                        : attackStartHeight;

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
            CurrentAutoCombo = null;
            ClearRekkaState();

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

        private void ResetAutoComboPresentation()
        {
            CurrentAutoComboAttack = null;
            CurrentAutoComboAttackIndex = -1;
            CurrentAutoComboAttackElapsedTick = 0;
            IsAutoComboTransitioning = false;
            CurrentAutoComboAnimation = null;
            CurrentAutoComboAnimationElapsedTick = 0;
            CurrentAutoComboAnimationDurationTicks = 0;
        }

        private void UpdateAutoComboPresentation()
        {
            ResetAutoComboPresentation();
            CurrentAttackPhase = AttackPhase.None;

            if (CurrentAutoCombo == null)
                return;

            AutoComboImpactData attack;
            int attackIndex;
            int attackElapsedTick;
            bool isTransition;
            if (!CurrentAutoCombo.TryGetAttackAtTick(
                Elapsed,
                out attack,
                out attackIndex,
                out attackElapsedTick,
                out isTransition) || attack == null)
            {
                PhaseElapsed = Elapsed;
                PhaseDuration = Duration;
                return;
            }

            CurrentAutoComboAttack = attack;
            CurrentAutoComboAttackIndex = attackIndex;
            CurrentAutoComboAttackElapsedTick = attackElapsedTick;
            IsAutoComboTransitioning = isTransition;

            PhaseAnimation animation;
            int animationElapsed;
            int animationDuration;
            AttackPhase phase;
            if (!attack.TryGetAnimationAtTick(
                attackElapsedTick,
                out animation,
                out animationElapsed,
                out animationDuration,
                out phase))
            {
                PhaseElapsed = attackElapsedTick;
                PhaseDuration = attack.AnimationDurationTicks;
                return;
            }

            CurrentAutoComboAnimation = animation;
            CurrentAutoComboAnimationElapsedTick = animationElapsed;
            CurrentAutoComboAnimationDurationTicks = animationDuration;
            CurrentAttackPhase = phase;
            PhaseElapsed = animationElapsed;
            PhaseDuration = animationDuration;
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
            if (IsHurt)
            {
                AdvanceHurtPhase();
                return;
            }

            if (IsAutoCombo)
            {
                CurrentAutoCombo = null;
                ResetAutoComboPresentation();
            }

            if (IsRekka)
            {
                FinishGroundedAttack();
                return;
            }

            if (State == FighterState.Parry)
            {
                bool succeeded = parrySuccessResolved;
                bool perfect = parrySuccessPerfect;
                int recoveryTicks = parrySuccessRecoveryTicks;
                int whiffTicks = configuredParryWhiffRecoveryTicks;

                parrySuccessResolved = false;
                parrySuccessPerfect = false;
                parrySuccessRecoveryTicks = 0;

                if (succeeded)
                {
                    if (!perfect && recoveryTicks > 0)
                    {
                        StartTimedState(
                            FighterState.ParryRecovery,
                            recoveryTicks);
                    }
                    else
                    {
                        // Perfect Parry grants control on this next tick.
                        Change(FighterState.Idle);
                        Elapsed = 0;
                        Duration = 0;
                        Distance = 0f;
                        Height = 0f;
                        Phase = BunnyPhase.None;
                        PhaseElapsed = 0;
                        PhaseDuration = 0;
                        CurrentAttack = null;
                        CurrentAttackPhase = AttackPhase.None;
                    }

                    return;
                }

                // A Parry with no valid confirmation finishes its own pose
                // first, then enters the vulnerable ParryWhiff state. If a
                // real hit arrived on the attempt tick, normal hit resolution
                // interrupts Parry directly into Hurt instead.
                StartTimedState(
                    FighterState.ParryWhiff,
                    whiffTicks);
                return;
            }

            if (IsAirAttacking)
            {
                FinishAirAttack();
                return;
            }

            if (State == FighterState.FlyingHurt)
            {
                EnterKnockdown();
                return;
            }

            if (State == FighterState.ComboLimitKnockdown)
            {
                EnterKnockdown(comboLimitKnockdownGroundTicks);
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