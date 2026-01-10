using UnityEngine;

[CreateAssetMenu(fileName = "NewAttack", menuName = "Combat/Melee Attack Data")]
public class MeleeAttackData : ScriptableObject
{
    public string animationTrigger;
    public float attackDuration;
    public float attackDelay;
    public float damage;
    public float horizontalKnockback;
    public float verticalKnockback;
    public float hitPauseTime = 0.1f;
}
