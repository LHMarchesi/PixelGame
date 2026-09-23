// Land of Fire · Comando reconocido.
//
// Un comando puede representar:
// - Bunny
// - ataque normal L/M/H
// - ataque especial mediante un AttackMoveData propio.

namespace LandOfFire.BunnyStep
{
    public enum AttackCommand
    {
        Light,
        Medium,
        Heavy
    }

    public enum FighterCommandType
    {
        None,
        BunnyForward,
        BunnyBackward,
        BunnyForwardLoop,
        Attack
    }

    public struct FighterCommand
    {
        public FighterCommandType Type;

        public AttackCommand Attack;

        // Si no es null, este ataque tiene prioridad sobre
        // el AttackCommand genérico.
        public AttackMoveData AttackMove;

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