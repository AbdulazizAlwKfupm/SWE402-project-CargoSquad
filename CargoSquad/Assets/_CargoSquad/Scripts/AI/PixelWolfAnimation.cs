using CargoSquad.Combat;
using UnityEngine;

namespace CargoSquad.AI
{
    // Runs after AI.Update and before the Animator advances. This prefab owns
    // visual playback; navigation, bite contact and cooldown stay with the AI.
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    [RequireComponent(typeof(MutatedWolfAI), typeof(Health))]
    public sealed class PixelWolfAnimation : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
        private static readonly int Walk = Animator.StringToHash("Base Layer.Walk");
        private static readonly int Run = Animator.StringToHash("Base Layer.Run");
        private static readonly int Bite = Animator.StringToHash("Base Layer.Bite");
        private static readonly int Hurt = Animator.StringToHash("Base Layer.Hurt");
        private static readonly int Stunned = Animator.StringToHash("Base Layer.Stunned");
        private static readonly int Death = Animator.StringToHash("Base Layer.Death");
        private MutatedWolfAI wolf;
        private Health health;
        private float previousHP;
        private bool recoilPending;
        private int playing;

        private void Awake()
        {
            wolf = GetComponent<MutatedWolfAI>();
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            playing = 0;
            previousHP = health.Current;
            health.Changed += OnHealthChanged;
        }

        private void OnDisable()
        {
            health.Changed -= OnHealthChanged;
            recoilPending = false;
        }

        private void OnHealthChanged(float hp, float maximum)
        {
            recoilPending |= hp > 0 && hp < previousHP;
            previousHP = hp;
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            int next;
            if (health.IsDead) next = Death;
            else if (wolf.State == WolfState.Stunned) next = Stunned;
            else if (wolf.State == WolfState.Attack) next = Bite;
            else if (recoilPending) next = Hurt;
            else if (playing == Hurt && (animator.IsInTransition(0) ||
                animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)) return;
            else next = wolf.State switch
            {
                WolfState.Chase => Run,
                WolfState.Patrol or WolfState.Investigate or WolfState.Search or WolfState.Return => Walk,
                _ => Idle
            };
            bool restart = next == Hurt && recoilPending;
            recoilPending = false;
            if (playing == next && !restart) return;
            playing = next;
            animator.CrossFadeInFixedTime(next, .045f, 0, 0f);
        }
    }
}

