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
    public enum FighterInputDirectionRequirement
    {
        Any,
        Neutral,
        Forward,
        Back,
        Up,
        Down
    }

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

        [Tooltip("Optional direction requirement for each input step. Keep the list empty to accept any direction. Attack tokens include Light, Medium, Heavy, LightKick and MediumKick. For an input such as Light, Light, Forward+Medium, use [Light, Light, Medium] and set the third requirement to Forward.")]
        public List<FighterInputDirectionRequirement> directionRequirements =
            new List<FighterInputDirectionRequirement>();

        [Tooltip(
            "AutoComboData asset used when Command Type = AutoCombo.")]
        public AutoComboData autoCombo;

        [Tooltip(
            "RekkaTreeData asset used when Command Type = Rekka.")]
        public RekkaTreeData rekkaTree;

        [HideInInspector]
        public RekkaBranchData rekkaBranch;

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