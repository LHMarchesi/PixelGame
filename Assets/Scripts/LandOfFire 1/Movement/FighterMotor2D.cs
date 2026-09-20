// Land of Fire · Física y coordinador actual del tick de un FighterRoot.
// Ejecuta la máquina lógica a 60 Hz, aplica movimiento y llama al módulo de combate.
// Mantiene el nombre y sus referencias de prefab para no romper la escena existente.
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

        [Tooltip("Opcional: al añadir FighterCombat2D habilita los golpes.")]
        public FighterCombat2D combat;

        [Tooltip("Opcional. Sin rival conserva la orientación configurada.")]
        public Transform facingTarget;

        public bool startsFacingRight = true;
        public bool frozen;

        [SerializeField] private FighterState currentState;
        [SerializeField] private BunnyPhase currentPhase;
        [SerializeField] private AttackPhase currentAttackPhase;
        [SerializeField] private int facing = 1;

        public FighterStateMachine Machine { get; } = new FighterStateMachine();

        public bool GuardRequested => Machine.State == FighterState.Guard;
        public int Facing => facing;

        public Vector2 BodyPosition =>
            body != null ? body.position : (Vector2)transform.position;

        public FighterMovementProfile RuntimeProfile { get; private set; }

        private Rigidbody2D body;
        private PhysicsMaterial2D material;
        private double accumulated;
        private long tick;
        private int poseTick;
        private int hitstopRemaining;
        private bool ready;

        private const double TickSeconds = 1.0 / 60.0;

        private const RigidbodyConstraints2D GroundConstraints =
            RigidbodyConstraints2D.FreezePositionY |
            RigidbodyConstraints2D.FreezeRotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();

            if (profile == null)
            {
                Debug.LogError("Asignar FighterMovementProfile al FighterMotor2D.", this);
                body.simulated = false;
                enabled = false;
                return;
            }

            RuntimeProfile = Instantiate(profile);
            RuntimeProfile.name = profile.name + " (Play: " + name + ")";
            RuntimeProfile.hideFlags = HideFlags.DontSave;

            if (commands == null)
                commands = GetComponent<FighterCommandSource>();

            if (presentation == null)
                presentation = GetComponent<FighterPresentation>();

            if (combat == null)
                combat = GetComponent<FighterCombat2D>();

            facing = startsFacingRight ? 1 : -1;

            body.bodyType = RigidbodyType2D.Dynamic;
            body.simulated = true;
            body.gravityScale = 0;
            body.constraints = GroundConstraints;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.mass = Mathf.Max(.01f, profile.mass);

#if UNITY_6000_0_OR_NEWER
            body.linearDamping = 0;
#else
            body.drag = 0;
#endif

            material = new PhysicsMaterial2D("Fighter contact (runtime)")
            {
                friction = 0,
                bounciness = 0
            };

            var box = GetComponent<BoxCollider2D>();
            box.isTrigger = false;
            box.size = new Vector2(
                Mathf.Max(.01f, profile.bodySize.x),
                Mathf.Max(.01f, profile.bodySize.y));

            box.offset = profile.bodyOffset;
            box.sharedMaterial = material;

            ready = true;
        }

        private void OnEnable()
        {
            if (!ready)
                return;

            Machine.Reset();
            accumulated = 0;
            tick = 0;
            poseTick = 0;
            hitstopRemaining = 0;

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearPresses();

            SetVelocity(Vector2.zero);
        }

        private void FixedUpdate()
        {
            if (!ready)
                return;

            if (frozen)
            {
                // Un luchador congelado no puede ser empujado por otro.
                body.constraints = RigidbodyConstraints2D.FreezeAll;
                SetVelocity(Vector2.zero);

                Machine.ForgetTap();
                accumulated = 0;

                if (commands != null)
                    commands.ClearPresses();

                return;
            }

            // El perfil sigue expresado a 60 ticks/s aunque Physics 2D tenga otro timestep.
            accumulated += Time.fixedDeltaTime;

            float delta = 0;
            bool pausedThisStep = false;
            bool resumedThisStep = false;

            while (accumulated + 1e-9 >= TickSeconds)
            {
                accumulated -= TickSeconds;

                if (hitstopRemaining > 0)
                {
                    hitstopRemaining--;
                    pausedThisStep = true;
                    tick++;
                    continue;
                }

                resumedThisStep = true;

                if (!Machine.IsTimed && facingTarget != null)
                {
                    float difference = facingTarget.position.x - body.position.x;

                    if (Mathf.Abs(difference) > .001f)
                        facing = difference > 0 ? 1 : -1;
                }

                bool hasCommands =
                    commands != null &&
                    commands.isActiveAndEnabled;

                Machine.ConfigureForwardLoop(
                    RuntimeProfile.forwardLoopTiming.Total,
                    RuntimeProfile.forwardLoopDistance,
                    RuntimeProfile.forwardLoopHeight);

                FighterState before = Machine.State;

                Machine.BeginTick(
                    facing,
                    hasCommands ? commands.Direction : 0);

                if (hasCommands)
                {
                    // =====================================================
                    // ATAQUES
                    // =====================================================
                    //
                    // Un ataque nuevo puede:
                    // 1. comenzar desde Idle;
                    // 2. cancelar otro ataque durante su Recovery window.
                    //
                    // Todos los ataques pueden cancelar a cualquier otro.
                    //
                    while (commands.TryReadAttack(out AttackCommand attackCommand))
                    {
                        AttackMoveData move =
                            combat != null
                                ? combat.MoveFor(attackCommand)
                                : null;

                        if (move == null)
                            continue;

                        // Si ya estamos atacando, solamente permitimos
                        // el nuevo ataque si estamos dentro de la ventana
                        // configurable de cancel.
                        if (Machine.IsAttacking)
                        {
                            if (Machine.TryCancelAttack(move))
                            {
                                if (combat != null)
                                    combat.BeginAttack(move);

                                // El cancel elimina cualquier desplazamiento
                                // voluntario que hubiera quedado del estado anterior.
                                delta = 0;
                            }
                        }
                        // Si no estamos atacando, intentamos iniciar
                        // normalmente el ataque.
                        else
                        {
                            if (Machine.TryStartAttack(move))
                            {
                                if (combat != null)
                                    combat.BeginAttack(move);

                                delta = 0;
                            }
                        }
                    }

                    // =====================================================
                    // MOVIMIENTO
                    // =====================================================

                    while (commands.TryReadPress(out int press))
                    {
                        Machine.Press(
                            press,
                            tick,
                            RuntimeProfile.doubleTapTicks,
                            RuntimeProfile.forwardTiming.Total,
                            RuntimeProfile.forwardDistance,
                            RuntimeProfile.forwardHeight,
                            RuntimeProfile.backwardTiming.Total,
                            RuntimeProfile.backwardDistance,
                            RuntimeProfile.backwardHeight);
                    }
                }
                else if (commands != null)
                {
                    commands.ClearPresses();
                }

                poseTick = before == Machine.State
                    ? poseTick + 1
                    : 0;

                delta += Machine.Advance();

                if (combat != null)
                    combat.ResolveTick(Machine, facing);

                tick++;
            }

            // Proponemos velocidad. Physics 2D resuelve contactos,
            // sin escribir transform.position.
            bool stopped =
                hitstopRemaining > 0 ||
                (pausedThisStep && !resumedThisStep);

            body.constraints =
                stopped
                    ? RigidbodyConstraints2D.FreezeAll
                    : GroundConstraints;

            SetVelocity(
                stopped
                    ? Vector2.zero
                    : new Vector2(delta / Time.fixedDeltaTime, 0));

            currentState = Machine.State;
            currentPhase = Machine.Phase;
            currentAttackPhase = Machine.CurrentAttackPhase;
        }

        private void LateUpdate()
        {
            if (ready && presentation != null)
                presentation.Render(
                    Machine,
                    RuntimeProfile,
                    facing,
                    poseTick);
        }

        /// <summary>
        /// Entrada de un hit YA aceptado.
        /// No calcula daño, parry ni invulnerabilidad.
        /// </summary>
        public bool ReceiveConfirmedHit(int durationTicks = -1)
        {
            if (!ready || !isActiveAndEnabled)
                return false;

            Machine.EnterHurt(
                durationTicks > 0
                    ? durationTicks
                    : RuntimeProfile.hurtTicks);

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearPresses();

            accumulated = 0;
            poseTick = 0;

            SetVelocity(Vector2.zero);

            currentState = Machine.State;
            currentPhase = Machine.Phase;
            currentAttackPhase = Machine.CurrentAttackPhase;

            if (presentation != null)
                presentation.Render(
                    Machine,
                    RuntimeProfile,
                    facing,
                    0);

            return true;
        }

        /// <summary>
        /// Congela el tiempo lógico del luchador;
        /// el golpe ya aceptado no se reevalúa.
        /// </summary>
        public void ApplyHitstop(int ticks)
        {
            if (!ready || ticks <= 0)
                return;

            hitstopRemaining = Mathf.Max(
                hitstopRemaining,
                ticks);

            body.constraints = RigidbodyConstraints2D.FreezeAll;
            SetVelocity(Vector2.zero);
        }

        [ContextMenu("Prueba: recibir hit confirmado (Play)")]
        private void DebugConfirmedHit()
        {
            if (Application.isPlaying)
                ReceiveConfirmedHit();
        }

        private void SetVelocity(Vector2 value)
        {
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = value;
#else
            body.velocity = value;
#endif
        }

        private void OnDisable()
        {
            if (!ready)
                return;

            SetVelocity(Vector2.zero);
            body.constraints = RigidbodyConstraints2D.FreezeAll;

            Machine.Reset();
            hitstopRemaining = 0;

            if (combat != null)
                combat.CancelAttack();

            if (commands != null)
                commands.ClearPresses();

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