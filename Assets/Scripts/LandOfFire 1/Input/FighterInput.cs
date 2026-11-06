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

        [Header("Parry")]
        [Tooltip("P solo = Parry imperfecto. P + ataque = Parry perfecto de esa categoría.")]
        [SerializeField]
        private InputActionReference parry;

        [SerializeField]
        private InputActionReference jump;

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
        private InputAction parryAction;
        private InputAction jumpAction;

        public int Direction { get; private set; }

        public bool IsAttackHeld(AttackCommand attack)
        {
            switch (attack)
            {
                case AttackCommand.Light:
                    return IsActionPressed(lightAction);
                case AttackCommand.Medium:
                    return IsActionPressed(mediumAction);
                case AttackCommand.Heavy:
                    return IsActionPressed(heavyAction);
                default:
                    return false;
            }
        }

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

            parryAction = CreateParryAction();

            jumpAction =
                CreateJumpAction();
        }

        private InputAction CreateJumpAction()
        {
            InputAction createdAction;

            if (jump != null &&
                jump.action != null)
            {
                createdAction =
                    jump.action.Clone();
            }
            else
            {
                createdAction =
                    new InputAction(
                        "Jump",
                        InputActionType.Button,
                        "<Keyboard>/space");
            }

            createdAction.performed += ReadJump;
            createdAction.Enable();

            return createdAction;
        }

        private InputAction CreateParryAction()
        {
            InputAction createdAction;

            if (parry != null && parry.action != null)
            {
                createdAction = parry.action.Clone();
            }
            else
            {
                createdAction = new InputAction(
                    "Parry",
                    InputActionType.Button,
                    "<Keyboard>/p");

                if (enableGamepad)
                    createdAction.AddBinding("<Gamepad>/rightShoulder");
            }

            createdAction.performed += ReadParry;
            createdAction.Enable();
            return createdAction;
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
            QueueAttackOrParry(AttackCommand.Light);
        }

        private void ReadMedium(
            InputAction.CallbackContext context)
        {
            QueueAttackOrParry(AttackCommand.Medium);
        }

        private void ReadHeavy(
            InputAction.CallbackContext context)
        {
            QueueAttackOrParry(AttackCommand.Heavy);
        }

        private void ReadParry(InputAction.CallbackContext context)
        {
            QueueParryOrChord();
        }

        private void QueueAttackOrParry(AttackCommand attack)
        {
            int frame = Time.frameCount;
            bool parryRequested =
                IsActionPressed(parryAction) ||
                HasQueuedEvent(frame, FighterInputEventType.Parry);

            if (!parryRequested)
            {
                inputBuffer.Enqueue(FighterInputEvent.AttackPress(attack, Direction));
                return;
            }

            QueueParryChord(frame, attack);
        }

        private void QueueParryOrChord()
        {
            int frame = Time.frameCount;
            if (HasAnyAttackModifier(frame))
            {
                QueueParryChord(frame, null);
                return;
            }

            // Keep P as a pending imperfect input for the current input
            // frame. If L/M/H is pressed in that same frame, the later
            // callback upgrades it into a perfect chord.
            RemoveQueuedButtonEvents(frame);
            inputBuffer.Enqueue(
                FighterInputEvent.ParryPress(ParryInputType.Imperfect));
        }

        private void QueueParryChord(int frame, AttackCommand? forcedAttack)
        {
            bool light = IsActionPressed(lightAction) || HasQueuedAttack(frame, AttackCommand.Light);
            bool medium = IsActionPressed(mediumAction) || HasQueuedAttack(frame, AttackCommand.Medium);
            bool heavy = IsActionPressed(heavyAction) || HasQueuedAttack(frame, AttackCommand.Heavy);

            if (forcedAttack.HasValue)
            {
                switch (forcedAttack.Value)
                {
                    case AttackCommand.Light: light = true; break;
                    case AttackCommand.Medium: medium = true; break;
                    case AttackCommand.Heavy: heavy = true; break;
                }
            }

            int count = (light ? 1 : 0) + (medium ? 1 : 0) + (heavy ? 1 : 0);
            ParryInputType inputType;

            if (count > 1)
                inputType = ParryInputType.Invalid;
            else if (light)
                inputType = ParryInputType.PerfectLight;
            else if (medium)
                inputType = ParryInputType.PerfectMedium;
            else if (heavy)
                inputType = ParryInputType.PerfectHeavy;
            else
                inputType = ParryInputType.Imperfect;

            RemoveQueuedButtonEvents(frame);
            inputBuffer.Enqueue(FighterInputEvent.ParryPress(inputType));
        }

        private bool HasAnyAttackModifier(int frame)
        {
            return IsActionPressed(lightAction) ||
                   IsActionPressed(mediumAction) ||
                   IsActionPressed(heavyAction) ||
                   HasQueuedAttack(frame, AttackCommand.Light) ||
                   HasQueuedAttack(frame, AttackCommand.Medium) ||
                   HasQueuedAttack(frame, AttackCommand.Heavy);
        }

        private bool HasQueuedAttack(int frame, AttackCommand attack)
        {
            foreach (FighterInputEvent queued in inputBuffer)
            {
                if (queued.InputFrame == frame &&
                    queued.Type == FighterInputEventType.Attack &&
                    queued.Attack == attack)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasQueuedEvent(int frame, FighterInputEventType type)
        {
            foreach (FighterInputEvent queued in inputBuffer)
            {
                if (queued.InputFrame == frame && queued.Type == type)
                    return true;
            }

            return false;
        }

        private static bool IsActionPressed(InputAction inputAction)
        {
            return inputAction != null && inputAction.enabled && inputAction.IsPressed();
        }

        private void RemoveQueuedButtonEvents(int frame)
        {
            if (inputBuffer.Count == 0)
                return;

            Queue<FighterInputEvent> retained = new Queue<FighterInputEvent>(inputBuffer.Count);
            while (inputBuffer.Count > 0)
            {
                FighterInputEvent queued = inputBuffer.Dequeue();
                bool remove = queued.InputFrame == frame &&
                    (queued.Type == FighterInputEventType.Attack ||
                     queued.Type == FighterInputEventType.Parry);

                if (!remove)
                    retained.Enqueue(queued);
            }

            while (retained.Count > 0)
                inputBuffer.Enqueue(retained.Dequeue());
        }

        private void ReadJump(
            InputAction.CallbackContext context)
        {
            inputBuffer.Enqueue(
                FighterInputEvent.JumpPress());
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

            DisposeAttackAction(
                ref parryAction,
                ReadParry);

            DisposeAttackAction(
                ref jumpAction,
                ReadJump);

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