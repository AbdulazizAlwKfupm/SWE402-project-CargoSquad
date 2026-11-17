using CargoSquad.Cargo;
using UnityEngine;

namespace CargoSquad.Visuals
{
    /// <summary>
    /// Visual-only hand placement for the front carry. CargoInteractor parents the carried cargo to the model's
    /// CargoCarrySocket; this component never moves the cargo. It reads the cargo's own size and pulls both wrists
    /// onto its side faces with humanoid IK, so every survivor proportion and cargo size shares the same clips.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FrontCarryVisual : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private CargoCarrySocket socket;
        [SerializeField, Tooltip("Wrist position on a cargo side: x = out from the side face, y = up from the cargo centre, z = forward from the near (body-side) face. Metres.")]
        private Vector3 grip = new Vector3(0.03f, -0.04f, 0.075f);
        [SerializeField, Min(0f), Tooltip("Seconds after pickup before the hands lock on (lets the pickup clip reach first).")]
        private float lockDelay = 0.3f;
        [SerializeField, Min(0.1f)] private float lockSpeed = 6f;

        private CargoInteractor carrier;
        private Transform preview;
        private Vector3 previewSize;
        private Transform held;
        private Vector3 heldSize;
        private float heldTime;
        private float ikWeight;

        public CargoCarrySocket Socket => socket;
        public Transform Held => held;
        public float IkWeight => ikWeight;

        /// <summary>World-space wrist target. side: -1 = character's left, +1 = right.</summary>
        public Vector3 GripPoint(int side)
        {
            var local = new Vector3(side * (heldSize.x * 0.5f + grip.x), grip.y, -heldSize.z * 0.5f + grip.z);
            return held.position + held.rotation * local;
        }

        /// <summary>Showcase / test hook: hold a stand-in object that is not gameplay cargo (attached once, like real cargo).</summary>
        public void SetPreviewCargo(Transform cargo, Vector3 size)
        {
            preview = cargo;
            previewSize = size;
            if (!cargo)
                return;
            cargo.SetParent(socket.transform, true);
            cargo.localPosition = socket.LocalCenterFor(size);
            cargo.localRotation = Quaternion.identity;
        }

        private void Awake()
        {
            if (!animator)
                animator = GetComponent<Animator>();
            if (!socket)
                socket = GetComponentInChildren<CargoCarrySocket>();
            carrier = GetComponentInParent<CargoInteractor>();
        }

        private void LateUpdate()
        {
            Transform now = null;
            Vector3 size = default;
            CargoItem carried = carrier ? carrier.CarriedCargo : null;
            if (carried && socket && carried.transform.parent == socket.transform)
            {
                now = carried.transform;
                size = carried.Size;
            }
            else if (preview)
            {
                now = preview;
                size = previewSize;
            }

            if (now != held)
            {
                held = now;
                heldTime = 0f;
            }

            heldSize = size;
            if (held)
                heldTime += Time.deltaTime;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!animator)
                return;
            ikWeight = Mathf.MoveTowards(ikWeight, held && heldTime >= lockDelay ? 1f : 0f, Time.deltaTime * lockSpeed);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, ikWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, ikWeight);
            if (!held || ikWeight <= 0f)
                return;
            animator.SetIKPosition(AvatarIKGoal.LeftHand, GripPoint(-1));
            animator.SetIKPosition(AvatarIKGoal.RightHand, GripPoint(1));
        }
    }
}
