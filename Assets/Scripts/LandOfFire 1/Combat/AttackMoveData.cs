// Land of Fire · FUN-COM-001
// Configuración de un golpe. Cada fase tiene sus propios ticks y clip; cambiar la
// cantidad de dibujos de un clip no modifica las ventanas lógicas del impacto.
// Es un asset compartible: durante un golpe, la máquina copia sus duraciones.

using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [CreateAssetMenu(menuName = "Land of Fire/Combat/Attack Move")]
    public sealed class AttackMoveData : ScriptableObject
    {
        [Header("Duración por fase (ticks a 60 Hz)")]
        [Min(1)] public int anticipationTicks = 4;
        [Min(1)] public int smearTicks = 1;
        [Min(1)] public int poseTicks = 1;
        [Min(1)] public int recoveryTicks = 8;

        [Header("Ventana de impacto")]
        public bool hitDuringSmear = true;
        public bool hitDuringPose;

        [Tooltip("Offset local desde la raíz; X se invierte con el facing.")]
        public Vector2 hitboxOffset = new Vector2(1f, 1f);

        public Vector2 hitboxSize = new Vector2(.9f, .55f);

        [Min(0)] public int damage = 8;
        [Min(1)] public int hitstunTicks = 18;
        [Min(0)] public int hitstopTicks = 3;

        [Header("Debug")]
        [Tooltip(
            "Si está activo, muestra visualmente la hitbox durante sus ticks activos."
        )]
        public bool showHitboxDebug;

        [Header("Cancel")]
        [Tooltip(
            "Primer tick de Pose en el que se puede cancelar. " +
            "El conteo comienza desde el inicio de Pose."
        )]
        [Min(1)] public int cancelStartTick = 1;

        [Tooltip(
            "Último tick de Pose en el que se puede cancelar. " +
            "Es inclusive."
        )]
        [Min(1)] public int cancelEndTick = 4;

        [Tooltip(
            "Cantidad máxima de ticks antes de la ventana de cancel " +
            "durante los que se puede guardar un input."
        )]
        [Min(0)] public int cancelBufferTicks = 2;

        [Header("Entrada")]
        [Tooltip(
            "Si se activa, el ataque puede interrumpir el Bunny. " +
            "Por defecto solo puede comenzar desde Idle."
        )]
        public bool allowBunnyCancel;

        [Header("Estados del Animator")]
        public PhaseAnimation anticipation =
            new PhaseAnimation("LightAnticipation");

        public PhaseAnimation smear =
            new PhaseAnimation("LightSmear");

        public PhaseAnimation pose =
            new PhaseAnimation("LightPose");

        public PhaseAnimation recovery =
            new PhaseAnimation("LightRecovery");

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

        public bool HasHitbox(AttackPhase phase) =>
            phase == AttackPhase.Smear && hitDuringSmear ||
            phase == AttackPhase.Pose && hitDuringPose;

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

        public bool IsCancelWindow(int poseElapsed)
        {
            return poseElapsed >= CancelStartTick &&
                   poseElapsed <= CancelEndTick;
        }

        // ================================================================
        // CANCEL BUFFER
        // ================================================================

        public bool IsCancelBufferWindow(int poseElapsed)
        {
            if (cancelBufferTicks <= 0)
                return false;

            int bufferStart =
                Mathf.Max(
                    1,
                    CancelStartTick - cancelBufferTicks);

            return poseElapsed >= bufferStart &&
                   poseElapsed < CancelStartTick;
        }

        // ================================================================
        // ANIMACIÓN
        // ================================================================

        public PhaseAnimation AnimationFor(AttackPhase phase)
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