using UnityEngine;
public class PlayerContext: MonoBehaviour // Save valuable data from the player
{
    private HandleInputs handleInputs;
    private PlayerController playerController;

    public HandleInputs HandleInputs { get => handleInputs; set => handleInputs = value; }
    public PlayerController PlayerController { get => playerController; set => playerController = value; }

    private void OnEnable()
    {
        HandleInputs = GetComponent<HandleInputs>();
        playerController = GetComponent<PlayerController>();
    }
}
