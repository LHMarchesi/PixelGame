// Land of Fire · FUN-COM-001
// Detecta hurtboxes en los ticks activos del golpe L. Cada objetivo recibe un
// solo impacto por ejecución; no requiere un rival asignado ni usa Animation Events.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FighterMotor2D))]
    public sealed class FighterCombat2D : MonoBehaviour
    {
        [Header("Primer golpe: L")]
        public AttackMoveData lightAttack;
        [Tooltip("Incluir la layer donde se encuentran las hurtboxes.")]
        public LayerMask hurtboxLayers = ~0;

        public event Action<FighterMotor2D, AttackMoveData> HitConfirmed;
        private readonly HashSet<FighterMotor2D> hitThisAttack = new HashSet<FighterMotor2D>();
        private readonly List<Collider2D> overlapResults = new List<Collider2D>(8);
        private FighterMotor2D owner;
        private AttackMoveData currentMove;

        private void Awake() { owner = GetComponent<FighterMotor2D>(); }

        public AttackMoveData MoveFor(AttackCommand command) =>
            command == AttackCommand.Light ? lightAttack : null;

        public void BeginAttack(AttackMoveData move)
        {
            currentMove = move;
            hitThisAttack.Clear();
        }

        public void CancelAttack()
        {
            currentMove = null;
            hitThisAttack.Clear();
        }

        public void ResolveTick(BunnyStateMachine machine, int facing)
        {
            if (!machine.IsAttacking || machine.CurrentAttack == null)
            {
                CancelAttack();
                return;
            }

            AttackMoveData move = machine.CurrentAttack;
            if (currentMove != move) BeginAttack(move);
            if (!move.HasHitbox(machine.CurrentAttackPhase)) return;

            Vector2 offset = move.hitboxOffset;
            Vector2 center = owner.BodyPosition +
                new Vector2(offset.x * facing, offset.y);
            Vector2 size = new Vector2(Mathf.Max(.01f, move.hitboxSize.x), Mathf.Max(.01f, move.hitboxSize.y));
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(hurtboxLayers);
            overlapResults.Clear();
            Physics2D.OverlapBox(center, size, 0f, filter, overlapResults);

            foreach (Collider2D collider in overlapResults)
            {
                FighterHurtbox2D hurtbox = collider.GetComponent<FighterHurtbox2D>();
                if (hurtbox == null || !hurtbox.isActiveAndEnabled) continue;
                FighterMotor2D target = hurtbox.Owner;
                FighterHealth health = hurtbox.Health;
                if (target == null || target == owner || !target.isActiveAndEnabled ||
                    health == null || health.CurrentHealth <= 0 || hitThisAttack.Contains(target)) continue;

                if (!target.ReceiveConfirmedHit(move.hitstunTicks)) continue;
                hitThisAttack.Add(target);
                health.TakeDamage(move.damage);
                owner.ApplyHitstop(move.hitstopTicks);
                target.ApplyHitstop(move.hitstopTicks);
                HitConfirmed?.Invoke(target, move);
            }
        }

        private void OnDisable() { CancelAttack(); }

        private void OnDrawGizmosSelected()
        {
            if (lightAttack == null) return;
            FighterMotor2D fighter = owner != null ? owner : GetComponent<FighterMotor2D>();
            int direction = fighter != null ? fighter.Facing : 1;
            Vector2 offset = lightAttack.hitboxOffset;
            Vector3 center = transform.position + new Vector3(offset.x * direction, offset.y, 0);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, lightAttack.hitboxSize);
        }
    }
}
