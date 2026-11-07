// Land of Fire · Auto Combo sequence data.
// One AutoComboData is a single logical move made of consecutive attacks.
// Each attack can reuse AttackMoveData animations or provide its own Animator
// state sequence. Combat timing (hitbox / Parry / Telegraph / cancel) is local
// to each attack and independent from the selected animation mode.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace LandOfFire.BunnyStep
{
    [Flags]
    public enum AutoComboCancelTarget
    {
        None = 0,
        SingleMove = 1 << 0,
        AutoCombo = 1 << 1
    }

    public enum AutoComboAnimationMode
    {
        UseAttackAnimations,
        CustomAnimationSequence
    }

    public enum AutoComboAttackTimingMode
    {
        InheritAttackMove,
        Override
    }

    [Serializable]
    public sealed class AutoComboAnimationStateData
    {
        [Tooltip("Exact Animator state used for this part of the custom attack animation.")]
        public PhaseAnimation animation = new PhaseAnimation("AutoComboCustomState");

        [Min(1)]
        [Tooltip("How many 60 Hz ticks this state occupies. The clip is sampled across this duration.")]
        public int durationTicks = 4;

        [Tooltip("Total displacement reached by the end of this custom state. X is relative to the fighter facing; Y changes visual height. This is applied progressively over this state's ticks.")]
        public Vector2 movement;

        public int DurationTicks => Mathf.Max(1, durationTicks);
    }

    [CreateAssetMenu(menuName = "Land of Fire/Fighter/Auto Combo")]
    public sealed class AutoComboData : ScriptableObject
    {
        [Header("Sequential Attacks")]
        [Tooltip("Ordered list of attacks. Each entry is one attack/impact. List order defines the sequence; entries are no longer sorted by hitbox timing.")]
        [FormerlySerializedAs("impacts")]
        public List<AutoComboImpactData> attacks = new List<AutoComboImpactData>();

        // Source/API compatibility for project scripts that referenced the
        // V1.1 list by its former name. Unity serializes the `attacks` field.
        public List<AutoComboImpactData> impacts
        {
            get { return attacks; }
            set { attacks = value; }
        }

        [Header("Combo Knockdown")]
        [Tooltip("When enabled, a confirmed impact at the selected index forces the character's configured light Combo Knockdown. Blocked, parried or missed impacts do not trigger it. The global Combo Hit Limit always has priority.")]
        public bool causesKnockdown;

        [Min(1)]
        [Tooltip("1-based attack number that triggers the Combo Knockdown after it successfully connects.")]
        public int knockdownAfterImpactNumber = 3;

        // Kept hidden so already-created V1.1 assets can be upgraded without
        // Unity discarding their old serialized main-animation fields. The
        // sequence presentation no longer reads these legacy values.
        [HideInInspector] public PhaseAnimation animation = new PhaseAnimation("AutoCombo");
        [HideInInspector] public int durationTicks = 60;
        [HideInInspector] public int startupTicks = 8;

        public bool HasValidAttacks => CountValidAttacks() > 0;

        public int DurationTicks
        {
            get
            {
                int total = 0;
                int validAttackCount = CountValidAttacks();
                if (validAttackCount == 0)
                    return 1;

                int seen = 0;
                for (int i = 0; i < attacks.Count; i++)
                {
                    AutoComboImpactData attack = attacks[i];
                    if (attack == null || attack.attackMove == null)
                        continue;

                    total += attack.AnimationDurationTicks;
                    seen++;
                    if (seen < validAttackCount)
                        total += Mathf.Max(0, attack.transitionDelayTicks);
                }

                return Mathf.Max(1, total);
            }
        }

        // The initial cancel skip applies to the startup of the first attack.
        public int StartupTicks
        {
            get
            {
                if (attacks == null)
                    return 1;

                for (int i = 0; i < attacks.Count; i++)
                {
                    if (attacks[i] != null && attacks[i].attackMove != null)
                        return attacks[i].StartupTicks;
                }

                return 1;
            }
        }

        public bool ShouldTriggerKnockdown(int impactIndex)
        {
            return causesKnockdown &&
                   impactIndex + 1 == Mathf.Max(1, knockdownAfterImpactNumber);
        }

        /// <summary>
        /// Maps a global Auto Combo tick to an attack and its local tick.
        /// During a configured transition delay, returns the previous attack
        /// with its final animation frame and sets isTransition=true.
        /// </summary>
        public bool TryGetAttackAtTick(
            int timelineTick,
            out AutoComboImpactData attack,
            out int attackIndex,
            out int attackElapsedTick,
            out bool isTransition)
        {
            attack = null;
            attackIndex = -1;
            attackElapsedTick = 0;
            isTransition = false;

            if (attacks == null || timelineTick < 1)
                return false;

            int cursor = 1;
            int validAttackCount = CountValidAttacks();
            int seen = 0;

            for (int i = 0; i < attacks.Count; i++)
            {
                AutoComboImpactData current = attacks[i];
                if (current == null || current.attackMove == null)
                    continue;

                seen++;
                int currentDuration = current.AnimationDurationTicks;
                int currentEnd = cursor + currentDuration - 1;

                if (timelineTick >= cursor && timelineTick <= currentEnd)
                {
                    attack = current;
                    attackIndex = seen - 1;
                    attackElapsedTick = timelineTick - cursor + 1;
                    return true;
                }

                int nextCursor = currentEnd + 1;
                int delay = seen < validAttackCount
                    ? Mathf.Max(0, current.transitionDelayTicks)
                    : 0;

                if (delay > 0 && timelineTick >= nextCursor && timelineTick < nextCursor + delay)
                {
                    attack = current;
                    attackIndex = seen - 1;
                    attackElapsedTick = currentDuration;
                    isTransition = true;
                    return true;
                }

                cursor = nextCursor + delay;
            }

            return false;
        }

        public AutoComboImpactData FindCancelWindow(int timelineTick)
        {
            AutoComboImpactData attack;
            int attackIndex;
            int localTick;
            bool isTransition;
            if (!TryGetAttackAtTick(
                timelineTick,
                out attack,
                out attackIndex,
                out localTick,
                out isTransition) || isTransition || attack == null)
            {
                return null;
            }

            return attack.IsCancelWindow(localTick) ? attack : null;
        }

        public bool HasCancelWindowAfterTick(int timelineTick)
        {
            int end = DurationTicks;
            for (int tick = Mathf.Max(1, timelineTick); tick <= end; tick++)
            {
                if (FindCancelWindow(tick) != null)
                    return true;
            }

            return false;
        }

        public AttackMoveData TelegraphMoveAtTick(int timelineTick)
        {
            AutoComboImpactData attack;
            int attackIndex;
            int localTick;
            bool isTransition;
            if (!TryGetAttackAtTick(
                timelineTick,
                out attack,
                out attackIndex,
                out localTick,
                out isTransition) || isTransition ||
                attack == null || attack.attackMove == null)
            {
                return null;
            }

            return attack.attackMove.IsTelegraphActive(localTick)
                ? attack.attackMove
                : null;
        }

        private int CountValidAttacks()
        {
            if (attacks == null)
                return 0;

            int count = 0;
            for (int i = 0; i < attacks.Count; i++)
            {
                if (attacks[i] != null && attacks[i].attackMove != null)
                    count++;
            }

            return count;
        }

        private void OnValidate()
        {
            knockdownAfterImpactNumber = Mathf.Max(1, knockdownAfterImpactNumber);
            if (attacks == null)
                return;

            for (int i = 0; i < attacks.Count; i++)
            {
                AutoComboImpactData attack = attacks[i];
                if (attack == null)
                    continue;

                attack.hitboxStartTick = Mathf.Max(1, attack.hitboxStartTick);
                attack.hitboxDurationTicks = Mathf.Max(1, attack.hitboxDurationTicks);
                attack.transitionDelayTicks = Mathf.Max(0, attack.transitionDelayTicks);
                attack.cancelWindowStartTick = Mathf.Max(1, attack.cancelWindowStartTick);
                attack.cancelWindowEndTick = Mathf.Max(
                    attack.cancelWindowStartTick,
                    attack.cancelWindowEndTick);

                if (attack.customAnimationStates != null)
                {
                    for (int stateIndex = 0; stateIndex < attack.customAnimationStates.Count; stateIndex++)
                    {
                        AutoComboAnimationStateData state = attack.customAnimationStates[stateIndex];
                        if (state != null)
                            state.durationTicks = Mathf.Max(1, state.durationTicks);
                    }
                }
            }

            if (CountValidAttacks() > 0)
            {
                knockdownAfterImpactNumber = Mathf.Clamp(
                    knockdownAfterImpactNumber,
                    1,
                    CountValidAttacks());
            }
        }
    }

    [Serializable]
    public sealed class AutoComboImpactData
    {
        [Header("Attack")]
        [Tooltip("Combat data used by this strike: category, damage, pushback, Parry window, Telegraph, block rules and static hitbox dimensions/offset. Auto Combo uses the Hitbox Window configured below instead of AttackMoveData's normal hitbox timing fields.")]
        public AttackMoveData attackMove;

        [Header("Animation")]
        public AutoComboAnimationMode animationMode = AutoComboAnimationMode.UseAttackAnimations;

        [Tooltip("When using Attack Animations, inherit all four phase durations from AttackMoveData or override them only for this Auto Combo entry.")]
        public AutoComboAttackTimingMode timingMode = AutoComboAttackTimingMode.InheritAttackMove;

        [Min(1)] public int anticipationTicks = 4;
        [Min(1)] public int smearTicks = 1;
        [Min(1)] public int poseTicks = 1;
        [Min(1)] public int recoveryTicks = 8;

        [Tooltip("Used only when Animation Mode = Custom Animation Sequence. Add one or more Animator states; each state has its own duration in 60 Hz ticks.")]
        public List<AutoComboAnimationStateData> customAnimationStates =
            new List<AutoComboAnimationStateData>();

        [Header("Hitbox Window (attack-local ticks)")]
        [FormerlySerializedAs("impactTick")]
        [Min(1)]
        [Tooltip("First tick on this attack's local timeline when its static hitbox becomes active. Independent of animation states and phase durations.")]
        public int hitboxStartTick = 5;

        [Min(1)]
        [Tooltip("Number of consecutive attack-local ticks for which the hitbox remains active. Each target can still be hit only once by this attack.")]
        public int hitboxDurationTicks = 1;

        [Min(0)]
        [Tooltip("Optional delay after this attack finishes and before the next attack starts. 0 starts the next attack immediately on the following tick. Ignored after the final attack.")]
        public int transitionDelayTicks;

        [Header("Cancel (attack-local ticks)")]
        [Tooltip("Allow this attack's configured local-tick window to cancel into selected move types.")]
        public bool allowCancel;

        [Min(1)]
        public int cancelWindowStartTick = 12;

        [Min(1)]
        public int cancelWindowEndTick = 14;

        public AutoComboCancelTarget allowedCancelTargets =
            AutoComboCancelTarget.SingleMove | AutoComboCancelTarget.AutoCombo;

        [Range(0f, 100f)]
        [Tooltip("Percentage of the next move's startup that is skipped when this attack cancels into it.")]
        public float cancelStartupSkipPercent;

        public int AnticipationTicks =>
            timingMode == AutoComboAttackTimingMode.Override
                ? Mathf.Max(1, anticipationTicks)
                : attackMove != null ? attackMove.AnticipationTicks : 1;

        public int SmearTicks =>
            timingMode == AutoComboAttackTimingMode.Override
                ? Mathf.Max(1, smearTicks)
                : attackMove != null ? attackMove.SmearTicks : 1;

        public int PoseTicks =>
            timingMode == AutoComboAttackTimingMode.Override
                ? Mathf.Max(1, poseTicks)
                : attackMove != null ? attackMove.PoseTicks : 1;

        public int RecoveryTicks =>
            timingMode == AutoComboAttackTimingMode.Override
                ? Mathf.Max(1, recoveryTicks)
                : attackMove != null ? attackMove.RecoveryTicks : 1;

        public int AnimationDurationTicks
        {
            get
            {
                int animationTicks = animationMode == AutoComboAnimationMode.CustomAnimationSequence
                    ? CustomAnimationDurationTicks
                    : AnticipationTicks + SmearTicks + PoseTicks + RecoveryTicks;

                // Combat windows are independent from the selected animation.
                // If a window extends beyond the last configured animation tick,
                // keep this attack alive and hold the final animation frame.
                int combatEnd = animationTicks;

                if (attackMove != null && attackMove.useHitbox)
                {
                    int hitboxEnd = Mathf.Max(1, hitboxStartTick) +
                                    Mathf.Max(1, hitboxDurationTicks) - 1;
                    combatEnd = Mathf.Max(combatEnd, hitboxEnd);
                    combatEnd = Mathf.Max(combatEnd,
                        Mathf.Max(1, attackMove.parryWindowEndTick));
                }

                if (attackMove != null)
                {
                    if (attackMove.showTelegraph && attackMove.telegraphDurationTicks > 0)
                    {
                        int telegraphEnd = Mathf.Max(1, attackMove.telegraphStartTick) +
                                           attackMove.telegraphDurationTicks - 1;
                        combatEnd = Mathf.Max(combatEnd, telegraphEnd);
                    }
                }

                if (allowCancel)
                    combatEnd = Mathf.Max(combatEnd, Mathf.Max(1, cancelWindowEndTick));

                return Mathf.Max(1, Mathf.Max(animationTicks, combatEnd));
            }
        }

        public int StartupTicks
        {
            get
            {
                if (animationMode == AutoComboAnimationMode.CustomAnimationSequence)
                {
                    if (customAnimationStates != null)
                    {
                        for (int i = 0; i < customAnimationStates.Count; i++)
                        {
                            if (customAnimationStates[i] != null)
                                return customAnimationStates[i].DurationTicks;
                        }
                    }

                    return 1;
                }

                return AnticipationTicks;
            }
        }

        private int CustomAnimationDurationTicks
        {
            get
            {
                if (customAnimationStates == null || customAnimationStates.Count == 0)
                    return 1;

                int total = 0;
                for (int i = 0; i < customAnimationStates.Count; i++)
                {
                    if (customAnimationStates[i] != null)
                        total += customAnimationStates[i].DurationTicks;
                }

                return Mathf.Max(1, total);
            }
        }

        public bool IsHitboxActive(int attackElapsedTick)
        {
            if (attackMove == null || !attackMove.useHitbox)
                return false;

            int start = Mathf.Max(1, hitboxStartTick);
            int endExclusive = start + Mathf.Max(1, hitboxDurationTicks);
            return attackElapsedTick >= start && attackElapsedTick < endExclusive;
        }

        public bool TryGetAnimationAtTick(
            int attackElapsedTick,
            out PhaseAnimation animation,
            out int animationElapsedTick,
            out int animationDurationTicks,
            out AttackPhase phase)
        {
            animation = null;
            animationElapsedTick = 0;
            animationDurationTicks = 0;
            phase = AttackPhase.None;

            int localTick = Mathf.Clamp(attackElapsedTick, 1, AnimationDurationTicks);

            if (animationMode == AutoComboAnimationMode.CustomAnimationSequence)
            {
                if (customAnimationStates == null || customAnimationStates.Count == 0)
                    return false;

                int cursor = 1;
                AutoComboAnimationStateData lastValid = null;
                for (int i = 0; i < customAnimationStates.Count; i++)
                {
                    AutoComboAnimationStateData state = customAnimationStates[i];
                    if (state == null)
                        continue;

                    int duration = state.DurationTicks;
                    int end = cursor + duration - 1;
                    lastValid = state;

                    if (localTick >= cursor && localTick <= end)
                    {
                        animation = state.animation;
                        animationElapsedTick = localTick - cursor + 1;
                        animationDurationTicks = duration;
                        return animation != null;
                    }

                    cursor = end + 1;
                }

                if (lastValid != null)
                {
                    animation = lastValid.animation;
                    animationDurationTicks = lastValid.DurationTicks;
                    animationElapsedTick = animationDurationTicks;
                    return animation != null;
                }

                return false;
            }

            if (attackMove == null)
                return false;

            int anticipationEnd = AnticipationTicks;
            int smearEnd = anticipationEnd + SmearTicks;
            int poseEnd = smearEnd + PoseTicks;

            if (localTick <= anticipationEnd)
            {
                phase = AttackPhase.Anticipation;
                animationElapsedTick = localTick;
                animationDurationTicks = AnticipationTicks;
            }
            else if (localTick <= smearEnd)
            {
                phase = AttackPhase.Smear;
                animationElapsedTick = localTick - anticipationEnd;
                animationDurationTicks = SmearTicks;
            }
            else if (localTick <= poseEnd)
            {
                phase = AttackPhase.Pose;
                animationElapsedTick = localTick - smearEnd;
                animationDurationTicks = PoseTicks;
            }
            else
            {
                phase = AttackPhase.Recovery;
                animationElapsedTick = localTick - poseEnd;
                animationDurationTicks = RecoveryTicks;
            }

            animation = attackMove.AnimationFor(phase);
            return animation != null;
        }

        /// <summary>
        /// Returns this attack's cumulative local displacement at the given
        /// attack-local tick. Custom sequences use each custom state's own
        /// movement vector; reused AttackMoveData animations use its grounded
        /// Anticipation/Smear/Pose/Recovery movement settings.
        /// </summary>
        public Vector2 MovementOffsetAtTick(int attackElapsedTick)
        {
            int localTick = Mathf.Clamp(attackElapsedTick, 1, AnimationDurationTicks);

            if (animationMode == AutoComboAnimationMode.CustomAnimationSequence)
            {
                if (customAnimationStates == null || customAnimationStates.Count == 0)
                    return Vector2.zero;

                Vector2 accumulated = Vector2.zero;
                int cursor = 1;

                for (int i = 0; i < customAnimationStates.Count; i++)
                {
                    AutoComboAnimationStateData state = customAnimationStates[i];
                    if (state == null)
                        continue;

                    int stateDuration = state.DurationTicks;
                    int stateEnd = cursor + stateDuration - 1;

                    if (localTick > stateEnd)
                    {
                        accumulated += state.movement;
                        cursor = stateEnd + 1;
                        continue;
                    }

                    if (localTick >= cursor)
                    {
                        float progress = stateDuration <= 1
                            ? 1f
                            : Mathf.Clamp01((float)(localTick - cursor) / (stateDuration - 1));
                        return accumulated + state.movement * progress;
                    }

                    cursor = stateEnd + 1;
                }

                return accumulated;
            }

            if (attackMove == null || !attackMove.useCustomGroundMovement)
                return Vector2.zero;

            int anticipationEnd = AnticipationTicks;
            int smearEnd = anticipationEnd + SmearTicks;
            int poseEnd = smearEnd + PoseTicks;

            AttackPhase phase;
            int phaseElapsed;
            int phaseDuration;

            if (localTick <= anticipationEnd)
            {
                phase = AttackPhase.Anticipation;
                phaseElapsed = localTick;
                phaseDuration = AnticipationTicks;
            }
            else if (localTick <= smearEnd)
            {
                phase = AttackPhase.Smear;
                phaseElapsed = localTick - anticipationEnd;
                phaseDuration = SmearTicks;
            }
            else if (localTick <= poseEnd)
            {
                phase = AttackPhase.Pose;
                phaseElapsed = localTick - smearEnd;
                phaseDuration = PoseTicks;
            }
            else
            {
                phase = AttackPhase.Recovery;
                phaseElapsed = localTick - poseEnd;
                phaseDuration = RecoveryTicks;
            }

            return attackMove.GroundMovementOffset(
                phase,
                phaseElapsed,
                phaseDuration);
        }

        public bool IsCancelWindow(int attackElapsedTick)
        {
            return allowCancel &&
                   attackElapsedTick >= Mathf.Max(1, cancelWindowStartTick) &&
                   attackElapsedTick <= Mathf.Max(cancelWindowStartTick, cancelWindowEndTick);
        }

        public bool AllowsCancelTo(AutoComboCancelTarget target)
        {
            return (allowedCancelTargets & target) == target;
        }
    }
}
