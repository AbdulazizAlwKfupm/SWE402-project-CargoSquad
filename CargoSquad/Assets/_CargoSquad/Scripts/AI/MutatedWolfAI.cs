using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Player;
using UnityEngine;
using UnityEngine.AI;

namespace CargoSquad.AI
{
    public enum WolfState { Idle, Patrol, Investigate, Chase, Attack, Search, Return, Death, Stunned }

    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public sealed class MutatedWolfAI : MonoBehaviour, IStunnable
    {
        [SerializeField] private Transform[] patrolPoints;
        [SerializeField] private Animator animator;
        [SerializeField] private float patrolSpeed = 1.5f;
        [SerializeField] private float chaseSpeed = 4.5f;
        [SerializeField] private float detectionRange = 8f;
        [SerializeField] private float hearingMultiplier = 1f;
        [SerializeField] private float loseTargetAfter = 2.5f;
        [SerializeField] private float maximumChaseRange = 15f;
        [SerializeField] private float searchDuration = 3f;
        [SerializeField] private float patrolPause = 0.7f;
        [SerializeField] private float attackRange = 1.65f;
        [Header("Bite Damage")]
        [SerializeField, Min(0f), InspectorName("Player Bite Damage")] private float biteDamage = 15f;
        [SerializeField, Min(0f), InspectorName("Cargo Condition Damage")] private float cargoBiteDamage = 10f;
        [SerializeField, Min(0f), Tooltip("Immediate grip percentage points lost per bite. Continuous cargo drain is unchanged.")]
        private float gripLoss = 25f;
        [SerializeField, Min(0.01f), InspectorName("Bite Cooldown")] private float attackCooldown = 1.4f;
        [SerializeField, Range(0f, 1f)] private float biteImpactFraction = 0.45f;
        [SerializeField] private float biteDuration = 0.8f;
        [Header("Stun Recovery")]
        [SerializeField, Min(.1f)] private float maximumStunDuration = 3f;
        [SerializeField, Min(.1f)] private float stunRecoveryImmunity = 2f;
        private float stunUntil, nextStunAllowed;

        public bool TryStun(float duration)
        {
            if (!isActiveAndEnabled || health.IsDead || duration <= 0 || !float.IsFinite(duration) ||
                State == WolfState.Stunned || Time.time < nextStunAllowed || !agent.isOnNavMesh) return false;
            biteApplied = true; // Cancel this attack's pending hit, keeping its existing cooldown.
            State = WolfState.Stunned;
            stunUntil = Time.time + Mathf.Min(duration, maximumStunDuration);
            nextStunAllowed = stunUntil + stunRecoveryImmunity;
            agent.isStopped = true; agent.ResetPath();
            Animate("Idle");
            return true;
        }

        public WolfState State { get; private set; }
        public Vector3 LastKnownPosition => lastKnown;
        public float AttackCooldown => attackCooldown;
        public float BiteDamage => biteDamage;
        /// <summary>Raised at the bite's impact frame (not for bites cancelled by stun or death).</summary>
        public event System.Action BiteContact;
        private NavMeshAgent agent;
        private Health health;
        private PlayerController player;
        private Health targetHealth;
        private CargoInteractor noiseSource;
        private Vector3 home;
        private Vector3 lastKnown;
        private int patrolIndex;
        private float stateUntil;
        private float lastSeen;
        private float nextAttack;
        private float biteStarted;
        private float nextRepath;
        private bool biteApplied;
        private string animationName;
        private readonly RaycastHit[] sightHits = new RaycastHit[32];

        public void ConfigurePatrol(Transform[] points) => patrolPoints = points ?? new Transform[0];

        private void Awake()
        {
            if (!GetComponent<CargoSquad.UI.WolfHealthIndicator>()) gameObject.AddComponent<CargoSquad.UI.WolfHealthIndicator>();
            EnemyAudio.Ensure(this);
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            home = transform.position;
            player = FindFirstObjectByType<PlayerController>();
            if (player)
            {
                targetHealth = player.GetComponent<Health>();
                noiseSource = player.GetComponent<CargoInteractor>();
            }
        }

        private void OnEnable()
        {
            if (health) health.Died += Die;
            if (noiseSource) noiseSource.NoiseEmitted += HearNoise;
        }

        private void Start()
        {
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            if (!agent.isOnNavMesh)
            {
                Debug.LogError("Mutated Wolf needs an EnemyNavigation surface under its spawn.", this);
                enabled = false;
                return;
            }
            if (health.IsDead) Die();
            else Enter(WolfState.Patrol);
        }

        private void OnDisable()
        {
            if (health) health.Died -= Die;
            if (noiseSource) noiseSource.NoiseEmitted -= HearNoise;
            if (agent && agent.isOnNavMesh) agent.isStopped = true;
        }

        public void HearNoise(GameplayNoise noise)
        {
            if (!isActiveAndEnabled || health.IsDead || Time.timeScale <= 0f ||
                State == WolfState.Chase || State == WolfState.Attack ||
                Vector3.Distance(transform.position, noise.Position) > noise.Radius * hearingMultiplier) return;
            lastKnown = noise.Position;
            if (State == WolfState.Stunned) return;
            Enter(WolfState.Investigate);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || State == WolfState.Death || !agent.isOnNavMesh) return;
            if (health.IsDead) { Die(); return; }
            if (State == WolfState.Stunned)
            {
                if (Time.time >= stunUntil) Enter(WolfState.Search);
                return;
            }

            bool visible = CanSeeTarget();
            if (visible)
            {
                lastSeen = Time.time;
                lastKnown = player.transform.position;
                if (State != WolfState.Attack && State != WolfState.Chase) Enter(WolfState.Chase);
            }
            switch (State)
            {
                case WolfState.Idle:
                    if (Time.time >= stateUntil) Enter(WolfState.Patrol);
                    break;
                case WolfState.Patrol:
                case WolfState.Return:
                    if (Arrived())
                    {
                        patrolIndex = patrolPoints.Length == 0 ? 0 : (patrolIndex + 1) % patrolPoints.Length;
                        Enter(WolfState.Idle);
                    }
                    break;
                case WolfState.Investigate:
                    if (Arrived() || Time.time >= stateUntil) Enter(WolfState.Search);
                    break;
                case WolfState.Chase:
                    if (!targetHealth || targetHealth.IsDead ||
                        (!visible && Time.time - lastSeen >= loseTargetAfter) ||
                        Vector3.Distance(transform.position, player.transform.position) > maximumChaseRange)
                    {
                        Enter(WolfState.Search);
                        break;
                    }
                    if (visible && DistanceToTarget() <= attackRange && Time.time >= nextAttack)
                        Enter(WolfState.Attack);
                    else if (Time.time >= nextRepath)
                    {
                        Move(lastKnown, chaseSpeed);
                        nextRepath = Time.time + 0.15f;
                    }
                    break;
                case WolfState.Attack:
                    FaceTarget();
                    if (!biteApplied && Time.time >= biteStarted + biteDuration * biteImpactFraction)
                    {
                        biteApplied = true;
                        BiteContact?.Invoke(); // Notification only (audio); the bite resolves exactly as before.
                        if (targetHealth && !targetHealth.IsDead && DistanceToTarget() <= attackRange && CanSeeTarget())
                        {
                            // Capture the impact cargo before health events can end the mission.
                            CargoItem carried = noiseSource ? noiseSource.CarriedCargo : null;
                            targetHealth.ApplyDamageFrom(biteDamage, transform.position - player.transform.position);
                            if (noiseSource) noiseSource.ApplyCarriedImpact(carried, cargoBiteDamage, gripLoss);
                        }
                    }
                    if (Time.time >= biteStarted + biteDuration) Enter(WolfState.Chase);
                    break;
                case WolfState.Search:
                    if (Time.time >= stateUntil) Enter(WolfState.Return);
                    else if (Arrived())
                    {
                        Vector2 offset = Random.insideUnitCircle * 1.5f;
                        Move(lastKnown + new Vector3(offset.x, 0, offset.y), patrolSpeed);
                    }
                    break;
            }
        }

        private bool CanSeeTarget()
        {
            if (!player || !player.enabled || !targetHealth || targetHealth.IsDead ||
                Vector3.Distance(transform.position, player.transform.position) > detectionRange) return false;
            Vector3 eye = transform.position + Vector3.up * 0.8f;
            Vector3 point = player.transform.position;
            Vector3 direction = point - eye;
            int count = Physics.RaycastNonAlloc(eye, direction.normalized, sightHits, direction.magnitude,
                ~0, QueryTriggerInteraction.Ignore);
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i];
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform.IsChildOf(player.transform)) continue;
                return false;
            }
            return true;
        }

        private void Enter(WolfState state)
        {
            State = state;
            switch (state)
            {
                case WolfState.Idle:
                    agent.isStopped = true;
                    stateUntil = Time.time + patrolPause;
                    Animate("Idle");
                    break;
                case WolfState.Patrol:
                case WolfState.Return:
                    Vector3 destination = patrolPoints.Length > 0 && patrolPoints[patrolIndex]
                        ? patrolPoints[patrolIndex].position : home;
                    Move(destination, patrolSpeed);
                    Animate("Walk");
                    break;
                case WolfState.Investigate:
                    Move(lastKnown, patrolSpeed);
                    stateUntil = Time.time + 10f;
                    Animate("Walk");
                    break;
                case WolfState.Chase:
                    Move(lastKnown, chaseSpeed);
                    Animate("Run");
                    break;
                case WolfState.Attack:
                    agent.isStopped = true;
                    biteStarted = Time.time;
                    nextAttack = Time.time + Mathf.Max(attackCooldown, biteDuration);
                    biteApplied = false;
                    Animate("Bite", true);
                    break;
                case WolfState.Search:
                    Move(lastKnown, patrolSpeed);
                    stateUntil = Time.time + searchDuration;
                    Animate("Walk");
                    break;
            }
        }

        private void Move(Vector3 point, float speed)
        {
            agent.isStopped = false;
            agent.speed = speed;
            agent.stoppingDistance = State == WolfState.Chase ? attackRange * 0.8f : 0.2f;
            if (NavMesh.SamplePosition(point, out var hit, 2f, agent.areaMask)) agent.SetDestination(hit.position);
        }

        private bool Arrived() => !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= 0.25f);
        private float DistanceToTarget() => Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up).magnitude;

        private void FaceTarget()
        {
            if (!player) return;
            Vector3 direction = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 500f * Time.deltaTime);
        }

        private void Animate(string state, bool restart = false)
        {
            if (!animator || (!restart && animationName == state)) return;
            animationName = state;
            animator.CrossFadeInFixedTime(state, 0.08f, 0, 0f);
        }

        private void Die()
        {
            State = WolfState.Death;
            biteApplied = true;
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
            // The approved pack has no death clip. Freeze idle and tip the visual as a clear corpse placeholder.
            if (animator)
            {
                animator.Play("Idle");
                animator.speed = 0f;
                animator.transform.localRotation *= Quaternion.Euler(0, 0, 75);
            }
            foreach (var collider in GetComponents<Collider>()) collider.enabled = false;
        }
    }
}
