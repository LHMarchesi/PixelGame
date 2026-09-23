// Land of Fire · Captura del Input System para un jugador.
// Registra pulsaciones entre ticks y ofrece Move y L/M/H sin repetir
// los ataques por hold.
using System.Collections.Generic;
using System;
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
        Attack
    }
    public struct FighterInputEvent
    {
        public FighterInputEventType Type;

        // -1 / 0 / 1 para dirección.
        public int Direction;

        public AttackCommand Attack;

        public static FighterInputEvent DirectionPress(
            int direction)
        {
            return new FighterInputEvent
            {
                Type = FighterInputEventType.Direction,
                Direction = direction,
                Attack = default
            };
        }

        public static FighterInputEvent AttackPress(
            AttackCommand attack)
        {
            return new FighterInputEvent
            {
                Type = FighterInputEventType.Attack,
                Direction = 0,
                Attack = attack
            };
        }
    }
}

   
