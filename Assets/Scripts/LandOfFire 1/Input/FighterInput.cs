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

        [SerializeField, Tooltip("InputActionReference del botón Light Kick. Asigná su binding desde tu Input Actions Asset.")]
        private InputActionReference lightKickAttack;

        [SerializeField, Tooltip("InputActionReference del botón Medium Kick. Asigná su binding desde tu Input Actions Asset.")]
        private InputActionReference mediumKickAttack;

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
        private InputAction lightKickAction;
        private InputAction mediumKickAction;
        private InputAction parryAction;
        private InputAction jumpAction;

        public int Direction { get; private set; }

        public int VerticalDirection { get; private set; }

        private FighterInputToken lastCardinalDirection = (FighterInputToken)(-1);

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
                case AttackCommand.LightKick:
                    return IsActionPressed(lightKickAction);
                case AttackCommand.MediumKick:
                    return IsActionPressed(mediumKickAction);
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
                            : "<Keyboard>/d")
                    .With(
                        "Up",
                        useArrowKeys
                            ? "<Keyboard>/upArrow"
                            : "<Keyboard>/w")
                    .With(
                        "Down",
                        useArrowKeys
                            ? "<Keyboard>/downArrow"
                            : "<Keyboard>/s");

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
            Vector2 value = context.ReadValue<Vector2>();

            int nextX = value.x > threshold
                ? 1
                : value.x < -threshold
                    ? -1
                    : 0;

            int nextY = value.y > threshold
                ? 1
                : value.y < -threshold
                    ? -1
                    : 0;

            // Keep horizontal movement semantics unchanged for the motor.
            Direction = nextX;
            VerticalDirection = nextY;

            // Command recognition uses four cardinal directions. For a
            // diagonal stick/D-pad value, the dominant axis determines the
            // command token; ties resolve horizontally for deterministic input.
            FighterInputToken nextToken = (FighterInputToken)(-1);
            int eventX = 0;
            int eventY = 0;

            if (nextX != 0 || nextY != 0)
            {
                if (Mathf.Abs(value.y) > Mathf.Abs(value.x) && nextY != 0)
                {
                    nextToken = nextY > 0
                        ? FighterInputToken.Up
                        : FighterInputToken.Down;
                    eventY = nextY;
                }
                else if (nextX != 0)
                {
                    nextToken = nextX > 0
                        ? FighterInputToken.Forward
                        : FighterInputToken.Back;
                    eventX = nextX;
                }
                else if (nextY != 0)
                {
                    nextToken = nextY > 0
                        ? FighterInputToken.Up
                        : FighterInputToken.Down;
                    eventY = nextY;
                }
            }

            if (nextToken == lastCardinalDirection)
                return;

            lastCardinalDirection = nextToken;

            if ((int)nextToken < 0)
                return;

            inputBuffer.Enqueue(
                FighterInputEvent.DirectionPress(eventX, eventY));
        }

        private void ReadMoveCanceled(
      InputAction.CallbackContext context)
        {
            Direction = 0;
            VerticalDirection = 0;
            lastCardinalDirection = (FighterInputToken)(-1);
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

            // Kick buttons intentionally have no fallback keyboard/gamepad
            // binding. Their InputActionReference and bindings are assigned
            // by the project in the Input Actions asset.
            lightKickAction =
                CreateAttackAction(
                    "Light Kick",
                    lightKickAttack,
                    null,
                    ReadLightKick);

            mediumKickAction =
                CreateAttackAction(
                    "Medium Kick",
                    mediumKickAttack,
                    null,
                    ReadMediumKick);

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
                // A null binding creates an unbound action. This is used by
                // LK/MK so the game does not silently assign keys that may
                // conflict with the user's custom control layout.
                createdAction = string.IsNullOrEmpty(keyboardBinding)
                    ? new InputAction(actionName, InputActionType.Button)
                    : new InputAction(
                        actionName,
                        InputActionType.Button,
                        keyboardBinding);

                if (enableGamepad && !string.IsNullOrEmpty(keyboardBinding))
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

        private void ReadLightKick(
            InputAction.CallbackContext context)
        {
            QueueAttackOrParry(AttackCommand.LightKick);
        }

        private void ReadMediumKick(
            InputAction.CallbackContext context)
        {
            QueueAttackOrParry(AttackCommand.MediumKick);
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
                inputBuffer.Enqueue(FighterInputEvent.AttackPress(
                    attack, Direction, VerticalDirection));
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
            // Punch and kick buttons share the same Parry category:
            // Light/LK => PerfectLight, Medium/MK => PerfectMedium.
            bool light = IsActionPressed(lightAction) ||
                         IsActionPressed(lightKickAction) ||
                         HasQueuedAttack(frame, AttackCommand.Light) ||
                         HasQueuedAttack(frame, AttackCommand.LightKick);
            bool medium = IsActionPressed(mediumAction) ||
                          IsActionPressed(mediumKickAction) ||
                          HasQueuedAttack(frame, AttackCommand.Medium) ||
                          HasQueuedAttack(frame, AttackCommand.MediumKick);
            bool heavy = IsActionPressed(heavyAction) ||
                         HasQueuedAttack(frame, AttackCommand.Heavy);

            if (forcedAttack.HasValue)
            {
                switch (forcedAttack.Value)
                {
                    case AttackCommand.Light: light = true; break;
                    case AttackCommand.Medium: medium = true; break;
                    case AttackCommand.Heavy: heavy = true; break;
                    case AttackCommand.LightKick: light = true; break;
                    case AttackCommand.MediumKick: medium = true; break;
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
                   IsActionPressed(lightKickAction) ||
                   IsActionPressed(mediumKickAction) ||
                   HasQueuedAttack(frame, AttackCommand.Light) ||
                   HasQueuedAttack(frame, AttackCommand.Medium) ||
                   HasQueuedAttack(frame, AttackCommand.Heavy) ||
                   HasQueuedAttack(frame, AttackCommand.LightKick) ||
                   HasQueuedAttack(frame, AttackCommand.MediumKick);
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
                ref lightKickAction,
                ReadLightKick);

            DisposeAttackAction(
                ref mediumKickAction,
                ReadMediumKick);

            DisposeAttackAction(
                ref parryAction,
                ReadParry);

            DisposeAttackAction(
                ref jumpAction,
                ReadJump);

            Direction = 0;
            VerticalDirection = 0;
            lastCardinalDirection = (FighterInputToken)(-1);

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