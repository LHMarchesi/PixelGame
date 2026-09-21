// Land of Fire · FUN-COM-001
// Configuración completa de un golpe.
//
// Lethal Hit NO depende de la vida restante.
// Si lethalHit está activo, cualquier impacto no bloqueado
// produce Flying Hurt.

using Cinemachine;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [CreateAssetMenu(
        menuName = "Land of Fire/Combat/Attack Move")]
    public sealed class AttackMoveData :
        ScriptableObject
    {
        [Header("Duración por fase (ticks a 60 Hz)")]

        [Min(1)]
        public int anticipationTicks = 4;

        [Min(1)]
        public int smearTicks = 1;

        [Min(1)]
        public int poseTicks = 1;

        [Min(1)]
        public int recoveryTicks = 8;

        [Header("Ventana de impacto")]

        public bool hitDuringSmear = true;
        public bool hitDuringPose;

        [Tooltip(
            "Offset local desde la raíz; X se invierte con el facing.")]
        public Vector2 hitboxOffset =
            new Vector2(1f, 1f);

        public Vector2 hitboxSize =
            new Vector2(.9f, .55f);

        [Min(0)]
        public int damage = 8;

        [Min(1)]
        public int hitstunTicks = 18;

        [Min(0)]
        public int hitstopTicks = 3;

        // ================================================================
        // PUSHBACK
        // ================================================================

        [Header("Pushback")]

        [Tooltip(
            "Distancia de pushback cuando el golpe entra normalmente.")]
        [Min(0)]
        public float hitPushback = .08f;

        [Tooltip(
            "Distancia de pushback cuando el rival está cubriendo.")]
        [Min(0)]
        public float guardPushback = .04f;

        // ================================================================
        // LETHAL HIT
        // ================================================================

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

        // ================================================================
        // DEBUG
        // ================================================================

        [Header("Debug")]

        [Tooltip(
            "Muestra la hitbox durante sus ticks activos.")]
        public bool showHitboxDebug;

        // ================================================================
        // CANCEL
        // ================================================================

        [Header("Cancel")]

        [Min(1)]
        public int cancelStartTick = 1;

        [Min(1)]
        public int cancelEndTick = 4;

        [Min(0)]
        public int cancelBufferTicks = 2;

        // ================================================================
        // ENTRADA
        // ================================================================

        [Header("Entrada")]

        public bool allowBunnyCancel;

        // ================================================================
        // ANIMACIÓN
        // ================================================================

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
            Mathf.Max(
                1,
                anticipationTicks);

        public int SmearTicks =>
            Mathf.Max(
                1,
                smearTicks);

        public int PoseTicks =>
            Mathf.Max(
                1,
                poseTicks);

        public int RecoveryTicks =>
            Mathf.Max(
                1,
                recoveryTicks);

        public int TotalTicks =>
            AnticipationTicks +
            SmearTicks +
            PoseTicks +
            RecoveryTicks;

        // ================================================================
        // HITBOX
        // ================================================================

        public bool HasHitbox(
            AttackPhase phase)
        {
            return
                phase == AttackPhase.Smear &&
                hitDuringSmear ||
                phase == AttackPhase.Pose &&
                hitDuringPose;
        }

        // ================================================================
        // CANCEL WINDOW
        // ================================================================

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

        // ================================================================
        // CANCEL BUFFER
        // ================================================================

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

        // ================================================================
        // ANIMATOR
        // ================================================================

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

