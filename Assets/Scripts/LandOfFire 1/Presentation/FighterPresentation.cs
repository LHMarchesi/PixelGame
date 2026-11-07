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
        private Color baseSpriteColor = Color.white;
        private readonly HashSet<string> warnings = new HashSet<string>();

        private void Initialize()
        {
            if (initialized) return;
            if (visualOffset != null) rest = visualOffset.localPosition;
            if (sprite != null) baseSpriteColor = sprite.color;
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
            PhaseAnimation settings = machine.IsAutoCombo
                ? machine.CurrentAutoComboAnimation
                : machine.IsAttacking && machine.CurrentAttack != null
                    ? machine.CurrentAttack.AnimationFor(machine.CurrentAttackPhase)
                    : profile.AnimationFor(machine.State, machine.Phase, machine.CurrentJumpPhase);
            if (settings != null && animator != null && animator.runtimeAnimatorController != null)
            {
                string path = settings.stateName ?? "";
                int hash = Animator.StringToHash(path);
                if (!string.IsNullOrEmpty(path) && animator.HasState(0, hash))
                {
                    int elapsed;
                    int duration;
                    if (machine.IsAutoCombo)
                    {
                        // Auto Combo samples the active attack's current phase
                        // or custom state. Each clip is fitted to its own tick
                        // duration instead of sampling one combo-wide clip.
                        elapsed = machine.CurrentAutoComboAnimationElapsedTick;
                        duration = machine.CurrentAutoComboAnimationDurationTicks;
                    }
                    else
                    {
                        bool combatTimed = machine.State == FighterState.BlockRecovery ||
                                           machine.State == FighterState.GuardBreak ||
                                           machine.IsHurt || machine.IsParryState ||
                                           machine.IsComboLimitKnockdown;
                        elapsed = combatTimed ? machine.Elapsed :
                            machine.IsStepping || machine.IsJumping || machine.IsAttacking ? machine.PhaseElapsed :
                            poseTick + 1;
                        duration = combatTimed ? machine.Duration :
                            machine.IsStepping || machine.IsJumping || machine.IsAttacking ? machine.PhaseDuration :
                            settings.loopTicks;
                    }

                    float normalizedTime;
                    bool fitCustomAutoComboState = machine.IsAutoCombo &&
                        machine.CurrentAutoComboAttack != null &&
                        machine.CurrentAutoComboAttack.animationMode ==
                            AutoComboAnimationMode.CustomAnimationSequence;

                    if (fitCustomAutoComboState)
                    {
                        // Custom Auto Combo states always complete within
                        // their configured tick count. The Animator is sampled
                        // directly on the shared 60 Hz timeline, so no frame-
                        // rate-dependent Animator.speed is required.
                        normalizedTime = duration <= 1
                            ? 0f
                            : Mathf.Min(.99999f,
                                Mathf.Max(0, elapsed - 1) / (float)(duration - 1));
                    }
                    else
                    {
                        // Reused AttackMoveData clips keep exactly the same
                        // playback policy as when the move is input normally.
                        normalizedTime = settings.Sample(elapsed, duration);
                    }

                    animator.Play(hash, 0, normalizedTime);
                    animator.Update(0);
                }
                else if (warnings.Add(path)) Debug.LogWarning("Falta estado Animator: " + path, this);
            }
            if (sprite != null)
            {
                sprite.flipX = spriteFacesRight ? facing < 0 : facing > 0;

                AttackMoveData telegraphMove = null;
                if (machine.IsAutoCombo && machine.CurrentAutoCombo != null)
                {
                    telegraphMove = machine.CurrentAutoCombo.TelegraphMoveAtTick(
                        machine.Elapsed);
                }
                else if (machine.IsAttacking && machine.CurrentAttack != null &&
                         machine.CurrentAttack.IsTelegraphActive(machine.Elapsed))
                {
                    telegraphMove = machine.CurrentAttack;
                }

                sprite.color = telegraphMove != null
                    ? telegraphMove.TelegraphColor
                    : baseSpriteColor;
            }
        }

        public void ResetHeight()
        {
            Initialize();
            if (visualOffset != null) visualOffset.localPosition = rest;
            if (sprite != null) sprite.color = baseSpriteColor;
        }
        private void OnDisable() { ResetHeight(); }
    }
}
