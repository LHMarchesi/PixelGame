// Land of Fire · Editor preview for Auto Combo impact hitboxes.
// Attach optionally to a Fighter root and assign the AutoComboData to preview.

using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FighterAutoComboGizmo : MonoBehaviour
    {
        public AutoComboData autoCombo;

        [Tooltip("Enable or disable the Auto Combo hitbox preview in the Scene view.")]
        public bool showHitboxes = true;

        [Tooltip("When enabled, draw every configured impact. When disabled, draw only Preview Impact Index.")]
        public bool showAllImpacts = true;

        [Min(0)]
        public int previewImpactIndex;

        [Tooltip("Preview the hitboxes as if the fighter were facing right. Disable to mirror them.")]
        public bool previewFacingRight = true;

        [Range(0.05f, 1f)]
        public float fillAlpha = 0.16f;

        private void OnDrawGizmosSelected()
        {
            if (!showHitboxes || autoCombo == null || autoCombo.impacts == null)
                return;

            int facing = previewFacingRight ? 1 : -1;
            for (int i = 0; i < autoCombo.impacts.Count; i++)
            {
                if (!showAllImpacts && i != previewImpactIndex)
                    continue;

                AutoComboImpactData impact = autoCombo.impacts[i];
                if (impact == null || impact.attackMove == null ||
                    !impact.attackMove.useHitbox)
                {
                    continue;
                }

                AttackMoveData move = impact.attackMove;
                Color categoryColor = CategoryColor(move.category);
                Vector2 center = (Vector2)transform.position + new Vector2(
                    move.hitboxOffset.x * facing,
                    move.hitboxOffset.y);
                Vector2 size = new Vector2(
                    Mathf.Max(0.01f, move.hitboxSize.x),
                    Mathf.Max(0.01f, move.hitboxSize.y));

                Color fill = categoryColor;
                fill.a = fillAlpha;
                Gizmos.color = fill;
                Gizmos.DrawCube(center, size);

                categoryColor.a = 1f;
                Gizmos.color = categoryColor;
                Gizmos.DrawWireCube(center, size);
            }
        }

        private static Color CategoryColor(AttackCategory category)
        {
            switch (category)
            {
                case AttackCategory.Medium:
                    return new Color(1f, 0.48f, 0.08f, 1f);
                case AttackCategory.Heavy:
                    return new Color(0.95f, 0.12f, 0.12f, 1f);
                default:
                    return new Color(0.12f, 0.55f, 1f, 1f);
            }
        }
    }
}
