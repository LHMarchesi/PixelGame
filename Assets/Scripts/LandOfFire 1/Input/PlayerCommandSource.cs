// Land of Fire · Fuente de comandos para un jugador.
//
// Conecta FighterInput con FighterCommandRecognizer.

using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [RequireComponent(typeof(FighterInput))]
    public sealed class PlayerCommandSource :
        FighterCommandSource
    {
        [SerializeField]
        private FighterInput input;

        [SerializeField]
        private FighterMoveset moveset;

        [SerializeField, Min(1)]
        private int commandBufferTicks = 12;

        private FighterCommandRecognizer recognizer;

        private readonly List<FighterInputEvent> pendingInputEvents =
            new List<FighterInputEvent>(8);

        private void Awake()
        {
            if (input == null)
            {
                input =
                    GetComponent<FighterInput>();
            }

            recognizer =
                new FighterCommandRecognizer();

            recognizer.Configure(
                moveset,
                commandBufferTicks);
        }

        public override int Direction =>
            input != null
                ? input.Direction
                : 0;

        public override bool IsAttackHeld(AttackCommand attack)
        {
            return input != null && input.IsAttackHeld(attack);
        }

        public override void ConfigureMovement(
            FighterMovementProfile profile)
        {
            if (recognizer == null || profile == null)
                return;

            recognizer.SetDoubleTapTicks(
                profile.doubleTapTicks);
        }

        public override void ConfigureRekkaContinuationBranches(
            IList<RekkaBranchData> branches)
        {
            if (recognizer != null)
                recognizer.SetRekkaContinuationBranches(branches);
        }

        public override void ClearRekkaContinuationBranches()
        {
            if (recognizer != null)
                recognizer.ClearRekkaContinuationBranches();
        }

        public override void ProcessInput(
            int facing,
            long tick)
        {
            if (input == null)
                return;

            pendingInputEvents.Clear();
            while (input.TryReadInput(out FighterInputEvent inputEvent))
                pendingInputEvents.Add(inputEvent);

            // Input System callbacks for a simultaneous direction + attack
            // may arrive in either order. Process same-frame directions first
            // so Forward+Medium is recognized consistently as one chord.
            for (int i = 1; i < pendingInputEvents.Count; i++)
            {
                FighterInputEvent value = pendingInputEvents[i];
                int j = i - 1;
                while (j >= 0 && CompareInputEvents(
                    value, pendingInputEvents[j]) < 0)
                {
                    pendingInputEvents[j + 1] = pendingInputEvents[j];
                    j--;
                }
                pendingInputEvents[j + 1] = value;
            }

            int activeInputFrame = int.MinValue;
            int sameFrameDirection = int.MinValue;
            int sameFrameVerticalDirection = int.MinValue;

            for (int i = 0; i < pendingInputEvents.Count; i++)
            {
                FighterInputEvent inputEvent = pendingInputEvents[i];
                if (inputEvent.InputFrame != activeInputFrame)
                {
                    activeInputFrame = inputEvent.InputFrame;
                    sameFrameDirection = int.MinValue;
                    sameFrameVerticalDirection = int.MinValue;
                }

                if (inputEvent.Type == FighterInputEventType.Direction)
                {
                    sameFrameDirection = inputEvent.Direction;
                    sameFrameVerticalDirection = inputEvent.VerticalDirection;
                }
                else if (inputEvent.Type == FighterInputEventType.Attack &&
                         (sameFrameDirection != int.MinValue ||
                          sameFrameVerticalDirection != int.MinValue))
                {
                    // The attack callback may run before the Move callback
                    // in Unity's Input System. Sorting puts the direction
                    // event first, preserving same-frame cardinal direction chords.
                    if (sameFrameDirection != int.MinValue)
                        inputEvent.Direction = sameFrameDirection;
                    if (sameFrameVerticalDirection != int.MinValue)
                        inputEvent.VerticalDirection = sameFrameVerticalDirection;
                }

                Debug.Log(
                    $"[PlayerCommandSource] INPUT " +
                    $"Type={inputEvent.Type} " +
                    $"Direction=({inputEvent.Direction},{inputEvent.VerticalDirection}) " +
                    $"Attack={inputEvent.Attack} " +
                    $"Tick={tick}");

                recognizer.Push(inputEvent, facing, tick);
            }

            recognizer.Advance(tick);
        }

        private static int CompareInputEvents(
            FighterInputEvent first,
            FighterInputEvent second)
        {
            int frame = first.InputFrame.CompareTo(second.InputFrame);
            if (frame != 0)
                return frame;

            int firstPriority = InputEventPriority(first.Type);
            int secondPriority = InputEventPriority(second.Type);
            return firstPriority.CompareTo(secondPriority);
        }

        private static int InputEventPriority(FighterInputEventType type)
        {
            switch (type)
            {
                case FighterInputEventType.Direction: return 0;
                case FighterInputEventType.Attack: return 1;
                default: return 2;
            }
        }

        public override bool TryReadCommand(
            out FighterCommand command)
        {
            return recognizer.TryReadCommand(
                out command);
        }

        public override void ClearCommands()
        {
            if (input != null)
                input.ClearInputs();

            recognizer.ClearRekkaContinuationBranches();
            recognizer.Clear();
        }
    }
}