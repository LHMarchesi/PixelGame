// Land of Fire · Asset de movimiento y animaciones de Bunny, Guard, Hurt,
// Flying Hurt, Knockdown y Reset.
// Cada FighterMotor2D crea una copia en Play para configurar a cada luchador por separado.

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

        [Header(
            "Hurt provisional; el ataque podrá indicar su duración")]

        [Min(1)]
        public int hurtTicks = 18;

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
            new Vector2(0, .8f);

        [Min(.01f)]
        public float mass = 1;

        [Header("Estados Animator")]

        public PhaseAnimation forwardPreparation =
            new PhaseAnimation(
                "ForwardPreparation");

        public PhaseAnimation forwardFlight =
            new PhaseAnimation(
                "ForwardFlight");

        public PhaseAnimation forwardRecovery =
            new PhaseAnimation(
                "ForwardRecovery");

        public PhaseAnimation backwardPreparation =
            new PhaseAnimation(
                "BackwardPreparation");

        public PhaseAnimation backwardFlight =
            new PhaseAnimation(
                "BackwardFlight");

        public PhaseAnimation backwardRecovery =
            new PhaseAnimation(
                "BackwardRecovery");

        public PhaseAnimation idle =
            new PhaseAnimation("Idle")
            {
                playback = PhasePlayback.Loop,
                loopTicks = 60
            };

        public PhaseAnimation guard =
            new PhaseAnimation("Guard")
            {
                playback = PhasePlayback.Loop,
                loopTicks = 60
            };

        public PhaseAnimation hurt =
            new PhaseAnimation("Hurt");

        public PhaseAnimation flyingHurt =
            new PhaseAnimation("FlyingHurt");

        public PhaseAnimation knockdown =
            new PhaseAnimation("Knockdown");

        public PhaseAnimation reset =
            new PhaseAnimation("GetUp");

        public PhaseAnimation AnimationFor(
            FighterState state,
            BunnyPhase phase)
        {
            if (state == FighterState.Idle)
                return idle;

            if (state == FighterState.Guard)
                return guard;

            if (state == FighterState.Hurt)
                return hurt;

            if (state == FighterState.FlyingHurt)
                return flyingHurt;

            if (state == FighterState.Knockdown)
                return knockdown;

            if (state == FighterState.Reset)
                return reset;

            bool backward =
                state == FighterState.BunnyBackward;

            if (phase == BunnyPhase.Preparation)
            {
                return backward
                    ? backwardPreparation
                    : forwardPreparation;
            }

            if (phase == BunnyPhase.Flight)
            {
                return backward
                    ? backwardFlight
                    : forwardFlight;
            }

            return backward
                ? backwardRecovery
                : forwardRecovery;
        }
    }
}