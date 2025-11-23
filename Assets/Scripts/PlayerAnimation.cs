using System.Collections;
using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;
    private HandleInputs inputs;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        inputs = GetComponent<HandleInputs>();
    }

    private void Update()
    {
        Vector2 move = inputs.GetMoveVector2();
        bool isMoving = move.magnitude > 0.1f;

        bool isRunning = inputs.IsRunning() && isMoving;
        bool isWalking = isMoving && !isRunning;

        animator.SetBool("IsWalking", isWalking);
        animator.SetBool("IsRunning", isRunning);   
    }

    public void PlayAttackAnimation(string attackTrigger)
    {
        animator.SetTrigger(attackTrigger);
    }

    public IEnumerator WaitForCurrentAnimation()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        while (state.normalizedTime == 0)
        {
            state = animator.GetCurrentAnimatorStateInfo(0);
            yield return null;
        }

        // Esperar hasta que termine
        while (state.normalizedTime < 1)
        {
            state = animator.GetCurrentAnimatorStateInfo(0);
            yield return null;
        }
    }
}

