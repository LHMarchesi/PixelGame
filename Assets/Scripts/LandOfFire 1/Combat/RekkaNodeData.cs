// Land of Fire · Rekka node and branch configuration.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [CreateAssetMenu(menuName = "Land of Fire/Fighter/Rekka Node")]
    public sealed class RekkaNodeData : ScriptableObject
    {
        [Header("Attack")]
        [Tooltip("Combat, hitbox, animation and X/Y movement configuration for this node. Uses the same data model as an Auto Combo attack.")]
        public AutoComboImpactData attack = new AutoComboImpactData();

        /// <summary>
        /// A Rekka node is usable only when it has combat data to execute.
        /// The attack move is required by the combat resolver; a node without
        /// it must not be offered as a branch destination or used as the root.
        /// </summary>
        public bool HasValidNode => attack != null && attack.attackMove != null;

        [Header("Branches (maximum 2)")]
        [Tooltip("Each branch is available only when its outcome condition matches the result of this node. A branch command must be completed during the configured Pose continuation window.")]
        public List<RekkaBranchData> branches = new List<RekkaBranchData>(2);

        public bool IsTerminal
        {
            get
            {
                if (branches == null)
                    return true;

                for (int i = 0; i < Mathf.Min(2, branches.Count); i++)
                {
                    if (branches[i] != null && branches[i].targetNode != null &&
                        branches[i].targetNode.HasValidNode &&
                        branches[i].sequence != null && branches[i].sequence.Count > 0)
                        return false;
                }

                return true;
            }
        }

        public int DurationTicks =>
            attack != null ? attack.RekkaDurationTicks : 1;

        public bool ContainsBranch(RekkaBranchData branch)
        {
            if (branch == null || branches == null)
                return false;

            int count = Mathf.Min(2, branches.Count);
            for (int i = 0; i < count; i++)
            {
                if (object.ReferenceEquals(branches[i], branch))
                    return true;
            }

            return false;
        }

        public void GetBranchesForOutcome(
            RekkaNodeOutcome outcome,
            List<RekkaBranchData> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            if (branches == null)
                return;

            int count = Mathf.Min(2, branches.Count);
            for (int i = 0; i < count; i++)
            {
                RekkaBranchData branch = branches[i];
                if (branch == null || branch.targetNode == null ||
                    !branch.targetNode.HasValidNode ||
                    branch.sequence == null || branch.sequence.Count == 0)
                    continue;

                if (outcome == RekkaNodeOutcome.Unknown ||
                    branch.IsAvailableFor(outcome))
                {
                    destination.Add(branch);
                }
            }
        }

        private void OnValidate()
        {
            if (attack == null)
                attack = new AutoComboImpactData();

            if (branches == null)
                branches = new List<RekkaBranchData>(2);

            if (branches.Count > 2)
            {
                branches.RemoveRange(2, branches.Count - 2);
                Debug.LogWarning(
                    "[RekkaNodeData] A Rekka node supports a maximum of two branches. Extra branches were removed.",
                    this);
            }

            if (attack.customAnimationStates != null)
            {
                for (int i = 0; i < attack.customAnimationStates.Count; i++)
                {
                    AutoComboAnimationStateData state = attack.customAnimationStates[i];
                    if (state != null)
                        state.durationTicks = Mathf.Max(1, state.durationTicks);
                }
            }
        }
    }

    [Serializable]
    public sealed class RekkaBranchData
    {
        [Header("Command")]
        public string branchName = "Branch";
        public List<FighterInputToken> sequence = new List<FighterInputToken>();
        public List<FighterInputDirectionRequirement> directionRequirements =
            new List<FighterInputDirectionRequirement>();

        [Min(0)]
        [Tooltip("Higher priority wins if two branches use the same command.")]
        public int priority;

        [Header("Conditions")]
        [Tooltip("Allow this branch when the current node successfully hits the opponent.")]
        public bool allowOnHit = true;

        [Tooltip("Allow this branch when the current node is blocked.")]
        public bool allowOnBlock;

        [Header("Destination")]
        public RekkaNodeData targetNode;

        public bool IsAvailableFor(RekkaNodeOutcome outcome)
        {
            switch (outcome)
            {
                case RekkaNodeOutcome.Hit:
                    return allowOnHit;
                case RekkaNodeOutcome.Block:
                    return allowOnBlock;
                default:
                    return false;
            }
        }
    }

}
