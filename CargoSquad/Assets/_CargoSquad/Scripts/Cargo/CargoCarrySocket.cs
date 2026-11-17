using UnityEngine;

namespace CargoSquad.Cargo
{
    /// <summary>
    /// Attachment point for carried cargo. Put it on a transform of the survivor model (identity scale) whose
    /// +Z points away from the body: CargoInteractor parents the cargo here on pickup, so the cargo follows the
    /// character's position and facing through the hierarchy with no per-frame code.
    /// The socket marks the plane the cargo rests against; each cargo is pushed out by half its own depth, so
    /// cargo of any size sits against the body instead of inside it.
    ///
    /// While cargo is held the socket also owns an obstruction box of the cargo's size. It is an ordinary collider
    /// of the carrier's Rigidbody, so the existing velocity-driven movement is stopped by walls and slides along
    /// them exactly as the capsule does. The cargo itself stays collider-off and rigidly attached.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CargoCarrySocket : MonoBehaviour
    {
        private static readonly Collider[] Overlaps = new Collider[16];
        private static PhysicsMaterial slideMaterial;

        [SerializeField, Min(0f), Tooltip("Extra gap between the socket plane and the cargo's near face (metres).")]
        private float clearance;
        [SerializeField, Min(0f), Tooltip("Largest sideways shift (metres) a turn may need from the carrier before the turn is refused.")]
        private float maxTurnPush = 0.3f;

        private BoxCollider obstruction;
        private Rigidbody body;

        /// <summary>True while a carried cargo's obstruction box is active.</summary>
        public bool IsObstructing => obstruction && obstruction.enabled;

        /// <summary>The active obstruction box, or null. Exposed for tests and debugging.</summary>
        public BoxCollider Obstruction => obstruction;

        /// <summary>Socket-local position of the cargo's collider centre.</summary>
        public Vector3 LocalCenterFor(Vector3 cargoSize) => new Vector3(0f, 0f, cargoSize.z * 0.5f + clearance);

        /// <summary>Socket-local position for the cargo's transform so that its collider centre is held.</summary>
        public Vector3 LocalPositionFor(CargoItem cargo)
        {
            Vector3 pivotToCenter = Vector3.Scale(cargo.Collider.center, cargo.transform.lossyScale);
            return LocalCenterFor(cargo.Size) - pivotToCenter;
        }

        /// <summary>Called by the interactor after the cargo has been parented here.</summary>
        internal void Hold(CargoItem cargo)
        {
            body = GetComponentInParent<Rigidbody>();
            if (!body)
                return;   // a socket on a display model has nothing to obstruct
            if (!obstruction)
            {
                var host = new GameObject("CarriedCargoObstruction");
                host.transform.SetParent(transform, false);
                obstruction = host.AddComponent<BoxCollider>();
                if (!slideMaterial)
                {
                    slideMaterial = new PhysicsMaterial("CarriedCargoSlide")
                    {
                        dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                        frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
                    };
                }
                obstruction.sharedMaterial = slideMaterial;
            }

            Vector3 size = cargo.Size;
            obstruction.transform.localPosition = LocalCenterFor(size);
            obstruction.transform.localRotation = Quaternion.identity;
            obstruction.size = size;
            obstruction.enabled = true;
        }

        internal void Release() => SetObstructionActive(false);

        internal void SetObstructionActive(bool active)
        {
            if (obstruction)
                obstruction.enabled = active;
        }

        /// <summary>
        /// Size-aware turn check. Returns false when turning the model (root) to the given world rotation would put
        /// the carried cargo inside solid geometry and the carrier has no room to be pushed clear of it.
        /// Small overlaps with room behind the carrier are allowed: physics then slides the carrier out.
        /// </summary>
        public bool CanTurnAlong(Transform root, Quaternion target)
        {
            if (!IsObstructing || !body) return true;
            Quaternion start = root.rotation;
            int samples = Mathf.Max(1, Mathf.CeilToInt(Quaternion.Angle(start, target) / 10f));
            // Do not enter a partial turn that cannot finish in a narrow passage. Keeping the
            // current safe facing lets the controller back out with the cargo still attached.
            for (int i = 1; i <= samples; i++)
                if (!CanTurnTo(root, Quaternion.Slerp(start, target, (float)i / samples))) return false;
            return true;
        }

        public bool CanTurnTo(Transform root, Quaternion rotation)
        {
            if (!IsObstructing || !body)
                return true;

            Transform box = obstruction.transform;
            Vector3 localInRoot = Vector3.Scale(root.InverseTransformPoint(box.position), root.lossyScale);
            Vector3 center = root.position + rotation * localInRoot;
            Quaternion boxRotation = rotation * Quaternion.Inverse(root.rotation) * box.rotation;
            Vector3 half = obstruction.size * 0.5f;
            int count = Physics.OverlapBoxNonAlloc(center, half, Overlaps, boxRotation, ~0, QueryTriggerInteraction.Ignore);
            Vector3 push = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Collider other = Overlaps[i];
                if (other.attachedRigidbody == body)
                    continue;
                if (Physics.ComputePenetration(obstruction, center, boxRotation, other, other.transform.position,
                        other.transform.rotation, out Vector3 direction, out float depth))
                {
                    push += direction * depth;
                }
            }

            push.y = 0f;
            float distance = push.magnitude;
            if (distance < 0.001f)
                return true;
            if (distance > maxTurnPush)
                return false;
            // Is there room for the carrier to give way? Sweep its current colliders along the push.
            return !body.SweepTest(push / distance, out _, distance + 0.02f, QueryTriggerInteraction.Ignore);
        }
    }
}
