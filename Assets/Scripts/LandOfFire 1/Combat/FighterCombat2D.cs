// Land of Fire · FUN-COM-001
// Construye contactos de hitbox por tick. La resolución se delega al
// FighterCombatResolver compartido para que los intercambios simultáneos
// no dependan del orden de FixedUpdate de cada luchador.

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

        [Header("Ataques aéreos")]
        [Tooltip("Si no se asigna un ataque aéreo, se reutiliza el ataque terrestre correspondiente.")]
        public AttackMoveData airLightAttack;
        public AttackMoveData airMediumAttack;
        public AttackMoveData airHeavyAttack;

        [Tooltip("Incluir la layer donde se encuentran las hurtboxes.")]
        public LayerMask hurtboxLayers = ~0;

        [Header("Clash")]
        [Min(0)]
        [Tooltip("Máxima diferencia entre los ticks de impacto para permitir Clash. 0 exige el mismo tick. Para un intercambio, se utiliza el menor valor configurado entre ambos luchadores.")]
        public int clashTimingToleranceTicks = 2;

        [Header("Parry")]
        [Min(0)]
        [Tooltip("Cantidad inicial de slots de Parry disponibles al iniciar el luchador. El sistema de recursos puede añadir slots durante la partida.")]
        public int startingParrySlots = 2;

        [Min(1)]
        [Tooltip("Duración en ticks de la animación/estado Parry al pulsar P. La validación defensiva sigue siendo únicamente en el tick de input.")]
        public int parryAttemptTicks = 6;

        [Min(0)]
        [Tooltip("Recuperación del defensor después de un Parry imperfecto. 0 = control inmediato tras la animación Parry.")]
        public int parryRecoveryTicks = 4;

        [Min(1)]
        [Tooltip("Duración de ParryWhiff después de terminar la animación Parry sin una confirmación válida.")]
        public int parryWhiffRecoveryTicks = 18;

        public int AvailableParrySlots => availableParrySlots;
        public int ClashTimingToleranceTicks => Mathf.Max(0, clashTimingToleranceTicks);

        public event Action<FighterMotor2D, AttackMoveData> HitConfirmed;

        private readonly HashSet<FighterMotor2D> hitThisAttack =
            new HashSet<FighterMotor2D>();

        private readonly HashSet<FighterMotor2D> targetsCollectedThisTick =
            new HashSet<FighterMotor2D>();

        private readonly List<Collider2D> overlapResults =
            new List<Collider2D>(8);

        private FighterMotor2D owner;
        private AttackMoveData currentMove;
        private FighterAttackDebugView debugView;
        private bool forceCurrentAttackLethal;
        private bool currentAttackIsMovesetSpecial;
        private int availableParrySlots;

        private void Awake()
        {
            owner = GetComponent<FighterMotor2D>();
            availableParrySlots = Mathf.Max(0, startingParrySlots);
            debugView = GetComponent<FighterAttackDebugView>();

            if (debugView == null)
                debugView = gameObject.AddComponent<FighterAttackDebugView>();
        }

        public void AddParrySlots(int amount)
        {
            if (amount <= 0)
                return;

            availableParrySlots = Mathf.Max(0, availableParrySlots + amount);
        }

        public void SetParrySlots(int amount)
        {
            availableParrySlots = Mathf.Max(0, amount);
        }

        public void ResetParrySlots()
        {
            availableParrySlots = Mathf.Max(0, startingParrySlots);
        }

        internal bool CanSpendParrySlots(int amount)
        {
            return amount >= 0 && availableParrySlots >= amount;
        }

        internal bool TrySpendParrySlots(int amount)
        {
            if (!CanSpendParrySlots(amount))
                return false;

            availableParrySlots -= amount;
            return true;
        }

        public AttackMoveData MoveFor(AttackCommand command)
        {
            return MoveFor(command, false);
        }

        public AttackMoveData MoveFor(AttackCommand command, bool airborne)
        {
            if (airborne)
            {
                AttackMoveData aerial = null;
                switch (command)
                {
                    case AttackCommand.Light: aerial = airLightAttack; break;
                    case AttackCommand.Medium: aerial = airMediumAttack; break;
                    case AttackCommand.Heavy: aerial = airHeavyAttack; break;
                }

                if (aerial != null)
                    return aerial;
            }

            switch (command)
            {
                case AttackCommand.Light: return lightAttack;
                case AttackCommand.Medium: return mediumAttack;
                case AttackCommand.Heavy: return heavyAttack;
                default: return null;
            }
        }

        public void BeginAttack(AttackMoveData move, bool isMovesetSpecial = false)
        {
            currentMove = move;
            currentAttackIsMovesetSpecial = isMovesetSpecial;
            hitThisAttack.Clear();
            forceCurrentAttackLethal = false;
        }

        public void SetCurrentAttackLethal(bool lethal)
        {
            forceCurrentAttackLethal = lethal;
        }

        internal void RegisterPendingClashTarget(FighterMotor2D target)
        {
            if (target != null)
                hitThisAttack.Add(target);
        }

        public void CancelAttack()
        {
            currentMove = null;
            currentAttackIsMovesetSpecial = false;
            hitThisAttack.Clear();
            targetsCollectedThisTick.Clear();
            forceCurrentAttackLethal = false;

            if (debugView != null)
                debugView.Hide();
        }

        /// <summary>
        /// Detecta contactos válidos de la hitbox actual. No aplica daño ni
        /// cambia estados: todos los luchadores deben terminar esta fase
        /// antes de que el resolver decida el resultado del tick.
        /// </summary>
        internal void CollectHitCandidates(
            FighterStateMachine machine,
            int facing,
            List<FighterHitCandidate> destination)
        {
            if (!machine.IsAttacking || machine.CurrentAttack == null)
            {
                if (currentMove != null)
                    CancelAttack();
                return;
            }

            AttackMoveData move = machine.CurrentAttack;
            if (currentMove != move)
                BeginAttack(move);

            int combatElapsedTick = machine.CombatAttackElapsedTick;
            bool hitboxActive = move.HasHitbox(
                machine.CurrentAttackPhase,
                combatElapsedTick,
                machine.IsAirAttacking);

            float airborneHeight =
                machine.IsAirAttacking && move.followAirborneHeight
                    ? machine.VisualHeight
                    : 0f;

            UpdateDebugView(move, hitboxActive, facing, airborneHeight);
            if (!hitboxActive)
                return;

            Vector2 offset = move.hitboxOffset;
            Vector2 center = owner.BodyPosition + new Vector2(
                offset.x * facing,
                offset.y + airborneHeight);

            Vector2 size = new Vector2(
                Mathf.Max(.01f, move.hitboxSize.x),
                Mathf.Max(.01f, move.hitboxSize.y));

            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(hurtboxLayers);
            overlapResults.Clear();
            targetsCollectedThisTick.Clear();

            Physics2D.OverlapBox(center, size, 0f, filter, overlapResults);

            foreach (Collider2D collider in overlapResults)
            {
                if (collider == null)
                    continue;

                FighterHurtbox2D hurtbox =
                    collider.GetComponent<FighterHurtbox2D>();

                if (hurtbox == null || !hurtbox.isActiveAndEnabled)
                    continue;

                FighterMotor2D target = hurtbox.Owner;
                FighterHealth health = hurtbox.Health;

                if (target == null || target == owner ||
                    !target.isActiveAndEnabled || health == null ||
                    health.CurrentHealth <= 0 ||
                    !target.CanReceiveCombatHit ||
                    hitThisAttack.Contains(target) ||
                    !targetsCollectedThisTick.Add(target))
                {
                    continue;
                }

                destination.Add(new FighterHitCandidate
                {
                    Combat = this,
                    Attacker = owner,
                    Target = target,
                    Health = health,
                    Move = move,
                    Facing = facing,
                    AttackElapsedTick = combatElapsedTick,
                    HoldChargeTicks = machine.HoldChargeTicks,
                    IsMovesetSpecial = currentAttackIsMovesetSpecial,
                    ForceLethal = forceCurrentAttackLethal
                });
            }
        }

        /// <summary>
        /// Aplica un contacto que ya fue seleccionado por el resolver.
        /// allowBlock=false se usa cuando una prioridad simultánea determina
        /// que el golpe ganador conecta como Hit, sin posibilidad de Guard.
        /// </summary>
        internal bool ResolveContact(
            FighterHitCandidate candidate,
            bool allowBlock)
        {
            // Candidate validity was snapshotted before any result from this
            // tick was applied. Do not re-check the attacker's state here:
            // a reciprocal contact can legitimately resolve as a trade when
            // neither Clash nor category priority applies.
            if (candidate.Attacker != owner ||
                candidate.Move == null ||
                candidate.Target == null ||
                !candidate.Target.isActiveAndEnabled ||
                candidate.Health == null ||
                candidate.Health.CurrentHealth <= 0)
            {
                return false;
            }

            AttackMoveData move = candidate.Move;
            FighterMotor2D target = candidate.Target;

            bool chargedHold = IsChargedHold(candidate);

            if (allowBlock && move.canBeBlocked &&
                target.IsHoldingBackAgainst(owner))
            {
                bool causesGuardBreak = chargedHold &&
                    move.blockBreakRequiredChargeTicks > 0 &&
                    candidate.HoldChargeTicks >= Mathf.Max(
                        move.HoldMinChargeTicks,
                        move.blockBreakRequiredChargeTicks);

                if (causesGuardBreak)
                {
                    bool broken = target.ReceiveGuardBreak(
                        move.guardBreakDurationTicks);
                    if (!broken)
                        return false;

                    candidate.Health.TakeDamage(move.damage + move.guardBreakDamage);
                    target.ApplyPushback(
                        move.guardPushback + move.guardBreakPushback,
                        candidate.Facing);
                    owner.ApplyHitstop(move.hitstopTicks);
                    target.ApplyHitstop(move.hitstopTicks);
                    hitThisAttack.Add(target);
                    HitConfirmed?.Invoke(target, move);
                    CombatFeedbackController.TriggerGuardBreak(target);
                    return true;
                }

                bool blocked = target.ReceiveBlockedHit(move.blockRecoveryTicks);
                if (!blocked)
                    return false;

                target.ApplyPushback(move.guardPushback, candidate.Facing);
                owner.ApplyHitstop(move.hitstopTicks);
                target.ApplyHitstop(move.hitstopTicks);
                hitThisAttack.Add(target);
                return true;
            }

            bool lethal = move.lethalHit || candidate.ForceLethal;
            bool accepted;

            if (lethal)
            {
                accepted = target.ReceiveFlyingHurt(
                    move.flyingHurtTicks,
                    move.flyingHurtDistance,
                    move.flyingHurtHeight,
                    candidate.Facing,
                    move.lethalHitKnockdownTicks);
            }
            else
            {
                accepted = target.ReceiveConfirmedHit(move.HurtTicks);
                if (accepted)
                {
                    float extraHoldPushback = chargedHold
                        ? move.holdHitPushback
                        : 0f;
                    target.ApplyPushback(
                        move.hitPushback + extraHoldPushback,
                        candidate.Facing);
                }
            }

            if (!accepted)
                return false;

            hitThisAttack.Add(target);
            candidate.Health.TakeDamage(move.damage);
            if (chargedHold && move.holdForwardDashDistance > 0f)
                owner.ApplyPushback(move.holdForwardDashDistance, candidate.Facing);
            owner.ApplyHitstop(move.hitstopTicks);
            target.ApplyHitstop(move.hitstopTicks);
            HitConfirmed?.Invoke(target, move);
            return true;
        }

        private static bool IsChargedHold(FighterHitCandidate candidate)
        {
            return candidate.Move != null &&
                   candidate.Move.allowHoldAttack &&
                   candidate.HoldChargeTicks >= candidate.Move.HoldMinChargeTicks;
        }

        internal void RegisterParriedTarget(FighterMotor2D target)
        {
            if (target != null)
                hitThisAttack.Add(target);
        }

        internal bool CanClash(FighterHitCandidate candidate)
        {
            return !candidate.IsMovesetSpecial &&
                   candidate.Move != null &&
                   candidate.Move.ResolvedClashGroup != AttackClashGroup.None;
        }

        private void UpdateDebugView(
            AttackMoveData move,
            bool hitboxActive,
            int facing,
            float airborneHeight)
        {
            if (debugView == null)
                return;

            if (!move.showHitboxDebug || !hitboxActive)
            {
                debugView.Hide();
                return;
            }

            debugView.Show(move, facing, airborneHeight, DebugColorFor(move));
        }

        private Color DebugColorFor(AttackMoveData move)
        {
            if (move == lightAttack)
                return new Color(.15f, .85f, 1f, .32f);
            if (move == mediumAttack)
                return new Color(1f, .85f, .15f, .32f);
            if (move == heavyAttack)
                return new Color(1f, .2f, .15f, .32f);

            return new Color(.65f, .2f, 1f, .32f);
        }

        private void OnDisable()
        {
            CancelAttack();
        }
    }
}
