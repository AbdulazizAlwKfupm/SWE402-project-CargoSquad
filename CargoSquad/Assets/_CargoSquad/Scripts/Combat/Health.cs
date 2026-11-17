using System;
using UnityEngine;

namespace CargoSquad.Combat
{
    public interface IDamageable { void ApplyDamage(float damage); }

    /// <summary>Shared damage target for the player and future enemies. No physics or movement ownership.</summary>
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1)] private float maximum = 100f;
        public float Maximum => maximum;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public event Action Died;
        private void Awake() => Current = maximum;
        public void ResetMaximum(float value) { maximum = Mathf.Max(1f, value); Current = maximum; }
        public void ApplyDamage(float damage)
        {
            if (IsDead || damage <= 0f) return;
            Current = Mathf.Max(0f, Current - damage);
            if (IsDead) Died?.Invoke();
        }
    }
}
