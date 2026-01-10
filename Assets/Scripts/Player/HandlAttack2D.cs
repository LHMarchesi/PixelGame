using System.Collections;
using UnityEngine;

public class HandlAttack2D : MonoBehaviour
{
    public MeleeAttack2D[] meleeComboAttacks;
    public float comboResetTime = 1f;
    public float timeBtwMeleeHits = 0.3f;

    private float nextTimeHit = 0f;
    private float comboTimer = 0f;

    private int comboIndex = 0;
    private bool attacking = false;

    private PlayerContext playerContext;

    private void Awake()
    {
        playerContext = GetComponentInParent<PlayerContext>();
    }

    private void Update()
    {
        if (nextTimeHit > 0)
            nextTimeHit -= Time.deltaTime;

        // reset combo
        if (comboIndex > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0)
                comboIndex = 0;
        }

        if (playerContext.InputHandler.IsAttacking() && nextTimeHit <= 0 && !attacking)
        {
            DoAttack();
        }
    }
    public MeleeAttack2D currentAttack;
    private void DoAttack()
    {
        attacking = true;

        currentAttack = meleeComboAttacks[comboIndex];
        MeleeAttackData meleeAttackData = currentAttack.GetData();
        // Animación
        playerContext.PlayerAnimation.PlayAttackAnimation(meleeAttackData.animationTrigger);
        playerContext.PlayerAnimation.SetAnimatorBool("IsAttacking", true);

        // Avanzar combo
        comboIndex = (comboIndex + 1) % meleeComboAttacks.Length;
        comboTimer = comboResetTime;

        // Anti spam
        nextTimeHit = timeBtwMeleeHits;

        StartCoroutine(EndAttackAfter(playerContext.PlayerAnimation.GetCurrentAnimationRemainingTime()));
    }

    private IEnumerator EndAttackAfter(float t)
    {
        yield return new WaitForSeconds(t);
        attacking = false;
        playerContext.PlayerAnimation.SetAnimatorBool("IsAttacking", false);
    }

    public bool IsAttacking() => attacking;
}
