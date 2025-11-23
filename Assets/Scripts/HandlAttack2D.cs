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

    private void DoAttack()
    {
        attacking = true;

        MeleeAttack2D attack = meleeComboAttacks[comboIndex];
        MeleeAttackData meleeAttackData = attack.GetData();
        // Animación
        anim.PlayAttackAnimation(meleeAttackData.animationTrigger);

        // Hitbox + daño
        attack.StartAttack(meleeAttackData);

        // Avanzar combo
        comboIndex = (comboIndex + 1) % meleeComboAttacks.Length;
        comboTimer = comboResetTime;

        // Anti spam
        nextTimeHit = timeBtwMeleeHits;

      StartCoroutine(EndAttackAfter(meleeAttackData.attackDuration + meleeAttackData.attackDelay));
    }

    private IEnumerator EndAttackAfter(float t)
    {
        yield return new WaitForSeconds(t);
        attacking = false;
    }

    public bool IsAttacking() => attacking;
}
