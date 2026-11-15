using System;
using UnityEngine;

namespace CargoSquad.Cargo
{
    [DisallowMultipleComponent]
    public sealed class CargoCondition : MonoBehaviour
    {
        [SerializeField] private CargoDefinition definition;

        public CargoDefinition Definition => definition;
        public float Maximum => definition ? definition.MaximumCondition : 100f;
        public float Current { get; private set; }
        public bool IsDestroyed => Current <= 0f;
        public event Action<float, float> Changed;
        public event Action Destroyed;

        private void Awake() => Current = Maximum;

        public bool ApplyDamage(float amount)
        {
            if (IsDestroyed || !float.IsFinite(amount) || amount <= 0f)
                return false;

            Current = Mathf.Max(0f, Current - amount);
            Changed?.Invoke(Current, Maximum);
            if (IsDestroyed)
                Destroyed?.Invoke();
            return true;
        }
    }
}
