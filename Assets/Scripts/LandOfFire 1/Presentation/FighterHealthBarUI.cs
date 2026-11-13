// Land of Fire · Health bar HUD binding.
// Reads FighterHealth and displays confirmed damage, including chip damage.
using UnityEngine;
using UnityEngine.UI;

namespace LandOfFire.BunnyStep
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Slider))]
    public sealed class FighterHealthBarUI : MonoBehaviour
    {
        [Header("Binding")]
        [Tooltip("Fighter whose health is displayed. If empty, the component looks for FighterHealth on this GameObject's parents.")]
        [SerializeField] private FighterMotor2D fighter;

        [Tooltip("Optional. If empty, the Slider on this GameObject is used.")]
        [SerializeField] private Slider healthSlider;

        [Header("Display")]
        [Tooltip("Smoothly animates the Slider toward the current health value.")]
        [SerializeField] private bool animateDamage = true;

        [Min(0.01f)]
        [Tooltip("Health units moved per second when Animate Damage is enabled.")]
        [SerializeField] private float animationSpeed = 500f;

        private FighterHealth health;
        private int targetHealth;
        private int targetMaxHealth = 1;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Bind();
            RefreshImmediate();
        }

        private void Start()
        {
            // FighterHealth resets itself on enable; refresh again after all
            // scene components have completed their OnEnable callbacks.
            RefreshImmediate();
        }

        private void Update()
        {
            if (healthSlider == null || health == null)
                return;

            if (!animateDamage)
            {
                healthSlider.value = targetHealth;
                return;
            }

            healthSlider.value = Mathf.MoveTowards(
                healthSlider.value,
                targetHealth,
                Mathf.Max(0.01f, animationSpeed) * Time.unscaledDeltaTime);
        }

        private void ResolveReferences()
        {
            if (healthSlider == null)
                healthSlider = GetComponent<Slider>();

            if (fighter == null)
                fighter = GetComponentInParent<FighterMotor2D>();

            if (fighter != null)
                health = fighter.GetComponent<FighterHealth>();
        }

        private void Bind()
        {
            if (health == null)
            {
                Debug.LogError(
                    "FighterHealthBarUI needs a FighterMotor2D with FighterHealth assigned or found in its parents.",
                    this);
                enabled = false;
                return;
            }

            health.HealthChanged -= OnHealthChanged;
            health.HealthChanged += OnHealthChanged;
        }

        private void OnHealthChanged(int current, int maximum)
        {
            targetMaxHealth = Mathf.Max(1, maximum);
            targetHealth = Mathf.Clamp(current, 0, targetMaxHealth);

            if (healthSlider != null)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = targetMaxHealth;
                if (!animateDamage)
                    healthSlider.value = targetHealth;
            }
        }

        private void RefreshImmediate()
        {
            if (health == null || healthSlider == null)
                return;

            targetMaxHealth = Mathf.Max(1, health.maxHealth);
            targetHealth = Mathf.Clamp(health.CurrentHealth, 0, targetMaxHealth);
            healthSlider.minValue = 0f;
            healthSlider.maxValue = targetMaxHealth;
            healthSlider.wholeNumbers = true;
            healthSlider.interactable = false;
            healthSlider.value = targetHealth;
        }

        private void OnDisable()
        {
            if (health != null)
                health.HealthChanged -= OnHealthChanged;
        }

        private void OnValidate()
        {
            animationSpeed = Mathf.Max(0.01f, animationSpeed);
        }
    }
}
