using CargoSquad.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace CargoSquad.AI
{
    /// <summary>
    /// Audible feedback for the three enemies. It only listens: footsteps follow the agent's real ground movement,
    /// attacks follow the AI's contact/launch events, hurt and death follow Health. It never changes AI, damage or
    /// movement. Each enemy AI adds it in Awake, so placed and spawned enemies are covered without scene edits.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class EnemyAudio : MonoBehaviour
    {
        [SerializeField] private EnemyAudioProfile profile;
        [SerializeField, Range(0f, 2f)] private float masterVolume = 1f;

        private Health health;
        private NavMeshAgent agent;
        private MutatedWolfAI wolf;
        private ArmoredCreatureAI armored;
        private AcidSpitterAI spitter;
        private AudioSource steps, attack, hurt;
        private Vector3 previousPosition;
        private float distanceSinceStep, nextHurt, stepStop, attackStop, hurtStop;
        private int stepIndex;
        private bool deathPlayed;

        public EnemyAudioProfile Profile => profile;
        public bool DeathPlayed => deathPlayed;
        public int StepsPlayed { get; private set; }
        public int AttacksPlayed { get; private set; }
        public int HurtsPlayed { get; private set; }

        public static EnemyAudio Ensure(MonoBehaviour ai)
        {
            var audio = ai.GetComponent<EnemyAudio>();
            return audio ? audio : ai.gameObject.AddComponent<EnemyAudio>();
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            agent = GetComponent<NavMeshAgent>();
            wolf = GetComponent<MutatedWolfAI>();
            armored = GetComponent<ArmoredCreatureAI>();
            spitter = GetComponent<AcidSpitterAI>();
            if (!profile)
                profile = Resources.Load<EnemyAudioProfile>("EnemyAudio/" +
                    (armored ? "ArmoredCreature" : spitter ? "AcidSpitter" : "MutatedWolf"));
            steps = CreateSource();
            attack = CreateSource();
            hurt = CreateSource();
            // Health may not have initialised yet (the AI adds this component from its own Awake), so the
            // "already dead" check waits for Start.
            stepStop = attackStop = hurtStop = float.PositiveInfinity;
        }

        private void Start()
        {
            if (health.IsDead) deathPlayed = true;
        }

        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            Configure(source);
            return source;
        }

        private void Configure(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            if (!profile) return;
            source.minDistance = profile.minDistance;
            source.maxDistance = Mathf.Max(profile.minDistance + 1f, profile.maxDistance);
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            distanceSinceStep = 0f;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            if (wolf) wolf.BiteContact += PlayPrimary;
            if (armored)
            {
                armored.SwipeContact += PlayPrimary;
                armored.GroundSmashContact += PlaySecondary;
            }
            if (spitter)
            {
                spitter.SpitLaunched += PlayPrimary;
                spitter.SwipeContact += PlaySecondary;
            }
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            if (wolf) wolf.BiteContact -= PlayPrimary;
            if (armored)
            {
                armored.SwipeContact -= PlayPrimary;
                armored.GroundSmashContact -= PlaySecondary;
            }
            if (spitter)
            {
                spitter.SpitLaunched -= PlayPrimary;
                spitter.SwipeContact -= PlaySecondary;
            }
            if (steps) steps.Stop();
        }

        private void PlayPrimary()
        {
            if (profile && Play(attack, profile.primaryAttack)) AttacksPlayed++;
        }

        private void PlaySecondary()
        {
            if (profile && Play(attack, profile.secondaryAttack)) AttacksPlayed++;
        }

        private void OnDamaged(Vector3? point, Vector3 normal)
        {
            // Death has its own sound; pellets from one blast land in the same frame and share one hurt sound.
            if (!profile || health.IsDead || Time.unscaledTime < nextHurt) return;
            nextHurt = Time.unscaledTime + profile.hurtInterval;
            if (Play(hurt, profile.hurt)) HurtsPlayed++;
        }

        private void OnDied()
        {
            if (deathPlayed || !profile || !profile.death.clip) return;
            deathPlayed = true;
            steps.Stop();
            attack.Stop();
            hurt.Stop();
            // A detached voice keeps playing even if the enemy's components are disabled or the object is removed.
            var voice = new GameObject(name + " Death Audio");
            voice.transform.position = transform.position;
            var source = voice.AddComponent<AudioSource>();
            Configure(source);
            Play(source, profile.death);
            float length = (profile.death.maxLength > 0f ? profile.death.maxLength
                : profile.death.clip.length - profile.death.startAt) / Mathf.Max(.1f, source.pitch);
            Destroy(voice, length + .2f);
        }

        private bool Play(AudioSource source, EnemyAudioProfile.Cue cue)
        {
            if (cue == null || !cue.clip || !source) return false;
            source.Stop();
            source.clip = cue.clip;
            source.volume = cue.volume * masterVolume;
            source.pitch = Random.Range(Mathf.Min(cue.pitch.x, cue.pitch.y), Mathf.Max(cue.pitch.x, cue.pitch.y));
            source.time = Mathf.Clamp(cue.startAt, 0f, cue.clip.length - .01f);
            source.Play();
            // A timed stop (not a scheduled end) works for every source and cuts single steps out of a recording.
            float stop = cue.maxLength > 0f ? Time.unscaledTime + cue.maxLength / source.pitch : float.PositiveInfinity;
            if (source == steps) stepStop = stop; else if (source == attack) attackStop = stop; else if (source == hurt) hurtStop = stop;
            return true;
        }

        private bool Moving()
        {
            if (health.IsDead || Time.timeScale <= 0f || !agent || !agent.enabled || !agent.isOnNavMesh || agent.isStopped)
                return false;
            if (wolf) return wolf.isActiveAndEnabled && wolf.State != WolfState.Stunned && wolf.State != WolfState.Death &&
                wolf.State != WolfState.Idle && wolf.State != WolfState.Attack;
            if (armored) return armored.isActiveAndEnabled &&
                (armored.State == ArmoredState.Walk || armored.State == ArmoredState.Charge);
            if (spitter) return spitter.isActiveAndEnabled &&
                (spitter.State == SpitterState.Walk || spitter.State == SpitterState.Run);
            return false;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now >= stepStop) { steps.Stop(); stepStop = float.PositiveInfinity; }
            if (now >= attackStop) { attack.Stop(); attackStop = float.PositiveInfinity; }
            if (now >= hurtStop) { hurt.Stop(); hurtStop = float.PositiveInfinity; }
        }

        private void FixedUpdate()
        {
            Vector3 position = transform.position;
            float travelled = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up).magnitude;
            previousPosition = position;
            if (!profile || !profile.footstepRecording || profile.stepStarts.Length == 0) return;
            // Real displacement, not requested speed: no steps while blocked, idle, stunned, attacking or dead.
            bool moving = Moving() && travelled > profile.minimumSpeed * Time.fixedDeltaTime && travelled < .5f;
            if (!moving)
            {
                distanceSinceStep = Mathf.Min(distanceSinceStep, profile.metresPerStep * .5f);
                return;
            }
            distanceSinceStep += travelled;
            if (distanceSinceStep < profile.metresPerStep) return;
            distanceSinceStep %= profile.metresPerStep;
            PlayStep();
        }

        private void PlayStep()
        {
            float[] starts = profile.stepStarts;
            int index = stepIndex++ % starts.Length;
            float start = starts[index];
            float next = index + 1 < starts.Length ? starts[index + 1] : profile.footstepRecording.length;
            var cue = new EnemyAudioProfile.Cue
            {
                clip = profile.footstepRecording, startAt = start, volume = profile.footstepVolume,
                maxLength = Mathf.Min(profile.maxStepLength, next - start), pitch = profile.footstepPitch
            };
            if (Play(steps, cue)) StepsPlayed++;
        }
    }
}
