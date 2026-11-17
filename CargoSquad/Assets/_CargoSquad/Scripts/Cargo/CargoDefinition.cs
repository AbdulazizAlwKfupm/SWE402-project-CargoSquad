using UnityEngine;

namespace CargoSquad.Cargo
{
    public enum CargoCategory { Medical, Military, Survival, Technical }
    public enum CargoSize { Small, Medium, Large }
    public enum CargoWeight { Light, Medium, Heavy }
    public enum CargoValue { Normal, Valuable, Rare }

    [CreateAssetMenu(menuName = "Cargo Squad/Cargo Definition")]
    public sealed class CargoDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Cargo";
        [SerializeField] private CargoCategory category;
        [SerializeField] private CargoSize size = CargoSize.Medium;
        [SerializeField] private CargoWeight weight = CargoWeight.Medium;
        [SerializeField] private CargoValue value;
        [SerializeField, Min(1)] private float maximumCondition = 100;
        [SerializeField, Range(0f, 0.85f), Tooltip("Fraction of travel speed lost at unit effective load.")]
        private float movementPenalty = 0.12f;
        [SerializeField, Min(0), Tooltip("Extra sprint stamina consumption at unit effective load.")]
        private float staminaModifier = 0.35f;
        [SerializeField, Min(0), Tooltip("Multiplier on sprint grip loss.")]
        private float gripModifier = 1f;
        [SerializeField, Min(0), Tooltip("Condition lost when an uncontrolled slip lands.")]
        private float slipDamage = 25f;

        public string DisplayName => displayName;
        public CargoCategory Category => category;
        public CargoSize Size => size;
        public CargoWeight Weight => weight;
        public CargoValue Value => value;
        public float MaximumCondition => Mathf.Max(1, maximumCondition);
        public float SlipDamage => Mathf.Max(0f, slipDamage);

        public float EffectiveLoad(float carryStrength)
        {
            float mass = weight switch { CargoWeight.Light => 0.65f, CargoWeight.Heavy => 1.5f, _ => 1f };
            return mass / Mathf.Max(0.1f, carryStrength);
        }

        public float SpeedMultiplier(float strength) =>
            1f - Mathf.Clamp(movementPenalty * EffectiveLoad(strength), 0f, 0.85f);

        public float StaminaMultiplier(float strength) => 1f + Mathf.Max(0f, staminaModifier) * EffectiveLoad(strength);

        public float GripDrainMultiplier(float strength, float stability) =>
            Mathf.Max(0f, gripModifier) * EffectiveLoad(strength) / Mathf.Max(0.1f, stability);
    }
}
