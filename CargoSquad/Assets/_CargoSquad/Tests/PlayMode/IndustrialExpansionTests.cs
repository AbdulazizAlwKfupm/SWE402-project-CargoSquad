#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Cargo;
using CargoSquad.Missions;
using CargoSquad.Player;
using CargoSquad.UI;
using CargoSquad.Visuals;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CargoSquad.Tests
{
    public sealed class IndustrialExpansionTests
    {
        private CharacterSelection selection;
        private CargoInteractor carrier;
        private CargoItem cargo;
        private CargoExtraction mission;
        private Keyboard keyboard;
        private Rigidbody body;

        [UnitySetUp]
        public IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialExpansion.unity", new LoadSceneParameters());
            yield return null;
            yield return null;
            selection = Object.FindFirstObjectByType<CharacterSelection>();
            carrier = Object.FindFirstObjectByType<CargoInteractor>();
            cargo = Object.FindFirstObjectByType<CargoItem>();
            mission = Object.FindFirstObjectByType<CargoExtraction>();
            body = carrier.GetComponent<Rigidbody>();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            Time.timeScale = 1;
            yield return null;
        }

        [UnityTest]
        public IEnumerator LayoutHasConnectedNavigationAndVisualOnlyHelicopter()
        {
            Assert.That(selection.HasStarted, Is.False);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            var heli = GameObject.Find("Helicopter Placeholder - Visual Only");
            Assert.That(heli.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(heli.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(heli.GetComponentsInChildren<Transform>().Count(t => t.name == "Hanging Rope"), Is.EqualTo(2));
            var warehouse = GameObject.Find("Warehouse - Open Roof").transform;
            Assert.That(warehouse.Find("Roof"), Is.Null);
            var marker = Object.FindFirstObjectByType<ExtractionMarker>();
            Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(mission.IsPrimaryCargo(cargo), Is.True);
            Assert.That(Object.FindFirstObjectByType<CargoMissionOverlay>(), Is.Not.Null);

            Vector3[] route = { new Vector3(-10, 0, -7), new Vector3(0, 0, -3),
                new Vector3(0, 0, 5.7f), new Vector3(7.5f, 0, -3), new Vector3(11, 0, -3) };
            for (int i = 1; i < route.Length; i++)
            {
                Assert.That(NavMesh.SamplePosition(route[i - 1], out var from, .5f, NavMesh.AllAreas), Is.True);
                Assert.That(NavMesh.SamplePosition(route[i], out var to, .5f, NavMesh.AllAreas), Is.True);
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            }
            yield return IndustrialExpansionScreenshots.Capture("01-character-selection");
            selection.StartSelectedCharacter();
            yield return IndustrialExpansionScreenshots.Overview();
            body.position = mission.transform.position;
            Physics.SyncTransforms();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(mission.IsComplete, Is.False, "Entering without the required cargo must not complete the mission.");
            Assert.That(marker.MissingCargoPrompt, Is.True);
        }

        [UnityTest]
        public IEnumerator HeavyCanWalkPickUpAndExtract() => CarryRoute(0);

        [UnityTest]
        public IEnumerator MedicCanWalkPickUpAndExtract() => CarryRoute(1);

        private IEnumerator CarryRoute(int character)
        {
            selection.SelectCharacter(character);
            selection.StartSelectedCharacter();
            yield return null;
            string role = character == 0 ? "heavy" : "medic";
            yield return IndustrialExpansionScreenshots.Capture(role + "-deployment");
            yield return WalkTo(new Vector2(-10, -3));
            yield return WalkTo(new Vector2(0, -3));
            yield return WalkTo(new Vector2(0, 5.7f));
            yield return IndustrialExpansionScreenshots.Capture(role + "-warehouse-ground-cargo");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
            yield return new WaitForSecondsRealtime(.35f);
            yield return IndustrialExpansionScreenshots.Capture(role + "-warehouse-carry");
            yield return WalkTo(new Vector2(0, -3));
            Assert.That(carrier.CarriedCargo, Is.SameAs(cargo), "Cargo must clear the warehouse entrance while carried.");
            yield return WalkTo(new Vector2(7, -3));
            yield return new WaitForSecondsRealtime(.35f);
            yield return IndustrialExpansionScreenshots.Capture(role + "-extraction-approach");
            yield return WalkTo(new Vector2(11, -3));
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.CargoRecovered));
            Assert.That(cargo.IsDelivered, Is.True);
            Assert.That(Object.FindFirstObjectByType<MissionResultPanel>().Visible, Is.True);
            yield return new WaitForSecondsRealtime(.35f);
            yield return IndustrialExpansionScreenshots.Capture(role + "-mission-complete");
        }

        private IEnumerator WalkTo(Vector2 destination)
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (!mission.IsComplete && Vector2.Distance(new Vector2(body.position.x, body.position.z), destination) > .18f)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Player blocked on route to " + destination);
                Vector2 delta = destination - new Vector2(body.position.x, body.position.z);
                Key horizontal = delta.x > 0 ? Key.D : Key.A;
                Key vertical = delta.y > 0 ? Key.W : Key.S;
                InputSystem.QueueStateEvent(keyboard, Mathf.Abs(delta.x) > .14f && Mathf.Abs(delta.y) > .14f
                    ? new KeyboardState(horizontal, vertical)
                    : new KeyboardState(Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? horizontal : vertical));
                yield return null;
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
    }
}
#endif
