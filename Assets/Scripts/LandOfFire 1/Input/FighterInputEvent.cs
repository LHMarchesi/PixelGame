// Land of Fire · Captura del Input System para un jugador.
// Registra pulsaciones entre ticks y ofrece Move y L/M/H sin repetir
// los ataques por hold.
using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public enum FighterInputToken
    {
        Forward,
        Back,
        Light,
        Medium,
        Heavy
    }
    public enum FighterInputEventType
    {
        Direction,
        Attack,
        Jump,
        Parry
    }
    public struct FighterInputEvent
    {
        public FighterInputEventType Type;

        // -1 / 0 / 1 para dirección.
        public int Direction;

        public AttackCommand Attack;

        public ParryInputType ParryInput;

        // Button events from the same Unity input frame can be combined
        // into a P+L/M/H chord regardless of callback order.
        public int InputFrame;

        public static FighterInputEvent DirectionPress(
            int direction)
        {
            return new FighterInputEvent
            {
                Type = FighterInputEventType.Direction,
                Direction = direction,
                Attack = default,
                ParryInput = default,
                InputFrame = UnityEngine.Time.frameCount
            };
        }

        public static FighterInputEvent AttackPress(
            AttackCommand attack)
        {
            return new FighterInputEvent
            {
                Type = FighterInputEventType.Attack,
                Direction = 0,
                Attack = attack,
                ParryInput = default,
                InputFrame = UnityEngine.Time.frameCount
            };
        }

        public static FighterInputEvent JumpPress()
        {
            return new FighterInputEvent
            {
                Type = FighterInputEventType.Jump,
                Direction = 0,
                Attack = default,
                ParryInput = default,
                InputFrame = UnityEngine.Time.frameCount
            };
        }

        public static FighterInputEvent ParryPress(ParryInputType inputType)
        {
            return new FighterInputEvent
            {
                Type = FighterInputEventType.Parry,
                Direction = 0,
                Attack = default,
                ParryInput = inputType,
                InputFrame = UnityEngine.Time.frameCount
            };
        }
    }
}


