// Land of Fire · Rekka tree entry point.
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public enum RekkaNodeOutcome
    {
        Unknown,
        Hit,
        Block,
        Parried,
        Miss,
        Clash
    }

    [CreateAssetMenu(menuName = "Land of Fire/Fighter/Rekka Tree")]
    public sealed class RekkaTreeData : ScriptableObject
    {
        [Tooltip("First node executed when this Rekka command is recognized.")]
        public RekkaNodeData rootNode;

        [Header("Combo Knockdown")]
        [Tooltip("If enabled, the selected Rekka node can trigger the existing Combo Knockdown after a valid hit. The global Combo Hit Limit always has priority.")]
        public bool causesKnockdown;

        [Min(1)]
        [Tooltip("1-based node number in the route that triggers Combo Knockdown when it connects. This is ignored for the terminal node, which uses Lethal Hit.")]
        public int knockdownAfterNodeNumber = 2;

        public bool ShouldTriggerKnockdown(int nodeNumber)
        {
            return causesKnockdown &&
                   nodeNumber == Mathf.Max(1, knockdownAfterNodeNumber);
        }

        public bool HasValidTree =>
            rootNode != null && rootNode.HasValidNode;

        public int StartupTicks =>
            HasValidTree ? rootNode.attack.StartupTicks : 1;
    }
}
