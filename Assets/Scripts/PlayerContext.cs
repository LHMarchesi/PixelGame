using UnityEngine;
public class PlayerContext: MonoBehaviour // Save valuable data from the player
{
    private HandleInputs handleInputs;
    private PlayerController playerController;
    private PlayerAnimation playerAnimation;
    private HandlAttack2D handleMeleeAttack;

    public HandleInputs InputHandler { get => handleInputs; set => handleInputs = value; }
    public PlayerController PlayerController { get => playerController; set => playerController = value; }
    public PlayerAnimation PlayerAnimation { get => playerAnimation; set => playerAnimation = value; }
    public HandlAttack2D HandleMeleeAttack { get => handleMeleeAttack; set => handleMeleeAttack = value; }

    private void OnEnable()
    {
        InputHandler = GetComponent<HandleInputs>();
        playerController = GetComponent<PlayerController>();
        playerAnimation = GetComponent<PlayerAnimation>();
        handleMeleeAttack = GetComponentInChildren<HandlAttack2D>();
    }
}
