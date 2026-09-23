// Land of Fire · Captura del Input System para un jugador.
// Registra pulsaciones entre ticks y conserva el orden temporal
// de direcciones y ataques dentro de un único buffer.
//
// Importante:
// el release de movimiento se procesa por separado para
// garantizar que un nuevo press sea detectado como una
// nueva entrada.
//
// Ejemplo:
//
// Back press
//      ↓
// Direction = -1
//      ↓
// DirectionPress(Back)
//
// Back release
//      ↓
// Direction = 0
//
// Back press
//      ↓
// Direction = -1
//      ↓
// DirectionPress(Back)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LandOfFire.BunnyStep
{
    // Una referencia Move distinta por jugador.
    // El prefab dummy puede omitirla.
    public sealed class FighterInput : MonoBehaviour
    {
        [SerializeField]
        private InputActionReference move;

        [Header("Ataques")]

        [SerializeField]
        private InputActionReference lightAttack;

        [SerializeField]
        private InputActionReference mediumAttack;

        [SerializeField]
        private InputActionReference heavyAttack;

        [Tooltip(
            "Si no se asigna Move, se crean controles A/D. " +
            "Activar para usar flechas.")]
        public bool useArrowKeys;

        public bool enableGamepad;

        [SerializeField, Range(0.1f, 0.95f)]
        private float threshold = 0.5f;

        // Un único buffer para conservar el orden real del input.
        private readonly Queue<FighterInputEvent>
            inputBuffer =
            new Queue<FighterInputEvent>();

        private InputAction action;

        private InputAction lightAction;
        private InputAction mediumAction;
        private InputAction heavyAction;

        public int Direction { get; private set; }

        // ================================================================
        // UNITY
        // ================================================================

        private void OnEnable()
        {
            SetupMove();
            SetupAttacks();
        }

        // ================================================================
        // MOVE
        // ================================================================

        private void SetupMove()
        {
            if (move != null &&
                move.action != null)
            {
                action =
                    move.action.Clone();
            }
            else
            {
                action =
                    new InputAction(
                        "Move",
                        InputActionType.Value,
                        expectedControlType: "Vector2");

                action.AddCompositeBinding(
                    "2DVector")
                    .With(
                        "Left",
                        useArrowKeys
                            ? "<Keyboard>/leftArrow"
                            : "<Keyboard>/a")
                    .With(
                        "Right",
                        useArrowKeys
                            ? "<Keyboard>/rightArrow"
                            : "<Keyboard>/d");

                if (enableGamepad)
                {
                    action.AddBinding(
                        "<Gamepad>/leftStick");

                    action.AddBinding(
                        "<Gamepad>/dpad");
                }
            }

            // Press / cambio de dirección.
            action.performed += ReadMove;

            // Release / vuelta al neutral.
            action.canceled += ReadMoveCanceled;

            action.Enable();
        }

        // ================================================================
        // MOVE CALLBACKS
        // ================================================================

        private void ReadMove(
            InputAction.CallbackContext context)
        {
            float x =
                context.ReadValue<Vector2>().x;

            int next =
                x > threshold
                    ? 1
                    : x < -threshold
                        ? -1
                        : 0;

            if (next == 0)
                return;

            if (next == Direction)
                return;

            Direction =
                next;

            inputBuffer.Enqueue(
                FighterInputEvent.DirectionPress(
                    next));
        }

        private void ReadMoveCanceled(
      InputAction.CallbackContext context)
        {
            Debug.Log(
                $"[FighterInput] Move CANCELED | " +
                $"Previous Direction: {Direction} | " +
                $"Frame: {Time.frameCount}");

            Direction = 0;
        }

        // ================================================================
        // ATTACKS
        // ================================================================

        private void SetupAttacks()
        {
            lightAction =
                CreateAttackAction(
                    "Light Attack",
                    lightAttack,
                    "<Keyboard>/j",
                    ReadLight);

            mediumAction =
                CreateAttackAction(
                    "Medium Attack",
                    mediumAttack,
                    "<Keyboard>/k",
                    ReadMedium);

            heavyAction =
                CreateAttackAction(
                    "Heavy Attack",
                    heavyAttack,
                    "<Keyboard>/l",
                    ReadHeavy);
        }

        private InputAction CreateAttackAction(
            string actionName,
            InputActionReference reference,
            string keyboardBinding,
            System.Action<InputAction.CallbackContext> callback)
        {
            InputAction createdAction;

            if (reference != null &&
                reference.action != null)
            {
                createdAction =
                    reference.action.Clone();
            }
            else
            {
                createdAction =
                    new InputAction(
                        actionName,
                        InputActionType.Button,
                        keyboardBinding);

                if (enableGamepad)
                {
                    createdAction.AddBinding(
                        "<Gamepad>/buttonSouth");
                }
            }

            createdAction.performed +=
                callback;

            createdAction.Enable();

            return createdAction;
        }

        // ================================================================
        // ATTACK CALLBACKS
        // ================================================================

        private void ReadLight(
            InputAction.CallbackContext context)
        {
            inputBuffer.Enqueue(
                FighterInputEvent.AttackPress(
                    AttackCommand.Light));
        }

        private void ReadMedium(
            InputAction.CallbackContext context)
        {
            inputBuffer.Enqueue(
                FighterInputEvent.AttackPress(
                    AttackCommand.Medium));
        }

        private void ReadHeavy(
            InputAction.CallbackContext context)
        {
            inputBuffer.Enqueue(
                FighterInputEvent.AttackPress(
                    AttackCommand.Heavy));
        }

        // ================================================================
        // INPUT BUFFER
        // ================================================================

        public bool TryReadInput(
            out FighterInputEvent inputEvent)
        {
            if (inputBuffer.Count == 0)
            {
                inputEvent =
                    default;

                return false;
            }

            inputEvent =
                inputBuffer.Dequeue();

            return true;
        }

        public void ClearInputs()
        {
            inputBuffer.Clear();
        }

        // ================================================================
        // CLEANUP
        // ================================================================

        private void OnDisable()
        {
            DisposeAction(
                ref action,
                ReadMove,
                ReadMoveCanceled);

            DisposeAttackAction(
                ref lightAction,
                ReadLight);

            DisposeAttackAction(
                ref mediumAction,
                ReadMedium);

            DisposeAttackAction(
                ref heavyAction,
                ReadHeavy);

            Direction = 0;

            ClearInputs();
        }

        private void DisposeAction(
            ref InputAction inputAction,
            System.Action<InputAction.CallbackContext> performed,
            System.Action<InputAction.CallbackContext> canceled)
        {
            if (inputAction == null)
                return;

            inputAction.performed -=
                performed;

            inputAction.canceled -=
                canceled;

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

            inputAction.performed -=
                callback;

            inputAction.Disable();
            inputAction.Dispose();

            inputAction = null;
        }
    }
}