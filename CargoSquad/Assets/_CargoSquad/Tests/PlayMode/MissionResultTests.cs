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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CargoSquad.Tests
{
    public sealed class MissionResultTests
    {
        private CargoExtraction mission;
        private MissionResultPanel panel;
        private CargoInteractor carrier;
        private CargoItem cargo;
        private Rigidbody body;
        private CharacterSelection selection;
        private Keyboard keyboard;
        private Mouse mouse;
        private int finalized;
        [UnitySetUp]
        public IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity", new LoadSceneParameters());
            yield return null; yield return null;
            mission = Object.FindFirstObjectByType<CargoExtraction>(); panel = Object.FindFirstObjectByType<MissionResultPanel>();
            carrier = GameObject.Find("PlayerPlaceholder").GetComponent<CargoInteractor>(); body = carrier.GetComponent<Rigidbody>();
            cargo = Object.FindFirstObjectByType<CargoItem>(); selection = Object.FindFirstObjectByType<CharacterSelection>();
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(4, 4) });
            finalized = 0; mission.ResultFinalized += _ => finalized++;
            Assert.That(panel, Is.Not.Null); Assert.That(panel.Visible, Is.False);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            yield return null;
        }
        private IEnumerator Deploy(int index, bool carrying)
        {
            selection.SelectCharacter(index); selection.StartSelectedCharacter();
            foreach (var wolf in Object.FindObjectsByType<MutatedWolfAI>(FindObjectsSortMode.None)) wolf.enabled = false;
            yield return null; yield return null;
            if (!carrying) yield break;
            body.position = cargo.transform.position + new Vector3(0, .775f, -1.9f); Physics.SyncTransforms();
            yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
        }
        private IEnumerator EnterZone()
        {
            body.position = mission.transform.position; body.linearVelocity = Vector3.zero; Physics.SyncTransforms();
            float deadline = Time.realtimeSinceStartup + 2;
            while (!panel.Visible && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(panel.Visible, Is.True); Assert.That(Time.timeScale, Is.Zero);
            yield return new WaitForSecondsRealtime(.3f); Assert.That(panel.Opacity, Is.EqualTo(1));
        }
        private IEnumerator CarrySuccess(int index)
        {
            yield return Deploy(index, true);
            var marker = Object.FindFirstObjectByType<ExtractionMarker>(); var zone = mission.GetComponent<BoxCollider>();
            Assert.That(zone.bounds.size, Is.EqualTo(new Vector3(3.25f, 2, 3.25f)));
            Assert.That(marker.Footprint, Is.EqualTo(new Vector2(3.25f, 3.25f)));
            Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty);
            var blockers = Physics.OverlapBox(zone.bounds.center, new Vector3(1.625f, .95f, 1.625f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => !c.attachedRigidbody && !c.GetComponentInParent<CargoItem>()).ToArray();
            Assert.That(blockers, Is.Empty);
            Vector3 cargoOffset = cargo.transform.TransformPoint(cargo.Collider.center) - body.transform.position;
            Assert.That(Mathf.Abs(cargoOffset.x) + cargo.Size.x / 2, Is.LessThan(1.625f));
            Assert.That(Mathf.Abs(cargoOffset.z) + cargo.Size.z / 2, Is.LessThan(1.625f));
            body.position = mission.transform.position + Vector3.left * 3.2f; Physics.SyncTransforms();
            yield return new WaitForSeconds(.35f); Assert.That(panel.Visible, Is.False);
            if (index == 0) yield return Capture("01-wide-extraction-heavy");
            cargo.Condition.ApplyDamage(10); carrier.GetComponent<Health>().ApplyDamage(15);
            yield return EnterZone();
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.CargoRecovered));
            Assert.That(mission.Result.Survivor, Is.EqualTo(index == 0 ? "Heavy" : "Medic"));
            Assert.That(mission.Result.CargoCondition, Is.EqualTo(90));
            Assert.That(mission.Result.Health, Is.EqualTo(index == 0 ? 115 : 80));
            Assert.That(panel.Heading, Is.EqualTo("MISSION COMPLETE")); Assert.That(panel.Details, Does.Contain("RECOVERED"));
            Assert.That(cargo.IsDelivered, Is.True); Assert.That(finalized, Is.EqualTo(1));
            Assert.That(carrier.enabled, Is.False); Assert.That(carrier.GetComponent<PlayerWeapon>().TryFire(), Is.False);
            Assert.That(carrier.GetComponent<CharacterAbility>().TryActivate(), Is.False);
            if (index == 0) yield return Capture("02-mission-victory");
            // Frozen report is a snapshot, even if an external caller changes the underlying components.
            carrier.GetComponent<Health>().ApplyDamage(999); cargo.Condition.ApplyDamage(999); yield return null;
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.CargoRecovered));
            Assert.That(mission.Result.CargoCondition, Is.EqualTo(90)); Assert.That(finalized, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator HeavyFitsAndVictorySnapshotIsStable() { yield return CarrySuccess(0); }
        [UnityTest] public IEnumerator MedicFitsAndExtracts() { yield return CarrySuccess(1); }
        [UnityTest]
        public IEnumerator DeathShowsFailureImmediatelyAndCargoConsequencesAreCaptured()
        {
            yield return Deploy(1, true);
            var held = carrier.CarriedCargo;
            carrier.GetComponent<Health>().ApplyDamage(999);
            carrier.ApplyCarriedImpact(held, 999, 25); // Same ordering as the actual bite transaction.
            yield return null; yield return new WaitForSecondsRealtime(.3f);
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.SurvivorKilled));
            Assert.That(panel.Heading, Is.EqualTo("MISSION FAILED")); Assert.That(panel.Details, Does.Contain("Survivor killed"));
            Assert.That(mission.Result.CargoCondition, Is.Zero); Assert.That(mission.Result.Health, Is.Zero);
            Assert.That(mission.Result.CargoOutcome, Does.Contain("DESTROYED")); Assert.That(finalized, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.Zero); Assert.That(carrier.GetComponent<PlayerController>().enabled, Is.False);
            Assert.That(carrier.GetComponent<PlayerWeapon>().ConeVisible, Is.False);
            yield return Capture("03-mission-defeat");
        }
        [UnityTest]
        public IEnumerator CargoDestructionRequiresEscapeAndNeverAwardsRecovery()
        {
            yield return Deploy(0, false); cargo.Condition.ApplyDamage(999); yield return null;
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.None)); Assert.That(panel.Visible, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            yield return EnterZone();
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.EscapedCargoLost));
            Assert.That(cargo.IsDelivered, Is.False); Assert.That(panel.Heading, Is.EqualTo("ESCAPED — CARGO LOST"));
            Assert.That(mission.RecoveryFailed, Is.True); Assert.That(finalized, Is.EqualTo(1));
            yield return Capture("04-escaped-cargo-lost"); yield return Capture("05-escape-640x360", 640, 360);
        }
        [UnityTest]
        public IEnumerator MissingCargoKeepsPlayingAndSameFrameDeathBeatsDelivery()
        {
            yield return Deploy(0, false); body.position = mission.transform.position; Physics.SyncTransforms();
            yield return new WaitForSeconds(.15f); Assert.That(panel.Visible, Is.False); Assert.That(mission.IsComplete, Is.False);
            body.position = cargo.transform.position + new Vector3(0, .775f, -1.9f); Physics.SyncTransforms(); yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
            // Queue valid delivery in the trigger, then kill before the late terminal decision.
            mission.SendMessage("OnTriggerStay", carrier.GetComponent<CapsuleCollider>());
            carrier.GetComponent<Health>().ApplyDamage(999); yield return null;
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.SurvivorKilled));
            Assert.That(cargo.IsDelivered, Is.False); Assert.That(finalized, Is.EqualTo(1));
        }
        private IEnumerator RestartViaPointer(MissionOutcome outcome)
        {
            yield return Deploy(0, outcome == MissionOutcome.CargoRecovered);
            if (outcome == MissionOutcome.SurvivorKilled) { carrier.GetComponent<Health>().ApplyDamage(999); yield return null; yield return new WaitForSecondsRealtime(.3f); }
            else { if (outcome == MissionOutcome.EscapedCargoLost) cargo.Condition.ApplyDamage(999); yield return EnterZone(); }
            Assert.That(panel.Visible, Is.True);
            Canvas.ForceUpdateCanvases(); Vector2 pos = RectTransformUtility.WorldToScreenPoint(null, panel.RestartButton.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos, buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos });
            yield return null; yield return null; yield return null;
            var next = Object.FindFirstObjectByType<CharacterSelection>();
            Assert.That(next.HasStarted, Is.False); Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Object.FindFirstObjectByType<MissionResultPanel>().Visible, Is.False);
            Assert.That(Object.FindObjectsByType<CargoExtraction>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<CargoExtraction>().Result.Outcome, Is.EqualTo(MissionOutcome.None));
            Assert.That(Object.FindFirstObjectByType<CargoItem>().Condition.Current, Is.EqualTo(100));
            next.StartSelectedCharacter(); yield return null; yield return null;
            var player = GameObject.Find("PlayerPlaceholder");
            Assert.That(player.GetComponent<Health>().Current, Is.EqualTo(130));
            Assert.That(player.GetComponent<PlayerWeapon>().Magazine, Is.EqualTo(5));
            Assert.That(player.GetComponent<CargoGrip>().Percent, Is.EqualTo(100));
            Assert.That(player.GetComponent<CargoInteractor>().CarriedCargo, Is.Null); Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Object.FindFirstObjectByType<MutatedWolfAI>().State, Is.Not.EqualTo(WolfState.Death));
        }
        [UnityTest] public IEnumerator RetryButtonRestartsFromDefeat() { yield return RestartViaPointer(MissionOutcome.SurvivorKilled); }
        [UnityTest] public IEnumerator RestartButtonRestartsFromEscape() { yield return RestartViaPointer(MissionOutcome.EscapedCargoLost); }
        [UnityTest] public IEnumerator RestartButtonRestartsFromVictory() { yield return RestartViaPointer(MissionOutcome.CargoRecovered); }
        private static IEnumerator Capture(string name, int width = 1280, int height = 800)
        {
            var camera = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled && c.isRootCanvas && c.renderMode != RenderMode.WorldSpace).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray(); var views = canvases.Select(c => c.worldCamera).ToArray();
            var distances = canvases.Select(c => c.planeDistance).ToArray();
            var target = new RenderTexture(width, height, 24); camera.targetTexture = target;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)) text.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory("ValidationResults/Extraction"); System.IO.File.WriteAllBytes("ValidationResults/Extraction/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; Object.Destroy(image); Object.Destroy(target);
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = views[i]; canvases[i].planeDistance = distances[i]; }
        }
    }
}
#endif
