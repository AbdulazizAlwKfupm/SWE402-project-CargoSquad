using System;
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
        private PlayerStamina stamina;
        private CargoSquad.Missions.CargoExtraction mission;
        private SurvivorVisual visual;
        private AimingConeIndicator cone;
        private WeaponFeedback feedback;
        private AudioSource audioSource;
        private AudioSource shellReloadSource;
        private bool finishingShellReload;
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
        public Transform ActiveWeaponVisual => visual ? visual.Weapon : null;
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
        public event Action<Transform> ShotgunFired;
        public event Action<Vector3, Transform> StunShotResolved;
        // Audible reload cues only; never forwarded to AI GameplayNoise.
        public event Action<AudioClip> ReloadCuePlayed;

        private void Awake()
        {
            health = GetComponent<Health>(); carrier = GetComponent<CargoInteractor>();
            ability = GetComponent<CharacterAbility>(); movement = GetComponent<PlayerController>(); stamina = GetComponent<PlayerStamina>();
            mission = FindFirstObjectByType<CargoSquad.Missions.CargoExtraction>();
            if (!aimCamera) aimCamera = Camera.main;
            random = new System.Random(spreadSeed);
            if (conePrefab) { cone = Instantiate(conePrefab); cone.Hide(); }
            var obj = new GameObject("Weapon feedback"); obj.transform.SetParent(transform, false);
            feedback = obj.AddComponent<WeaponFeedback>(); feedback.Initialize(feedbackMaterial);
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            shellReloadSource = gameObject.AddComponent<AudioSource>();
            shellReloadSource.playOnAwake = false; shellReloadSource.spatialBlend = 0;
        }
        private void OnEnable() { if (health) health.Died += CancelReload; }
        public void Equip(SurvivorKind kind, SurvivorVisual activeVisual)
        {
            CancelReload();
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
                CancelAim(true); CancelReload(); return;
            }
            bool overUI = ability.PointerOverUI();
            bool validAim = mouse != null && AimAtScreen(mouse.position.ReadValue());
            if (Reloading && Definition.type == WeaponType.Shotgun && Magazine > 0 && !overUI && validAim &&
                !waitForRelease && mouse.leftButton.wasPressedThisFrame) CancelReload();
            if (Reloading && Time.time >= reloadEnds) AdvanceReload();
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) TryReload();
            if (overUI || !validAim || Reloading)
            {
                bool shellReload = Reloading && Definition.type == WeaponType.Shotgun;
                CancelAim(!shellReload || overUI || !validAim); return;
            }
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
            if (!focused) { CancelAim(true); if (Definition && Definition.type == WeaponType.Shotgun) CancelReload(); }
        }
        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            CancelAim(true); if (Definition && Definition.type == WeaponType.Shotgun) CancelReload();
        }
        public bool TryReload()
        {
            if (!CanUse || Reloading || Magazine >= Definition.magazineCapacity || Reserve <= 0) return false;
            CancelAim(true);
            Reloading = true; finishingShellReload = false;
            if (Definition.type == WeaponType.Shotgun)
                reloadEnds = Time.time + ShellInterval;
            else
            {
                reloadEnds = Time.time + Definition.reloadDuration;
                Play(Definition.reloadClip);
                if (Definition.reloadClip) ReloadCuePlayed?.Invoke(Definition.reloadClip);
            }
            Changed?.Invoke(); return true;
        }
        private float ShellInterval => Mathf.Max(.05f, Definition.shellInsertInterval,
            Definition.shellInsertClip ? Definition.shellInsertClip.length : 0f);

        private void AdvanceReload()
        {
            if (Definition.type != WeaponType.Shotgun)
            {
                int amount = Mathf.Min(Definition.magazineCapacity - Magazine, Reserve);
                Magazine += amount; Reserve -= amount; Reloading = false; Changed?.Invoke();
                return;
            }
            if (finishingShellReload)
            {
                Reloading = false; finishingShellReload = false; Changed?.Invoke();
                return;
            }
            if (Magazine >= Definition.magazineCapacity || Reserve <= 0)
            {
                // Waited for the final insertion cue to finish before playing the rack once.
                finishingShellReload = true;
                reloadEnds = Time.time + (Definition.reloadFinishClip ? Definition.reloadFinishClip.length : .05f);
                PlayShellReload(Definition.reloadFinishClip);
                return;
            }
            // One committed shell and one insertion cue, in the same frame. Never catch up
            // multiple shells in a stalled frame, which would stack sounds and grant extra ammo.
            Magazine++; Reserve--;
            reloadEnds = Time.time + ShellInterval;
            PlayShellReload(Definition.shellInsertClip);
            Changed?.Invoke();
        }

        private void PlayShellReload(AudioClip clip)
        {
            if (!clip) return;
            shellReloadSource.clip = clip; shellReloadSource.volume = Definition.volume;
            shellReloadSource.Play();
            ReloadCuePlayed?.Invoke(clip);
        }

        public void CancelReload()
        {
            bool wasReloading = Reloading;
            Reloading = false; finishingShellReload = false;
            if (shellReloadSource) shellReloadSource.Stop();
            if (wasReloading) Changed?.Invoke();
        }
        public bool TryFire()
        {
            if (!CanUse || waitForRelease || ability.PointerOverUI() || Time.time < nextShot) return false;
            if (Reloading)
            {
                if (Definition.type != WeaponType.Shotgun || Magazine <= 0) return false;
                CancelReload();
            }
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
            var stunVisual = Definition.type == WeaponType.Stun ? StunShotResolved : null;
            if (Definition.type == WeaponType.Shotgun && ShotgunFired != null)
                ShotgunFired.Invoke(visual.Weapon);
            else if (stunVisual == null)
                feedback.Spark(origin + AimDirection * .5f, color, .14f);
            for (int i = 0; i < Definition.pelletCount; i++)
            {
                float yaw = WeaponDefinition.SampleYaw(i, Definition.pelletCount, EffectiveConeAngle,
                    Definition.spreadPattern, (float)random.NextDouble());
                Vector3 direction = Quaternion.Euler(0, yaw, 0) * AimDirection;
                Vector3 end = origin + direction * Definition.range;
                Transform stunnedTarget = null;
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
                    float strength = stamina && stamina.Definition ? stamina.Definition.DamageStrength : 1f;
                    float damage = Definition.damage * strength * Mathf.Lerp(1, Definition.endRangeDamageMultiplier, hit.distance / Definition.range);
                    var healthTarget = target as Health;
                    bool hasDamageFeedback = healthTarget && healthTarget.HasDamageFeedback;
                    if (healthTarget) healthTarget.ApplyDamage(damage, hit.point, hit.normal);
                    else target?.ApplyDamage(damage);
                    if (Definition.stunDuration > 0)
                        hit.collider.GetComponentInParent<IStunnable>()?.TryStun(Definition.stunDuration);
                    if (stunVisual == null && !hasDamageFeedback)
                        feedback.Spark(end, color, target != null ? .18f : .08f);
                    else if (stunVisual != null && target != null)
                        stunnedTarget = hit.collider.transform;
                }
                if (stunVisual != null) stunVisual.Invoke(end, stunnedTarget);
                else if (Definition.type == WeaponType.Stun) feedback.Electric(origin + direction * .4f, end, color);
                else feedback.Segment(origin + direction * .4f, end, color);
            }
            return true;
        }
        private void Play(AudioClip clip) { if (clip) audioSource.PlayOneShot(clip, Definition.volume); }
        private void OnDisable()
        {
            if (health) health.Died -= CancelReload;
            CancelAim(true); CancelReload(); if (feedback) feedback.Clear();
        }
        private void OnDestroy() { if (cone) Destroy(cone.gameObject); }
    }
}
