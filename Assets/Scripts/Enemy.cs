using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] private float health;
    public void TakeDamage(float damage)
    {
        health -= damage;
        if (health <= 0)
        {
            Dead();
        }
    }

    private void Dead()
    {
        Destroy(gameObject);
    }
}
