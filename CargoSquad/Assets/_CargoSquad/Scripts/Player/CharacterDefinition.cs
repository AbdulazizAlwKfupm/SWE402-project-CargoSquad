using UnityEngine;

namespace CargoSquad.Player
{
    [CreateAssetMenu(menuName = "Cargo Squad/Character Definition")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Display identity for selection and HUDs.")] private string displayName = "Survivor";
        [SerializeField, Min(1)] private float maxHealth = 100;
        [SerializeField, Min(0), Tooltip("Metres per second.")] private float walkSpeed = 4;
        [SerializeField, Min(0), Tooltip("Metres per second.")] private float runSpeed = 7;
        [SerializeField, Min(1)] private float maxStamina = 100;
        [SerializeField, Min(0), Tooltip("Stamina restored per second when not sprinting.")] private float staminaRecovery = 15;
        [SerializeField, Min(0), Tooltip("Stamina spent per second at an unloaded sprint.")] private float sprintConsumption = 22;
        [SerializeField, Min(0.1f), Tooltip("Higher strength reduces the effective cargo load.")] private float carryStrength = 1;
        [SerializeField, Min(0.1f), Tooltip("Higher stability reduces sprint grip loss.")] private float cargoStability = 1;
        [SerializeField, Min(0)] private float stealthModifier = 1;
        [SerializeField, Min(0)] private float scavengingModifier = 1;

        public string DisplayName => displayName;
        public float MaxHealth => Mathf.Max(1, maxHealth);
        public float WalkSpeed => Mathf.Max(0, walkSpeed);
        public float RunSpeed => Mathf.Max(WalkSpeed, runSpeed);
        public float MaxStamina => Mathf.Max(1, maxStamina);
        public float StaminaRecovery => Mathf.Max(0, staminaRecovery);
        public float SprintConsumption => Mathf.Max(0, sprintConsumption);
        public float CarryStrength => Mathf.Max(0.1f, carryStrength);
        public float CargoStability => Mathf.Max(0.1f, cargoStability);
        public float StealthModifier => Mathf.Max(0, stealthModifier);
        public float ScavengingModifier => Mathf.Max(0, scavengingModifier);
    }
}
