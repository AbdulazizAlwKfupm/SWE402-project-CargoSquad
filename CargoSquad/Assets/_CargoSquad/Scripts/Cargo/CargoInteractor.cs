using System;
using CargoSquad.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CargoSquad.Cargo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class CargoInteractor : MonoBehaviour
    {
        private static readonly Vector3[] DropDirections =
        {
            Vector3.forward, Vector3.right, Vector3.back, Vector3.left,
            new Vector3(1f, 0f, 1f).normalized, new Vector3(1f, 0f, -1f).normalized,
            new Vector3(-1f, 0f, -1f).normalized, new Vector3(-1f, 0f, 1f).normalized
        };

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private CargoItem cargo;
        [SerializeField] private CargoItem[] additionalCargo = Array.Empty<CargoItem>();
        [SerializeField, Min(0f), Tooltip("World-space slip noise radius.")] private float slipNoiseRadius = 8f;
        [SerializeField] private BoxCollider ground;
        [SerializeField, Min(0.1f)] private float interactionRange = 2f;
        [SerializeField, Tooltip("Used only when no carry socket is available.")] private Vector3 carryOffset = new Vector3(0f, 1.8f, 0f);
        [SerializeField, Tooltip("Optional. Leave empty to use the CargoCarrySocket on the survivor model under this object.")]
        private CargoCarrySocket carrySocket;

        private CapsuleCollider capsule;
        private InputAction interact;
        private bool dropBlocked;

        private CargoItem carriedCargo;
        private CargoCarrySocket activeSocket;

        public CargoItem CarriedCargo
        {
            get => carriedCargo;
            private set
            {
                carriedCargo = value;
                if (!value && activeSocket)
                {
                    // Every way of losing the cargo (drop, slip, delivery, destruction) frees the socket.
                    activeSocket.Release();
                    activeSocket = null;
                }
            }
        }
        public CargoGrip Grip { get; private set; }
        public bool HasPendingSlip => Grip && CarriedCargo && Grip.SlipPending;
        public event Action<GameplayNoise> NoiseEmitted;
        public string Prompt { get; private set; }

        public void SetCarrySocket(CargoCarrySocket socket)
        {
            if (CarriedCargo) throw new InvalidOperationException("Cannot switch visuals while carrying cargo.");
            carrySocket = socket;
        }

        private void Awake()
        {
            capsule = GetComponent<CapsuleCollider>();
            Grip = GetComponent<CargoGrip>();
            var binding = inputActions ? inputActions.FindAction("Player/Interact") : null;
            if (binding == null || !cargo || !ground)
            {
                Debug.LogError("Assign the interaction action asset, cargo item, and drop ground.", this);
                enabled = false;
                return;
            }

            interact = binding.Clone();
        }

        private void OnEnable() => interact?.Enable();
        private void OnDestroy() => interact?.Dispose();

        private void OnDisable()
        {
            interact?.Disable();
            Prompt = null;
        }

        private void Update()
        {
            var ability = GetComponent<CharacterAbility>();
            if (ability && ability.IsCharging) return;
            bool pressed = interact.WasPressedThisFrame();
            if (CarriedCargo)
            {
                if (CarriedCargo.IsDestroyed)
                {
                    CarriedCargo.Discard();
                    CarriedCargo = null;
                    Prompt = null;
                    return;
                }
                if (HasPendingSlip)
                {
                    TryResolveSlip();
                    Prompt = CarriedCargo ? "Grip lost. Move to clear ground." : null;
                    return;
                }
                if (pressed)
                    dropBlocked = !TryDrop();

                Prompt = CarriedCargo
                    ? (dropBlocked ? "No room to drop. Move and press E again." : "Press E to Drop Cargo")
                    : "Press E to Pick Up Cargo";
                return;
            }

            CargoItem candidate = FindCargoInReach();
            Prompt = candidate ? "Press E to Pick Up Cargo" : null;
            if (candidate && pressed)
            {
                CargoCarrySocket socket = carrySocket ? carrySocket : GetComponentInChildren<CargoCarrySocket>();
                if (socket)
                {
                    candidate.PickUp(socket.transform, socket.LocalPositionFor(candidate));
                    socket.Hold(candidate);
                    activeSocket = socket;
                }
                else
                    candidate.PickUp(transform, carryOffset);
                CarriedCargo = candidate;
                if (Grip)
                    Grip.BeginCarry();
                dropBlocked = false;
                Prompt = "Press E to Drop Cargo";
            }
        }

        public void TickGrip(bool sprinting, bool moving, CharacterDefinition character, float seconds)
        {
            if (!Grip || !CarriedCargo || CarriedCargo.IsDestroyed || !CarriedCargo.Definition)
                return;

            float strength = character ? character.CarryStrength : 1f;
            float stability = character ? character.CargoStability : 1f;
            Grip.Tick(sprinting, moving, CarriedCargo.Definition.GripDrainMultiplier(strength, stability), seconds);
        }

        private void TryResolveSlip()
        {
            CargoItem slippingCargo = CarriedCargo;
            if (!TryDrop())
                return;

            slippingCargo.Condition.ApplyDamage(slippingCargo.Definition.SlipDamage);
            NoiseEmitted?.Invoke(new GameplayNoise(slippingCargo.transform.position, slipNoiseRadius));
        }

        private CargoItem FindCargoInReach()
        {
            CargoItem nearest = null;
            float distance = interactionRange * interactionRange;
            ConsiderCandidate(cargo, ref nearest, ref distance);
            foreach (CargoItem candidate in additionalCargo)
                ConsiderCandidate(candidate, ref nearest, ref distance);
            return nearest;
        }

        private void ConsiderCandidate(CargoItem candidate, ref CargoItem nearest, ref float distance)
        {
            if (!candidate || !candidate.isActiveAndEnabled || candidate.IsCarried ||
                candidate.IsDelivered || candidate.IsDestroyed)
                return;

            Bounds cargoBounds = candidate.Collider.bounds;
            Vector3 origin = capsule.bounds.center;
            float candidateDistance = cargoBounds.SqrDistance(origin);
            if (candidateDistance <= distance &&
                Physics.Linecast(origin, cargoBounds.center, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider == candidate.Collider)
            {
                nearest = candidate;
                distance = candidateDistance;
            }
        }

        private bool TryDrop()
        {
            if (!ground.enabled || !ground.gameObject.activeInHierarchy)
                return false;

            Bounds floor = ground.bounds;
            Vector3 halfSize = CarriedCargo.DropHalfExtents;
            Quaternion rotation = CarriedCargo.GroundRotation;
            // Project the box onto the ground axes so the entire footprint stays over the platform.
            Vector3 x = rotation * Vector3.right * halfSize.x;
            Vector3 y = rotation * Vector3.up * halfSize.y;
            Vector3 z = rotation * Vector3.forward * halfSize.z;
            var footprint = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            Vector3 origin = capsule.bounds.center;
            float distance = capsule.bounds.extents.x + Mathf.Max(footprint.x, footprint.z) + 0.15f;

            // The carried cargo's own obstruction box must not block the search for a place to put it down.
            if (activeSocket)
                activeSocket.SetObstructionActive(false);
            if (TryPlace(floor, halfSize, rotation, footprint, origin, distance))
                return true;
            if (activeSocket)
                activeSocket.SetObstructionActive(true);
            return false;
        }

        private bool TryPlace(Bounds floor, Vector3 halfSize, Quaternion rotation, Vector3 footprint, Vector3 origin, float distance)
        {
            foreach (Vector3 direction in DropDirections)
            {
                Vector3 center = origin + direction * distance;
                center.y = floor.max.y + footprint.y + 0.02f;
                if (center.x - footprint.x < floor.min.x || center.x + footprint.x > floor.max.x ||
                    center.z - footprint.z < floor.min.z || center.z + footprint.z > floor.max.z ||
                    Physics.Linecast(origin, center, ~0, QueryTriggerInteraction.Ignore) ||
                    Physics.CheckBox(center, halfSize, rotation, ~0, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                CarriedCargo.DropAt(center);
                CarriedCargo = null;
                return true;
            }

            return false;
        }

        public bool TryDeliver(CargoItem primaryCargo)
        {
            if (!primaryCargo || primaryCargo.IsDestroyed || CarriedCargo != primaryCargo || HasPendingSlip)
                return false;

            primaryCargo.Deliver();
            CarriedCargo = null;
            Prompt = null;
            return true;
        }
    }
}
