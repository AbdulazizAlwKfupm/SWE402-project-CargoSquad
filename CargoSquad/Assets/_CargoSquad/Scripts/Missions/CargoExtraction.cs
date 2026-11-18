using System;
using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Player;
using UnityEngine;
using UnityEngine.Events;

namespace CargoSquad.Missions
{
    public enum MissionOutcome { None, CargoRecovered, EscapedCargoLost, SurvivorKilled }

    public readonly struct MissionResult
    {
        public MissionOutcome Outcome { get; }
        public string Survivor { get; }
        public string CargoOutcome { get; }
        public float CargoCondition { get; }
        public float CargoMaximum { get; }
        public float Health { get; }
        public float MaximumHealth { get; }
        public MissionResult(MissionOutcome outcome, string survivor, string cargoOutcome,
            float condition, float maximumCondition, float health, float maximumHealth)
        {
            Outcome = outcome; Survivor = survivor; CargoOutcome = cargoOutcome;
            CargoCondition = condition; CargoMaximum = maximumCondition;
            Health = health; MaximumHealth = maximumHealth;
        }
    }

    [DefaultExecutionOrder(10000)]
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
        public MissionResult Result { get; private set; }
        public event Action<MissionResult> ResultFinalized;
        public bool IsComplete => deathPending || Result.Outcome != MissionOutcome.None;
        public bool RecoveryFailed => deathPending || Result.Outcome == MissionOutcome.EscapedCargoLost || Result.Outcome == MissionOutcome.SurvivorKilled;
        public bool SurvivorLost => deathPending || Result.Outcome == MissionOutcome.SurvivorKilled;
        private bool deathPending;
        private Health survivorHealth;
        private CargoInteractor pendingCarrier;

        private void OnEnable()
        {
            var player = FindFirstObjectByType<PlayerController>();
            survivorHealth = player ? player.GetComponent<Health>() : null;
            if (survivorHealth) survivorHealth.Died += OnSurvivorDied;
        }

        private void OnDisable()
        {
            if (survivorHealth) survivorHealth.Died -= OnSurvivorDied;
            pendingCarrier = null;
        }

        private void OnSurvivorDied()
        {
            if (IsComplete) return;
            // Reserve failure immediately; snapshot in LateUpdate after this bite's cargo consequences.
            deathPending = true;
            pendingCarrier = null;
        }

        private void OnTriggerStay(Collider other)
        {
            if (IsComplete || !other.attachedRigidbody ||
                !other.attachedRigidbody.TryGetComponent(out CargoInteractor carrier))
            {
                return;
            }
            var health = carrier.GetComponent<Health>();
            if (health && health.IsDead)
            {
                OnSurvivorDied();
                return;
            }
            pendingCarrier = carrier;
        }

        private void OnTriggerExit(Collider other)
        {
            if (pendingCarrier && other.attachedRigidbody == pendingCarrier.GetComponent<Rigidbody>())
                pendingCarrier = null;
        }

        private void LateUpdate()
        {
            if (Result.Outcome != MissionOutcome.None) return;
            if (deathPending) { Complete(MissionOutcome.SurvivorKilled); return; }
            var carrier = pendingCarrier;
            pendingCarrier = null;
            if (IsComplete || !carrier) return;
            var health = carrier.GetComponent<Health>();
            if (health && health.IsDead)
            {
                OnSurvivorDied();
                return;
            }

            // Resolve after this frame's attack updates, so a fatal bite has priority over delivery.
            if (CargoDestroyed)
            {
                Complete(MissionOutcome.EscapedCargoLost);
            }
            else if (carrier.TryDeliver(primaryCargo))
            {
                Complete(MissionOutcome.CargoRecovered);
            }
        }
        private void Complete(MissionOutcome outcome)
        {
            if (Result.Outcome != MissionOutcome.None) return;
            var ability = survivorHealth ? survivorHealth.GetComponent<CharacterAbility>() : null;
            string cargoOutcome = outcome == MissionOutcome.CargoRecovered ? "RECOVERED" : CargoDestroyed ? "LOST / DESTROYED" : "NOT RECOVERED";
            Result = new MissionResult(outcome, ability ? ability.Kind.ToString() : "Survivor", cargoOutcome,
                PrimaryCondition ? PrimaryCondition.Current : 0, PrimaryCondition ? PrimaryCondition.Maximum : 0,
                survivorHealth ? survivorHealth.Current : 0, survivorHealth ? survivorHealth.Maximum : 0);
            deathPending = false; pendingCarrier = null;
            if (outcome == MissionOutcome.CargoRecovered) missionSucceeded.Invoke();
            else if (outcome == MissionOutcome.EscapedCargoLost) survivorEscaped.Invoke();
            ResultFinalized?.Invoke(Result);
        }
    }
}
