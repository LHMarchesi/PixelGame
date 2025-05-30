using UnityEngine;
public class PlayerContext: MonoBehaviour // Save valuable data from the player
{
    private HandleInputs handleInputs;
    private HandleAnimations handleAnimations;
    private PlayerController playerController;

    public HandleAnimations HandleAnimations { get => handleAnimations; set => handleAnimations = value; }
    public HandleInputs HandleInputs { get => handleInputs; set => handleInputs = value; }
    public PlayerController PlayerController { get => playerController; set => playerController = value; }

    private void OnEnable()
    {
        HandleInputs = GetComponent<HandleInputs>();
        HandleAnimations = GetComponentInChildren<HandleAnimations>();
        playerController = GetComponent<PlayerController>();
    }
}
