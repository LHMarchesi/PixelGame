using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [CreateAssetMenu(menuName = "Land of Fire/Fighter Movement Profile")]
    public sealed class FighterMovementProfile : ScriptableObject
    {
        [Header("Adelante")]
        [Min(1)] public int forwardTicks = 12;
        [Min(0)] public float forwardDistance = 1.2f;
        [Min(0)] public float forwardHeight = .3f;
        [Header("Adelante sostenido: recuperación > avance > recuperación")]
        [Min(1)] public int forwardLoopTicks = 12;
        [Min(0)] public float forwardLoopDistance = 1.2f;
        [Min(0)] public float forwardLoopHeight = .3f;
        [Header("Hurt provisional; el ataque podrá indicar su duración")]
        [Min(1)] public int hurtTicks = 18;
        [Header("Atrás")]
        [Min(1)] public int backwardTicks = 14;
        [Min(0)] public float backwardDistance = .9f;
        [Min(0)] public float backwardHeight = .25f;
        [Min(1)] public int doubleTapTicks = 12;
        [Header("Cuerpo físico (se configura al iniciar)")]
        public Vector2 bodySize = new Vector2(.8f, 1.6f);
        public Vector2 bodyOffset = new Vector2(0, .8f);
        [Min(.01f)] public float mass = 1;
    }
}
