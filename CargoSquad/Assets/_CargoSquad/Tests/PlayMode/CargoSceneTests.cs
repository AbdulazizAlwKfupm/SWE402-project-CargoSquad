#if UNITY_EDITOR
using System.Collections;
using CargoSquad.Cargo;
using CargoSquad.Missions;
using CargoSquad.Player;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace CargoSquad.Tests
{
    public sealed class CargoSceneTests
    {

        private const string Scene = "Assets/_CargoSquad/Scenes/IndustrialDistrict.unity";

        [UnityTest]
        public IEnumerator StaminaAndSceneCargoLoop()
        {
            EditorSceneManager.LoadSceneInPlayMode(Scene, new UnityEngine.SceneManagement.LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null;
            Object.FindFirstObjectByType<CargoSquad.UI.CharacterSelection>()?.StartSelectedCharacter();
            var player = Object.FindFirstObjectByType<CargoInteractor>();
            var cargo = Object.FindFirstObjectByType<CargoItem>();
            var extraction = Object.FindFirstObjectByType<CargoExtraction>();
            var body = player.GetComponent<Rigidbody>();
            var stamina = player.GetComponent<PlayerStamina>();
            Assert.That(Camera.main.GetComponent<TopDownCameraFollow>(), Is.Not.Null);
            Assert.That(stamina.Tick(true, 1, 100), Is.False);
            Assert.That(stamina.Current, Is.Zero);
            Assert.That(stamina.Tick(true, 1, 0.01f), Is.False);
            Assert.That(stamina.IsExhausted, Is.True);
            stamina.Tick(false, 1, 100);
            Assert.That(stamina.IsExhausted, Is.False);

            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Vector3 start = body.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                yield return new WaitForSeconds(0.2f);
                Assert.That(body.position.x, Is.GreaterThan(start.x));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());

                body.position = extraction.transform.position;
                yield return new WaitForSeconds(0.12f);
                Assert.That(extraction.IsComplete, Is.False);
                Vector3 depotPosition = cargo.transform.position;
                cargo.transform.position = new Vector3(-2.35f, 0.75f, 6.5f);
                body.position = new Vector3(-4.1f, 1f, 6.5f);
                yield return new WaitForSeconds(0.12f);
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.Null);
                cargo.transform.position = depotPosition;
                body.position = cargo.transform.position + new Vector3(0f, 0.25f, -1.9f);
                yield return new WaitForSeconds(0.12f);
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.SameAs(cargo));

                float condition = cargo.Condition.Current;
                player.Grip.Tick(true, true, 1f, 1f);
                float lowGrip = player.Grip.Percent;
                player.Grip.Tick(false, true, 1f, 1f);
                float walkingGain = player.Grip.Percent - lowGrip;
                player.Grip.Tick(false, false, 1f, 1f);
                Assert.That(player.Grip.Percent, Is.GreaterThan(lowGrip + walkingGain * 2));
                Assert.That(cargo.Condition.Current, Is.EqualTo(condition));

                body.position = new Vector3(0, 1, -4);
                yield return new WaitForSeconds(0.12f);
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.Null);
                Assert.That(cargo.Condition.Current, Is.EqualTo(condition));
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.SameAs(cargo));

                int noises = 0;
                player.NoiseEmitted += _ => noises++;
                var ground = GameObject.Find("Ground").GetComponent<BoxCollider>();
                ground.enabled = false;
                player.Grip.Tick(true, true, 1f, 100f);
                yield return null;
                yield return null;
                Assert.That(player.HasPendingSlip, Is.True);
                Assert.That(player.CarriedCargo, Is.SameAs(cargo));
                Assert.That(noises, Is.Zero);
                Assert.That(cargo.Condition.Current, Is.EqualTo(condition));
                ground.enabled = true;
                body.position = new Vector3(0, 1, -4);
                yield return new WaitForSeconds(0.12f);
                Assert.That(player.CarriedCargo, Is.Null);
                Assert.That(noises, Is.EqualTo(1));
                Assert.That(cargo.Condition.Current, Is.EqualTo(condition - cargo.Definition.SlipDamage));
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.SameAs(cargo));
                Assert.That(player.Grip.Percent, Is.EqualTo(100f));
                body.position = extraction.transform.position;
                yield return new WaitForSeconds(0.12f);
                Assert.That(extraction.IsComplete, Is.True);
                Assert.That(extraction.RecoveryFailed, Is.False);
                Assert.That(noises, Is.EqualTo(1));
            }
            finally { InputSystem.RemoveDevice(keyboard); }

        }

        [UnityTest]
        public IEnumerator SharedDefinitionKeepsIndependentConditionAndDestroyedCargoAllowsEscape()
        {
            EditorSceneManager.LoadSceneInPlayMode(Scene, new UnityEngine.SceneManagement.LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null;
            Object.FindFirstObjectByType<CargoSquad.UI.CharacterSelection>()?.StartSelectedCharacter();
            var cargo = Object.FindFirstObjectByType<CargoItem>();
            var duplicate = Object.Instantiate(cargo.gameObject);
            var other = duplicate.GetComponent<CargoCondition>();
            var extraction = Object.FindFirstObjectByType<CargoExtraction>();
            int destroyed = 0;
            cargo.Condition.Destroyed += () => destroyed++;
            Assert.That(other.Definition, Is.SameAs(cargo.Definition));
            Assert.That(cargo.Condition.ApplyDamage(float.NaN), Is.False);
            Assert.That(cargo.Condition.ApplyDamage(10000), Is.True);
            Assert.That(cargo.Condition.ApplyDamage(1), Is.False);
            Assert.That(cargo.Condition.Current, Is.Zero);
            Assert.That(other.Current, Is.EqualTo(other.Maximum));
            Assert.That(destroyed, Is.EqualTo(1));
            Assert.That(extraction.CargoDestroyed, Is.True);
            Assert.That(extraction.IsComplete, Is.False);
            Object.Destroy(duplicate);
            var player = Object.FindFirstObjectByType<CargoInteractor>();
            player.GetComponent<Rigidbody>().position = extraction.transform.position;
            yield return new WaitForSeconds(0.12f);
            Assert.That(extraction.IsComplete, Is.True);
            Assert.That(extraction.RecoveryFailed, Is.True);
            Assert.That(cargo.IsDelivered, Is.False);

        }

        private static IEnumerator Tap(Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return new WaitForSeconds(0.08f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.08f);
        }
    }
}

#endif
