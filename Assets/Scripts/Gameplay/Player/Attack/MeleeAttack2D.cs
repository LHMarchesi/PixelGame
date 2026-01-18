using System.Collections;
using UnityEngine;
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class MeleeAttack2D : MonoBehaviour
{
    [SerializeField] private Collider2D hitbox;
    [SerializeField] private SpriteRenderer render;
    [SerializeField] private MeleeAttackData currentAttack;

    private void Awake()
    {
        render = GetComponent<SpriteRenderer>();
        hitbox.enabled = false;
        render.enabled = false;
    }

    public MeleeAttackData GetData() {  return this.currentAttack; }

    public void StartAttack(MeleeAttackData attackData)
    {
        currentAttack = attackData;
        StartCoroutine(AttackSequence());
    }

    private IEnumerator AttackSequence()
    {
        hitbox.enabled = true;
        render.enabled = true;

        yield return new WaitForSeconds(currentAttack.attackDuration);

        hitbox.enabled = false;
        render.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!hitbox.enabled) return;
        if (!other.CompareTag("Enemy")) return;

        Enemy e = other.GetComponent<Enemy>();
        e.TakeDamage(currentAttack.damage);
        e.TakeKnockback(currentAttack.verticalKnockback, currentAttack.horizontalKnockback);
    }
}
