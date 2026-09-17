// Land of Fire · FUN-COM-001
// Vida lógica de un luchador. Solo cambia después de un impacto confirmado;
// el HUD puede suscribirse al evento sin decidir la validez del golpe.
using System;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [DisallowMultipleComponent]
    public sealed class FighterHealth : MonoBehaviour
    {
        [Min(1)] public int maxHealth = 100;
        public int CurrentHealth { get; private set; }
        public event Action<int, int> HealthChanged;

        private void OnEnable() { ResetHealth(); }

        public void ResetHealth()
        {
            CurrentHealth = Mathf.Max(1, maxHealth);
            HealthChanged?.Invoke(CurrentHealth, Mathf.Max(1, maxHealth));
        }

        public int TakeDamage(int amount)
        {
            int before = CurrentHealth;
            CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.Max(0, amount));
            if (CurrentHealth != before) HealthChanged?.Invoke(CurrentHealth, Mathf.Max(1, maxHealth));
            return before - CurrentHealth;
        }
    }
}
