#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using CargoSquad.AI;
using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Missions;
using CargoSquad.Player;
using CargoSquad.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CargoSquad.Tests
{
    public sealed class WolfImpactTests
    {
        private CharacterSelection selection;
        private PlayerController player;
        private Health health;
        private CargoInteractor carrier;
        private CargoItem cargo;
        private CargoExtraction mission;
        private MutatedWolfAI wolf;
        private Keyboard keyboard;
        private Mouse mouse;
        private CargoMissionOverlay hud;

        [UnitySetUp]
        public IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null;
            yield return null;
            selection = Object.FindFirstObjectByType<CharacterSelection>();
            player = Object.FindFirstObjectByType<PlayerController>();
            health = player.GetComponent<Health>();
            carrier = player.GetComponent<CargoInteractor>();
            cargo = Object.FindFirstObjectByType<CargoItem>();
            mission = Object.FindFirstObjectByType<CargoExtraction>();
            wolf = Object.FindFirstObjectByType<MutatedWolfAI>();
            hud = Object.FindFirstObjectByType<CargoMissionOverlay>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(4, 4) });
            Assert.That(wolf, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            Time.timeScale = 1f;
            var ground = GameObject.Find("Ground");
            if (ground) ground.GetComponent<BoxCollider>().enabled = true;
            yield return null;
        }

        private TMP_Text Label(string name) => hud.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == name);

        private IEnumerator Deploy(int character, bool carrying)
        {
            selection.SelectCharacter(character);
            selection.StartSelectedCharacter();
            yield return null;
            Assert.That(health.Maximum, Is.EqualTo(character == 0 ? 130 : 95));
            Assert.That(health.Current, Is.EqualTo(health.Maximum));
            Assert.That(Label("Health").text, Is.EqualTo($"HEALTH   {health.Current:0} / {health.Maximum:0} HP"));
            if (!carrying) yield break;
            player.GetComponent<Rigidbody>().position = cargo.transform.position + new Vector3(0, .25f, -1.9f);
            yield return new WaitForSeconds(.12f);
            yield return Tap();
            Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
        }

        private IEnumerator Tap()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private void ArrangeBite()
        {
            Assert.That(wolf.GetComponent<NavMeshAgent>().Warp(new Vector3(7, 0, -7)), Is.True);
            player.GetComponent<Rigidbody>().position = new Vector3(5.7f, 1, -7);
            Physics.SyncTransforms();
        }

        private IEnumerator OneBite()
        {
            float initial = health.Current;
            float deadline = Time.time + 3;
            while (health.Current == initial && Time.time < deadline) yield return null;
            Assert.That(health.Current, Is.LessThan(initial), "The actual animation-timed bite must land.");
            wolf.enabled = false;
        }

        private IEnumerator Matrix(int character, bool carrying)
        {
            yield return Deploy(character, carrying);
            float hp = health.Current, condition = cargo.Condition.Current;
            int healthChanges = 0, conditionChanges = 0;
            float gripLoss = 0, priorGrip = carrier.Grip.Percent;
            string immediateHealth = null;
            health.Changed += (current, max) => { healthChanges++; immediateHealth = Label("Health").text; };
            cargo.Condition.Changed += (current, max) => conditionChanges++;
            carrier.Grip.Changed += value =>
            {
                if (value < priorGrip) gripLoss += priorGrip - value;
                priorGrip = value;
            };
            ArrangeBite();
            yield return OneBite();
            Assert.That(health.Current, Is.EqualTo(hp - wolf.BiteDamage));
            Assert.That(healthChanges, Is.EqualTo(1));
            Assert.That(immediateHealth, Is.EqualTo($"HEALTH   {hp - wolf.BiteDamage:0} / {hp:0} HP"));
            Assert.That(cargo.Condition.Current, Is.EqualTo(condition - (carrying ? 10 : 0)));
            Assert.That(conditionChanges, Is.EqualTo(carrying ? 1 : 0));
            Assert.That(gripLoss, Is.EqualTo(carrying ? 25 : 0).Within(.001f));
            Assert.That(carrier.CarriedCargo, carrying ? Is.SameAs(cargo) : Is.Null);
            if (carrying)
            {
                Assert.That(Label("Condition").text, Does.Contain("90%"));
                Assert.That(Label("Grip").text, Does.Contain("75%"));
                Assert.That(Label("Condition").color.r, Is.GreaterThan(.95f));
                Assert.That(Label("Grip").color.g, Is.InRange(.5f, .7f));
                if (character == 0) Capture("bite-health-cargo-grip");
            }
        }

        [UnityTest] public IEnumerator HeavyWithoutCargo() { yield return Matrix(0, false); }
        [UnityTest] public IEnumerator MedicWithoutCargo() { yield return Matrix(1, false); }
        [UnityTest] public IEnumerator HeavyCarryingCargo() { yield return Matrix(0, true); }
        [UnityTest] public IEnumerator MedicCarryingCargo() { yield return Matrix(1, true); }

        private IEnumerator BiteSlip(bool blocked, bool destroysOnDrop, bool fatalSurvivor = false)
        {
            yield return Deploy(0, true);
            if (fatalSurvivor) health.ApplyDamage(health.Current - wolf.BiteDamage);
            if (destroysOnDrop) cargo.Condition.ApplyDamage(cargo.Condition.Current - 30f);
            float before = cargo.Condition.Current;
            int drops = 0, noises = 0, conditionChanges = 0;
            carrier.CargoDropped += item => drops++;
            carrier.NoiseEmitted += noise => noises++;
            cargo.Condition.Changed += (current, maximum) => conditionChanges++;
            carrier.Grip.ApplyLoss(99.9f);
            ArrangeBite();
            var ground = GameObject.Find("Ground").GetComponent<BoxCollider>();
            if (blocked)
            {
                player.GetComponent<Rigidbody>().useGravity = false;
                ground.enabled = false;
            }
            yield return OneBite();
            Assert.That(carrier.Grip.Percent, Is.Zero);
            if (blocked)
            {
                Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
                Assert.That(carrier.HasPendingSlip, Is.True);
                Assert.That(cargo.Condition.Current, Is.EqualTo(before - 10));
                Assert.That(drops + noises, Is.Zero);
                yield return new WaitForSeconds(.1f);
                Assert.That(conditionChanges, Is.EqualTo(1), "Blocked placement must not repeat damage.");
                ground.enabled = true;
                player.GetComponent<Rigidbody>().useGravity = true;
                yield return null;
                yield return null;
            }
            Assert.That(carrier.CarriedCargo, Is.Null);
            Assert.That(cargo.IsCarried, Is.False);
            Assert.That(drops, Is.EqualTo(1));
            Assert.That(noises, Is.EqualTo(1));
            Assert.That(conditionChanges, Is.EqualTo(2));
            Assert.That(cargo.Condition.Current, Is.EqualTo(Mathf.Max(0, before - 10 - cargo.Definition.SlipDamage)));
            yield return new WaitForSeconds(.1f);
            Assert.That(drops + noises, Is.EqualTo(2), "Automatic slip must resolve exactly once.");
            if (destroysOnDrop)
            {
                Assert.That(cargo.IsDestroyed && mission.CargoDestroyed, Is.True);
                Assert.That(cargo.gameObject.activeSelf, Is.False);
            }
            else
            {
                Assert.That(cargo.Collider.enabled, Is.True);
                var overlaps = Physics.OverlapBox(cargo.Collider.bounds.center, cargo.Size * .49f,
                    cargo.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
                Assert.That(overlaps.Where(collider => collider != cargo.Collider), Is.Empty);
                if (!fatalSurvivor) Assert.That(Label("Warning").text, Is.EqualTo("CARGO DROPPED"));
            }
            if (fatalSurvivor) Assert.That(mission.SurvivorLost && mission.RecoveryFailed, Is.True);
        }

        [UnityTest] public IEnumerator BiteDropsOnceWithNormalConsequences() { yield return BiteSlip(false, false); }
        [UnityTest] public IEnumerator BlockedDropRetriesWithoutDuplicateDamage() { yield return BiteSlip(true, false); }
        [UnityTest] public IEnumerator DropCanDestroyCargo() { yield return BiteSlip(false, true); }
        [UnityTest] public IEnumerator FatalBiteStillDropsSurvivingCargoOnce() { yield return BiteSlip(false, false, true); }

        [UnityTest]
        public IEnumerator DirectCargoDestructionAllowsEscapeOnly()
        {
            yield return Deploy(1, true);
            cargo.Condition.ApplyDamage(cargo.Condition.Current - 5);
            int drops = 0, noises = 0, successes = 0, escapes = 0;
            carrier.CargoDropped += item => drops++;
            carrier.NoiseEmitted += noise => noises++;
            MissionEvent("missionSucceeded").AddListener(() => successes++);
            MissionEvent("survivorEscaped").AddListener(() => escapes++);
            ArrangeBite();
            yield return OneBite();
            Assert.That(cargo.Condition.Current, Is.Zero);
            Assert.That(carrier.CarriedCargo, Is.Null);
            Assert.That(drops + noises, Is.Zero, "Direct destruction must not also slip an already destroyed item.");
            Assert.That(health.IsDead, Is.False);
            Assert.That(carrier.TryDeliver(cargo), Is.False);
            yield return null;
            Assert.That(Label("Objective").text, Is.EqualTo("ESCAPE ALIVE"));
            player.GetComponent<Rigidbody>().position = mission.transform.position;
            yield return new WaitForSeconds(.12f);
            Assert.That(mission.IsComplete && mission.RecoveryFailed && !mission.SurvivorLost, Is.True);
            Assert.That(escapes, Is.EqualTo(1));
            Assert.That(successes, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FatalBiteAndCargoDestructionHaveFailurePriorityAndRestartCleanly()
        {
            yield return Deploy(0, true);
            health.ApplyDamage(health.Current - wolf.BiteDamage);
            cargo.Condition.ApplyDamage(cargo.Condition.Current - 5);
            carrier.Grip.ApplyLoss(99.9f);
            int died = 0, successes = 0, escapes = 0;
            health.Died += () => died++;
            MissionEvent("missionSucceeded").AddListener(() => successes++);
            MissionEvent("survivorEscaped").AddListener(() => escapes++);
            ArrangeBite();
            yield return OneBite();
            Assert.That(health.Current, Is.Zero);
            Assert.That(cargo.Condition.Current, Is.Zero);
            Assert.That(carrier.CarriedCargo, Is.Null);
            Assert.That(mission.IsComplete && mission.RecoveryFailed && mission.SurvivorLost, Is.True);
            Assert.That(died, Is.EqualTo(1));
            health.ApplyDamage(100f);
            Assert.That(died, Is.EqualTo(1));
            Assert.That(player.GetComponent<CharacterAbility>().TryActivate(), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return new WaitForSeconds(.1f);
            Assert.That(player.GetComponent<Rigidbody>().linearVelocity.x, Is.Zero.Within(.01f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            player.GetComponent<Rigidbody>().position = mission.transform.position;
            yield return new WaitForSeconds(.12f);
            Assert.That(successes + escapes, Is.Zero);
            Assert.That(Label("Health").text, Is.EqualTo("HEALTH   0 / 130 HP"));
            Assert.That(Label("Objective").text, Is.EqualTo("MISSION FAILED"));
            Capture("survivor-death");

            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null;
            yield return null;
            var fresh = Object.FindFirstObjectByType<CharacterSelection>();
            fresh.SelectCharacter(1);
            fresh.StartSelectedCharacter();
            var freshPlayer = Object.FindFirstObjectByType<PlayerController>();
            var freshHealth = freshPlayer.GetComponent<Health>();
            Assert.That(freshHealth.Maximum, Is.EqualTo(95));
            Assert.That(freshHealth.Current, Is.EqualTo(95));
            Assert.That(freshPlayer.GetComponent<CargoGrip>().Percent, Is.EqualTo(100));
            Assert.That(freshPlayer.GetComponent<CargoInteractor>().CarriedCargo, Is.Null);
            Assert.That(Object.FindFirstObjectByType<CargoExtraction>().SurvivorLost, Is.False);
        }

        [UnityTest]
        public IEnumerator HealthColorsAndDeathBlockPickupAndExtraction()
        {
            yield return Deploy(0, false);
            wolf.enabled = false;
            var fill = hud.GetComponentsInChildren<Image>().Single(image => image.name == "HealthFill");
            health.ApplyDamage(health.Current - health.Maximum * .60f);
            Assert.That(fill.color.r, Is.GreaterThan(.95f));
            yield return new WaitForSecondsRealtime(.23f);
            Assert.That(fill.color.g, Is.InRange(.65f, .8f));
            health.ApplyDamage(health.Current - health.Maximum * .30f);
            yield return new WaitForSecondsRealtime(.23f);
            Assert.That(fill.color.g, Is.LessThan(.4f));
            Assert.That(fill.rectTransform.anchorMax.x, Is.EqualTo(.3f).Within(.01f));
            health.ApplyDamage(1000);
            player.GetComponent<Rigidbody>().position = cargo.transform.position + new Vector3(0, .25f, -1.9f);
            yield return new WaitForSeconds(.12f);
            yield return Tap();
            Assert.That(carrier.CarriedCargo, Is.Null);
            player.GetComponent<Rigidbody>().position = mission.transform.position;
            yield return new WaitForSeconds(.12f);
            Assert.That(mission.SurvivorLost, Is.True);
        }

        [UnityTest]
        public IEnumerator FatalDamageWinsOverDeliveryQueuedInSameFrame()
        {
            yield return Deploy(0, true);
            wolf.enabled = false;
            int successes = 0;
            MissionEvent("missionSucceeded").AddListener(() => successes++);
            mission.SendMessage("OnTriggerStay", player.GetComponent<CapsuleCollider>());
            health.ApplyDamage(1000);
            yield return null;
            Assert.That(successes, Is.Zero);
            Assert.That(mission.SurvivorLost, Is.True);
            Assert.That(cargo.IsDelivered, Is.False);
        }

        private UnityEvent MissionEvent(string name) => (UnityEvent)typeof(CargoExtraction)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mission);

        private void Capture(string name)
        {
            var camera = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.renderMode != RenderMode.WorldSpace).ToArray();
            var modes = canvases.Select(canvas => canvas.renderMode).ToArray();
            var cameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
            var distances = canvases.Select(canvas => canvas.planeDistance).ToArray();
            var target = new RenderTexture(1280, 800, 24);
            camera.targetTexture = target;
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var old = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            image.Apply();
            System.IO.Directory.CreateDirectory("ValidationResults/WolfV2");
            System.IO.File.WriteAllBytes("ValidationResults/WolfV2/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = old;
            camera.targetTexture = null;
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = modes[i];
                canvases[i].worldCamera = cameras[i];
                canvases[i].planeDistance = distances[i];
            }
            Object.Destroy(image);
            Object.Destroy(target);
        }
    }
}
#endif
