using System;
using UnityEngine;

namespace CargoSquad.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerStamina : MonoBehaviour
    {
        [SerializeField] private CharacterDefinition definition;
        [SerializeField, Range(0.01f, 1f), Tooltip("Fraction needed to sprint again after exhaustion.")]
        private float resumeFraction = 0.25f;

        public CharacterDefinition Definition => definition;
        public float Maximum => definition ? definition.MaxStamina : 100f;
        public float Current { get; private set; }
        public bool IsExhausted { get; private set; }
        public event Action<float, float> Changed;

        private void Awake() => Current = Maximum;

        public void SetDefinition(CharacterDefinition character)
        {
            definition = character;
            Current = Maximum;
            IsExhausted = false;
            Changed?.Invoke(Current, Maximum);
        }

        // Restore to a target without ever reducing existing stamina.
        public void RestoreTo(float target)
        {
            Current = Mathf.Max(Current, Mathf.Clamp(target, 0f, Maximum));
            if (Current >= Maximum * Mathf.Clamp(resumeFraction, 0.01f, 1f)) IsExhausted = false;
            Changed?.Invoke(Current, Maximum);
        }

        // Returns whether the current physics step can use sprint speed.
        public bool Tick(bool wantsSprint, float loadMultiplier, float seconds)
        {
            if (!definition || seconds <= 0f)
                return false;

            bool sprinting = wantsSprint && !IsExhausted;
            float rate = sprinting ? -definition.SprintConsumption * Mathf.Max(0f, loadMultiplier) :
                definition.StaminaRecovery;
            float previous = Current;
            Current = Mathf.Clamp(Current + rate * seconds, 0f, Maximum);
            if (Current <= 0f)
                IsExhausted = true;
            else if (Current >= Maximum * Mathf.Clamp(resumeFraction, 0.01f, 1f))
                IsExhausted = false;

            if (Current != previous)
                Changed?.Invoke(Current, Maximum);
            return sprinting && !IsExhausted;
        }
    }
}
