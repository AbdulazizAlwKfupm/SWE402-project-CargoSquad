using CargoSquad.Cargo;
using UnityEngine;

namespace CargoSquad.Visuals
{
    /// <summary>
    /// Visual-only follower for a survivor model. Lives on a child of the gameplay root, reads the existing
    /// Rigidbody velocity and CargoInteractor state, and drives facing + Animator parameters.
    /// It never moves the gameplay root, the cargo, colliders, stamina or grip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivorVisual : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int CarryingParameter = Animator.StringToHash("Carrying");

        [SerializeField] private Animator animator;
        [SerializeField, Tooltip("Weapon object moved between the hand and back sockets.")] private Transform weapon;
        [SerializeField] private Transform handSocket;
        [SerializeField] private Transform stowSocket;
        [SerializeField, Min(0f), Tooltip("Degrees per second.")] private float turnSpeed = 900f;
        [SerializeField, Min(0.1f), Tooltip("Speed (m/s) above which clip playback is sped up.")] private float fullSpeedReference = 6f;
        [SerializeField, Min(1f)] private float maxPlaybackSpeed = 1.3f;
        [SerializeField, Min(0f)] private float speedDamping = 0.08f;

        private Rigidbody body;
        private CargoInteractor carrier;
        private CargoCarrySocket carrySocket;
        private bool stowed;
        private CargoSquad.Combat.PlayerWeapon combat;

        public Animator Animator => animator;
        public bool IsCarrying { get; private set; }
        public Transform Weapon => weapon;
        public Transform StowSocket => stowSocket;
        public Transform HandSocket => handSocket;

        private void Awake()
        {
            if (!animator)
                animator = GetComponentInChildren<Animator>();
            body = GetComponentInParent<Rigidbody>();
            combat = GetComponentInParent<CargoSquad.Combat.PlayerWeapon>();
            carrier = GetComponentInParent<CargoInteractor>();
            carrySocket = GetComponentInChildren<CargoCarrySocket>();
            if (animator)
                animator.applyRootMotion = false;
        }

        private void LateUpdate()
        {
            Vector3 velocity = body ? body.linearVelocity : Vector3.zero;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            bool aiming = combat && combat.ControlsFacing;
            if (speed > 0.15f || aiming)
            {
                Quaternion target = Quaternion.LookRotation(aiming ? combat.AimDirection : velocity / speed, Vector3.up);
                Quaternion next = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
                // Carried cargo must not be swung into a wall: the socket refuses turns that have no room.
                if (!carrySocket || (carrySocket.CanTurnAlong(transform, target) && carrySocket.CanTurnTo(transform, next)))
                    transform.rotation = next;
            }

            IsCarrying = carrier && carrier.CarriedCargo;
            if (weapon && handSocket && stowSocket && stowed != IsCarrying)
            {
                stowed = IsCarrying;
                weapon.SetParent(stowed ? stowSocket : handSocket, false);
                weapon.localPosition = Vector3.zero;
                weapon.localRotation = Quaternion.identity;
            }

            if (!animator)
                return;

            animator.SetFloat(SpeedParameter, speed, speedDamping, Time.deltaTime);
            animator.SetBool(CarryingParameter, IsCarrying);
            animator.speed = Mathf.Clamp(speed / fullSpeedReference, 1f, maxPlaybackSpeed);
        }
    }
}
