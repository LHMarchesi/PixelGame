// Land of Fire · Captura del Input System para un jugador.
// Registra pulsaciones entre ticks y ofrece Move y L/M/H sin repetir
// los ataques por hold.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LandOfFire.BunnyStep
{
    // Una referencia Move distinta por jugador. El prefab dummy puede omitirla.
    public sealed class FighterInput : MonoBehaviour
    {
        [SerializeField] private InputActionReference move;

        [Header("Ataques")]
        [SerializeField] private InputActionReference lightAttack;
        [SerializeField] private InputActionReference mediumAttack;
        [SerializeField] private InputActionReference heavyAttack;

        [Tooltip("Si no se asigna Move, se crean controles A/D. Activar para usar flechas.")]
        public bool useArrowKeys;

        public bool enableGamepad;

        [SerializeField, Range(0.1f, 0.95f)]
        private float threshold = 0.5f;

        private readonly Queue<int> edges = new Queue<int>();
        private readonly Queue<AttackCommand> attackEdges = new Queue<AttackCommand>();

        private InputAction action;
        private InputAction lightAction;
        private InputAction mediumAction;
        private InputAction heavyAction;

        public int Direction { get; private set; }

        private void OnEnable()
        {
            SetupMove();
            SetupAttacks();
        }

        private void SetupMove()
        {
            if (move != null && move.action != null)
            {
                action = move.action.Clone();
            }
            else
            {
                action = new InputAction(
                    "Move",
                    InputActionType.Value,
                    expectedControlType: "Vector2"
                );

                action.AddCompositeBinding("2DVector")
                    .With("Left",
                        useArrowKeys
                            ? "<Keyboard>/leftArrow"
                            : "<Keyboard>/a")
                    .With("Right",
                        useArrowKeys
                            ? "<Keyboard>/rightArrow"
                            : "<Keyboard>/d");

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

        private void SetupAttacks()
        {
            lightAction = CreateAttackAction(
                "Light Attack",
                lightAttack,
                "<Keyboard>/j",
                AttackCommand.Light,
                ReadLight
            );

            mediumAction = CreateAttackAction(
                "Medium Attack",
                mediumAttack,
                "<Keyboard>/k",
                AttackCommand.Medium,
                ReadMedium
            );

            heavyAction = CreateAttackAction(
                "Heavy Attack",
                heavyAttack,
                "<Keyboard>/l",
                AttackCommand.Heavy,
                ReadHeavy
            );
        }

        private InputAction CreateAttackAction(
            string actionName,
            InputActionReference reference,
            string keyboardBinding,
            AttackCommand command,
            System.Action<InputAction.CallbackContext> callback)
        {
            InputAction createdAction;

            if (reference != null && reference.action != null)
            {
                createdAction = reference.action.Clone();
            }
            else
            {
                createdAction = new InputAction(
                    actionName,
                    InputActionType.Button,
                    keyboardBinding
                );

                if (enableGamepad)
                {
                    createdAction.AddBinding("<Gamepad>/buttonSouth");
                }
            }

            createdAction.performed += callback;
            createdAction.Enable();

            return createdAction;
        }

        private void ReadLight(InputAction.CallbackContext context)
        {
            attackEdges.Enqueue(AttackCommand.Light);
        }

        private void ReadMedium(InputAction.CallbackContext context)
        {
            attackEdges.Enqueue(AttackCommand.Medium);
        }

        private void ReadHeavy(InputAction.CallbackContext context)
        {
            attackEdges.Enqueue(AttackCommand.Heavy);
        }

        private void Read(InputAction.CallbackContext context)
        {
            float x = context.ReadValue<Vector2>().x;

            int next =
                x > threshold ? 1 :
                x < -threshold ? -1 :
                0;

            if (next == Direction)
                return;

            Direction = next;

            if (next != 0)
                edges.Enqueue(next);
        }

        public bool TryReadPress(out int direction)
        {
            direction = 0;

            if (edges.Count == 0)
                return false;

            direction = edges.Dequeue();
            return true;
        }

        public bool TryReadAttack(out AttackCommand command)
        {
            command = default;

            if (attackEdges.Count == 0)
                return false;

            command = attackEdges.Dequeue();
            return true;
        }

        public void ClearPresses()
        {
            edges.Clear();
            attackEdges.Clear();
        }

        private void OnDisable()
        {
            DisposeAction(ref action, Read, Read);

            DisposeAttackAction(ref lightAction, ReadLight);
            DisposeAttackAction(ref mediumAction, ReadMedium);
            DisposeAttackAction(ref heavyAction, ReadHeavy);

            Direction = 0;
            ClearPresses();
        }

        private void DisposeAction(
            ref InputAction inputAction,
            System.Action<InputAction.CallbackContext> performed,
            System.Action<InputAction.CallbackContext> canceled)
        {
            if (inputAction == null)
                return;

            inputAction.performed -= performed;
            inputAction.canceled -= canceled;
            inputAction.Disable();
            inputAction.Dispose();
            inputAction = null;
        }

        private void DisposeAttackAction(
            ref InputAction inputAction,
            System.Action<InputAction.CallbackContext> callback)
        {
            if (inputAction == null)
                return;

            inputAction.performed -= callback;
            inputAction.Disable();
            inputAction.Dispose();
            inputAction = null;
        }
    }
}