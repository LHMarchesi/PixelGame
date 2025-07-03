using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallTrigger : MonoBehaviour
{
    [SerializeField] private int damageAmount;
    [SerializeField] Transform startPos;

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent(out IDamageable damageable))
        {
            damageable.TakeDamage(damageAmount);
            collision.gameObject.transform.position = startPos.position;
        }
    }
}
