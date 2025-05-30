using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("DataFromPlayer")]
    [SerializeField] private PlayerContext playerContext;

    [Header("UI Elements")]
    [SerializeField] private SliderPassValue healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Image damagePanel;
    public SliderPassValue HealthSlider { get => healthSlider; set { } }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Singleton UI Manager
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        playerContext = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerContext>();  // Find the PlayerContext in the scene
    }


    private void Start()
    {
        playerContext.PlayerController.OnTakeDamage += OnPlayerTakeDamage; // Subscribe to the player's damage event
        HealthSlider.ChangeValue(playerContext.PlayerController.MaxHealth);
    }

    private void ShowDamageFlash()
    {
        StopAllCoroutines();
        StartCoroutine(DamageFlashCoroutine());
    }

    private IEnumerator DamageFlashCoroutine()
    {
        damagePanel.gameObject.SetActive(true); // Aparece 

        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        damagePanel.gameObject.SetActive(false); // Desaparece
    }

    public void OnPlayerTakeDamage()
    {
        HealthSlider.ChangeValue(playerContext.PlayerController.CurrentHealth);
        healthText.text = playerContext.PlayerController.CurrentHealth.ToString() + "/" + playerContext.PlayerController.MaxHealth.ToString();
        ShowDamageFlash();
    }

    private void OnDisable()
    {
        playerContext.PlayerController.OnTakeDamage -= OnPlayerTakeDamage; // Unsubscribe from the player's damage event
    }
}
