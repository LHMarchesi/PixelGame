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
        [Tooltip("Opcional. Sin rival conserva la orientación configurada.")]
        public Transform facingTarget;
        public bool startsFacingRight = true;
        public bool frozen;
        [SerializeField] private FighterState currentState;
        [SerializeField] private int facing = 1;
        public BunnyStateMachine Machine { get; } = new BunnyStateMachine();
        public bool GuardRequested => Machine.State == FighterState.Guard;
        public int Facing => facing;
        private Rigidbody2D body;
        private PhysicsMaterial2D material;
        private double accumulated;
        private long tick;
        private int poseTick;
        private bool ready;
        private const double TickSeconds = 1.0 / 60.0;
        private const RigidbodyConstraints2D GroundConstraints =
            RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;

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
            if (GetComponent<BunnyFighter>() != null)
            {
                Debug.LogError("Quitar el componente BunnyFighter anterior antes de usar FighterMotor2D.", this);
                body.simulated = false;
                enabled = false;
                return;
            }
            if (commands == null) commands = GetComponent<FighterCommandSource>();
            if (presentation == null) presentation = GetComponent<FighterPresentation>();
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
            material = new PhysicsMaterial2D("Fighter contact (runtime)") { friction = 0, bounciness = 0 };
            var box = GetComponent<BoxCollider2D>();
            box.isTrigger = false;
            box.size = new Vector2(Mathf.Max(.01f, profile.bodySize.x), Mathf.Max(.01f, profile.bodySize.y));
            box.offset = profile.bodyOffset;
            box.sharedMaterial = material;
            ready = true;
        }

        private void OnEnable()
        {
            if (!ready) return;
            Machine.Reset(); accumulated = 0; tick = 0; poseTick = 0;
            if (commands != null) commands.ClearPresses();
            SetVelocity(Vector2.zero);
        }

        private void FixedUpdate()
        {
            if (!ready) return;
            if (frozen)
            {
                // Un luchador congelado no puede ser empujado por otro.
                body.constraints = RigidbodyConstraints2D.FreezeAll;
                SetVelocity(Vector2.zero);
                Machine.ForgetTap(); accumulated = 0;
                if (commands != null) commands.ClearPresses();
                return;
            }
            body.constraints = GroundConstraints;
            // El perfil sigue expresado a 60 ticks/s aunque Physics 2D tenga otro timestep.
            accumulated += Time.fixedDeltaTime;
            float delta = 0;
            while (accumulated + 1e-9 >= TickSeconds)
            {
                accumulated -= TickSeconds;
                if (!Machine.IsTimed && facingTarget != null)
                {
                    float difference = facingTarget.position.x - body.position.x;
                    if (Mathf.Abs(difference) > .001f) facing = difference > 0 ? 1 : -1;
                }
                bool hasCommands = commands != null && commands.isActiveAndEnabled;
                Machine.ConfigureForwardLoop(profile.forwardLoopTicks, profile.forwardLoopDistance, profile.forwardLoopHeight);
                FighterState before = Machine.State;
                Machine.BeginTick(facing, hasCommands ? commands.Direction : 0);
                if (hasCommands)
                    while (commands.TryReadPress(out int press))
                        Machine.Press(press, tick, profile.doubleTapTicks,
                            profile.forwardTicks, profile.forwardDistance, profile.forwardHeight,
                            profile.backwardTicks, profile.backwardDistance, profile.backwardHeight);
                else if (commands != null) commands.ClearPresses();
                poseTick = before == Machine.State ? poseTick + 1 : 0;
                delta += Machine.Advance();
                tick++;
            }
            // Proponemos velocidad. Physics 2D resuelve contactos, sin escribir transform.position.
            SetVelocity(new Vector2(delta / Time.fixedDeltaTime, 0));
            currentState = Machine.State;
        }

        private void LateUpdate()
        {
            if (ready && presentation != null)
                presentation.Render(Machine.State, Machine.Progress, Machine.VisualHeight, facing, poseTick);
        }

        /// <summary>Entrada de un hit YA aceptado. No calcula daño, parry ni invulnerabilidad.</summary>
        public bool ReceiveConfirmedHit(int durationTicks = -1)
        {
            if (!ready || !isActiveAndEnabled) return false;
            Machine.EnterHurt(durationTicks > 0 ? durationTicks : profile.hurtTicks);
            if (commands != null) commands.ClearPresses();
            accumulated = 0;
            poseTick = 0;
            SetVelocity(Vector2.zero); // Cancelar inmediatamente el movimiento voluntario.
            currentState = Machine.State;
            if (presentation != null)
                presentation.Render(Machine.State, 0, Machine.VisualHeight, facing, 0);
            return true;
        }

        [ContextMenu("Prueba: recibir hit confirmado (Play)")]
        private void DebugConfirmedHit()
        {
            if (Application.isPlaying) ReceiveConfirmedHit();
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
            if (!ready) return;
            SetVelocity(Vector2.zero);
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            Machine.Reset();
            if (commands != null) commands.ClearPresses();
            if (presentation != null) presentation.ResetHeight();
        }
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
