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

        // Internal feed to the independent Combo System. Direction is the
        // attacker's facing captured with the resolved contact. This event is
        // emitted only for a valid hit/guard break, never for Block or Parry.
        internal event Action<FighterMotor2D, AttackMoveData, int> ValidImpactConfirmed;

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
        private int activeAutoComboImpactIndex = -1;
        private int activeRekkaNodeSerial = -1;
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
            activeAutoComboImpactIndex = -1;
            activeRekkaNodeSerial = -1;
            hitThisAttack.Clear();
            forceCurrentAttackLethal = false;
        }

        public void BeginAutoCombo()
        {
            currentMove = null;
            currentAttackIsMovesetSpecial = false;
            activeAutoComboImpactIndex = -1;
            activeRekkaNodeSerial = -1;
            hitThisAttack.Clear();
            targetsCollectedThisTick.Clear();
            forceCurrentAttackLethal = false;

            if (debugView != null)
                debugView.Hide();
        }

        public void BeginRekkaNode(int nodeSerial)
        {
            currentMove = null;
            currentAttackIsMovesetSpecial = false;
            activeAutoComboImpactIndex = -1;
            activeRekkaNodeSerial = nodeSerial;
            hitThisAttack.Clear();
            targetsCollectedThisTick.Clear();
            forceCurrentAttackLethal = false;

            if (debugView != null)
                debugView.Hide();
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
            activeAutoComboImpactIndex = -1;
            activeRekkaNodeSerial = -1;
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
            if (machine.IsAutoCombo && machine.CurrentAutoCombo != null)
            {
                CollectAutoComboHitCandidates(machine, facing, destination);
                return;
            }

            if (machine.IsRekka && machine.CurrentRekkaNode != null)
            {
                CollectRekkaHitCandidates(machine, facing, destination);
                return;
            }

            if (!machine.IsAttacking || machine.CurrentAttack == null)
            {
                if (currentMove != null || activeAutoComboImpactIndex >= 0 ||
                    activeRekkaNodeSerial >= 0)
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

            bool followsVisualHeight =
                (machine.IsAirAttacking && move.followAirborneHeight) ||
                (!machine.IsAirAttacking && move.useCustomGroundMovement);

            float attackHeightOffset = followsVisualHeight
                ? machine.VisualHeight
                : 0f;

            UpdateDebugView(move, hitboxActive, facing, attackHeightOffset);
            if (!hitboxActive)
                return;

            Vector2 offset = move.hitboxOffset;
            Vector2 center = owner.BodyPosition + new Vector2(
                offset.x * facing,
                offset.y + attackHeightOffset);

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

        private void CollectAutoComboHitCandidates(
            FighterStateMachine machine,
            int facing,
            List<FighterHitCandidate> destination)
        {
            int impactIndex = machine.CurrentAutoComboAttackIndex;
            AutoComboImpactData impact = machine.CurrentAutoComboAttack;
            int attackElapsedTick = machine.CurrentAutoComboAttackElapsedTick;

            if (impact == null || impact.attackMove == null ||
                machine.IsAutoComboTransitioning)
            {
                if (debugView != null)
                    debugView.Hide();
                return;
            }

            if (activeAutoComboImpactIndex != impactIndex)
            {
                activeAutoComboImpactIndex = impactIndex;
                hitThisAttack.Clear();
            }

            AttackMoveData move = impact.attackMove;
            bool hitboxActive = impact.IsHitboxActive(attackElapsedTick);
            float comboHeightOffset = machine.VisualHeight;
            UpdateDebugView(move, hitboxActive, facing, comboHeightOffset);
            if (!hitboxActive)
                return;

            Vector2 offset = move.hitboxOffset;
            Vector2 center = owner.BodyPosition + new Vector2(
                offset.x * facing,
                offset.y + comboHeightOffset);
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

                FighterHurtbox2D hurtbox = collider.GetComponent<FighterHurtbox2D>();
                if (hurtbox == null || !hurtbox.isActiveAndEnabled)
                    continue;

                FighterMotor2D target = hurtbox.Owner;
                FighterHealth health = hurtbox.Health;
                if (target == null || target == owner ||
                    !target.isActiveAndEnabled || health == null ||
                    health.CurrentHealth <= 0 || !target.CanReceiveCombatHit ||
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
                    // Parry and Telegraph timing is local to this attack,
                    // not to the complete Auto Combo timeline.
                    AttackElapsedTick = attackElapsedTick,
                    HoldChargeTicks = 0,
                    IsMovesetSpecial = false,
                    IsAutoCombo = true,
                    AutoComboImpactIndex = impactIndex,
                    TriggersComboKnockdown =
                        machine.CurrentAutoCombo.ShouldTriggerKnockdown(impactIndex) &&
                        !move.lethalHit,
                    ForceLethal = false
                });
            }
        }

        private void CollectRekkaHitCandidates(
            FighterStateMachine machine,
            int facing,
            List<FighterHitCandidate> destination)
        {
            RekkaNodeData node = machine.CurrentRekkaNode;
            AutoComboImpactData attack = node != null ? node.attack : null;

            if (attack == null || attack.attackMove == null)
            {
                if (debugView != null)
                    debugView.Hide();
                return;
            }

            int nodeSerial = machine.CurrentRekkaNodeSerial;
            if (activeRekkaNodeSerial != nodeSerial)
            {
                activeRekkaNodeSerial = nodeSerial;
                hitThisAttack.Clear();
            }

            AttackMoveData move = attack.attackMove;
            int attackElapsedTick = machine.CurrentRekkaNodeElapsedTick;
            bool hitboxActive = attack.IsHitboxActive(attackElapsedTick);
            float heightOffset = machine.VisualHeight;
            UpdateDebugView(move, hitboxActive, facing, heightOffset);

            if (!hitboxActive)
                return;

            Vector2 center = owner.BodyPosition + new Vector2(
                move.hitboxOffset.x * facing,
                move.hitboxOffset.y + heightOffset);
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

                FighterHurtbox2D hurtbox = collider.GetComponent<FighterHurtbox2D>();
                if (hurtbox == null || !hurtbox.isActiveAndEnabled)
                    continue;

                FighterMotor2D target = hurtbox.Owner;
                FighterHealth health = hurtbox.Health;
                if (target == null || target == owner ||
                    !target.isActiveAndEnabled || health == null ||
                    health.CurrentHealth <= 0 || !target.CanReceiveCombatHit ||
                    hitThisAttack.Contains(target) ||
                    !targetsCollectedThisTick.Add(target))
                    continue;

                destination.Add(new FighterHitCandidate
                {
                    Combat = this,
                    Attacker = owner,
                    Target = target,
                    Health = health,
                    Move = move,
                    Facing = facing,
                    AttackElapsedTick = attackElapsedTick,
                    HoldChargeTicks = 0,
                    IsMovesetSpecial = false,
                    IsRekka = true,
                    RekkaNodeSerial = nodeSerial,
                    ForceLethal = node.IsTerminal,
                    TriggersComboKnockdown =
                        machine.CurrentRekkaTree != null &&
                        machine.CurrentRekkaTree.ShouldTriggerKnockdown(
                            machine.CurrentRekkaNodeIndex) &&
                        !node.IsTerminal
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
                    ValidImpactConfirmed?.Invoke(target, move, candidate.Facing);
                    if (candidate.IsRekka)
                    {
                        owner.Machine.ResolveRekkaNodeOutcome(
                            candidate.RekkaNodeSerial,
                            RekkaNodeOutcome.Hit);
                    }
                    if (candidate.TriggersComboKnockdown)
                    {
                        FighterCombatResolver.QueueAutoComboKnockdown(
                            target, candidate.Facing);
                    }
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
                if (candidate.IsRekka)
                {
                    owner.Machine.ResolveRekkaNodeOutcome(
                        candidate.RekkaNodeSerial,
                        RekkaNodeOutcome.Block);
                }
                return true;
            }

            bool lethal = candidate.IsRekka
                ? candidate.ForceLethal
                : move.lethalHit || candidate.ForceLethal;
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
            candidate.Health.TakeDamage(move.damage + (lethal ? Mathf.Max(0, move.lethalHitBonusDamage) : 0));
            if (chargedHold && move.holdForwardDashDistance > 0f)
                owner.ApplyPushback(move.holdForwardDashDistance, candidate.Facing);
            owner.ApplyHitstop(move.hitstopTicks);
            target.ApplyHitstop(move.hitstopTicks);
            HitConfirmed?.Invoke(target, move);
            ValidImpactConfirmed?.Invoke(target, move, candidate.Facing);
            if (candidate.IsRekka)
            {
                owner.Machine.ResolveRekkaNodeOutcome(
                    candidate.RekkaNodeSerial,
                    RekkaNodeOutcome.Hit);

                if (lethal || candidate.TriggersComboKnockdown)
                    owner.Machine.EndRekkaRoute(candidate.RekkaNodeSerial);
            }
            if (candidate.TriggersComboKnockdown)
            {
                FighterCombatResolver.QueueAutoComboKnockdown(
                    target, candidate.Facing);
            }
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
            if (move == null)
                return new Color(.65f, .2f, 1f, .32f);

            switch (move.category)
            {
                case AttackCategory.Medium:
                    return new Color(1f, .48f, .08f, .32f);
                case AttackCategory.Heavy:
                    return new Color(.95f, .12f, .12f, .32f);
                default:
                    return new Color(.12f, .55f, 1f, .32f);
            }
        }

        private void OnDisable()
        {
            CancelAttack();
        }
    }
}
