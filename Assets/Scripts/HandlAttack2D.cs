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

    private HandleInputs input;
    private PlayerAnimation anim;

    private void Awake()
    {
        input = GetComponentInParent<HandleInputs>();
        anim = GetComponentInParent<PlayerAnimation>();
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

        if (input.IsAttacking() && nextTimeHit <= 0 && !attacking)
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
        anim.PlayAttackAnimation(meleeAttackData.animationTrigger);
        anim.SetAnimatorBool("IsAttacking", true);

        // Avanzar combo
        comboIndex = (comboIndex + 1) % meleeComboAttacks.Length;
        comboTimer = comboResetTime;

        // Anti spam
        nextTimeHit = timeBtwMeleeHits;

        StartCoroutine(EndAttackAfter(anim.GetCurrentAnimationRemainingTime()));
    }

    private IEnumerator EndAttackAfter(float t)
    {
        yield return new WaitForSeconds(t);
        attacking = false;
        anim.SetAnimatorBool("IsAttacking", false);
    }

    public bool IsAttacking() => attacking;
}
