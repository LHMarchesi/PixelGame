// Land of Fire · Reconocedor universal de comandos.
//
// Recibe inputs en orden temporal y los compara
// contra las secuencias definidas en FighterMoveset.
//
// Soporta:
// - Bunny Forward (doble tap hacia adelante)
// - Bunny Backward (doble tap hacia atrás)
// - ataques L/M/H normales
// - ataques especiales con AttackMoveData propio
//
// Mantiene la resolución por coincidencia más larga.

using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public sealed class FighterCommandRecognizer
    {
        private sealed class BufferedToken
        {
            public FighterInputToken Token;
            public long Tick;

            // Keep the orientation that was active when this sequence
            // started. A target/facing update between the two taps must
            // not turn the same physical direction into the opposite token.
            public int Facing;
        }

        private readonly List<FighterCommandDefinition>
            definitions =
            new List<FighterCommandDefinition>();

        private readonly List<BufferedToken>
            buffer =
            new List<BufferedToken>();

        private readonly Queue<FighterCommand>
            commands =
            new Queue<FighterCommand>();

        private int sequenceGapTicks = 12;

        private int doubleTapTicks = 12;

        // ================================================================
        // CONFIGURATION
        // ================================================================

        public void Configure(
            FighterMoveset moveset,
            int gapTicks)
        {
            definitions.Clear();

            sequenceGapTicks =
                Mathf.Max(
                    1,
                    gapTicks);

            doubleTapTicks = 12;

            AddDefaultCommands();

            if (moveset == null)
                return;

            foreach (
                FighterCommandDefinition definition
                in moveset.commands)
            {
                if (definition == null)
                    continue;

                if (definition.sequence == null)
                    continue;

                if (definition.sequence.Count == 0)
                    continue;

                if (definition.commandType == FighterCommandType.None ||
                    definition.commandType == FighterCommandType.Parry)
                {
                    continue;
                }

                // Bunny directions are reserved double-tap commands.
                // Do not let a Moveset entry with a higher priority
                // accidentally remap Back,Back to BunnyForward (or vice versa).
                if (definition.commandType ==
                        FighterCommandType.BunnyForward ||
                    definition.commandType ==
                        FighterCommandType.BunnyBackward)
                {
                    continue;
                }

                definitions.Add(
                    definition);
            }
        }

        public void SetDoubleTapTicks(int ticks)
        {
            doubleTapTicks = Mathf.Max(1, ticks);
        }

        // ================================================================
        // DEFAULT COMMANDS
        // ================================================================

        private void AddDefaultCommands()
        {
            // Bunny is now triggered by a forward double tap.
            definitions.Add(
                Create(
                    FighterInputToken.Forward,
                    FighterInputToken.Forward,
                    FighterCommandType.BunnyForward,
                    -1000));

            definitions.Add(
                Create(
                    FighterInputToken.Back,
                    FighterInputToken.Back,
                    FighterCommandType.BunnyBackward,
                    -1000));

            definitions.Add(
                CreateAttack(
                    FighterInputToken.Light,
                    AttackCommand.Light,
                    -1000));

            definitions.Add(
                CreateAttack(
                    FighterInputToken.Medium,
                    AttackCommand.Medium,
                    -1000));

            definitions.Add(
                CreateAttack(
                    FighterInputToken.Heavy,
                    AttackCommand.Heavy,
                    -1000));

        }

        private static FighterCommandDefinition Create(
            FighterInputToken token,
            FighterCommandType type,
            int priority)
        {
            return new FighterCommandDefinition
            {
                sequence =
                    new List<FighterInputToken>
                    {
                        token
                    },

                commandType =
                    type,

                priority =
                    priority
            };
        }

        private static FighterCommandDefinition Create(
            FighterInputToken first,
            FighterInputToken second,
            FighterCommandType type,
            int priority)
        {
            return new FighterCommandDefinition
            {
                sequence =
                    new List<FighterInputToken>
                    {
                        first,
                        second
                    },

                commandType =
                    type,

                priority =
                    priority
            };
        }

        private static FighterCommandDefinition CreateAttack(
            FighterInputToken token,
            AttackCommand attack,
            int priority)
        {
            return new FighterCommandDefinition
            {
                sequence =
                    new List<FighterInputToken>
                    {
                        token
                    },

                commandType =
                    FighterCommandType.Attack,

                attack =
                    attack,

                attackMove =
                    null,

                priority =
                    priority
            };
        }

        // ================================================================
        // INPUT
        // ================================================================

        public void Push(
            FighterInputEvent input,
            int facing,
            long tick)
        {
            if (input.Type ==
                FighterInputEventType.Direction)
            {
                if (input.Direction == 0)
                    return;

                // Resolve both taps against the facing captured by the
                // first tap. This keeps a double tap consistent even if the
                // opponent crosses the fighter during the tap window.
                int sequenceFacing =
                    facing >= 0 ? 1 : -1;

                if (buffer.Count > 0 &&
                    IsBunnyCandidate())
                {
                    sequenceFacing =
                        buffer[0].Facing;
                }

                FighterInputToken token =
                    DirectionToToken(
                        input.Direction,
                        sequenceFacing);

                Debug.Log(
                    $"[FighterCommandRecognizer] Direction press " +
                    $"world={input.Direction}, facing={sequenceFacing}, " +
                    $"token={token}, tick={tick}");

                PushToken(
                    token,
                    tick,
                    sequenceFacing);

                return;
            }

            // Jump is a standalone command. Do not put it through the
            // directional sequence buffer, otherwise it could interact
            // with the Bunny double-tap candidate.
            if (input.Type == FighterInputEventType.Jump)
            {
                commands.Enqueue(
                    FighterCommand.Jump);

                return;
            }

            // Parry is a one-tick defensive command, not a token in the
            // Moveset sequence buffer. It must reach the motor immediately.
            if (input.Type == FighterInputEventType.Parry)
            {
                commands.Enqueue(FighterCommand.Parry(input.ParryInput));
                return;
            }

            switch (input.Attack)
            {
                case AttackCommand.Light:

                    PushToken(
                        FighterInputToken.Light,
                        tick,
                        facing);

                    break;

                case AttackCommand.Medium:

                    PushToken(
                        FighterInputToken.Medium,
                        tick,
                        facing);

                    break;

                case AttackCommand.Heavy:

                    PushToken(
                        FighterInputToken.Heavy,
                        tick,
                        facing);

                    break;
            }
        }

        private void PushToken(
            FighterInputToken token,
            long tick,
            int facing)
        {
            if (buffer.Count > 0)
            {
                long elapsed =
                    tick -
                    buffer[
                        buffer.Count - 1
                    ].Tick;

                int allowedGap =
                    IsBunnyCandidate()
                        ? doubleTapTicks
                        : sequenceGapTicks;

                if (elapsed > allowedGap)
                {
                    FlushBuffer();
                }
            }

            buffer.Add(
                new BufferedToken
                {
                    Token =
                        token,

                    Tick =
                        tick,

                    Facing =
                        facing >= 0 ? 1 : -1
                });

            ResolveBuffer();
        }

        public void Advance(
            long tick)
        {
            if (buffer.Count == 0)
                return;

            long elapsed =
                tick -
                buffer[
                    buffer.Count - 1
                ].Tick;

            int allowedGap =
                IsBunnyCandidate()
                    ? doubleTapTicks
                    : sequenceGapTicks;

            if (elapsed > allowedGap)
            {
                FlushBuffer();
            }
        }

        // ================================================================
        // BUNNY CANDIDATE
        // ================================================================

        private bool IsBunnyCandidate()
        {
            if (buffer.Count == 0)
                return false;

            FighterInputToken first =
                buffer[0].Token;

            if (!IsDirectionToken(first))
                return false;

            if (buffer.Count == 1)
            {
                return HasLongerSequenceStartingWith(
                    first);
            }

            if (buffer.Count == 2)
            {
                FighterInputToken second =
                    buffer[1].Token;

                return
                    first == second &&
                    IsDirectionToken(first);
            }

            return false;
        }

        // ================================================================
        // RESOLUTION
        // ================================================================

        private void ResolveBuffer()
        {
            while (
                buffer.Count > 0)
            {
                if (buffer.Count == 1 &&
                    IsDirectionToken(
                        buffer[0].Token) &&
                    HasLongerSequenceStartingWith(
                        buffer[0].Token))
                {
                    return;
                }

                FighterCommandDefinition exact =
                    FindBestExact(
                        buffer);

                bool isPrefix =
                    IsPrefix(
                        buffer);

                if (exact != null &&
                    isPrefix)
                {
                    return;
                }

                if (exact != null)
                {
                    Enqueue(
                        exact);

                    RemovePrefix(
                        exact.sequence.Count);

                    continue;
                }

                int prefixLength =
                    FindLongestExactPrefixLength();

                if (prefixLength > 0)
                {
                    FighterCommandDefinition prefix =
                        FindBestExactPrefix(
                            prefixLength);

                    if (prefix != null)
                    {
                        Enqueue(
                            prefix);

                        RemovePrefix(
                            prefix.sequence.Count);

                        continue;
                    }
                }

                buffer.RemoveAt(0);
            }
        }

        // ================================================================
        // PREFIX DETECTION
        // ================================================================

        private bool HasLongerSequenceStartingWith(
            FighterInputToken token)
        {
            foreach (
                FighterCommandDefinition definition
                in definitions)
            {
                if (definition.sequence == null)
                    continue;

                if (definition.sequence.Count <= 1)
                    continue;

                if (definition.sequence[0] ==
                    token)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPrefix(
            List<BufferedToken> tokens)
        {
            foreach (
                FighterCommandDefinition definition
                in definitions)
            {
                if (definition.sequence == null)
                    continue;

                if (definition.sequence.Count <=
                    tokens.Count)
                {
                    continue;
                }

                if (MatchesPrefix(
                    definition.sequence,
                    tokens,
                    tokens.Count))
                {
                    return true;
                }
            }

            return false;
        }

        // ================================================================
        // FLUSH
        // ================================================================

        private void FlushBuffer()
        {
            while (
                buffer.Count > 0)
            {
                int longestLength =
                    FindLongestExactPrefixLength();

                if (longestLength <= 0)
                {
                    buffer.RemoveAt(0);
                    continue;
                }

                FighterCommandDefinition definition =
                    FindBestExactPrefix(
                        longestLength);

                if (definition == null)
                {
                    buffer.RemoveAt(0);
                    continue;
                }

                Enqueue(
                    definition);

                RemovePrefix(
                    definition.sequence.Count);
            }
        }

        // ================================================================
        // SEARCH
        // ================================================================

        private int FindLongestExactPrefixLength()
        {
            int best = 0;

            for (
                int length = 1;
                length <= buffer.Count;
                length++)
            {
                if (FindExact(
                    buffer,
                    length) != null)
                {
                    best = length;
                }
            }

            return best;
        }

        private FighterCommandDefinition
            FindBestExactPrefix(
                int length)
        {
            if (length <= 0)
                return null;

            FighterCommandDefinition best =
                null;

            foreach (
                FighterCommandDefinition definition
                in definitions)
            {
                if (definition.sequence == null)
                    continue;

                if (definition.sequence.Count !=
                    length)
                {
                    continue;
                }

                if (!MatchesPrefix(
                    definition.sequence,
                    buffer,
                    length))
                {
                    continue;
                }

                if (best == null ||
                    definition.priority >
                    best.priority)
                {
                    best =
                        definition;
                }
            }

            return best;
        }

        private FighterCommandDefinition
            FindBestExact(
                List<BufferedToken> tokens)
        {
            FighterCommandDefinition best =
                null;

            foreach (
                FighterCommandDefinition definition
                in definitions)
            {
                if (definition.sequence == null)
                    continue;

                if (definition.sequence.Count !=
                    tokens.Count)
                {
                    continue;
                }

                if (!MatchesPrefix(
                    definition.sequence,
                    tokens,
                    tokens.Count))
                {
                    continue;
                }

                if (best == null ||
                    definition.priority >
                    best.priority)
                {
                    best =
                        definition;
                }
            }

            return best;
        }

        private FighterCommandDefinition
            FindExact(
                List<BufferedToken> tokens,
                int length)
        {
            foreach (
                FighterCommandDefinition definition
                in definitions)
            {
                if (definition.sequence == null)
                    continue;

                if (definition.sequence.Count !=
                    length)
                {
                    continue;
                }

                if (MatchesPrefix(
                    definition.sequence,
                    tokens,
                    length))
                {
                    return definition;
                }
            }

            return null;
        }

        private static bool MatchesPrefix(
            List<FighterInputToken> sequence,
            List<BufferedToken> tokens,
            int length)
        {
            if (sequence == null ||
                tokens == null)
            {
                return false;
            }

            if (sequence.Count < length ||
                tokens.Count < length)
            {
                return false;
            }

            for (
                int i = 0;
                i < length;
                i++)
            {
                if (sequence[i] !=
                    tokens[i].Token)
                {
                    return false;
                }
            }

            return true;
        }

        // ================================================================
        // OUTPUT
        // ================================================================

        private void Enqueue(
            FighterCommandDefinition definition)
        {
            if (definition == null)
                return;

            FighterCommand command =
                new FighterCommand
                {
                    Type =
                        definition.commandType,

                    Attack =
                        definition.attack,

                    AttackMove =
                        definition.attackMove
                };

            if (definition.commandType == FighterCommandType.BunnyForward ||
                definition.commandType == FighterCommandType.BunnyBackward)
            {
                Debug.Log(
                    $"[FighterCommandRecognizer] Recognized " +
                    $"{definition.commandType}");
            }

            commands.Enqueue(
                command);
        }

        private void RemovePrefix(
            int count)
        {
            count =
                Mathf.Clamp(
                    count,
                    0,
                    buffer.Count);

            if (count <= 0)
                return;

            buffer.RemoveRange(
                0,
                count);
        }

        public bool TryReadCommand(
            out FighterCommand command)
        {
            if (commands.Count == 0)
            {
                command =
                    default;

                return false;
            }

            command =
                commands.Dequeue();

            return true;
        }

        public void Clear()
        {
            buffer.Clear();
            commands.Clear();
        }

        // ================================================================
        // DIRECTION
        // ================================================================

        private static bool IsDirectionToken(
            FighterInputToken token)
        {
            return
                token ==
                FighterInputToken.Forward ||
                token ==
                FighterInputToken.Back;
        }

        private static FighterInputToken
            DirectionToToken(
                int direction,
                int facing)
        {
            return direction == facing
                ? FighterInputToken.Forward
                : FighterInputToken.Back;
        }
    }
}