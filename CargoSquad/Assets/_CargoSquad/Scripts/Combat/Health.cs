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
        public event Action<float, float> Changed;
        public event Action Died;
        private void Awake() => Current = maximum;
        public void ResetMaximum(float value)
        {
            if (!float.IsFinite(value)) return;
            maximum = Mathf.Max(1f, value);
            Current = maximum;
            Changed?.Invoke(Current, Maximum);
        }
        public void ApplyDamage(float damage)
        {
            if (IsDead || !float.IsFinite(damage) || damage <= 0f) return;
            Current = Mathf.Max(0f, Current - damage);
            Changed?.Invoke(Current, Maximum);
            if (IsDead) Died?.Invoke();
        }
    }
}
