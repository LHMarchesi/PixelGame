// Land of Fire · Fuente de comandos para un jugador.
//
// Conecta FighterInput con FighterCommandRecognizer.

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

        public override void ProcessInput(
     int facing,
     long tick)
        {
            if (input == null)
                return;

            while (
                input.TryReadInput(
                    out FighterInputEvent inputEvent))
            {
                Debug.Log(
                    $"[PlayerCommandSource] INPUT " +
                    $"Type={inputEvent.Type} " +
                    $"Direction={inputEvent.Direction} " +
                    $"Attack={inputEvent.Attack} " +
                    $"Tick={tick}");

                recognizer.Push(
                    inputEvent,
                    facing,
                    tick);
            }

            recognizer.Advance(
                tick);
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

            recognizer.Clear();
        }
    }
}