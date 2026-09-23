// Land of Fire · Definición de comandos de un Fighter.
//
// El Moveset define:
// - secuencias de input
// - comando resultante
// - prioridad
// - AttackMoveData opcional para ataques especiales

using System;
using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [CreateAssetMenu(
        menuName = "Land of Fire/Fighter/Moveset")]
    public sealed class FighterMoveset :
        ScriptableObject
    {
        [Tooltip(
            "Lista de comandos específicos de este Fighter.")]
        public List<FighterCommandDefinition>
            commands =
            new List<FighterCommandDefinition>();
    }

    [Serializable]
    public sealed class FighterCommandDefinition
    {
        [Tooltip(
            "Secuencia de inputs que activa este comando.")]
        public List<FighterInputToken>
            sequence =
            new List<FighterInputToken>();

        [Tooltip(
            "Tipo de comando generado.")]
        public FighterCommandType commandType =
            FighterCommandType.None;

        [Tooltip(
            "Ataque L/M/H usado cuando Command Type = Attack " +
            "y no se asigna un Attack Move propio.")]
        public AttackCommand attack;

        [Tooltip(
            "AttackMoveData específico de esta técnica. " +
            "Cuando está asignado, se usa directamente.")]
        public AttackMoveData attackMove;

        [Tooltip(
            "Mayor prioridad gana cuando varias definiciones " +
            "tienen la misma secuencia.")]
        public int priority;
    }
}