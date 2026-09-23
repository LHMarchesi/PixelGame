using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [CreateAssetMenu(
        menuName = "Land of Fire/Fighter Movement Profile")]
    public sealed class FighterMovementProfile :
        ScriptableObject
    {
        [Header("Adelante")]

        public BunnyTiming forwardTiming =
            new BunnyTiming(2, 7, 3);

        [HideInInspector]
        public int forwardTicks = 12;

        [Min(0)]
        public float forwardDistance = 1.2f;

        [Min(0)]
        public float forwardHeight = .3f;


        [Header(
            "Adelante sostenido: usa solo Flight y Recovery " +
            "(Preparation se ignora)")]

        public BunnyTiming forwardLoopTiming =
            new BunnyTiming(0, 7, 3);

        [HideInInspector]
        public int forwardLoopTicks = 12;

        [Min(0)]
        public float forwardLoopDistance = 1.2f;

        [Min(0)]
        public float forwardLoopHeight = .3f;


        [Header("Levantarse")]

        [Tooltip(
            "Cantidad de ticks que el personaje tarda en " +
            "levantarse después del Knockdown.")]
        [Min(1)]
        public int resetTicks = 20;


        [Header("Atrás")]

        public BunnyTiming backwardTiming =
            new BunnyTiming(2, 9, 3);

        [HideInInspector]
        public int backwardTicks = 14;

        [Min(0)]
        public float backwardDistance = .9f;

        [Min(0)]
        public float backwardHeight = .25f;

        [Min(1)]
        public int doubleTapTicks = 12;


        [Header("Cuerpo físico")]

        public Vector2 bodySize =
            new Vector2(.8f, 1.6f);

        public Vector2 bodyOffset =
            new Vector2(0f, .8f);

        [Min(.01f)]
        public float mass = 1f;


        [Header("Estados Animator")]

        public PhaseAnimation forwardPreparation =
            new PhaseAnimation(
                "BunnyForwardPreparation");

        public PhaseAnimation forwardFlight =
            new PhaseAnimation(
                "BunnyForwardFlight");

        public PhaseAnimation forwardRecovery =
            new PhaseAnimation(
                "BunnyForwardRecovery");


        public PhaseAnimation backwardPreparation =
            new PhaseAnimation(
                "BunnyBackwardPreparation");

        public PhaseAnimation backwardFlight =
            new PhaseAnimation(
                "BunnyBackwardFlight");

        public PhaseAnimation backwardRecovery =
            new PhaseAnimation(
                "BunnyBackwardRecovery");


        public PhaseAnimation idle =
            new PhaseAnimation(
                "Idle");

        public PhaseAnimation guard =
            new PhaseAnimation(
                "Guard");

        public PhaseAnimation hurt =
            new PhaseAnimation(
                "Hurt");

        public PhaseAnimation flyingHurt =
            new PhaseAnimation(
                "FlyingHurt");

        public PhaseAnimation knockdown =
            new PhaseAnimation(
                "Knockdown");

        public PhaseAnimation reset =
            new PhaseAnimation(
                "GetUp");


        public PhaseAnimation AnimationFor(
            FighterState state,
            BunnyPhase phase)
        {
            switch (state)
            {
                case FighterState.Idle:
                    return idle;

                case FighterState.Guard:
                    return guard;

                case FighterState.Hurt:
                    return hurt;

                case FighterState.FlyingHurt:
                    return flyingHurt;

                case FighterState.Knockdown:
                    return knockdown;

                case FighterState.Reset:
                    return reset;
            }

            switch (phase)
            {
                case BunnyPhase.Preparation:

                    if (state == FighterState.BunnyForward)
                        return forwardPreparation;

                    if (state == FighterState.BunnyBackward)
                        return backwardPreparation;

                    return null;

                case BunnyPhase.Flight:

                    if (state == FighterState.BunnyForward ||
                        state == FighterState.BunnyForwardLoop)
                    {
                        return forwardFlight;
                    }

                    if (state == FighterState.BunnyBackward)
                        return backwardFlight;

                    return null;

                case BunnyPhase.Recovery:

                    if (state == FighterState.BunnyForward ||
                        state == FighterState.BunnyForwardLoop)
                    {
                        return forwardRecovery;
                    }

                    if (state == FighterState.BunnyBackward)
                        return backwardRecovery;

                    return null;

                default:
                    return null;
            }
        }
    }
}