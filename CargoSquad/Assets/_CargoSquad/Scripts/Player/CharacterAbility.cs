using System.Collections.Generic;
using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Visuals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CargoSquad.Player
{
    public enum SurvivorKind { Heavy, Medic }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(PlayerStamina), typeof(Health))]
    public sealed class CharacterAbility : MonoBehaviour
    {
        [Header("Heavy / shoulder bash")]
        [SerializeField, Min(0.1f)] private float chargeSpeed = 11f;
        [SerializeField, Min(0.1f)] private float chargeDistance = 3f;
        [SerializeField, Min(0f)] private float chargeDamage = 35f;
        [SerializeField, Min(0f)] private float chargeCooldown = 4f;
        [Header("Medic / second wind")]
        [SerializeField, Min(0f)] private float medicCooldown = 12f;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private PlayerStamina stamina;
        private CargoInteractor carrier;
        private Health health;
        private SurvivorVisual visual;
        private float readyAt;
        private float remaining;
        private Vector3 direction;
        private bool selected;
        private bool waitForRelease;
        private int blockedThroughFrame;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        public SurvivorKind Kind { get; private set; }
        public bool IsCharging => remaining > 0f;
        public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);
        public float ChargeDistance => chargeDistance;
        public float ChargeSpeed => chargeSpeed;
        public float ChargeDamage => chargeDamage;

        private void Awake()
        {
            body = GetComponent<Rigidbody>(); capsule = GetComponent<CapsuleCollider>();
            stamina = GetComponent<PlayerStamina>(); carrier = GetComponent<CargoInteractor>();
            health = GetComponent<Health>();
        }

        public void Configure(SurvivorKind kind, SurvivorVisual activeVisual)
        {
            CancelCharge(); Kind = kind; visual = activeVisual;
            selected = true; readyAt = 0f;
            // Guard both event orderings: Start on pointer-up and a press still held when UI disappears.
            blockedThroughFrame = Time.frameCount + 1; waitForRelease = true;
        }

        private void Update()
        {
            if (waitForRelease && Time.frameCount > blockedThroughFrame &&
                (Mouse.current == null || !Mouse.current.leftButton.isPressed)) waitForRelease = false;
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame) TryActivate();
            if (health.IsDead || (carrier && carrier.CarriedCargo)) CancelCharge();
        }

        public bool PointerOverUI()
        {
            if (Mouse.current == null) return false;
            Vector2 position = Mouse.current.position.ReadValue();
            var legacyHud = FindFirstObjectByType<CargoSquad.UI.CargoMissionOverlay>();
            if (legacyHud && legacyHud.ContainsScreenPoint(position)) return true;
            if (!EventSystem.current) return false;
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            uiHits.Clear(); EventSystem.current.RaycastAll(pointer, uiHits);
            return uiHits.Count > 0;
        }

        public bool TryActivate()
        {
            if (!enabled || !selected || !visual || !visual.gameObject.activeInHierarchy ||
                Time.timeScale <= 0f || health.IsDead || (carrier && carrier.CarriedCargo) ||
                waitForRelease || Time.frameCount <= blockedThroughFrame || IsCharging ||
                CooldownRemaining > 0f || PointerOverUI()) return false;
            if (Kind == SurvivorKind.Medic)
            {
                stamina.RestoreTo(100f);
                readyAt = Time.time + medicCooldown;
            }
            else
            {
                direction = Vector3.ProjectOnPlane(visual.transform.forward, Vector3.up).normalized;
                remaining = chargeDistance;
                readyAt = Time.time + chargeCooldown;
                if (visual.Animator)
                {
                    visual.Animator.SetFloat("BashSpeed", 0.65f / (chargeDistance / chargeSpeed + 0.08f));
                    visual.Animator.SetTrigger("Bash");
                }
            }
            return true;
        }

        // Called only by PlayerController's existing FixedUpdate: one owner of gameplay velocity.
        public bool TryGetChargeVelocity(float seconds, out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!IsCharging) return false;
            if (health.IsDead || (carrier && carrier.CarriedCargo)) { CancelCharge(); return true; }
            float step = Mathf.Min(chargeSpeed * seconds, remaining);
            const float skin = 0.025f;
            Vector3 scale = transform.lossyScale;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float half = Mathf.Max(radius, capsule.height * Mathf.Abs(scale.y) * 0.5f);
            Vector3 center = transform.TransformPoint(capsule.center);
            Vector3 upper = center + Vector3.up * (half - radius);
            Vector3 lower = center - Vector3.up * (half - radius);
            var hits = Physics.CapsuleCastAll(lower, upper, Mathf.Max(0.01f, radius - skin), direction,
                step + skin, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit? nearest = null;
            foreach (var hit in hits)
            {
                if (hit.collider.attachedRigidbody == body || hit.collider.transform.IsChildOf(transform) ||
                    Vector3.Dot(hit.normal, Vector3.up) > 0.65f) continue;
                if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
            }
            if (nearest.HasValue)
            {
                step = Mathf.Clamp(nearest.Value.distance - skin, 0f, step);
                nearest.Value.collider.GetComponentInParent<IDamageable>()?.ApplyDamage(chargeDamage);
                remaining = 0f;
            }
            else remaining = Mathf.Max(0f, remaining - step);
            velocity = direction * (step / Mathf.Max(seconds, 0.0001f));
            return true;
        }

        public void CancelCharge() => remaining = 0f;
        private void OnDisable() => CancelCharge();
    }
}
