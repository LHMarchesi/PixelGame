using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LandOfFire.BunnyStep
{
    // Una referencia Move distinta por jugador. El prefab dummy puede omitirla.
    public sealed class FighterInput : MonoBehaviour
    {
        [SerializeField] private InputActionReference move;
        [Tooltip("Si no se asigna Move, se crean controles A/D. Activar para usar flechas.")]
        public bool useArrowKeys;
        public bool enableGamepad;
        [SerializeField, Range(0.1f, 0.95f)] private float threshold = 0.5f;
        private readonly Queue<int> edges = new Queue<int>();
        private InputAction action;
        public int Direction { get; private set; }

        private void OnEnable()
        {
            if (move != null) action = move.action.Clone();
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

        public void ClearPresses() { edges.Clear(); }

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
            Direction = 0;
            edges.Clear();
        }
    }
}
