using System;
using CargoSquad.AI;
using CargoSquad.Cargo;
using CargoSquad.Player;
using CargoSquad.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CargoSquad.Combat
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health), typeof(CargoInteractor))]
    public sealed class PlayerWeapon : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition heavyWeapon, medicWeapon;
        [SerializeField] private AimingConeIndicator conePrefab;
        [SerializeField] private Material feedbackMaterial;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private float groundHeight;
        [SerializeField] private float groundOffset = .035f;
        [SerializeField] private int spreadSeed = 402;
        private Health health;
        private CargoInteractor carrier;
        private CharacterAbility ability;
        private PlayerController movement;
        private CargoSquad.Missions.CargoExtraction mission;
        private SurvivorVisual visual;
        private AimingConeIndicator cone;
        private WeaponFeedback feedback;
        private AudioSource audioSource;
        private System.Random random;
        private float nextShot, reloadEnds, facingUntil, noticeUntil;
        private string notice;
        private bool waitForRelease;
        private bool aimArmed;
        private bool hasFocus = true;
        private int equipFrame;
        public WeaponDefinition Definition { get; private set; }
        public int Magazine { get; private set; }
        public int Reserve { get; private set; }
        public bool Reloading { get; private set; }
        public Vector3 AimDirection { get; private set; } = Vector3.forward;
        public bool IsAiming { get; private set; }
        public bool ControlsFacing => CanUse && (IsAiming || Time.time < facingUntil);
        public bool ConeVisible => cone && cone.Visible;
        public bool CanUse => isActiveAndEnabled && Definition && visual && visual.gameObject.activeInHierarchy &&
            movement && movement.enabled && hasFocus && !health.IsDead && Time.timeScale > 0 && !ability.IsCharging &&
            (!mission || !mission.IsComplete) &&
            (!carrier.CarriedCargo || Definition.supportsCarrying);
        public float EffectiveConeAngle => Definition ? Mathf.Min(160, Definition.coneAngle *
            (carrier.CarriedCargo ? Definition.carryingSpreadMultiplier : 1) *
            (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed &&
             GetComponent<Rigidbody>().linearVelocity.sqrMagnitude > .1f ? Definition.sprintSpreadMultiplier : 1)) : 0;
        public string HudText => !Definition ? "" : $"{Definition.displayName}  {Magazine} / {Reserve}" +
            (Reloading ? "   RELOADING" : Time.time < noticeUntil ? "   " + notice : "");
        public event Action Changed;

        private void Awake()
        {
            health = GetComponent<Health>(); carrier = GetComponent<CargoInteractor>();
            ability = GetComponent<CharacterAbility>(); movement = GetComponent<PlayerController>();
            mission = FindFirstObjectByType<CargoSquad.Missions.CargoExtraction>();
            if (!aimCamera) aimCamera = Camera.main;
            random = new System.Random(spreadSeed);
            if (conePrefab) { cone = Instantiate(conePrefab); cone.Hide(); }
            var obj = new GameObject("Weapon feedback"); obj.transform.SetParent(transform, false);
            feedback = obj.AddComponent<WeaponFeedback>(); feedback.Initialize(feedbackMaterial);
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
        }
        public void Equip(SurvivorKind kind, SurvivorVisual activeVisual)
        {
            Definition = kind == SurvivorKind.Heavy ? heavyWeapon : medicWeapon; visual = activeVisual;
            Magazine = Definition ? Definition.magazineCapacity : 0; Reserve = Definition ? Definition.reserveAmmunition : 0;
            Reloading = false; nextShot = 0; facingUntil = 0; noticeUntil = 0;
            random = new System.Random(spreadSeed); waitForRelease = true; equipFrame = Time.frameCount;
            CancelAim(false);
            if (cone) cone.Hide(); feedback.Clear(); Changed?.Invoke();
        }
        public bool AimAtScreen(Vector2 cursor)
        {
            if (!aimCamera) return false;
            var plane = new Plane(Vector3.up, new Vector3(0, groundHeight, 0));
            var ray = aimCamera.ScreenPointToRay(cursor);
            if (!plane.Raycast(ray, out float distance)) return false;
            Vector3 direction = ray.GetPoint(distance) - transform.position; direction.y = 0;
            if (direction.sqrMagnitude < .001f) return false;
            AimDirection = direction.normalized; return true;
        }
        private void Update()
        {
            var mouse = Mouse.current;
            if (waitForRelease && Time.frameCount > equipFrame + 1 && (mouse == null || !mouse.leftButton.isPressed)) waitForRelease = false;
            if (!CanUse)
            {
                CancelAim(true); Reloading = false; return;
            }
            if (Reloading && Time.time >= reloadEnds)
            {
                int amount = Mathf.Min(Definition.magazineCapacity - Magazine, Reserve);
                Magazine += amount; Reserve -= amount; Reloading = false; Changed?.Invoke();
            }
            bool overUI = ability.PointerOverUI();
            bool validAim = mouse != null && AimAtScreen(mouse.position.ReadValue());
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) TryReload();
            if (overUI || !validAim || Reloading) { CancelAim(true); return; }
            if (mouse.leftButton.wasPressedThisFrame && !waitForRelease) aimArmed = true;
            // Only a gesture that began in the world may fire. Cancelled/UI-held gestures stay cancelled.
            if (aimArmed && mouse.leftButton.wasReleasedThisFrame)
            {
                CancelAim(false);
                TryFire();
            }
            IsAiming = aimArmed && mouse.leftButton.isPressed;
            if (cone)
            {
                // The sector and rays share the body-centered origin and direction. The mesh has no collider.
                if (IsAiming) cone.Show(new Vector3(transform.position.x, groundHeight + groundOffset, transform.position.z),
                    AimDirection, Definition.range, EffectiveConeAngle);
                else cone.Hide();
            }
        }
        private void CancelAim(bool requireRelease)
        {
            aimArmed = false; IsAiming = false;
            if (requireRelease) waitForRelease = true;
            if (cone) cone.Hide();
        }
        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused) CancelAim(true);
        }
        private void OnApplicationPause(bool paused) { if (paused) CancelAim(true); }
        public bool TryReload()
        {
            if (!CanUse || Reloading || Magazine >= Definition.magazineCapacity || Reserve <= 0) return false;
            CancelAim(true);
            Reloading = true; reloadEnds = Time.time + Definition.reloadDuration;
            Play(Definition.reloadClip); Changed?.Invoke(); return true;
        }
        public bool TryFire()
        {
            if (!CanUse || Reloading || waitForRelease || ability.PointerOverUI() || Time.time < nextShot) return false;
            if (Magazine <= 0)
            {
                nextShot = Time.time + .2f; notice = "EMPTY / R TO RELOAD"; noticeUntil = Time.time + 1;
                Play(Definition.emptyClip); Changed?.Invoke(); return false;
            }
            Magazine--; nextShot = Time.time + 1 / Mathf.Max(.1f, Definition.shotsPerSecond);
            facingUntil = Time.time + .18f; Changed?.Invoke(); Play(Definition.fireClip);
            Vector3 origin = transform.position;
            carrier.EmitNoise(new GameplayNoise(origin, Definition.noiseRadius));
            Color color = Definition.effectColor;
            feedback.Spark(origin + AimDirection * .5f, color, .14f);
            for (int i = 0; i < Definition.pelletCount; i++)
            {
                float yaw = WeaponDefinition.SampleYaw(i, Definition.pelletCount, EffectiveConeAngle,
                    Definition.spreadPattern, (float)random.NextDouble());
                Vector3 direction = Quaternion.Euler(0, yaw, 0) * AimDirection;
                Vector3 end = origin + direction * Definition.range;
                // All hits are sorted by distance through nearest selection; self/carry sockets are ignored.
                // RaycastAll avoids silently missing a wall when a fixed non-alloc buffer overflows.
                var hits = Physics.RaycastAll(origin, direction, Definition.range, hitMask, QueryTriggerInteraction.Ignore);
                RaycastHit? nearest = null;
                foreach (var hit in hits)
                {
                    if (hit.collider.transform.IsChildOf(transform)) continue;
                    if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
                }
                if (nearest.HasValue)
                {
                    var hit = nearest.Value; end = hit.point;
                    var target = hit.collider.GetComponentInParent<IDamageable>();
                    target?.ApplyDamage(Definition.damage * Mathf.Lerp(1, Definition.endRangeDamageMultiplier, hit.distance / Definition.range));
                    var wolf = hit.collider.GetComponentInParent<MutatedWolfAI>();
                    if (wolf && Definition.stunDuration > 0) wolf.TryStun(Definition.stunDuration);
                    feedback.Spark(end, color, target != null ? .18f : .08f);
                }
                if (Definition.type == WeaponType.Stun) feedback.Electric(origin + direction * .4f, end, color);
                else feedback.Segment(origin + direction * .4f, end, color);
            }
            return true;
        }
        private void Play(AudioClip clip) { if (clip) audioSource.PlayOneShot(clip, Definition.volume); }
        private void OnDisable() { CancelAim(true); Reloading = false; if (feedback) feedback.Clear(); }
        private void OnDestroy() { if (cone) Destroy(cone.gameObject); }
    }
}
