using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [CreateAssetMenu(
        menuName = "Land of Fire/Fighter Movement Profile")]
    public sealed class FighterMovementProfile :
        ScriptableObject
    {
        [Header("Ground Walk")]

        [Min(0f), Tooltip("Velocidad de caminata en unidades por segundo.")]
        public float walkSpeed = 2.5f;

        [Tooltip("Si está activo, mantener atrás camina hacia atrás en lugar de activar Guard.")]
        public bool allowBackwardWalk = false;

        [Header("Adelante")]

        public BunnyTiming forwardTiming =
            new BunnyTiming(2, 7, 3);

        [HideInInspector]
        public int forwardTicks = 12;

        [Min(0)]
        public float forwardDistance = 1.2f;

        [Min(0)]
        public float forwardHeight = .3f;


        [Header("Bunny Forward Loop (opcional desde FighterMoveset)")]

        public BunnyTiming forwardLoopTiming =
            new BunnyTiming(0, 7, 3);

        [HideInInspector]
        public int forwardLoopTicks = 12;

        [Min(0)]
        public float forwardLoopDistance = 1.2f;

        [Min(0)]
        public float forwardLoopHeight = .3f;


        [Header("Salto")]

        [Min(1)]
        public int jumpAnticipationTicks = 3;

        [Min(1)]
        public int jumpAirTicks = 12;

        [Min(1)]
        public int jumpDescendingTicks = 12;

        [Min(1)]
        public int jumpRecoveryTicks = 3;

        [Header("Air Attack Input Window")]

        [Min(1)]
        [Tooltip(
            "Primer tick en el que se acepta un Air Attack. " +
            "El conteo comienza en el primer tick de JumpPhase.Air: " +
            "1..jumpAirTicks son Air y los siguientes pertenecen a Descending. " +
            "Usá jumpAirTicks para abrir desde el ápice, o jumpAirTicks + 1 " +
            "para abrir solamente durante Descending.")]
        public int airAttackInputStartTick = 12;

        [Min(1)]
        [Tooltip(
            "Último tick inclusivo en el que se acepta el primer Air Attack. " +
            "Se cuenta desde el primer tick de JumpPhase.Air y puede extenderse " +
            "hasta el final de Descending.")]
        public int airAttackInputEndTick = 24;

        [Min(0)]
        public float jumpHeight = 1.2f;

        [Min(0)]
        [Tooltip("Desplazamiento horizontal por tick mientras está en Air o Descending.")]
        public float jumpAirControl = .035f;

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

        [Header("Bunny Step Input")]

        [Min(1), Tooltip("Máximo de ticks entre las dos pulsaciones para ejecutar Bunny Forward o Bunny Backward.")]
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

        public PhaseAnimation walk =
            new PhaseAnimation("Walk")
            {
                playback = PhasePlayback.Loop,
                loopTicks = 24
            };

        public PhaseAnimation jumpAnticipation =
            new PhaseAnimation(
                "JumpAnticipation");

        public PhaseAnimation jumpAir =
            new PhaseAnimation(
                "JumpAir");

        public PhaseAnimation jumpDescending =
            new PhaseAnimation(
                "JumpDescending");

        public PhaseAnimation jumpRecovery =
            new PhaseAnimation(
                "JumpRecovery");

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
            return AnimationFor(state, phase, JumpPhase.None);
        }

        public PhaseAnimation AnimationFor(
            FighterState state,
            BunnyPhase phase,
            JumpPhase jumpPhase)
        {
            switch (state)
            {
                case FighterState.Idle:
                    return idle;

                case FighterState.Guard:
                    return guard;

                case FighterState.Walk:
                    return walk != null ? walk : idle;

                case FighterState.Jump:
                    switch (jumpPhase)
                    {
                        case JumpPhase.Anticipation:
                            return jumpAnticipation;
                        case JumpPhase.Air:
                            return jumpAir;
                        case JumpPhase.Descending:
                            return jumpDescending;
                        case JumpPhase.Recovery:
                            return jumpRecovery;
                        default:
                            return null;
                    }

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