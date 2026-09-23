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
        private AttackPhase currentAttackPhase;

        [SerializeField]
        private int facing = 1;

        public FighterStateMachine Machine { get; } =
            new FighterStateMachine();

        public bool GuardRequested =>
            Machine.State ==
            FighterState.Guard;

        public bool CanReceiveCombatHit =>
            ready &&
            isActiveAndEnabled &&
            !Machine.IsFlyingHurt &&
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

        private double accumulated;
        private long tick;

        private int poseTick;
        private int hitstopRemaining;

        private bool ready;

        // ---------------------------------------------------------------
        // Attack buffer
        // ---------------------------------------------------------------

        private bool hasBufferedAttack;

        private FighterCommand bufferedAttack;

        // ---------------------------------------------------------------
        // Pushback
        // ---------------------------------------------------------------

        private float pendingPushback;

        private const double TickSeconds =
            1.0 / 60.0;

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

            accumulated = 0;
            tick = 0;

            poseTick = 0;
            hitstopRemaining = 0;

            pendingPushback = 0;

            ClearAttackBuffer();

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearCommands();

            SetVelocity(
                Vector2.zero);
        }

        private void FixedUpdate()
        {
            if (!ready)
                return;

            if (frozen)
            {
                body.constraints =
                    RigidbodyConstraints2D.FreezeAll;

                SetVelocity(
                    Vector2.zero);

                accumulated = 0;

                pendingPushback = 0;

                ClearAttackBuffer();

                if (commands != null)
                    commands.ClearCommands();

                return;
            }

            accumulated +=
                Time.fixedDeltaTime;

            float delta = 0;

            bool pausedThisStep = false;
            bool resumedThisStep = false;

            while (
                accumulated + 1e-9 >=
                TickSeconds)
            {
                accumulated -=
                    TickSeconds;

                if (hitstopRemaining > 0)
                {
                    hitstopRemaining--;

                    pausedThisStep = true;

                    tick++;

                    continue;
                }

                resumedThisStep = true;

                if (Mathf.Abs(
                    pendingPushback) >
                    0.0001f)
                {
                    delta +=
                        pendingPushback;

                    pendingPushback = 0f;
                }

                if (!Machine.IsTimed &&
                    facingTarget != null)
                {
                    float difference =
                        facingTarget.position.x -
                        body.position.x;

                    if (Mathf.Abs(
                        difference) >
                        .001f)
                    {
                        facing =
                            difference > 0
                                ? 1
                                : -1;
                    }
                }

                bool hasCommands =
                    commands != null &&
                    commands.isActiveAndEnabled;

                ConfigureMachine();

                FighterState before =
                    Machine.State;

                Machine.BeginTick(
                    facing,
                    hasCommands
                        ? commands.Direction
                        : 0);

                if (hasCommands)
                {
                    commands.ProcessInput(
                        facing,
                        tick);

                    ProcessRecognizedCommands();

                    TryExecuteBufferedAttack();
                }
                else if (commands != null)
                {
                    commands.ClearCommands();
                    ClearAttackBuffer();
                }

                poseTick =
                    before == Machine.State
                        ? poseTick + 1
                        : 0;

                delta +=
                    Machine.Advance();

                if (combat != null)
                {
                    combat.ResolveTick(
                        Machine,
                        facing);
                }

                tick++;
            }

            bool stopped =
                hitstopRemaining > 0 ||
                (pausedThisStep &&
                 !resumedThisStep);

            body.constraints =
                stopped
                    ? RigidbodyConstraints2D.FreezeAll
                    : GroundConstraints;

            SetVelocity(
                stopped
                    ? Vector2.zero
                    : new Vector2(
                        delta /
                        Time.fixedDeltaTime,
                        0));

            currentState =
                Machine.State;

            currentPhase =
                Machine.Phase;

            currentAttackPhase =
                Machine.CurrentAttackPhase;
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
                    case FighterCommandType.Attack:

                        HandleAttackInput(
                            command);

                        break;

                    case FighterCommandType.BunnyForward:

                        ExecuteBunnyForward();

                        break;

                    case FighterCommandType.BunnyBackward:

                        ExecuteBunnyBackward();

                        break;

                    case FighterCommandType.BunnyForwardLoop:

                        ExecuteBunnyForwardLoop();

                        break;
                }
            }
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

            AttackMoveData move =
                ResolveAttackMove(
                    command);

            if (move == null)
                return;

            if (!Machine.IsAttacking)
            {
                ClearAttackBuffer();

                if (Machine.TryStartAttack(
                    move))
                {
                    combat.BeginAttack(
                        move);
                }

                return;
            }

            AttackMoveData currentAttack =
                Machine.CurrentAttack;

            if (currentAttack == null)
                return;

            if (Machine.CurrentAttackPhase ==
                AttackPhase.Recovery)
            {
                ClearAttackBuffer();
                return;
            }

            if (Machine.IsAttackCancelWindow)
            {
                ClearAttackBuffer();

                if (Machine.TryCancelAttack(
                    move))
                {
                    combat.BeginAttack(
                        move);
                }

                return;
            }

            if (Machine.CurrentAttackPhase ==
                    AttackPhase.Pose &&
                currentAttack.IsCancelBufferWindow(
                    Machine.PhaseElapsed))
            {
                bufferedAttack =
                    command;

                hasBufferedAttack = true;
            }
        }

        private void TryExecuteBufferedAttack()
        {
            if (!hasBufferedAttack)
                return;

            if (!Machine.IsAttacking)
            {
                ClearAttackBuffer();
                return;
            }

            if (Machine.CurrentAttackPhase ==
                AttackPhase.Recovery)
            {
                ClearAttackBuffer();
                return;
            }

            if (!Machine.IsAttackCancelWindow)
                return;

            if (combat == null)
            {
                ClearAttackBuffer();
                return;
            }

            FighterCommand command =
                bufferedAttack;

            ClearAttackBuffer();

            AttackMoveData move =
                ResolveAttackMove(
                    command);

            if (move == null)
                return;

            if (Machine.TryCancelAttack(
                move))
            {
                combat.BeginAttack(
                    move);
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
                command.Attack);
        }

        private void ClearAttackBuffer()
        {
            hasBufferedAttack = false;

            bufferedAttack =
                default(FighterCommand);
        }

        // ================================================================
        // NORMAL HURT
        // ================================================================

        public bool ReceiveConfirmedHit(
            int durationTicks = -1)
        {
            if (!CanReceiveCombatHit)
                return false;

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

            accumulated = 0;
            poseTick = 0;

            SetVelocity(
                Vector2.zero);

            currentState =
                Machine.State;

            currentPhase =
                Machine.Phase;

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

            accumulated = 0;
            poseTick = 0;

            SetVelocity(
                Vector2.zero);

            currentState =
                Machine.State;

            currentPhase =
                Machine.Phase;

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

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearCommands();

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