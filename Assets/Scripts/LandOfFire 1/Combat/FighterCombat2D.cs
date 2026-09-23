// Land of Fire · FUN-COM-001
// Detecta hurtboxes en los ticks activos de cualquier ataque.
// Cada objetivo recibe un solo impacto por ejecución.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FighterMotor2D))]
    public sealed class FighterCombat2D : MonoBehaviour
    {
        [Header("Ataques")]

        public AttackMoveData lightAttack;
        public AttackMoveData mediumAttack;
        public AttackMoveData heavyAttack;

        [Tooltip(
            "Incluir la layer donde se encuentran las hurtboxes."
        )]
        public LayerMask hurtboxLayers = ~0;

        public event Action<
            FighterMotor2D,
            AttackMoveData> HitConfirmed;

        private readonly HashSet<FighterMotor2D>
            hitThisAttack =
            new HashSet<FighterMotor2D>();

        private readonly List<Collider2D>
            overlapResults =
            new List<Collider2D>(8);

        private FighterMotor2D owner;

        private AttackMoveData currentMove;

        private FighterAttackDebugView debugView;

        private bool forceCurrentAttackLethal;

        private void Awake()
        {
            owner =
                GetComponent<FighterMotor2D>();

            debugView =
                GetComponent<FighterAttackDebugView>();

            if (debugView == null)
            {
                debugView =
                    gameObject.AddComponent<
                        FighterAttackDebugView>();
            }
        }

        public AttackMoveData MoveFor(
            AttackCommand command)
        {
            switch (command)
            {
                case AttackCommand.Light:
                    return lightAttack;

                case AttackCommand.Medium:
                    return mediumAttack;

                case AttackCommand.Heavy:
                    return heavyAttack;

                default:
                    return null;
            }
        }

        public void BeginAttack(
            AttackMoveData move)
        {
            currentMove =
                move;

            hitThisAttack.Clear();

            forceCurrentAttackLethal =
                false;
        }

        public void SetCurrentAttackLethal(
            bool lethal)
        {
            forceCurrentAttackLethal =
                lethal;
        }

        public void CancelAttack()
        {
            currentMove = null;

            hitThisAttack.Clear();

            forceCurrentAttackLethal =
                false;

            if (debugView != null)
                debugView.Hide();
        }

        public void ResolveTick(
            FighterStateMachine machine,
            int facing)
        {
            if (!machine.IsAttacking ||
                machine.CurrentAttack == null)
            {
                CancelAttack();
                return;
            }

            AttackMoveData move =
                machine.CurrentAttack;

            if (currentMove != move)
                BeginAttack(move);

            // ============================================================
            // DEBUG
            // ============================================================

            UpdateDebugView(
                move,
                machine.CurrentAttackPhase,
                facing);

            // ============================================================
            // HITBOX
            // ============================================================

            if (!move.HasHitbox(
                    machine.CurrentAttackPhase))
            {
                return;
            }

            Vector2 offset =
                move.hitboxOffset;

            Vector2 center =
                owner.BodyPosition +
                new Vector2(
                    offset.x * facing,
                    offset.y);

            Vector2 size =
                new Vector2(
                    Mathf.Max(
                        .01f,
                        move.hitboxSize.x),
                    Mathf.Max(
                        .01f,
                        move.hitboxSize.y));

            var filter =
                new ContactFilter2D
                {
                    useTriggers = true
                };

            filter.SetLayerMask(
                hurtboxLayers);

            overlapResults.Clear();

            Physics2D.OverlapBox(
                center,
                size,
                0f,
                filter,
                overlapResults);

            foreach (
                Collider2D collider
                in overlapResults)
            {
                FighterHurtbox2D hurtbox =
                    collider.GetComponent<
                        FighterHurtbox2D>();

                if (hurtbox == null ||
                    !hurtbox.isActiveAndEnabled)
                {
                    continue;
                }

                FighterMotor2D target =
                    hurtbox.Owner;

                FighterHealth health =
                    hurtbox.Health;

                if (target == null ||
                    target == owner ||
                    !target.isActiveAndEnabled ||
                    health == null ||
                    health.CurrentHealth <= 0 ||
                    hitThisAttack.Contains(target))
                {
                    continue;
                }

                // ========================================================
                // GUARD
                // ========================================================

                bool blocked =
                    target.GuardRequested;

                if (blocked)
                {
                    target.ApplyPushback(
                        move.guardPushback,
                        facing);

                    owner.ApplyHitstop(
                        move.hitstopTicks);

                    target.ApplyHitstop(
                        move.hitstopTicks);

                    hitThisAttack.Add(
                        target);

                    continue;
                }

                // ========================================================
                // LETHAL
                // ========================================================

                bool lethal =
                    move.lethalHit ||
                    forceCurrentAttackLethal;

                bool accepted;

                if (lethal)
                {
                    accepted =
                        target.ReceiveFlyingHurt(
                            move.flyingHurtTicks,
                            move.flyingHurtDistance,
                            move.flyingHurtHeight,
                            facing,
                            move.lethalHitKnockdownTicks);
                }
                else
                {
                    accepted =
                        target.ReceiveConfirmedHit(
                            move.HurtTicks);

                    if (accepted)
                    {
                        target.ApplyPushback(
                            move.hitPushback,
                            facing);
                    }
                }

                if (!accepted)
                    continue;

                hitThisAttack.Add(
                    target);

                health.TakeDamage(
                    move.damage);

                owner.ApplyHitstop(
                    move.hitstopTicks);

                target.ApplyHitstop(
                    move.hitstopTicks);

                HitConfirmed?.Invoke(
                    target,
                    move);
            }
        }

        // ================================================================
        // DEBUG
        // ================================================================

        private void UpdateDebugView(
            AttackMoveData move,
            AttackPhase phase,
            int facing)
        {
            if (debugView == null)
                return;

            if (!move.showHitboxDebug ||
                !move.HasHitbox(phase))
            {
                debugView.Hide();
                return;
            }

            debugView.Show(
                move,
                facing,
                DebugColorFor(move));
        }

        private Color DebugColorFor(
            AttackMoveData move)
        {
            if (move == lightAttack)
            {
                return new Color(
                    0.15f,
                    0.85f,
                    1f,
                    0.32f);
            }

            if (move == mediumAttack)
            {
                return new Color(
                    1f,
                    0.85f,
                    0.15f,
                    0.32f);
            }

            if (move == heavyAttack)
            {
                return new Color(
                    1f,
                    0.2f,
                    0.15f,
                    0.32f);
            }

            // Cualquier AttackMoveData que no sea
            // Light / Medium / Heavy base se considera
            // un ataque especial.
            return new Color(
                0.65f,
                0.2f,
                1f,
                0.32f);
        }

        private void OnDisable()
        {
            CancelAttack();
        }
    }
}