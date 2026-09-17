// Land of Fire · FUN-COM-001
// Marca un Collider2D como zona que puede recibir golpes. Va en un hijo de
// FighterRoot, fuera de Visual, y reconoce a su propio luchador sin enlazar rival.
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class FighterHurtbox2D : MonoBehaviour
    {
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
