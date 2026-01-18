using UnityEngine;

public class UIManager : Singleton<UIManager>
{
    private PlayerContext playerContext; // Data from player

    [SerializeField] private GameObject pausePanel;

    private void Start()
    {
        playerContext = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerContext>();  // Find the PlayerContext in the scene
    }

    public void SetPause(bool value)
    {
        pausePanel.SetActive(value);
    }
}
