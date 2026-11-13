// Land of Fire · Comando reconocido.
//
// Un comando puede representar:
// - Bunny
// - ataques normales Light/Medium/Heavy Punch y Light/Medium Kick
// - ataque especial mediante un AttackMoveData propio.

namespace LandOfFire.BunnyStep
{
    public enum AttackCommand
    {
        Light,
        Medium,
        Heavy,
        LightKick,
        MediumKick
    }

    public enum FighterCommandType
    {
        None,
        Jump,
        BunnyForward,
        BunnyBackward,
        BunnyForwardLoop,
        Attack,
        Parry,
        AutoCombo,
        Rekka,
        RekkaContinue
    }

    public enum ParryInputType
    {
        Imperfect,
        PerfectLight,
        PerfectMedium,
        PerfectHeavy,
        Invalid
    }

    public struct FighterCommand
    {
        public FighterCommandType Type;

        public AttackCommand Attack;

        // Si no es null, este ataque tiene prioridad sobre
        // el AttackCommand genérico.
        public AttackMoveData AttackMove;

        public AutoComboData AutoCombo;

        public RekkaTreeData RekkaTree;

        public RekkaBranchData RekkaBranch;

        public ParryInputType ParryInput;

        public static FighterCommand Parry(ParryInputType inputType)
        {
            return new FighterCommand
            {
                Type = FighterCommandType.Parry,
                ParryInput = inputType
            };
        }

        public static FighterCommand Jump =>
            new FighterCommand
            {
                Type =
                    FighterCommandType.Jump
            };

        public static FighterCommand BunnyForward =>
            new FighterCommand
            {
                Type =
                    FighterCommandType.BunnyForward
            };

        public static FighterCommand BunnyBackward =>
            new FighterCommand
            {
                Type =
                    FighterCommandType.BunnyBackward
            };

        public static FighterCommand BunnyForwardLoop =>
            new FighterCommand
            {
                Type =
                    FighterCommandType.BunnyForwardLoop
            };

        public static FighterCommand AttackCommand(
            AttackCommand attack)
        {
            return new FighterCommand
            {
                Type =
                    FighterCommandType.Attack,

                Attack =
                    attack,

                AttackMove =
                    null
            };
        }
    }
}