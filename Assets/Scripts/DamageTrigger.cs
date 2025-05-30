using UnityEngine;

public class DamageTrigger : MonoBehaviour
{
    [SerializeField] private int damageAmount; // Amount of damage to apply
    [SerializeField] private float damageInterval; 
    private float lastDamageTime;
    private void OnTriggerStay2D(Collider2D collision)
    {
        if(collision.TryGetComponent(out IDamageable damageable))
        {
            // Verifica si ha pasado el intervalo de daño
            if (Time.time - lastDamageTime >= damageInterval)
            {
                damageable.TakeDamage(damageAmount);
                lastDamageTime = Time.time; // Actualiza el tiempo del último daño
            }
        }
    }
}

public interface IDamageable
{
    void TakeDamage(int amount);
}
