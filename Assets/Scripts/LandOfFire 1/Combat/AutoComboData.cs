// Land of Fire · Auto Combo timeline data.
// One AutoComboData represents one continuous animation containing multiple
// independently configured impact moments. Each impact reuses AttackMoveData
// for its static hitbox and combat properties.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [Flags]
    public enum AutoComboCancelTarget
    {
        None = 0,
        SingleMove = 1 << 0,
        AutoCombo = 1 << 1
    }

    [CreateAssetMenu(menuName = "Land of Fire/Fighter/Auto Combo")]
    public sealed class AutoComboData : ScriptableObject
    {
        [Header("Animation")]
        [Tooltip("Single Animator state used for the complete Auto Combo. The animation is sampled over Duration Ticks.")]
        public PhaseAnimation animation = new PhaseAnimation("AutoCombo");

        [Min(1)]
        [Tooltip("Complete duration of the Auto Combo animation, measured in 60 Hz ticks.")]
        public int durationTicks = 60;

        [Min(0)]
        [Tooltip("Initial portion of the animation that can be skipped when this Auto Combo is entered through a cancel. Normal neutral activation always starts at tick 0.")]
        public int startupTicks = 8;

        [Header("Impacts")]
        [Tooltip("Each entry is one impact moment. The referenced AttackMoveData supplies category, static hitbox, damage, pushback, Block, Parry, Telegraph and optional Lethal Hit properties. Impact Tick uses the Auto Combo timeline.")]
        public List<AutoComboImpactData> impacts = new List<AutoComboImpactData>();

        [Header("Combo Knockdown")]
        [Tooltip("When enabled, a confirmed impact at the selected index forces the character's configured light Combo Knockdown. Blocked, parried or missed impacts do not trigger it. The global Combo Hit Limit always has priority.")]
        public bool causesKnockdown;

        [Min(1)]
        [Tooltip("1-based impact number that triggers the Combo Knockdown after it successfully connects.")]
        public int knockdownAfterImpactNumber = 3;

        public int DurationTicks => Mathf.Max(1, durationTicks);
        public int StartupTicks => Mathf.Clamp(startupTicks, 0, DurationTicks - 1);

        public bool ShouldTriggerKnockdown(int impactIndex)
        {
            return causesKnockdown &&
                   impactIndex + 1 == Mathf.Max(1, knockdownAfterImpactNumber);
        }

        public AutoComboImpactData FindImpactAtTick(int timelineTick, out int impactIndex)
        {
            impactIndex = -1;
            if (impacts == null)
                return null;

            for (int i = 0; i < impacts.Count; i++)
            {
                AutoComboImpactData impact = impacts[i];
                if (impact == null || impact.attackMove == null)
                    continue;

                if (Mathf.Max(1, impact.impactTick) == timelineTick)
                {
                    impactIndex = i;
                    return impact;
                }
            }

            return null;
        }

        public AutoComboImpactData FindCancelWindow(int timelineTick)
        {
            if (impacts == null)
                return null;

            // Later impacts take precedence if windows overlap.
            for (int i = impacts.Count - 1; i >= 0; i--)
            {
                AutoComboImpactData impact = impacts[i];
                if (impact != null && impact.IsCancelWindow(timelineTick))
                    return impact;
            }

            return null;
        }

        public bool HasCancelWindowAfterTick(int timelineTick)
        {
            if (impacts == null)
                return false;

            for (int i = 0; i < impacts.Count; i++)
            {
                AutoComboImpactData impact = impacts[i];
                if (impact == null || !impact.allowCancel)
                    continue;

                if (impact.cancelWindowEndTick >= timelineTick)
                    return true;
            }

            return false;
        }

        public AttackMoveData TelegraphMoveAtTick(int timelineTick)
        {
            if (impacts == null)
                return null;

            for (int i = 0; i < impacts.Count; i++)
            {
                AutoComboImpactData impact = impacts[i];
                if (impact != null && impact.attackMove != null &&
                    impact.attackMove.IsTelegraphActive(timelineTick))
                {
                    return impact.attackMove;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            durationTicks = Mathf.Max(1, durationTicks);
            startupTicks = Mathf.Clamp(startupTicks, 0, durationTicks - 1);
            knockdownAfterImpactNumber = Mathf.Max(1, knockdownAfterImpactNumber);

            if (impacts == null)
                return;

            for (int i = 0; i < impacts.Count; i++)
            {
                if (impacts[i] == null)
                    continue;

                impacts[i].impactTick = Mathf.Clamp(
                    impacts[i].impactTick,
                    1,
                    durationTicks);

                impacts[i].cancelWindowStartTick = Mathf.Clamp(
                    impacts[i].cancelWindowStartTick,
                    1,
                    durationTicks);

                impacts[i].cancelWindowEndTick = Mathf.Clamp(
                    impacts[i].cancelWindowEndTick,
                    impacts[i].cancelWindowStartTick,
                    durationTicks);
            }

            impacts.Sort((a, b) =>
            {
                if (object.ReferenceEquals(a, b)) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return a.impactTick.CompareTo(b.impactTick);
            });

            if (impacts.Count > 0)
            {
                knockdownAfterImpactNumber = Mathf.Clamp(
                    knockdownAfterImpactNumber,
                    1,
                    impacts.Count);
            }

            for (int i = 1; i < impacts.Count; i++)
            {
                AutoComboImpactData previous = impacts[i - 1];
                AutoComboImpactData current = impacts[i];
                if (previous != null && current != null &&
                    previous.impactTick == current.impactTick)
                {
                    Debug.LogWarning(
                        $"[{name}] Auto Combo has more than one impact at tick " +
                        $"{current.impactTick}. Only the first will be evaluated; " +
                        "use unique impact ticks.",
                        this);
                }
            }
        }
    }

    [Serializable]
    public sealed class AutoComboImpactData
    {
        [Min(1)]
        [Tooltip("Exact tick on the shared Auto Combo animation at which this impact becomes active.")]
        public int impactTick = 12;

        [Tooltip("Combat data for this impact. Configure one static hitbox in this AttackMoveData asset. Its phase timings are not used by Auto Combos; Telegraph and Parry ticks are relative to the Auto Combo timeline.")]
        public AttackMoveData attackMove;

        [Header("Cancel")]
        [Tooltip("Allow this impact's configured window to cancel into selected move types.")]
        public bool allowCancel;

        [Min(1)]
        [Tooltip("First Auto Combo timeline tick in which a cancel can be entered.")]
        public int cancelWindowStartTick = 12;

        [Min(1)]
        [Tooltip("Last Auto Combo timeline tick in which a cancel can be entered.")]
        public int cancelWindowEndTick = 14;

        public AutoComboCancelTarget allowedCancelTargets =
            AutoComboCancelTarget.SingleMove | AutoComboCancelTarget.AutoCombo;

        [Range(0f, 100f)]
        [Tooltip("Percentage of the next move's startup that is skipped when this impact cancels into it.")]
        public float cancelStartupSkipPercent;

        public bool IsCancelWindow(int timelineTick)
        {
            return allowCancel &&
                   timelineTick >= Mathf.Max(1, cancelWindowStartTick) &&
                   timelineTick <= Mathf.Max(cancelWindowStartTick, cancelWindowEndTick);
        }

        public bool AllowsCancelTo(AutoComboCancelTarget target)
        {
            return (allowedCancelTargets & target) == target;
        }
    }
}
