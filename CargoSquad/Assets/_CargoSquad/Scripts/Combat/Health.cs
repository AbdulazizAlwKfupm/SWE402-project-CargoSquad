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
        public event Action<Vector3?, Vector3> Damaged;
        public bool HasDamageFeedback => Damaged != null;
        private void Awake() => Current = maximum;
        public void ResetMaximum(float value)
        {
            if (!float.IsFinite(value)) return;
            maximum = Mathf.Max(1f, value);
            Current = maximum;
            Changed?.Invoke(Current, Maximum);
        }
        // Healing never revives a dead survivor or exceeds the existing maximum.
        public float Restore(float amount)
        {
            if (IsDead || !float.IsFinite(amount) || amount <= 0f) return 0f;
            float restored = Mathf.Min(amount, Maximum - Current);
            if (restored <= 0f) return 0f;
            Current += restored; Changed?.Invoke(Current, Maximum);
            return restored;
        }

        public void ApplyDamage(float damage) => ApplyDamage(damage, null, Vector3.zero);

        public void ApplyDamage(float damage, Vector3? point, Vector3 normal)
        {
            if (IsDead || !float.IsFinite(damage) || damage <= 0f) return;
            float previous = Current;
            Current = Mathf.Max(0f, Current - damage);
            Changed?.Invoke(Current, Maximum);
            if (Current < previous) Damaged?.Invoke(point, normal);
            if (IsDead) Died?.Invoke();
        }
    }
}
