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

        [Header("Entrada")]
        [Tooltip("Si se activa, L interrumpe el Bunny en cualquier fase. Por defecto solo desde Idle.")]
        public bool allowBunnyCancel;

        [Header("Estados del Animator")]
        public PhaseAnimation anticipation = new PhaseAnimation("LightAnticipation");
        public PhaseAnimation smear = new PhaseAnimation("LightSmear");
        public PhaseAnimation pose = new PhaseAnimation("LightPose");
        public PhaseAnimation recovery = new PhaseAnimation("LightRecovery");

        public int AnticipationTicks => Mathf.Max(1, anticipationTicks);
        public int SmearTicks => Mathf.Max(1, smearTicks);
        public int PoseTicks => Mathf.Max(1, poseTicks);
        public int RecoveryTicks => Mathf.Max(1, recoveryTicks);
        public int TotalTicks => AnticipationTicks + SmearTicks + PoseTicks + RecoveryTicks;

        public bool HasHitbox(AttackPhase phase) =>
            phase == AttackPhase.Smear && hitDuringSmear ||
            phase == AttackPhase.Pose && hitDuringPose;

        public PhaseAnimation AnimationFor(AttackPhase phase)
        {
            switch (phase)
            {
                case AttackPhase.Anticipation: return anticipation;
                case AttackPhase.Smear: return smear;
                case AttackPhase.Pose: return pose;
                case AttackPhase.Recovery: return recovery;
                default: return null;
            }
        }
    }
}
