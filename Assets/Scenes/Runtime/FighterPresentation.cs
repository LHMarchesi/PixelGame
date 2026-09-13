using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public sealed class FighterPresentation : MonoBehaviour
    {
        [Tooltip("Hijo VisualOffset; el Animator debe estar en un hijo de este objeto.")]
        public Transform visualOffset;
        public SpriteRenderer sprite;
        public Animator animator;
        public bool spriteFacesRight = true;
        [Min(1)] public int idleLoopTicks = 60;
        private Vector3 restPosition;
        private bool initialized;
        private readonly int[] hashes = {
            Animator.StringToHash("Base Layer.Idle"), Animator.StringToHash("Base Layer.Guard"),
            Animator.StringToHash("Base Layer.BunnyForward"), Animator.StringToHash("Base Layer.BunnyBackward") };
        private readonly bool[] warned = new bool[4];

        private void Initialize()
        {
            if (initialized) return;
            if (visualOffset != null) restPosition = visualOffset.localPosition;
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.speed = 0; // El reloj de combate controla las poses.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            initialized = true;
        }

        public void Render(FighterState state, float progress, float height, int facing, int poseTick)
        {
            Initialize();
            if (visualOffset != null) visualOffset.localPosition = restPosition + Vector3.up * height;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                int index = (int)state;
                if (animator.HasState(0, hashes[index]))
                {
                    bool stepping = state == FighterState.BunnyForward || state == FighterState.BunnyBackward;
                    float time = stepping ? Mathf.Min(progress, 0.99999f) :
                        (poseTick % Mathf.Max(1, idleLoopTicks)) / (float)Mathf.Max(1, idleLoopTicks);
                    animator.Play(hashes[index], 0, time);
                    animator.Update(0);
                }
                else if (!warned[index])
                {
                    warned[index] = true;
                    Debug.LogWarning("Falta estado Animator: Base Layer." + state, this);
                }
            }
            if (sprite != null) sprite.flipX = spriteFacesRight ? facing < 0 : facing > 0;
        }

        public void ResetHeight()
        {
            Initialize();
            if (visualOffset != null) visualOffset.localPosition = restPosition;
        }
        private void OnDisable() { ResetHeight(); }
    }
}
