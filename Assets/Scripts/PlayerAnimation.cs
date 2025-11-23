using System.Collections;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;
    private PlayerContext playerContext;
    private void Awake()
    {
        animator = GetComponent<Animator>();
        playerContext = GetComponentInParent<PlayerContext>();

    }

    private void Update()
    {
        Vector2 move = playerContext.InputHandler.GetMoveVector2();
        bool isMoving = move.magnitude > 0.1f;

        bool isRunning = playerContext.InputHandler.IsRunning() && isMoving;
        bool isWalking = isMoving && !isRunning && !playerContext.HandleMeleeAttack.IsAttacking();

        animator.SetBool("IsWalking", isWalking);
        animator.SetBool("IsRunning", isRunning);   
    }

    public bool SetAnimatorBool(string boolName, bool value)
    {
        animator.SetBool(boolName, value);
        return value;
    }

    public void PlayAttackAnimation(string attackTrigger)
    {
        animator.SetTrigger(attackTrigger);
        StartCoroutine(WaitForCurrentAnimation());
    }

    public IEnumerator WaitForCurrentAnimation()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        // Espera a que empiece
        while (state.normalizedTime == 0)
        {
            state = animator.GetCurrentAnimatorStateInfo(0);
            yield return null;
        }

        // Espera a que termine
        while (state.normalizedTime < 1)
        {
            state = animator.GetCurrentAnimatorStateInfo(0);
            yield return null;
        }

    }
    public float GetCurrentAnimationRemainingTime()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        float length = state.length;                   // duración total
        float normalized = state.normalizedTime;       // progreso (1 = terminó una vez)

        float elapsed = normalized * length;           // tiempo reproducido
        float remaining = length - elapsed;            // cuánto falta

        return Mathf.Max(remaining, 0);
    }
}

