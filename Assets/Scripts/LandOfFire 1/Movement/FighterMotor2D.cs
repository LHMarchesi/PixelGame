// Land of Fire · Física y coordinador actual del tick de un FighterRoot.
// Ejecuta la máquina lógica a 60 Hz, aplica movimiento y llama al módulo de combate.

using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class FighterMotor2D : MonoBehaviour
    {
        public FighterMovementProfile profile;
        public FighterCommandSource commands;
        public FighterPresentation presentation;

        [Tooltip(
            "Opcional: al añadir FighterCombat2D habilita los golpes.")]
        public FighterCombat2D combat;

        [Tooltip(
            "Opcional. Sin rival conserva la orientación configurada.")]
        public Transform facingTarget;

        public bool startsFacingRight = true;
        public bool frozen;

        [Header("Knockdown")]

        [Tooltip(
            "Si está activo, después del Knockdown el Fighter realiza " +
            "automáticamente el Reset/GetUp.")]
        public bool autoGetUp = true;

        [Header("Debug")]

        [Tooltip(
            "Duración usada únicamente por la prueba manual " +
            "de recibir un hit.")]
        [SerializeField, Min(1)]
        private int debugHurtTicks = 18;

        [SerializeField]
        private FighterState currentState;

        [SerializeField]
        private BunnyPhase currentPhase;

        [SerializeField]
        private JumpPhase currentJumpPhase;

        [SerializeField]
        private AttackPhase currentAttackPhase;

        [SerializeField]
        private int facing = 1;

        public FighterStateMachine Machine { get; } =
            new FighterStateMachine();

        // Back is read as a defensive intent only when a blockable
        // impact is actually resolved. It does not put the fighter into
        // Guard while there is no incoming hit.
        public bool IsHoldingBackAgainst(FighterMotor2D attacker)
        {
            if (!ready || attacker == null ||
                !attacker.isActiveAndEnabled ||
                commands == null || !commands.isActiveAndEnabled)
            {
                return false;
            }

            if (Machine.State != FighterState.Idle &&
                Machine.State != FighterState.Walk &&
                Machine.State != FighterState.Guard)
            {
                return false;
            }

            float toAttacker = attacker.BodyPosition.x - BodyPosition.x;
            if (Mathf.Abs(toAttacker) > .001f)
            {
                int directionToAttacker = toAttacker > 0f ? 1 : -1;
                if (directionToAttacker != facing)
                    return false;
            }

            return commands.Direction == -facing;
        }

        public bool CanReceiveCombatHit =>
            ready &&
            isActiveAndEnabled &&
            !Machine.IsFlyingHurt &&
            !Machine.IsComboLimitKnockdown &&
            !Machine.IsKnockdown &&
            !Machine.IsResetting;

        public int Facing =>
            facing;

        public Vector2 BodyPosition =>
            body != null
                ? body.position
                : (Vector2)transform.position;

        public FighterMovementProfile RuntimeProfile
        {
            get;
            private set;
        }

        private Rigidbody2D body;
        private PhysicsMaterial2D material;

        private long tick;

        private int poseTick;
        private int hitstopRemaining;

        // Motion accumulated by the shared 60 Hz combat coordinator during
        // the current Unity FixedUpdate frame.
        private float coordinatedFrameDelta;
        private float coordinatedLastTickDelta;
        private bool pausedThisStep;
        private bool resumedThisStep;
        private bool coordinatedFrozen;
        private bool advancedThisTick;

        internal bool CanCollectAttackThisTick =>
            ready && !frozen && advancedThisTick;

        private bool ready;

        // ---------------------------------------------------------------
        // Attack buffer
        // ---------------------------------------------------------------

        private bool hasBufferedAttack;

        private FighterCommand bufferedAttack;

        // A Parry is only a pending one-tick intent until the shared
        // resolver confirms a matching incoming strike on this exact tick.
        private bool hasPendingParry;
        private ParryInputType pendingParryInput;

        // Tracks which standard attack button must remain held to charge
        // the current Hold Attack. Moveset-defined command sequences do not
        // opt into button charging.
        private bool hasHoldAttackInput;
        private AttackCommand holdAttackInput;

        // ---------------------------------------------------------------
        // Pushback
        // ---------------------------------------------------------------

        private float pendingPushback;

        private const RigidbodyConstraints2D GroundConstraints =
            RigidbodyConstraints2D.FreezePositionY |
            RigidbodyConstraints2D.FreezeRotation;

        // ================================================================
        // UNITY
        // ================================================================

        private void Awake()
        {
            body =
                GetComponent<Rigidbody2D>();

            if (profile == null)
            {
                Debug.LogError(
                    "Asignar FighterMovementProfile al FighterMotor2D.",
                    this);

                body.simulated = false;
                enabled = false;
                return;
            }

            RuntimeProfile =
                Instantiate(profile);

            RuntimeProfile.name =
                profile.name +
                " (Play: " +
                name +
                ")";

            RuntimeProfile.hideFlags =
                HideFlags.DontSave;

            if (commands == null)
            {
                commands =
                    GetComponent<FighterCommandSource>();
            }

            if (presentation == null)
            {
                presentation =
                    GetComponent<FighterPresentation>();
            }

            if (combat == null)
            {
                combat =
                    GetComponent<FighterCombat2D>();
            }

            facing =
                startsFacingRight
                    ? 1
                    : -1;

            body.bodyType =
                RigidbodyType2D.Dynamic;

            body.simulated = true;
            body.gravityScale = 0;

            body.constraints =
                GroundConstraints;

            body.collisionDetectionMode =
                CollisionDetectionMode2D.Continuous;

            body.interpolation =
                RigidbodyInterpolation2D.Interpolate;

            body.mass =
                Mathf.Max(
                    .01f,
                    profile.mass);

#if UNITY_6000_0_OR_NEWER
            body.linearDamping = 0;
#else
            body.drag = 0;
#endif

            material =
                new PhysicsMaterial2D(
                    "Fighter contact (runtime)")
                {
                    friction = 0,
                    bounciness = 0
                };

            BoxCollider2D box =
                GetComponent<BoxCollider2D>();

            box.isTrigger = false;

            box.size =
                new Vector2(
                    Mathf.Max(
                        .01f,
                        profile.bodySize.x),
                    Mathf.Max(
                        .01f,
                        profile.bodySize.y));

            box.offset =
                profile.bodyOffset;

            box.sharedMaterial =
                material;

            ready = true;
        }

        private void OnEnable()
        {
            if (!ready)
                return;

            Machine.Reset();

            ConfigureMachine();

            tick = 0;

            poseTick = 0;
            hitstopRemaining = 0;

            pendingPushback = 0;
            coordinatedFrameDelta = 0f;
            coordinatedLastTickDelta = 0f;
            pausedThisStep = false;
            resumedThisStep = false;

            ClearAttackBuffer();
            hasPendingParry = false;
            pendingParryInput = default;
            ClearHoldAttackInput();

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearCommands();

            SetVelocity(
                Vector2.zero);

            FighterCombatResolver.Register(this);
        }

        // FighterCombatResolver owns the fixed-tick loop. Keeping fighter
        // simulation out of individual FixedUpdate methods is what lets all
        // combatants collect their contacts before any result is applied.
        internal void BeginCoordinatedFrame()
        {
            coordinatedFrameDelta = 0f;
            coordinatedLastTickDelta = 0f;
            pausedThisStep = false;
            resumedThisStep = false;
            coordinatedFrozen = frozen;

            if (!ready || !frozen)
                return;

            body.constraints = RigidbodyConstraints2D.FreezeAll;
            SetVelocity(Vector2.zero);
            pendingPushback = 0f;
            ClearAttackBuffer();

            if (commands != null)
                commands.ClearCommands();
        }

        /// <summary>Advances exactly one shared 60 Hz simulation tick.</summary>
        internal void AdvanceCoordinatedTick()
        {
            coordinatedLastTickDelta = 0f;
            advancedThisTick = false;
            if (!ready || frozen)
                return;

            if (hitstopRemaining > 0)
            {
                hitstopRemaining--;
                pausedThisStep = true;
                tick++;
                return;
            }

            resumedThisStep = true;
            float delta = 0f;

            if (Mathf.Abs(pendingPushback) > 0.0001f)
            {
                delta += pendingPushback;
                pendingPushback = 0f;
            }

            if (!Machine.IsTimed && facingTarget != null)
            {
                float difference = facingTarget.position.x - body.position.x;
                if (Mathf.Abs(difference) > .001f)
                    facing = difference > 0 ? 1 : -1;
            }

            bool hasCommands = commands != null && commands.isActiveAndEnabled;
            ConfigureMachine();

            FighterState before = Machine.State;
            Machine.BeginTick(
                facing,
                hasCommands ? commands.Direction : 0);

            if (hasCommands)
            {
                commands.ProcessInput(facing, tick);
                ProcessRecognizedCommands();
                TryExecuteBufferedAttack();
            }
            else if (commands != null)
            {
                commands.ClearCommands();
                ClearAttackBuffer();
            }

            bool holdButtonHeld = hasHoldAttackInput &&
                hasCommands &&
                commands.IsAttackHeld(holdAttackInput);
            Machine.UpdateHoldAttackInput(holdButtonHeld);

            poseTick = before == Machine.State ? poseTick + 1 : 0;
            delta += Machine.Advance();
            if (!Machine.IsAttacking)
                ClearHoldAttackInput();

            // Keep the Parry intent alive only while the Parry state is
            // active. If its animation expires, the state machine has
            // already transitioned to ParryWhiff and the intent must not
            // leak into later states.
            if (Machine.State != FighterState.Parry && hasPendingParry)
                ClearPendingParry();

            advancedThisTick = true;
            coordinatedLastTickDelta = delta;
            coordinatedFrameDelta += delta;
            tick++;
        }

        internal void EndCoordinatedFrame(float fixedDeltaTime)
        {
            if (!ready)
                return;

            bool stopped = coordinatedFrozen || hitstopRemaining > 0 ||
                           (pausedThisStep && !resumedThisStep);

            body.constraints = stopped
                ? RigidbodyConstraints2D.FreezeAll
                : GroundConstraints;

            SetVelocity(stopped
                ? Vector2.zero
                : new Vector2(coordinatedFrameDelta / Mathf.Max(.000001f, fixedDeltaTime), 0f));

            currentState = Machine.State;
            currentPhase = Machine.Phase;
            currentJumpPhase = Machine.CurrentJumpPhase;
            currentAttackPhase = Machine.CurrentAttackPhase;
        }

        internal void CancelAttackForClash()
        {
            if (!ready || (!Machine.IsAttacking && !Machine.IsAutoCombo))
                return;

            coordinatedFrameDelta -= coordinatedLastTickDelta;
            coordinatedLastTickDelta = 0f;
            Machine.CancelAttackToIdle();
            ClearHoldAttackInput();

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearCommands();

            ClearAttackBuffer();
            pendingPushback = 0f;
            poseTick = 0;
            SetVelocity(Vector2.zero);

            currentState = Machine.State;
            currentPhase = Machine.Phase;
            currentJumpPhase = Machine.CurrentJumpPhase;
            currentAttackPhase = Machine.CurrentAttackPhase;

            if (presentation != null)
                presentation.Render(Machine, RuntimeProfile, facing, 0);
        }

        private void LateUpdate()
        {
            if (!ready ||
                presentation == null)
            {
                return;
            }

            presentation.Render(
                Machine,
                RuntimeProfile,
                facing,
                poseTick);
        }

        // ================================================================
        // MACHINE CONFIGURATION
        // ================================================================

        private void ConfigureMachine()
        {
            Machine.ConfigureWalk(
                RuntimeProfile.walkSpeed,
                RuntimeProfile.allowBackwardWalk);

            if (commands != null)
                commands.ConfigureMovement(RuntimeProfile);

            Machine.ConfigureJump(
                RuntimeProfile.jumpAnticipationTicks,
                RuntimeProfile.jumpAirTicks,
                RuntimeProfile.jumpDescendingTicks,
                RuntimeProfile.jumpRecoveryTicks,
                RuntimeProfile.jumpHeight,
                RuntimeProfile.jumpAirControl,
                RuntimeProfile.airAttackInputStartTick,
                RuntimeProfile.airAttackInputEndTick);

            Machine.ConfigureBunny(
                RuntimeProfile.forwardTiming.Total,
                RuntimeProfile.forwardDistance,
                RuntimeProfile.forwardHeight,
                RuntimeProfile.forwardTiming,

                RuntimeProfile.backwardTiming.Total,
                RuntimeProfile.backwardDistance,
                RuntimeProfile.backwardHeight,
                RuntimeProfile.backwardTiming,

                RuntimeProfile.forwardLoopTiming.Total,
                RuntimeProfile.forwardLoopDistance,
                RuntimeProfile.forwardLoopHeight,
                RuntimeProfile.forwardLoopTiming);

            Machine.ConfigureKnockdown(
                autoGetUp);

            Machine.ConfigureReset(
                RuntimeProfile.resetTicks);
        }

        // ================================================================
        // COMMAND PROCESSING
        // ================================================================

        private void ProcessRecognizedCommands()
        {
            if (commands == null)
                return;

            while (
                commands.TryReadCommand(
                    out FighterCommand command))
            {
                switch (command.Type)
                {
                    case FighterCommandType.Jump:

                        if (!hasPendingParry)
                            ExecuteJump();

                        break;

                    case FighterCommandType.Attack:

                        if (!hasPendingParry)
                            HandleAttackInput(command);

                        break;

                    case FighterCommandType.AutoCombo:

                        if (!hasPendingParry)
                            HandleAutoComboInput(command);

                        break;

                    case FighterCommandType.Parry:

                        HandleParryInput(command.ParryInput);

                        break;

                    case FighterCommandType.BunnyForward:

                        if (!hasPendingParry)
                            ExecuteBunnyForward();

                        break;

                    case FighterCommandType.BunnyBackward:

                        if (!hasPendingParry)
                            ExecuteBunnyBackward();

                        break;

                    case FighterCommandType.BunnyForwardLoop:

                        if (!hasPendingParry)
                            ExecuteBunnyForwardLoop();

                        break;
                }
            }
        }

        private void HandleParryInput(ParryInputType inputType)
        {
            if (hasPendingParry || !ready || !isActiveAndEnabled || combat == null)
                return;

            // Parry cannot interrupt an attack, a Bunny, a Jump, Hurt,
            // or any other timed action. It is available only from a
            // grounded neutral state (Idle/Walk/Guard).
            if (Machine.IsAttacking || Machine.IsTimed ||
                (Machine.State != FighterState.Idle &&
                 Machine.State != FighterState.Walk &&
                 Machine.State != FighterState.Guard))
            {
                return;
            }

            hasPendingParry = true;
            pendingParryInput = inputType;

            // Pressing P immediately enters a dedicated Parry state so its
            // Animator clip is rendered. The shared resolver still validates
            // the attempt only against contacts collected on this exact tick.
            Machine.EnterParryAttempt(
                combat.parryAttemptTicks,
                combat.parryWhiffRecoveryTicks);

            ClearAttackBuffer();
            poseTick = 0;
        }

        internal bool TryGetActiveParry(out ParryInputType inputType)
        {
            // Pressing Parry starts a short active state. Do not consume the
            // intent on its first tick: the incoming hitbox may overlap a few
            // ticks later while the Parry animation is still active.
            if (!hasPendingParry || Machine.State != FighterState.Parry)
            {
                inputType = default;
                return false;
            }

            inputType = pendingParryInput;
            return true;
        }

        private void ClearPendingParry()
        {
            hasPendingParry = false;
            pendingParryInput = default;
        }

        public void ReceiveParryWhiff(int recoveryTicks)
        {
            if (!ready || !isActiveAndEnabled || Machine.IsAttacking)
                return;

            Machine.EnterParryWhiff(recoveryTicks);
            ClearAfterParryTransition();
        }

        public void ReceiveParrySuccess(int recoveryTicks, bool perfect)
        {
            if (!ready || !isActiveAndEnabled)
                return;

            // Keep the Parry state active for its configured animation.
            // Once the animation ends, perfect returns to Idle and imperfect
            // transitions into ParryRecovery.
            Machine.ResolveParrySuccess(perfect, recoveryTicks);
            ClearAfterParryTransition();
        }

        private void ClearAfterParryTransition()
        {
            ClearPendingParry();
            ClearHoldAttackInput();

            if (commands != null)
                commands.ClearCommands();

            ClearAttackBuffer();
            pendingPushback = 0f;
            poseTick = 0;
            SetVelocity(Vector2.zero);

            currentState = Machine.State;
            currentPhase = Machine.Phase;
            currentJumpPhase = Machine.CurrentJumpPhase;
            currentAttackPhase = Machine.CurrentAttackPhase;
        }

        internal void CancelAttackForParry(int parryStunTicks)
        {
            if (!ready || !Machine.IsAttacking)
                return;

            coordinatedFrameDelta -= coordinatedLastTickDelta;
            coordinatedLastTickDelta = 0f;
            Machine.EnterParryStun(parryStunTicks);
            ClearHoldAttackInput();

            if (combat != null)
                combat.CancelAttack();

            hasPendingParry = false;
            pendingParryInput = default;
            if (commands != null)
                commands.ClearCommands();

            ClearAttackBuffer();
            pendingPushback = 0f;
            poseTick = 0;
            SetVelocity(Vector2.zero);

            currentState = Machine.State;
            currentPhase = Machine.Phase;
            currentJumpPhase = Machine.CurrentJumpPhase;
            currentAttackPhase = Machine.CurrentAttackPhase;

            if (presentation != null)
                presentation.Render(Machine, RuntimeProfile, facing, 0);
        }

        private void ExecuteJump()
        {
            Machine.ExecuteJump();
        }

        private void ExecuteBunnyForward()
        {
            Machine.ExecuteBunnyForward(
                facing);
        }

        private void ExecuteBunnyBackward()
        {
            Machine.ExecuteBunnyBackward(
                facing);
        }

        private void ExecuteBunnyForwardLoop()
        {
            Machine.ExecuteBunnyForwardLoop(
                facing);
        }

        // ================================================================
        // ATTACK INPUT
        // ================================================================

        private void HandleAttackInput(
            FighterCommand command)
        {
            if (combat == null)
                return;

            AttackMoveData move = ResolveAttackMove(command);
            if (move == null)
                return;

            if (Machine.IsAutoCombo)
            {
                if (Machine.TryCancelAutoComboToAttack(
                    move, facing, command.AttackMove == null))
                {
                    ClearAttackBuffer();
                    combat.BeginAttack(move, command.AttackMove != null);
                    SetHoldAttackInput(command);
                    return;
                }

                // Auto Combo inputs can be entered before the configured
                // impact cancel tick and buffered until that window opens.
                if (Machine.HasFutureAutoComboCancelWindow)
                {
                    bufferedAttack = command;
                    hasBufferedAttack = true;
                }

                return;
            }

            if (!Machine.IsAttacking)
            {
                ClearAttackBuffer();

                bool requestHoldCharge = command.AttackMove == null;
                bool started = Machine.IsJumping
                    ? Machine.TryStartAirAttack(move, facing, requestHoldCharge)
                    : Machine.TryStartAttack(move, requestHoldCharge);

                if (started)
                {
                    combat.BeginAttack(move, command.AttackMove != null);
                    SetHoldAttackInput(command);
                }

                return;
            }

            AttackMoveData currentAttack = Machine.CurrentAttack;
            if (currentAttack == null)
                return;

            if (Machine.CurrentAttackPhase == AttackPhase.Recovery)
            {
                ClearAttackBuffer();
                return;
            }

            if (Machine.IsAttackCancelWindow)
            {
                ClearAttackBuffer();
                if (Machine.TryCancelAttack(
                    move, facing, command.AttackMove == null))
                {
                    combat.BeginAttack(move, command.AttackMove != null);
                    SetHoldAttackInput(command);
                }
                return;
            }

            if (Machine.CurrentAttackPhase == AttackPhase.Pose &&
                currentAttack.IsCancelBufferWindow(Machine.PhaseElapsed))
            {
                bufferedAttack = command;
                hasBufferedAttack = true;
            }
        }

        private void HandleAutoComboInput(FighterCommand command)
        {
            if (combat == null || command.AutoCombo == null)
                return;

            if (Machine.IsAutoCombo)
            {
                if (Machine.TryCancelAutoComboToAutoCombo(command.AutoCombo))
                {
                    ClearAttackBuffer();
                    ClearHoldAttackInput();
                    combat.BeginAutoCombo();
                }
                else if (Machine.HasFutureAutoComboCancelWindow)
                {
                    bufferedAttack = command;
                    hasBufferedAttack = true;
                }

                return;
            }

            if (Machine.IsAttacking)
            {
                if (Machine.TryCancelAttackToAutoCombo(command.AutoCombo))
                {
                    ClearAttackBuffer();
                    ClearHoldAttackInput();
                    combat.BeginAutoCombo();
                    return;
                }

                AttackMoveData current = Machine.CurrentAttack;
                if (current != null && current.allowAutoComboCancel &&
                    Machine.CurrentAttackPhase == AttackPhase.Pose)
                {
                    bufferedAttack = command;
                    hasBufferedAttack = true;
                }

                return;
            }

            if (Machine.TryStartAutoCombo(command.AutoCombo))
            {
                ClearAttackBuffer();
                ClearHoldAttackInput();
                combat.BeginAutoCombo();
            }
        }

        private void TryExecuteBufferedAttack()
        {
            if (hasPendingParry || !hasBufferedAttack)
                return;

            if (combat == null)
            {
                ClearAttackBuffer();
                return;
            }

            FighterCommand command = bufferedAttack;

            if (Machine.IsAutoCombo)
            {
                if (!Machine.IsAutoComboCancelWindow)
                {
                    if (Machine.Elapsed < Machine.Duration &&
                        Machine.HasFutureAutoComboCancelWindow)
                    {
                        return;
                    }

                    ClearAttackBuffer();
                    return;
                }

                ClearAttackBuffer();
                if (command.Type == FighterCommandType.AutoCombo &&
                    command.AutoCombo != null)
                {
                    if (Machine.TryCancelAutoComboToAutoCombo(command.AutoCombo))
                    {
                        ClearHoldAttackInput();
                        combat.BeginAutoCombo();
                    }
                }
                else if (command.Type == FighterCommandType.Attack)
                {
                    AttackMoveData targetMove = ResolveAttackMove(command);
                    if (targetMove != null && Machine.TryCancelAutoComboToAttack(
                        targetMove, facing, command.AttackMove == null))
                    {
                        combat.BeginAttack(targetMove, command.AttackMove != null);
                        SetHoldAttackInput(command);
                    }
                }

                return;
            }

            if (!Machine.IsAttacking)
            {
                ClearAttackBuffer();
                return;
            }

            if (Machine.CurrentAttackPhase == AttackPhase.Recovery)
            {
                ClearAttackBuffer();
                return;
            }

            if (!Machine.IsAttackCancelWindow)
                return;

            ClearAttackBuffer();
            if (command.Type == FighterCommandType.AutoCombo &&
                command.AutoCombo != null)
            {
                if (Machine.TryCancelAttackToAutoCombo(command.AutoCombo))
                {
                    ClearHoldAttackInput();
                    combat.BeginAutoCombo();
                }
                return;
            }

            if (command.Type != FighterCommandType.Attack)
                return;

            AttackMoveData move = ResolveAttackMove(command);
            if (move == null)
                return;

            if (Machine.TryCancelAttack(move, facing, command.AttackMove == null))
            {
                combat.BeginAttack(move, command.AttackMove != null);
                SetHoldAttackInput(command);
            }
        }

        private AttackMoveData ResolveAttackMove(
            FighterCommand command)
        {
            // Ataque especial definido directamente
            // desde el FighterMoveset.
            if (command.AttackMove != null)
            {
                return command.AttackMove;
            }

            // Ataque normal L/M/H.
            return combat.MoveFor(
                command.Attack,
                Machine.IsJumping || Machine.IsAirAttacking);
        }

        private void SetHoldAttackInput(FighterCommand command)
        {
            hasHoldAttackInput = command.AttackMove == null;
            holdAttackInput = command.Attack;
        }

        private void ClearHoldAttackInput()
        {
            hasHoldAttackInput = false;
            holdAttackInput = default;
            Machine.UpdateHoldAttackInput(false);
        }

        private void ClearAttackBuffer()
        {
            hasBufferedAttack = false;

            bufferedAttack =
                default(FighterCommand);
        }

        // ================================================================
        // BLOCK / BLOCK RECOVERY
        // ================================================================

        public bool ReceiveBlockedHit(int blockRecoveryTicks)
        {
            if (!ready || !isActiveAndEnabled)
                return false;

            // A zero value deliberately leaves the fighter controllable.
            // If Back is still held, the next impact tick can be guarded too.
            if (blockRecoveryTicks <= 0)
                return true;

            if (Machine.State != FighterState.Idle &&
                Machine.State != FighterState.Walk &&
                Machine.State != FighterState.Guard)
            {
                return false;
            }

            Machine.EnterBlockRecovery(blockRecoveryTicks);

            if (commands != null)
                commands.ClearCommands();

            ClearAttackBuffer();
            poseTick = 0;
            SetVelocity(Vector2.zero);

            currentState = Machine.State;
            currentPhase = Machine.Phase;
            currentJumpPhase = Machine.CurrentJumpPhase;
            currentAttackPhase = Machine.CurrentAttackPhase;

            return true;
        }

        public bool ReceiveGuardBreak(int durationTicks)
        {
            if (!ready || !isActiveAndEnabled || !CanReceiveCombatHit)
                return false;

            ClearPendingParry();
            ClearHoldAttackInput();
            Machine.EnterGuardBreak(durationTicks);

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearCommands();

            ClearAttackBuffer();
            pendingPushback = 0f;
            poseTick = 0;
            SetVelocity(Vector2.zero);

            currentState = Machine.State;
            currentPhase = Machine.Phase;
            currentJumpPhase = Machine.CurrentJumpPhase;
            currentAttackPhase = Machine.CurrentAttackPhase;

            if (presentation != null)
                presentation.Render(Machine, RuntimeProfile, facing, 0);

            return true;
        }

        // ================================================================
        // COMBO LIMIT KNOCKDOWN
        // ================================================================

        /// <summary>
        /// Forces the dedicated light fall after the shared Combo System has
        /// evaluated every valid impact for the current tick. Unlike
        /// ReceiveFlyingHurt, this uses its own profile values and state.
        /// </summary>
        public bool ReceiveComboSpecificKnockdown(int direction)
        {
            // Auto Combo-specific Knockdown currently shares the dedicated
            // mild-fall presentation/profile used by the global combo limit.
            return ReceiveComboLimitKnockdown(direction);
        }

        public bool ReceiveComboLimitKnockdown(int direction)
        {
            if (!ready || !isActiveAndEnabled || RuntimeProfile == null)
                return false;

            ClearPendingParry();
            ClearHoldAttackInput();

            Machine.EnterComboLimitKnockdown(
                RuntimeProfile.comboLimitKnockdownFallTicks,
                RuntimeProfile.comboLimitKnockdownDistance,
                RuntimeProfile.comboLimitKnockdownHeight,
                RuntimeProfile.comboLimitKnockdownGroundTicks,
                direction);

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearCommands();

            ClearAttackBuffer();
            pendingPushback = 0f;
            poseTick = 0;
            SetVelocity(Vector2.zero);

            currentState = Machine.State;
            currentPhase = Machine.Phase;
            currentJumpPhase = Machine.CurrentJumpPhase;
            currentAttackPhase = Machine.CurrentAttackPhase;

            if (presentation != null)
                presentation.Render(Machine, RuntimeProfile, facing, 0);

            return true;
        }

        // ================================================================
        // NORMAL HURT
        // ================================================================

        public bool ReceiveConfirmedHit(
            int durationTicks = -1)
        {
            if (!CanReceiveCombatHit)
                return false;

            // A real hit interrupts an unsuccessful Parry attempt. Clear
            // its latched input so it cannot survive into Hurt or Idle.
            ClearPendingParry();
            ClearHoldAttackInput();

            int hurtTicks =
                durationTicks > 0
                    ? durationTicks
                    : debugHurtTicks;

            Machine.EnterHurt(
                Mathf.Max(
                    1,
                    hurtTicks));

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearCommands();

            ClearAttackBuffer();

            poseTick = 0;

            SetVelocity(
                Vector2.zero);

            currentState =
                Machine.State;

            currentPhase =
                Machine.Phase;

            currentJumpPhase =
                Machine.CurrentJumpPhase;

            currentAttackPhase =
                Machine.CurrentAttackPhase;

            if (presentation != null)
            {
                presentation.Render(
                    Machine,
                    RuntimeProfile,
                    facing,
                    0);
            }

            return true;
        }

        // ================================================================
        // FLYING HURT
        // ================================================================

        public bool ReceiveFlyingHurt(
            int flyingTicks,
            float flyingDistance,
            float flyingHeight,
            int direction,
            int knockdownTicks)
        {
            if (!CanReceiveCombatHit)
                return false;

            ClearPendingParry();
            ClearHoldAttackInput();

            Machine.EnterFlyingHurt(
                flyingTicks,
                flyingDistance,
                flyingHeight,
                direction,
                knockdownTicks);

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearCommands();

            ClearAttackBuffer();

            poseTick = 0;

            SetVelocity(
                Vector2.zero);

            currentState =
                Machine.State;

            currentPhase =
                Machine.Phase;

            currentJumpPhase =
                Machine.CurrentJumpPhase;

            currentAttackPhase =
                Machine.CurrentAttackPhase;

            if (presentation != null)
            {
                presentation.Render(
                    Machine,
                    RuntimeProfile,
                    facing,
                    0);
            }

            return true;
        }

        // ================================================================
        // HITSTOP
        // ================================================================

        public void ApplyHitstop(
            int ticks)
        {
            if (!ready ||
                ticks <= 0)
            {
                return;
            }

            hitstopRemaining =
                Mathf.Max(
                    hitstopRemaining,
                    ticks);

            body.constraints =
                RigidbodyConstraints2D.FreezeAll;

            SetVelocity(
                Vector2.zero);
        }

        // ================================================================
        // PUSHBACK
        // ================================================================

        public void ApplyPushback(
            float distance,
            int direction)
        {
            if (!ready ||
                distance <= 0)
            {
                return;
            }

            if (Machine.IsFlyingHurt ||
                Machine.IsComboLimitKnockdown ||
                Machine.IsKnockdown ||
                Machine.IsResetting)
            {
                return;
            }

            float sign =
                direction >= 0
                    ? 1f
                    : -1f;

            pendingPushback +=
                Mathf.Abs(distance) *
                sign;
        }

        // ================================================================
        // DEBUG HIT
        // ================================================================

        [ContextMenu(
            "Prueba: recibir hit confirmado (Play)")]
        private void DebugConfirmedHit()
        {
            if (Application.isPlaying)
            {
                ReceiveConfirmedHit();
            }
        }

        // ================================================================
        // CLEANUP
        // ================================================================

        private void SetVelocity(
            Vector2 value)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity =
                value;
#else
            body.velocity =
                value;
#endif
        }

        private void OnDisable()
        {
            FighterCombatResolver.Unregister(this);

            if (!ready)
                return;

            SetVelocity(
                Vector2.zero);

            body.constraints =
                RigidbodyConstraints2D.FreezeAll;

            Machine.Reset();

            hitstopRemaining = 0;
            pendingPushback = 0;

            ClearAttackBuffer();
            hasPendingParry = false;
            pendingParryInput = default;
            ClearHoldAttackInput();

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearCommands();

            coordinatedFrameDelta = 0f;
            coordinatedLastTickDelta = 0f;
            pausedThisStep = false;
            resumedThisStep = false;
            advancedThisTick = false;

            if (presentation != null)
                presentation.ResetHeight();
        }

        private void OnDestroy()
        {
            if (material != null)
                Destroy(material);

            if (RuntimeProfile != null)
                Destroy(RuntimeProfile);
        }
    }
}