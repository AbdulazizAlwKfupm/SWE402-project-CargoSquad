using UnityEngine;

namespace CargoSquad.Cargo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CargoItem : MonoBehaviour
    {
        private BoxCollider cargoCollider;
        private Transform storageParent;
        private Quaternion groundRotation;

        public BoxCollider Collider => cargoCollider;
        public CargoCondition Condition { get; private set; }
        public CargoDefinition Definition => Condition ? Condition.Definition : null;
        public bool IsDestroyed => Condition && Condition.IsDestroyed;
        public bool IsCarried { get; private set; }
        public bool IsDelivered { get; private set; }

        private void Awake()
        {
            cargoCollider = GetComponent<BoxCollider>();
            Condition = GetComponent<CargoCondition>();
            storageParent = transform.parent;
            groundRotation = transform.rotation;
        }

        internal void PickUp(Transform carrier, Vector3 localOffset)
        {
            IsCarried = true;
            cargoCollider.enabled = false;
            transform.SetParent(carrier, true);
            transform.localPosition = localOffset;
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>World-space dimensions of this cargo (its own collider and scale; nothing global).</summary>
        public Vector3 Size => Vector3.Scale(cargoCollider.size, transform.lossyScale);
        internal Vector3 DropHalfExtents => Size * 0.5f;
        internal Quaternion GroundRotation => groundRotation;

        internal void DropAt(Vector3 colliderCenter)
        {
            transform.SetParent(storageParent, true);
            transform.rotation = groundRotation;
            transform.position += colliderCenter - transform.TransformPoint(cargoCollider.center);
            cargoCollider.enabled = true;
            IsCarried = false;
        }

        internal void Deliver()
        {
            IsDelivered = true;
            Discard();
        }

        internal void Discard()
        {
            IsCarried = false;
            transform.SetParent(storageParent, true);
            gameObject.SetActive(false);
        }
    }
}
