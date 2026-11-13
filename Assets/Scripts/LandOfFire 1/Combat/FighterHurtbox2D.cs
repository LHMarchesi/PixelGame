// Land of Fire · FUN-COM-001
// Marca un Collider2D como zona que puede recibir golpes. Va en un hijo de
// FighterRoot, fuera de Visual, y reconoce a su propio luchador sin enlazar rival.
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    /// <summary>Reaction family used by attacks and resolved from a hurtbox when set to None.</summary>
    public enum FighterHurtType
    {
        None,
        Overhead,
        Body,
        Legs
    }

    /// <summary>Physical collider zones. Body is zero to keep existing single-hurtbox setups usable by default.</summary>
    public enum FighterHurtboxRegion
    {
        Body,
        Overhead,
        Legs
    }

    public enum FighterHurtPhase
    {
        None,
        Collision,
        Impact,
        Recovery
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class FighterHurtbox2D : MonoBehaviour
    {
        [Tooltip("Physical region represented by this collider. Used by AttackMoveData when its Hurt Reaction is None.")]
        public FighterHurtboxRegion region = FighterHurtboxRegion.Body;

        public FighterMotor2D Owner { get; private set; }
        public FighterHealth Health { get; private set; }

        private void Awake()
        {
            Owner = GetComponentInParent<FighterMotor2D>();
            if (Owner != null) Health = Owner.GetComponent<FighterHealth>();
            GetComponent<BoxCollider2D>().isTrigger = true;
            if (Owner == null || Health == null)
                Debug.LogError("La Hurtbox necesita FighterMotor2D y FighterHealth en FighterRoot.", this);
        }
    }
}
