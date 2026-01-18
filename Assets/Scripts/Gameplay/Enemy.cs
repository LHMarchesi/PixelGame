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


    public void TakeKnockback(float verticalKnockback, float horizontalKnockback)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.AddForce(new Vector2(horizontalKnockback * 5, verticalKnockback * 5), ForceMode2D.Impulse);
        }
    }

    private void Dead()
    {
        Destroy(gameObject);
    }
}
