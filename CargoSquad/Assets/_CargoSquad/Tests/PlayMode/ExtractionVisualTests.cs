#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Missions;
using CargoSquad.Player;
using CargoSquad.UI;
using CargoSquad.Visuals;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CargoSquad.Tests
{
    public sealed class ExtractionVisualTests
    {
        private ExtractionMarker marker;
        private CargoExtraction mission;
        private CargoInteractor carrier;
        private CargoItem cargo;
        private Rigidbody body;
        private CharacterSelection selection;
        private Keyboard keyboard;
        private Mouse mouse;
        private Material brown;
        [UnitySetUp]
        public IEnumerator Load()
        {
            successes = escapes = 0;
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity", new LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null; yield return null;
            marker = Object.FindFirstObjectByType<ExtractionMarker>(); mission = Object.FindFirstObjectByType<CargoExtraction>();
            Assert.That(marker, Is.Not.Null); Assert.That(Object.FindObjectsByType<ExtractionMarker>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            selection = Object.FindFirstObjectByType<CharacterSelection>(); selection.StartSelectedCharacter();
            foreach (var wolf in Object.FindObjectsByType<MutatedWolfAI>(FindObjectsSortMode.None)) wolf.enabled = false;
            carrier = Object.FindFirstObjectByType<CargoInteractor>(); cargo = Object.FindFirstObjectByType<CargoItem>(); body = carrier.GetComponent<Rigidbody>();
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(4, 4) });
            brown = AssetDatabase.LoadAssetAtPath<Material>("Assets/_CargoSquad/Art/Materials/CargoWeatheredBrown.mat");
            yield return null; yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            yield return null;
        }
        private IEnumerator Move(Vector3 point)
        {
            body.position = point; body.linearVelocity = Vector3.zero; Physics.SyncTransforms();
            for (int step = 0; step < 8; step++) yield return new WaitForFixedUpdate();
            yield return null;
        }
        private IEnumerator Tap()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        private int successes, escapes;
        private void ObserveOutcomes()
        {
            typeof(CargoExtraction).GetField("missionSucceeded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(mission).AsEvent().AddListener(() => successes++);
            typeof(CargoExtraction).GetField("survivorEscaped", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(mission).AsEvent().AddListener(() => escapes++);
        }
        [UnityTest]
        public IEnumerator MarkerPulsesHasNoCollisionAndMissingCargoDoesNotComplete()
        {
            ObserveOutcomes();
            Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(marker.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(mission.GetComponent<BoxCollider>().isTrigger, Is.True);
            Assert.That(mission.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(marker.MarkerHeight, Is.EqualTo(2.8f)); Assert.That(marker.Footprint.x, Is.EqualTo(3.25f)); Assert.That(marker.Footprint.y, Is.EqualTo(3.25f));
            Assert.That(marker.GetComponentsInChildren<TMP_Text>().Single().text, Is.EqualTo("EXTRACT"));
            float pulse = marker.PulseBrightness; yield return new WaitForSeconds(.4f);
            Assert.That(Mathf.Abs(marker.PulseBrightness - pulse), Is.GreaterThan(.005f));
            yield return Move(mission.transform.position + Vector3.left * 3.2f);
            yield return new WaitForSeconds(.3f); yield return Capture("01-extraction-marker");
            yield return Move(mission.transform.position);
            Assert.That(marker.MissingCargoPrompt, Is.True); Assert.That(mission.IsComplete, Is.False);
            Assert.That(Object.FindFirstObjectByType<CargoMissionOverlay>().GetComponentsInChildren<TMP_Text>().Single(t => t.name == "Warning").text,
                Does.Contain("CARGO MISSING"));
            Assert.That(successes, Is.Zero); Assert.That(escapes, Is.Zero); yield return Capture("04-inside-missing-cargo");
        }
        [UnityTest]
        public IEnumerator BrownCargoCarriesDropsAndExtractsExactlyOnce()
        {
            ObserveOutcomes();
            Assert.That(cargo.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(brown));
            Assert.That(Vector3.Distance(cargo.Size, new Vector3(.6f, .45f, .45f)), Is.LessThan(.0001f));
            yield return Move(cargo.transform.position + new Vector3(1.65f, .775f, 0));
            yield return new WaitForSeconds(.3f); yield return Capture("02-brown-cargo-ground");
            yield return Tap(); Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
            carrier.Grip.ApplyLoss(100); yield return null; yield return null;
            Assert.That(carrier.CarriedCargo, Is.Null); Assert.That(cargo.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(brown));
            Assert.That(cargo.Condition.Current, Is.EqualTo(cargo.Condition.Maximum - cargo.Definition.SlipDamage));
            yield return Move(cargo.transform.position + new Vector3(0, .8f, -1.4f)); yield return Tap();
            Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
            yield return Move(mission.transform.position + Vector3.left * 3.2f);
            selection.ActiveVisual.transform.rotation = Quaternion.LookRotation(Vector3.right);
            yield return new WaitForSeconds(.3f); yield return Capture("03-heavy-brown-cargo-approaching");
            Assert.That(mission.IsComplete, Is.False);
            yield return Move(mission.transform.position); yield return new WaitForSeconds(.15f);
            Assert.That(mission.IsComplete, Is.True); Assert.That(cargo.IsDelivered, Is.True); Assert.That(mission.RecoveryFailed, Is.False);
            Assert.That(successes, Is.EqualTo(1)); Assert.That(escapes, Is.Zero); yield return Capture("05-successful-extraction");
            yield return new WaitForSeconds(.2f); Assert.That(successes, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator DestroyedCargoAllowsEscapeAndDeathHasPriority()
        {
            ObserveOutcomes(); cargo.Condition.ApplyDamage(10000);
            yield return Move(mission.transform.position + Vector3.left * 3.2f);
            yield return new WaitForSeconds(.3f); yield return Capture("06-escape-only-available");
            yield return Move(mission.transform.position);
            Assert.That(mission.IsComplete, Is.True); Assert.That(mission.RecoveryFailed, Is.True);
            Assert.That(cargo.IsDelivered, Is.False); Assert.That(successes, Is.Zero); Assert.That(escapes, Is.EqualTo(1));
            yield return Capture("07-survivor-escaped");
        }
        [UnityTest]
        public IEnumerator DeadSurvivorCannotExtractAndReloadHasSingleMarker()
        {
            ObserveOutcomes(); carrier.GetComponent<Health>().ApplyDamage(10000); cargo.Condition.ApplyDamage(10000);
            yield return Move(mission.transform.position);
            Assert.That(mission.SurvivorLost, Is.True); Assert.That(successes, Is.Zero); Assert.That(escapes, Is.Zero);
            Assert.That(marker.MissingCargoPrompt, Is.False);
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity", new LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null; yield return null;
            Assert.That(Object.FindObjectsByType<ExtractionMarker>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<CargoExtraction>().IsComplete, Is.False);
            var item = Object.FindFirstObjectByType<CargoItem>(); Assert.That(item.Condition.Current, Is.EqualTo(item.Condition.Maximum));
            Assert.That(item.GetComponent<Renderer>().sharedMaterial, Is.SameAs(brown));
        }
        [UnityTest]
        public IEnumerator GunfireCrossesMarkerAndWolfCanNavigateThroughIt()
        {
            yield return Move(mission.transform.position + Vector3.back * 2.5f);
            var weapon = carrier.GetComponent<PlayerWeapon>();
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube); target.transform.position = mission.transform.position + Vector3.forward * 1.5f;
            target.transform.localScale = new Vector3(3, 2, .2f); var hp = target.AddComponent<Health>(); hp.ResetMaximum(1000); Physics.SyncTransforms();
            Vector3 point = new Vector3(target.transform.position.x, 0, target.transform.position.z);
            weapon.AimAtScreen(Camera.main.WorldToScreenPoint(point)); Assert.That(weapon.TryFire(), Is.True);
            Assert.That(hp.Current, Is.LessThan(1000), "Decorative geometry must not block real pellet rays."); Object.Destroy(target);
            var wolf = Object.FindFirstObjectByType<MutatedWolfAI>(); var agent = wolf.GetComponent<NavMeshAgent>();
            Vector3 goal = marker.transform.position;
            Assert.That(NavMesh.SamplePosition(goal, out var destination, 1, NavMesh.AllAreas), Is.True);
            Assert.That(agent.Warp(goal + Vector3.back * 2), Is.True);
            var settings = new SerializedObject(wolf); settings.FindProperty("detectionRange").floatValue = 0; settings.ApplyModifiedPropertiesWithoutUndo();
            wolf.enabled = true; wolf.HearNoise(new GameplayNoise(destination.position, 30));
            float initial = Vector3.Distance(wolf.transform.position, destination.position);
            float deadline = Time.time + 2;
            while (Vector3.Distance(wolf.transform.position, destination.position) > .4f && Time.time < deadline) yield return null;
            Assert.That(Vector3.Distance(wolf.transform.position, destination.position), Is.LessThan(initial - .8f));
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
        }
        private static IEnumerator Capture(string name)
        {
            var camera = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray(); var views = canvases.Select(c => c.worldCamera).ToArray();
            var distances = canvases.Select(c => c.planeDistance).ToArray();
            var target = new RenderTexture(1280, 800, 24); camera.targetTexture = target;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory("ValidationResults/Extraction");
            System.IO.File.WriteAllBytes("ValidationResults/Extraction/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(image); Object.Destroy(target);
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = views[i]; canvases[i].planeDistance = distances[i]; }
        }
    }
    internal static class ExtractionEventTestExtension
    {
        public static UnityEvent AsEvent(this object value) => (UnityEvent)value;
    }
}
#endif
