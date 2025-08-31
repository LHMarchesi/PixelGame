using System;
using UnityEngine;

public class HandlAttack2D : MonoBehaviour
{
    [SerializeField] private Transform controller;
    [SerializeField] private float attackRadius;
    [SerializeField] private float attackDamage;
    [SerializeField] private float timeBtwHits;
    [SerializeField] private float nextTimehit;

    [SerializeField] private float comboResetTime = 1f; // Tiempo para reiniciar el combo

    private Animator animator;
    private int comboStep = 0; // 0 = sin combo, 1 = primer golpe, 2 = segundo
    private float comboTimer = 0f;

    private void Awake()
    {
        animator = GetComponentInParent<Animator>();
    }

    private void Update()
    {
        if (nextTimehit > 0)
            nextTimehit -= Time.deltaTime;

        if (comboStep > 0)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0)
            {
                comboStep = 0; // reiniciar combo
            }
        }

        if (Input.GetButtonDown("Fire1") && nextTimehit <= 0)
        {
            nextTimehit = timeBtwHits;
            comboTimer = comboResetTime;

            if (comboStep == 0)
            {
                comboStep = 1;
                Attack("Hit1");
            }
            else if (comboStep == 1)
            {
                Attack("Hit2");
                comboStep = 0;
            }
        }
    }

    public void Attack(string animationTrigger)
    {
        animator.SetTrigger(animationTrigger);

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(controller.position, attackRadius);
        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy.CompareTag("Enemy"))
            {
                Debug.Log($"Attacking {enemy.name} for {attackDamage} damage.");
                enemy.GetComponent<Enemy>().TakeDamage(attackDamage);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(controller.position, attackRadius);
    }


}
