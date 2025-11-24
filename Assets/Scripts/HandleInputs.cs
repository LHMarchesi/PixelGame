using UnityEngine;
using UnityEngine.InputSystem;

public class HandleInputs : MonoBehaviour
{
    public PlayerInput playerInput;
    private Vector2 move;
    private float isRunning, isJumping, isDashing, isAttacking;
    public void OnMove(InputAction.CallbackContext context) // Catch player input
    {
        move = context.ReadValue<Vector2>();
    }
   
    public void OnRunning(InputAction.CallbackContext context) // Catch run input
    {
        isRunning = context.ReadValue<float>();
    }
    public void OnJump(InputAction.CallbackContext context) // Catch run input
    {
        isJumping = context.ReadValue<float>();
    }
    public void OnDash(InputAction.CallbackContext context) // Catch run input
    {
        isDashing = context.ReadValue<float>();
    }

    public void OnAttack(InputAction.CallbackContext context) // Catch run input
    {
        isAttacking = context.ReadValue<float>();
    }
    public void SetPaused(bool paused)
    {
        if (paused)
            playerInput.DeactivateInput();
        else
            playerInput.ActivateInput();
    }
    public Vector2 GetMoveVector2() { return move; }  // Return public values

    public bool IsRunning() { return isRunning == 1f; }

    public bool IsAttacking() { return isAttacking == 1f; }
    
    public bool IsJumping() { return isJumping == 1f; }

    public bool IsDashing() { return isDashing == 1f; }

}
