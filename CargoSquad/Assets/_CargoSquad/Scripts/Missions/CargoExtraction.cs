using CargoSquad.Cargo;
using UnityEngine;
using UnityEngine.Events;

namespace CargoSquad.Missions
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CargoExtraction : MonoBehaviour
    {
        [SerializeField] private CargoItem primaryCargo;
        [SerializeField] private UnityEvent missionSucceeded = new UnityEvent();
        [SerializeField] private UnityEvent survivorEscaped = new UnityEvent();

        public bool CargoDestroyed => primaryCargo && primaryCargo.IsDestroyed;
        public CargoCondition PrimaryCondition => primaryCargo ? primaryCargo.Condition : null;
        public bool IsPrimaryCargo(CargoItem cargo) => primaryCargo && cargo == primaryCargo;
        public bool IsComplete { get; private set; }
        public bool RecoveryFailed { get; private set; }

        private void OnTriggerStay(Collider other)
        {
            if (IsComplete || !other.attachedRigidbody ||
                !other.attachedRigidbody.TryGetComponent(out CargoInteractor carrier))
            {
                return;
            }

            if (CargoDestroyed)
            {
                RecoveryFailed = true;
                IsComplete = true;
                survivorEscaped.Invoke();
            }
            else if (carrier.TryDeliver(primaryCargo))
            {
                IsComplete = true;
                missionSucceeded.Invoke();
            }
        }
    }
}
