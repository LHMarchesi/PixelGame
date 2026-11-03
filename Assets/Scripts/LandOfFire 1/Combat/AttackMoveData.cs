// Land of Fire · FUN-COM-001
// Configuración completa de un golpe.
//
// Lethal Hit NO depende de la vida restante.
// Si lethalHit está activo, cualquier impacto no bloqueado
// produce Flying Hurt.

using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public enum AttackCategory
    {
        Light,
        Medium,
        Heavy
    }

    [CreateAssetMenu(
        menuName = "Land of Fire/Combat/Attack Move")]
    public sealed class AttackMoveData :
        ScriptableObject
    {
        [Header("Categoría y Telegraph")]

        [Tooltip("Categoría usada para identificar visualmente el ataque. Light = azul, Medium = naranja, Heavy = rojo.")]
        public AttackCategory category = AttackCategory.Light;

        [Tooltip("Activa una señal visual breve antes del impacto.")]
        public bool showTelegraph;

        [Min(1)]
        [Tooltip("Tick exacto en el que aparece el Telegraph. Se cuenta desde el primer tick de Anticipation.")]
        public int telegraphStartTick = 1;

        [Min(0)]
        [Tooltip("Cantidad de ticks que permanece visible. No depende de la ventana de Parry ni de la carga de Hold.")]
        public int telegraphDurationTicks;

        [Tooltip("Permite reemplazar el color predeterminado de la categoría.")]
        public bool useCustomTelegraphColor;

        public Color customTelegraphColor = Color.cyan;

        public Color TelegraphColor
        {
            get
            {
                if (useCustomTelegraphColor)
                    return customTelegraphColor;

                switch (category)
                {
                    case AttackCategory.Medium:
                        return new Color(1f, 0.48f, 0.08f, 1f);
                    case AttackCategory.Heavy:
                        return new Color(0.95f, 0.12f, 0.12f, 1f);
                    default:
                        return new Color(0.12f, 0.55f, 1f, 1f);
                }
            }
        }

        public bool IsTelegraphActive(int attackElapsedTick)
        {
            if (!showTelegraph || telegraphDurationTicks <= 0)
                return false;

            int start = Mathf.Max(1, telegraphStartTick);
            int endExclusive = start + telegraphDurationTicks;
            return attackElapsedTick >= start && attackElapsedTick < endExclusive;
        }

        [Header("Duración por fase (ticks a 60 Hz)")]

        [Min(1)]
        public int anticipationTicks = 4;

        [Min(1)]
        public int smearTicks = 1;

        [Min(1)]
        public int poseTicks = 1;

        [Min(1)]
        public int recoveryTicks = 8;

        [Header("Ventana de impacto (ticks del ataque)")]

        [Tooltip("Desactiva este golpe si el movimiento no debe tener hitbox.")]
        public bool useHitbox = true;

        [Tooltip("Para ataques terrestres: activado usa Start/End; desactivado conserva la ventana antigua por Smear/Pose. AirAttack siempre usa la ventana por ticks.")]
        public bool useTickHitWindow = false;

        [Min(1)]
        [Tooltip("Primer tick activo de la hitbox (inclusive), contando desde el primer tick de Anticipation.")]
        public int hitboxStartTick = 5;

        [Min(1)]
        [Tooltip("Último tick activo de la hitbox (inclusive), contando desde el primer tick de Anticipation.")]
        public int hitboxEndTick = 5;

        // Compatibilidad con AttackMoveData creados antes de la ventana por ticks.
        // Solo se consultan cuando useTickHitWindow está desactivado.
        [HideInInspector] public bool hitDuringSmear = true;
        [HideInInspector] public bool hitDuringPose;

        [Header("Movimiento aéreo por fase")]

        [Tooltip("Activado: reemplaza la trayectoria vertical del Jump por el movimiento aéreo personalizado de este ataque. Desactivado: continúa la trayectoria normal del Jump y conserva el control aéreo horizontal. En este modo se ignoran los vectores personalizados.")]
        public bool useCustomAirMovement = true;

        [Tooltip("Desplazamiento total aplicado durante Anticipation. X positivo avanza hacia el facing; Y positivo sube.")]
        public Vector2 airAnticipationMovement;

        [Tooltip("Desplazamiento total aplicado durante Smear. X positivo avanza hacia el facing; Y positivo sube.")]
        public Vector2 airSmearMovement;

        [Tooltip("Desplazamiento total aplicado durante Pose. X positivo avanza hacia el facing; Y positivo sube.")]
        public Vector2 airPoseMovement;

        [Tooltip("Desplazamiento total aplicado durante Recovery. X positivo avanza hacia el facing; Y positivo sube.")]
        public Vector2 airRecoveryMovement;

        [Header("Hitbox")]

        [Tooltip("Si está activo durante AirAttack, la hitbox acompaña la altura visual actual del luchador. hitboxOffset se aplica desde ese punto. Desactivalo para mantener la hitbox relativa a la raíz física.")]
        public bool followAirborneHeight = true;

        [Tooltip("Offset local desde el punto de anclaje. X se invierte con el facing. En AirAttack, si Follow Airborne Height está activo, este offset se suma a la altura visual del salto.")]
        public Vector2 hitboxOffset =
            new Vector2(1f, 1f);

        public Vector2 hitboxSize =
            new Vector2(.9f, .55f);

        [Min(0)]
        public int damage = 8;

        [Tooltip(
            "Duración del estado Hurt que aplica este ataque.")]
        [Min(1)]
        public int hitstunTicks = 18;

        public int HurtTicks =>
            Mathf.Max(1, hitstunTicks);

        [Min(0)]
        public int hitstopTicks = 3;

        [Header("Pushback")]

        [Tooltip(
            "Distancia de pushback cuando el golpe entra normalmente.")]
        [Min(0)]
        public float hitPushback = .08f;

        [Tooltip(
            "Distancia de pushback cuando el rival está cubriendo.")]
        [Min(0)]
        public float guardPushback = .04f;

        [Tooltip("Si está desactivado, Back no puede bloquear este ataque. Usar para Heavy/Aura y strikes especiales que no admiten Guard.")]
        public bool canBeBlocked = true;

        [Min(0)]
        [Tooltip("Ticks durante los que el defensor no puede actuar después de bloquear este ataque. 0 = sin BlockRecovery.")]
        public int blockRecoveryTicks;

        [Header("Lethal Hit")]

        [Tooltip(
            "Si está activo, cualquier impacto no bloqueado " +
            "produce Flying Hurt sin importar el HP restante.")]
        public bool lethalHit;

        [Tooltip(
            "Duración del Flying Hurt.")]
        [Min(1)]
        public int flyingHurtTicks = 24;

        [Tooltip(
            "Distancia horizontal total del Flying Hurt.")]
        [Min(0)]
        public float flyingHurtDistance = 3f;

        [Tooltip(
            "Altura máxima de la parábola.")]
        [Min(0)]
        public float flyingHurtHeight = 1.5f;

        [Tooltip(
            "Tiempo del Knockdown después de aterrizar.")]
        [Min(1)]
        public int lethalHitKnockdownTicks = 30;

        [Header("Debug")]

        [Tooltip(
            "Muestra la hitbox durante sus ticks activos.")]
        public bool showHitboxDebug;

        [Header("Cancel")]

        [Min(1)]
        public int cancelStartTick = 1;

        [Min(1)]
        public int cancelEndTick = 4;

        [Min(0)]
        public int cancelBufferTicks = 2;

        [Header("Entrada")]

        public bool allowBunnyCancel;

        [Header("Estados del Animator")]

        public PhaseAnimation anticipation =
            new PhaseAnimation(
                "LightAnticipation");

        public PhaseAnimation smear =
            new PhaseAnimation(
                "LightSmear");

        public PhaseAnimation pose =
            new PhaseAnimation(
                "LightPose");

        public PhaseAnimation recovery =
            new PhaseAnimation(
                "LightRecovery");

        public int AnticipationTicks =>
            Mathf.Max(1, anticipationTicks);

        public int SmearTicks =>
            Mathf.Max(1, smearTicks);

        public int PoseTicks =>
            Mathf.Max(1, poseTicks);

        public int RecoveryTicks =>
            Mathf.Max(1, recoveryTicks);

        public int TotalTicks =>
            AnticipationTicks +
            SmearTicks +
            PoseTicks +
            RecoveryTicks;

        public bool HasHitbox(
            AttackPhase phase,
            int attackElapsedTick,
            bool forceTickWindow = false)
        {
            if (!useHitbox)
                return false;

            if (useTickHitWindow || forceTickWindow)
            {
                int firstTick = Mathf.Max(1, hitboxStartTick);
                int lastTick = Mathf.Max(1, hitboxEndTick);

                return lastTick >= firstTick &&
                       attackElapsedTick >= firstTick &&
                       attackElapsedTick <= lastTick;
            }

            return
                phase == AttackPhase.Smear && hitDuringSmear ||
                phase == AttackPhase.Pose && hitDuringPose;
        }

        public bool HasHitbox(AttackPhase phase)
        {
            if (!useHitbox)
                return false;

            return
                phase == AttackPhase.Smear && hitDuringSmear ||
                phase == AttackPhase.Pose && hitDuringPose;
        }

        public int CancelStartTick =>
            Mathf.Clamp(
                cancelStartTick,
                1,
                PoseTicks);

        public int CancelEndTick =>
            Mathf.Clamp(
                cancelEndTick,
                CancelStartTick,
                PoseTicks);

        public bool IsCancelWindow(
            int poseElapsed)
        {
            return
                poseElapsed >= CancelStartTick &&
                poseElapsed <= CancelEndTick;
        }

        public bool IsCancelBufferWindow(
            int poseElapsed)
        {
            if (cancelBufferTicks <= 0)
                return false;

            int bufferStart =
                Mathf.Max(
                    1,
                    CancelStartTick -
                    cancelBufferTicks);

            return
                poseElapsed >= bufferStart &&
                poseElapsed < CancelStartTick;
        }

        public PhaseAnimation AnimationFor(
            AttackPhase phase)
        {
            switch (phase)
            {
                case AttackPhase.Anticipation:
                    return anticipation;

                case AttackPhase.Smear:
                    return smear;

                case AttackPhase.Pose:
                    return pose;

                case AttackPhase.Recovery:
                    return recovery;

                default:
                    return null;
            }
        }
    }
}
