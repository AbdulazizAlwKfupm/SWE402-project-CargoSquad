using CargoSquad.Cargo;
using CargoSquad.Combat;
using UnityEngine;

namespace CargoSquad.Player
{
    /// <summary>Audible feedback only. Never emits gameplay noise or owns movement.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAudio : MonoBehaviour
    {
        private PlayerAudioSettings settings;
        private PlayerController movement;
        private CharacterAbility ability;
        private Health health;
        private CargoInteractor carrier;
        private Rigidbody body;
        private CapsuleCollider capsule;
        private AudioSource steps, cargo;
        private Vector3 previousPosition;
        private float distanceSinceStep;
        private int stepIndex;

        private void Awake()
        {
            settings = Resources.Load<PlayerAudioSettings>("CargoSquadPlayerAudio");
            movement = GetComponent<PlayerController>();
            ability = GetComponent<CharacterAbility>();
            health = GetComponent<Health>();
            carrier = GetComponent<CargoInteractor>();
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            steps = CreateSource();
            cargo = CreateSource();
            if (!GetComponent<CargoSquad.Missions.MissionSuccessAudio>())
                gameObject.AddComponent<CargoSquad.Missions.MissionSuccessAudio>();
        }

        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // Consistent local feedback under the top-down camera.
            source.volume = 1f;
            return source;
        }

        private void OnEnable()
        {
            previousPosition = body ? body.position : transform.position;
            distanceSinceStep = 0f;
            if (!carrier) return;
            carrier.CarriedCargoChanged += OnCarriedCargoChanged;
            carrier.CargoDropped += OnCargoDropped;
        }

        private void OnDisable()
        {
            if (carrier)
            {
                carrier.CarriedCargoChanged -= OnCarriedCargoChanged;
                carrier.CargoDropped -= OnCargoDropped;
            }
            if (steps) steps.Stop();
            if (cargo) cargo.Stop();
        }

        private void OnDestroy()
        {
            if (steps) Destroy(steps);
            if (cargo) Destroy(cargo);
        }

        private void OnCarriedCargoChanged(CargoItem item)
        {
            // Null also means delivery/destruction; neither is a pickup or drop sound.
            if (item && settings) Play(cargo, settings.pickup, settings.cargoVolume);
        }

        private void OnCargoDropped(CargoItem item)
        {
            if (!settings) return;
            // CargoDropped fires after placement for both paths. Grip retains the slip flag
            // until the next pickup, even though CarriedCargo has already been cleared.
            bool slipped = carrier.Grip && carrier.Grip.SlipPending;
            Play(cargo, slipped ? settings.slip : settings.drop, settings.cargoVolume);
        }

        private static void Play(AudioSource source, AudioClip clip, float volume)
        {
            if (!clip) return;
            source.clip = clip;
            source.volume = volume;
            source.Play(); // One voice per channel; rapid interactions cannot stack cargo impacts.
        }

        private void FixedUpdate()
        {
            Vector3 position = body.position;
            float travelled = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up).magnitude;
            previousPosition = position;
            bool active = settings && movement && movement.enabled && Time.timeScale > 0f &&
                (!health || !health.IsDead) && (!ability || !ability.IsCharging);
            // Measure resolved physics displacement, not requested velocity (e.g. walking into walls).
            // Ignore teleports and reset cadence on stopping, air time, menus, and death.
            if (!active || travelled < .001f || travelled > .5f || !IsGrounded())
            {
                distanceSinceStep = 0f;
                steps.Stop();
                return;
            }
            distanceSinceStep += travelled;
            if (distanceSinceStep < settings.metresPerStep || settings.footsteps.Length == 0) return;
            distanceSinceStep %= settings.metresPerStep;
            Play(steps, settings.footsteps[stepIndex++ % settings.footsteps.Length], settings.footstepVolume);
        }

        private bool IsGrounded()
        {
            Bounds bounds = capsule.bounds;
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + .16f, bounds.center.z);
            foreach (var hit in Physics.SphereCastAll(origin, .06f, Vector3.down, .22f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.attachedRigidbody == body) continue;
                if (hit.normal.y > .55f) return true;
            }
            return false;
        }
    }
}
