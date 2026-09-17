// Land of Fire · Captura del Input System para un jugador.
// Registra pulsaciones entre ticks y ofrece Move y L sin repetir el ataque por hold.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LandOfFire.BunnyStep
{
    // Una referencia Move distinta por jugador. El prefab dummy puede omitirla.
    public sealed class FighterInput : MonoBehaviour
    {
        [SerializeField] private InputActionReference move;
        [SerializeField] private InputActionReference lightAttack;
        [Tooltip("Si no se asigna Move, se crean controles A/D. Activar para usar flechas.")]
        public bool useArrowKeys;
        public bool enableGamepad;
        [SerializeField, Range(0.1f, 0.95f)] private float threshold = 0.5f;
        private readonly Queue<int> edges = new Queue<int>();
        private readonly Queue<AttackCommand> attackEdges = new Queue<AttackCommand>();
        private InputAction action;
        private InputAction lightAction;
        public int Direction { get; private set; }

        private void OnEnable()
        {
            if (move != null && move.action != null) action = move.action.Clone();
            else
            {
                action = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
                action.AddCompositeBinding("2DVector")
                    .With("Left", useArrowKeys ? "<Keyboard>/leftArrow" : "<Keyboard>/a")
                    .With("Right", useArrowKeys ? "<Keyboard>/rightArrow" : "<Keyboard>/d");
                if (enableGamepad)
                {
                    action.AddBinding("<Gamepad>/leftStick");
                    action.AddBinding("<Gamepad>/dpad");
                }
            }
            action.performed += Read;
            action.canceled += Read;
            action.Enable();

            if (lightAttack != null && lightAttack.action != null) lightAction = lightAttack.action.Clone();
            else
            {
                lightAction = new InputAction("Light Attack", InputActionType.Button, "<Keyboard>/j");
                if (enableGamepad) lightAction.AddBinding("<Gamepad>/buttonSouth");
            }
            lightAction.performed += ReadLight;
            lightAction.Enable();
        }

        private void ReadLight(InputAction.CallbackContext context)
        {
            attackEdges.Enqueue(AttackCommand.Light); // Hold no repite por sí mismo.
        }

        private void Read(InputAction.CallbackContext context)
        {
            float x = context.ReadValue<Vector2>().x;
            int next = x > threshold ? 1 : x < -threshold ? -1 : 0;
            if (next == Direction) return;
            Direction = next;
            if (next != 0) edges.Enqueue(next);
        }

        public bool TryReadPress(out int direction)
        {
            direction = 0;
            if (edges.Count == 0) return false;
            direction = edges.Dequeue();
            return true;
        }

        public bool TryReadAttack(out AttackCommand command)
        {
            command = default(AttackCommand);
            if (attackEdges.Count == 0) return false;
            command = attackEdges.Dequeue();
            return true;
        }

        public void ClearPresses() { edges.Clear(); attackEdges.Clear(); }

        private void OnDisable()
        {
            if (action != null)
            {
                action.performed -= Read;
                action.canceled -= Read;
                action.Disable();
                action.Dispose();
                action = null;
            }
            if (lightAction != null)
            {
                lightAction.performed -= ReadLight;
                lightAction.Disable();
                lightAction.Dispose();
                lightAction = null;
            }
            Direction = 0;
            ClearPresses();
        }
    }
}
