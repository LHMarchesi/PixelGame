// Land of Fire · Presentación visual del estado lógico.
// Muestra el clip de cada fase y el arco visual; nunca mueve la raíz física ni decide hits.
using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public sealed class FighterPresentation : MonoBehaviour
    {
        public Transform visualOffset;
        public SpriteRenderer sprite;
        public Animator animator;
        public bool spriteFacesRight = true;
        private bool initialized;
        private Vector3 rest;
        private readonly HashSet<string> warnings = new HashSet<string>();

        private void Initialize()
        {
            if (initialized) return;
            if (visualOffset != null) rest = visualOffset.localPosition;
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.speed = 0;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            initialized = true;
        }

        public void Render(FighterStateMachine machine, FighterMovementProfile profile, int facing, int poseTick)
        {
            Initialize();
            if (visualOffset != null) visualOffset.localPosition = rest + Vector3.up * machine.VisualHeight;
            PhaseAnimation settings = machine.IsAttacking && machine.CurrentAttack != null
                ? machine.CurrentAttack.AnimationFor(machine.CurrentAttackPhase)
                : profile.AnimationFor(machine.State, machine.Phase);
            if (settings != null && animator != null && animator.runtimeAnimatorController != null)
            {
                string path = settings.stateName ?? "";
                int hash = Animator.StringToHash(path);
                if (!string.IsNullOrEmpty(path) && animator.HasState(0, hash))
                {
                    int elapsed = machine.IsStepping || machine.IsAttacking ? machine.PhaseElapsed :
                        machine.IsHurt ? machine.Elapsed : poseTick + 1;
                    int duration = machine.IsStepping || machine.IsAttacking ? machine.PhaseDuration :
                        machine.IsHurt ? machine.Duration : settings.loopTicks;
                    animator.Play(hash, 0, settings.Sample(elapsed, duration));
                    animator.Update(0);
                }
                else if (warnings.Add(path)) Debug.LogWarning("Falta estado Animator: " + path, this);
            }
            if (sprite != null) sprite.flipX = spriteFacesRight ? facing < 0 : facing > 0;
        }

        public void ResetHeight()
        {
            Initialize();
            if (visualOffset != null) visualOffset.localPosition = rest;
        }
        private void OnDisable() { ResetHeight(); }
    }
}
