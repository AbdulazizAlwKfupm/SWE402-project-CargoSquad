using CargoSquad.Cargo;
using CargoSquad.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CargoSquad.Tests
{
    public sealed class CargoGameplayTests
    {
        private const string Assets = "Assets/_CargoSquad/ScriptableObjects/";

        [Test]
        public void CharacterAssetsDifferentiateMovementAndStamina()
        {
            var heavy = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Assets + "Characters/Heavy.asset");
            var scout = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Assets + "Characters/Scout.asset");
            Assert.That(scout.WalkSpeed, Is.GreaterThan(heavy.WalkSpeed));
            Assert.That(scout.RunSpeed, Is.GreaterThan(heavy.RunSpeed));
            Assert.That(scout.MaxStamina, Is.GreaterThan(heavy.MaxStamina));
            Assert.That(scout.StaminaRecovery, Is.GreaterThan(heavy.StaminaRecovery));
            Assert.That(heavy.CarryStrength, Is.GreaterThan(scout.CarryStrength));
        }

        [Test]
        public void WeightAndStrengthScaleAllCarryPenalties()
        {
            var cargo = ScriptableObject.CreateInstance<CargoDefinition>();
            using var settings = new SerializedObject(cargo);
            try
            {
                float speed = 1f;
                float stamina = 1f;
                float grip = 0f;
                foreach (CargoWeight weight in System.Enum.GetValues(typeof(CargoWeight)))
                {
                    settings.FindProperty("weight").enumValueIndex = (int)weight;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    Assert.That(cargo.SpeedMultiplier(1), Is.LessThan(speed));
                    Assert.That(cargo.StaminaMultiplier(1), Is.GreaterThan(stamina));
                    Assert.That(cargo.GripDrainMultiplier(1, 1), Is.GreaterThan(grip));
                    speed = cargo.SpeedMultiplier(1);
                    stamina = cargo.StaminaMultiplier(1);
                    grip = cargo.GripDrainMultiplier(1, 1);
                    Assert.That(cargo.SpeedMultiplier(2), Is.GreaterThan(speed));
                    Assert.That(cargo.StaminaMultiplier(2), Is.LessThan(stamina));
                    Assert.That(cargo.GripDrainMultiplier(2, 2), Is.LessThan(grip));
                }
            }
            finally { Object.DestroyImmediate(cargo); }
        }

    }
}
