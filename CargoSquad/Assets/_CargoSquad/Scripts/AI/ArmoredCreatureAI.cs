using CargoSquad.Combat;
using CargoSquad.Player;
using UnityEngine;
using UnityEngine.AI;

namespace CargoSquad.AI
{
    public enum ArmoredState { Idle, Walk, Charge, HeavySwipe, GroundSmash, Hurt, Stunned, Death }

    [DisallowMultipleComponent, RequireComponent(typeof(Health), typeof(NavMeshAgent))]
    public sealed class ArmoredCreatureAI : MonoBehaviour, IStunnable
    {
        [SerializeField] private Animator animator;
        [Header("Guard and pursuit")]
        [SerializeField, Min(.1f)] private float detectionRange = 9f;
        [SerializeField, Min(.1f)] private float guardSpeed = 1.2f;
        [SerializeField, Min(.1f)] private float pursuitSpeed = 2.4f;
        [Header("Melee")]
        [SerializeField, Min(.25f)] private float reach = 2.1f;
        [SerializeField, Range(0, 90)] private float hitHalfAngle = 62f;
        [SerializeField, Min(0)] private float swipeDamage = 22f;
        [SerializeField, Min(0)] private float smashDamage = 32f;
        [SerializeField, Min(0)] private float attackRecovery = .65f;
        [Tooltip("Normalized contact frames in the two authored attacks.")]
        [SerializeField, Range(0, 1)] private float swipeContact = 23f / 42f;
        [SerializeField, Range(0, 1)] private float smashContact = 32f / 54f;
        [Header("Stun")]
        [SerializeField, Min(0)] private float stunRecoveryImmunity = 1.2f;

        private NavMeshAgent agent;
        private Health health;
        private PlayerController player;
        private Health survivor;
        private Vector3 guardPoint;
        private bool nextIsSmash, contactSpent, started;
        private float nextAttack, recoverAt, immuneUntil, nextPath, previousHealth;
        public ArmoredState State { get; private set; }
        public float AttackReach => reach;
        public float AttackHalfAngle => hitHalfAngle;
        public event System.Action GroundSmashContact;

        public bool TryGetSmashWindup(out float progress)
        {
            progress = 0f;
            if (!isActiveAndEnabled || State != ArmoredState.GroundSmash || contactSpent || Time.timeScale <= 0f)
                return false;
            var pose = animator.GetCurrentAnimatorStateInfo(0);
            if (pose.fullPathHash == Clips[(int)ArmoredState.GroundSmash])
                progress = Mathf.Clamp01(pose.normalizedTime / Mathf.Max(.001f, smashContact));
            return true;
        }
        private static readonly int[] Clips = System.Array.ConvertAll(
            System.Enum.GetNames(typeof(ArmoredState)), name => Animator.StringToHash("Base Layer." + name));

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            guardPoint = transform.position;
        }

        private void OnEnable()
        {
            previousHealth = health.Current;
            health.Changed += OnHealth;
        }

        private void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player) survivor = player.GetComponent<Health>();
            started = true;
            if (health.IsDead) { Die(); return; }
            if (!agent.isOnNavMesh)
            {
                Debug.LogError("Place the armored creature on a baked navigation surface.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            health.Changed -= OnHealth;
            StopMoving();
            contactSpent = true;
        }

        private void StopMoving()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        private void Play(ArmoredState state)
        {
            if (State == state) return;
            State = state;
            // The sampled attack playhead is the single clock for wind-up, contact and recovery.
            animator.CrossFadeInFixedTime(Clips[(int)state], .065f, 0, 0f);
        }

        private void OnHealth(float current, float maximum)
        {
            bool damaged = current < previousHealth;
            previousHealth = current;
            if (current <= 0) { Die(); return; }
            if (!started || !damaged || State == ArmoredState.Stunned || Attacking) return;
            StopMoving();
            Play(ArmoredState.Hurt);
        }

        private void Die()
        {
            contactSpent = true;
            StopMoving();
            GetComponent<CapsuleCollider>().enabled = false;
            Play(ArmoredState.Death);
        }

        public bool TryStun(float duration)
        {
            if (!isActiveAndEnabled || health.IsDead || !float.IsFinite(duration) ||
                duration <= 0 || Time.time < immuneUntil) return false;
            StopMoving();
            contactSpent = true;
            recoverAt = Time.time + duration;
            immuneUntil = recoverAt + stunRecoveryImmunity;
            nextAttack = Mathf.Max(nextAttack, recoverAt + attackRecovery);
            Play(ArmoredState.Stunned);
            return true;
        }

        private bool Attacking => State == ArmoredState.HeavySwipe || State == ArmoredState.GroundSmash;

        private bool SeesSurvivor()
        {
            if (!player || !player.isActiveAndEnabled || !survivor || survivor.IsDead) return false;
            Vector3 eye = transform.position + Vector3.up * 1.35f;
            return !Physics.Linecast(eye, player.transform.position, out var hit, ~0, QueryTriggerInteraction.Ignore)
                || hit.transform.IsChildOf(player.transform);
        }

        private void Update()
        {
            if (Time.timeScale <= 0 || health.IsDead || !agent.isOnNavMesh) return;
            if (State == ArmoredState.Stunned && Time.time < recoverAt) return;
            if (Attacking || State == ArmoredState.Hurt)
            {
                var pose = animator.GetCurrentAnimatorStateInfo(0);
                if (pose.fullPathHash != Clips[(int)State]) return;
                if (Attacking && !contactSpent && pose.normalizedTime >=
                    (State == ArmoredState.HeavySwipe ? swipeContact : smashContact))
                {
                    contactSpent = true;
                    if (State == ArmoredState.GroundSmash) GroundSmashContact?.Invoke();
                    if (SeesSurvivor())
                    {
                        Vector3 offset = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
                        if (offset.magnitude <= reach && Vector3.Angle(transform.forward, offset) <= hitHalfAngle)
                            survivor.ApplyDamageFrom(State == ArmoredState.HeavySwipe ? swipeDamage : smashDamage,
                                -offset, powerful: true);
                    }
                }
                if (pose.normalizedTime < 1f) return;
                if (Attacking) nextAttack = Time.time + attackRecovery;
            }
            Vector3 target = player ? player.transform.position : guardPoint;
            Vector3 toTarget = Vector3.ProjectOnPlane(target - transform.position, Vector3.up);
            bool pursuing = toTarget.sqrMagnitude <= detectionRange * detectionRange && SeesSurvivor();
            if (pursuing && toTarget.magnitude <= reach * .91f)
            {
                StopMoving();
                if (toTarget.sqrMagnitude > .001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(toTarget), agent.angularSpeed * Time.deltaTime);
                if (Time.time >= nextAttack && Vector3.Angle(transform.forward, toTarget) < 18f)
                {
                    contactSpent = false;
                    Play(nextIsSmash ? ArmoredState.GroundSmash : ArmoredState.HeavySwipe);
                    nextIsSmash = !nextIsSmash;
                }
                else Play(ArmoredState.Idle);
                return;
            }
            Vector3 destination = pursuing ? target : guardPoint;
            if (!pursuing && Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).magnitude < .35f)
            {
                if (!agent.isStopped) StopMoving();
                Play(ArmoredState.Idle);
                return;
            }
            agent.isStopped = false;
            agent.speed = pursuing ? pursuitSpeed : guardSpeed;
            if (!agent.hasPath || Time.time >= nextPath)
            {
                agent.SetDestination(destination);
                nextPath = Time.time + .18f;
            }
            Play(pursuing ? ArmoredState.Charge : ArmoredState.Walk);
        }
    }
}
