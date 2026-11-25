using CargoSquad.Combat;
using CargoSquad.Player;
using UnityEngine;
using UnityEngine.AI;

namespace CargoSquad.AI
{
    public enum SpitterState { Idle, Walk, Run, SpitWindup, SpitAttack, MeleeSwipe, Hurt, Stunned, Death }

    [DisallowMultipleComponent, RequireComponent(typeof(Health), typeof(NavMeshAgent), typeof(CapsuleCollider))]
    public sealed class AcidSpitterAI : MonoBehaviour, IStunnable
    {
        [SerializeField] Animator animator;
        [SerializeField] Transform mouth;
        [SerializeField] AcidGlob projectile;
        [Header("Spacing")]
        [SerializeField, Min(.5f)] float detectionRange = 13f;
        [SerializeField, Min(.5f)] float preferredNear = 4f;
        [SerializeField, Min(.5f)] float preferredFar = 7f;
        [SerializeField, Min(.1f)] float moveSpeed = 3.1f;
        [Header("Acid spit")]
        [SerializeField, Min(.1f)] float projectileSpeed = 7f;
        [SerializeField, Min(0)] float spitDamage = 14f;
        [SerializeField, Min(.1f)] float spitCooldown = 1.75f;
        [SerializeField, Range(0,1)] float launchFrame = 8f / 24f;
        [Header("Claw swipe")]
        [SerializeField, Min(.1f)] float meleeReach = 1.85f;
        [SerializeField, Min(0)] float swipeDamage = 11f;
        [SerializeField, Min(.1f)] float swipeCooldown = 1f;
        [SerializeField, Range(0,1)] float contactFrame = 23f / 42f;
        [SerializeField, Min(0)] float stunImmunity = 1.2f;

        public SpitterState State { get; private set; }
        static readonly int[] Clips = System.Array.ConvertAll(System.Enum.GetNames(typeof(SpitterState)),
            name => Animator.StringToHash("Base Layer." + name));
        NavMeshAgent agent;
        CapsuleCollider bodyCollider;
        Health health, survivor;
        PlayerController player;
        Vector3 home, aimPoint;
        Quaternion homeRotation;
        float lastHealth, nextPath, nextAttack, recoverAt, immuneUntil;
        bool spent, returnRequested;
        NavMeshPath returnPath;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            bodyCollider = GetComponent<CapsuleCollider>();
            returnPath = new NavMeshPath();
            health = GetComponent<Health>();
            home = transform.position;
            homeRotation = transform.rotation;
        }

        void OnEnable()
        {
            lastHealth = health.Current;
            health.Changed += React;
        }

        void Start()
        {
            lastHealth = health.Current;
            player = FindFirstObjectByType<PlayerController>();
            if (player) survivor = player.GetComponent<Health>();
        }

        void OnDisable()
        {
            health.Changed -= React;
            returnRequested = false;
            StopMoving();
        }

        void StopMoving()
        {
            nextPath = 0f;
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.updateRotation = false;
            if (agent.isStopped) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        void Enter(SpitterState state)
        {
            if (State == state) return;
            State = state;
            spent = false;
            animator.CrossFadeInFixedTime(Clips[(int)state], .055f, 0, 0f);
        }

        void React(float current, float maximum)
        {
            bool lost = current < lastHealth;
            lastHealth = current;
            if (!lost) return;
            returnRequested = false;
            StopMoving();
            if (health.IsDead)
            {
                bodyCollider.enabled = false;
                agent.enabled = false;
                Enter(SpitterState.Death);
            }
            else if (State != SpitterState.Stunned) Enter(SpitterState.Hurt);
        }

        public bool TryStun(float seconds)
        {
            if (!isActiveAndEnabled || health.IsDead || !float.IsFinite(seconds) ||
                seconds <= 0f || Time.time < immuneUntil) return false;
            returnRequested = false;
            StopMoving();
            recoverAt = Time.time + seconds;
            immuneUntil = recoverAt + stunImmunity;
            nextAttack = Mathf.Max(nextAttack, recoverAt + .35f);
            Enter(SpitterState.Stunned);
            return true;
        }

        bool TargetAlive => player && player.isActiveAndEnabled && survivor && !survivor.IsDead;
        bool CanSee()
        {
            // Keep sight independent of model facing. The ray starts inside our own capsule.
            return !Physics.Linecast(bodyCollider.bounds.center, player.transform.position, out var hit, ~0,
                QueryTriggerInteraction.Ignore) || hit.transform.IsChildOf(player.transform);
        }

        void Move(Vector3 destination, SpitterState state)
        {
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.speed = state == SpitterState.Run ? moveSpeed : moveSpeed * .67f;
            if (Time.time >= nextPath)
            {
                agent.SetDestination(destination);
                nextPath = Time.time + .16f;
            }
            Enter(state);
        }

        void ReturnHome()
        {
            float arrivalDistance = Mathf.Max(.4f, agent.stoppingDistance);
            if (!returnRequested)
            {
                returnRequested = true;
                // Resolve the authored spawn to the nearby navigation surface once per return.
                if (NavMesh.SamplePosition(home, out var destination, .5f,
                        new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask }) &&
                    agent.CalculatePath(destination.position, returnPath) &&
                    returnPath.status == NavMeshPathStatus.PathComplete &&
                    agent.SetPath(returnPath))
                {
                    agent.updateRotation = true;
                    agent.isStopped = false;
                    agent.speed = moveSpeed * .67f;
                    nextPath = 0f;
                }
                else StopMoving();
            }

            if (!agent.isStopped && (agent.pathPending ||
                (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete &&
                 agent.remainingDistance > arrivalDistance)))
            {
                Enter(SpitterState.Walk);
                return;
            }

            StopMoving();
            Enter(SpitterState.Idle);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation,
                agent.angularSpeed * Time.deltaTime);
        }

        bool FinishAction()
        {
            var pose = animator.GetCurrentAnimatorStateInfo(0);
            if (pose.fullPathHash != Clips[(int)State]) return false;
            if (State == SpitterState.SpitWindup && pose.normalizedTime >= 1f)
            {
                if (!TargetAlive || !CanSee()) { Enter(SpitterState.Idle); return false; }
                aimPoint = player.transform.position;
                Enter(SpitterState.SpitAttack);
                return false;
            }
            if (State == SpitterState.SpitAttack && !spent && pose.normalizedTime >= launchFrame)
            {
                spent = true;
                if (TargetAlive)
                    Instantiate(projectile, mouth.position, Quaternion.identity)
                        .Launch(transform, aimPoint, projectileSpeed, spitDamage, detectionRange);
                nextAttack = Time.time + spitCooldown;
            }
            if (State == SpitterState.MeleeSwipe && !spent && pose.normalizedTime >= contactFrame)
            {
                spent = true;
                if (TargetAlive)
                {
                    Vector3 offset = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
                    if (offset.magnitude <= meleeReach && Vector3.Angle(transform.forward, offset) < 62f && CanSee())
                        survivor.ApplyDamageFrom(swipeDamage, -offset);
                }
                nextAttack = Time.time + swipeCooldown;
            }
            if (pose.normalizedTime < 1f) return false;
            Enter(SpitterState.Idle);
            return true;
        }

        void Update()
        {
            if (Time.timeScale <= 0f || health.IsDead || !agent.enabled || !agent.isOnNavMesh) return;
            if (State == SpitterState.Stunned && Time.time < recoverAt) return;
            if (State is SpitterState.SpitWindup or SpitterState.SpitAttack or SpitterState.MeleeSwipe or SpitterState.Hurt)
                if (!FinishAction()) return;
            Vector3 offset = TargetAlive ? Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up) : Vector3.zero;
            float distance = offset.magnitude;
            if (!TargetAlive || distance > detectionRange || !CanSee())
            {
                ReturnHome();
                return;
            }
            returnRequested = false;
            if (distance > preferredFar) { Move(player.transform.position, SpitterState.Run); return; }
            if (distance > meleeReach && distance < preferredNear &&
                NavMesh.SamplePosition(transform.position - offset.normalized * 1.8f, out var retreat, 1f, agent.areaMask) &&
                !agent.Raycast(retreat.position, out _))
            {
                Move(retreat.position, SpitterState.Walk);
                return;
            }
            StopMoving();
            if (distance > .01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(offset), 320f * Time.deltaTime);
            if (Time.time >= nextAttack && Vector3.Angle(transform.forward, offset) < 18f)
                Enter(distance <= meleeReach ? SpitterState.MeleeSwipe : SpitterState.SpitWindup);
            else Enter(SpitterState.Idle);
        }
    }
}
