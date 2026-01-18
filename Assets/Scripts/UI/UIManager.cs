using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : Singleton<UIManager>
{
    [Header("DataFromPlayer")]
    [SerializeField] private PlayerContext playerContext;

    [SerializeField] private Image damagePanel;
    [SerializeField] private GameObject pausePanel;

    private void Start()
    {
        playerContext = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerContext>();  // Find the PlayerContext in the scene

        playerContext.PlayerController.OnTakeDamage += OnPlayerTakeDamage; // Subscribe to the player's damage event
    }

    public void SetPause(bool value)
    {
        pausePanel.SetActive(value);
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
        ShowDamageFlash();
    }

    private void OnDisable()
    {
      //  playerContext.PlayerController.OnTakeDamage -= OnPlayerTakeDamage; // Unsubscribe from the player's damage event
    }
}
