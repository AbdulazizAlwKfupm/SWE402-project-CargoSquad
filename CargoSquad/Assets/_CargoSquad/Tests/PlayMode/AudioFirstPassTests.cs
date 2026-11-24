#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using CargoSquad.AI;
using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CargoSquad.Tests
{
    public sealed class AudioFirstPassTests
    {
        private CargoInteractor carrier;
        private PlayerAudio audio;
        private PlayerWeapon weapon;
        private Rigidbody body;
        private CharacterSelection selection;
        private Keyboard keyboard;
        private Mouse mouse;
        private GameObject floor;
        private int noises;

        private static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        [UnitySetUp]
        public IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity", new LoadSceneParameters());
            yield return null;
            MissionResultTestSupport.KeepSimulationRunning();
            foreach (var wolf in Object.FindObjectsByType<MutatedWolfAI>(FindObjectsSortMode.None)) wolf.enabled = false;
            selection = Object.FindFirstObjectByType<CharacterSelection>();
            selection.StartSelectedCharacter();
            carrier = Object.FindFirstObjectByType<CargoInteractor>();
            body = carrier.GetComponent<Rigidbody>();
            weapon = carrier.GetComponent<PlayerWeapon>();
            audio = carrier.GetComponent<PlayerAudio>();
            Assert.That(audio, Is.Not.Null);
            carrier.NoiseEmitted += _ => noises++;
            noises = 0;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * .5f, Screen.height * .4f) });
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (floor) Object.Destroy(floor);
            yield return null;
        }

        private IEnumerator TapCargo()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        [UnityTest]
        public IEnumerator AllWeaponDefinitionsPlayDistinctFireReloadAndDryFireWithoutExtraAINoise()
        {
            var definitions = new[] { "HeavyShotgun", "MedicStun", "ScoutPistol", "NavyRifle" }
                .Select(n => AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_CargoSquad/Data/Weapons/" + n + ".asset")).ToArray();
            Assert.That(definitions.Select(d => d.fireClip).Distinct().Count(), Is.EqualTo(4));
            var source = Field<AudioSource>(weapon, "audioSource");
            foreach (var definition in definitions)
            {
                var reloadClips = definition.type == WeaponType.Shotgun
                    ? new[] { definition.shellInsertClip, definition.reloadFinishClip }
                    : new[] { definition.reloadClip };
                Assert.That(definition.fireClip && definition.emptyClip, Is.True);
                Assert.That(reloadClips.All(c => c), Is.True);
                Assert.That(definition.volume, Is.LessThanOrEqualTo(.3f));
                foreach (var clip in new[] { definition.fireClip, definition.emptyClip }.Concat(reloadClips))
                {
                    var samples = new float[clip.samples * clip.channels];
                    Assert.That(clip.GetData(samples, 0), Is.True);
                    Assert.That(samples.Max(v => Mathf.Abs(v)), Is.InRange(.01f, .37f));
                }
                var settings = new SerializedObject(weapon);
                settings.FindProperty("heavyWeapon").objectReferenceValue = definition;
                settings.ApplyModifiedPropertiesWithoutUndo();
                weapon.Equip(SurvivorKind.Heavy, selection.ActiveVisual);
                yield return new WaitForSeconds(.12f);
                int before = noises;
                Assert.That(weapon.TryFire(), Is.True, definition.name);
                Assert.That(source.isPlaying, Is.True, "Fire playback started");
                Assert.That(noises, Is.EqualTo(before + 1), "Only successful shots emit AI noise");
                yield return AssertOutput(source);
                source.Stop();
                Assert.That(weapon.TryReload(), Is.True);
                if (definition.type == WeaponType.Shotgun)
                {
                    yield return new WaitForSeconds(definition.shellInsertInterval + .05f);
                    Assert.That(Field<AudioSource>(weapon, "shellReloadSource").isPlaying, Is.True, "Shell insertion playback started");
                }
                else Assert.That(source.isPlaying, Is.True, "Reload playback started");
                Assert.That(noises, Is.EqualTo(before + 1));
                // Reset only the runtime weapon instance, then exhaust through the real fire path.
                weapon.Equip(SurvivorKind.Heavy, selection.ActiveVisual);
                yield return new WaitForSeconds(.12f);
                for (int i = 0; i < definition.magazineCapacity; i++)
                {
                    Assert.That(weapon.TryFire(), Is.True);
                    yield return new WaitForSeconds(1f / definition.shotsPerSecond + .02f);
                }
                source.Stop(); before = noises;
                Assert.That(weapon.TryFire(), Is.False);
                Assert.That(source.isPlaying, Is.True, "Dry-fire playback started");
                Assert.That(noises, Is.EqualTo(before), "Dry fire stays inaudible to AI");
            }
        }

        private static IEnumerator AssertOutput(AudioSource source)
        {
            var samples = new float[256];
            bool nonzero = false;
            for (int i = 0; i < 12; i++)
            {
                source.GetOutputData(samples, 0);
                if (samples.Any(v => Mathf.Abs(v) > .00001f)) { nonzero = true; break; }
                yield return new WaitForSecondsRealtime(.01f);
            }
            Assert.That(nonzero, Is.True, "Unity DSP produced nonzero source output (run with -forceAudio)");
        }

        [UnityTest]
        public IEnumerator FootstepsRequireResolvedGroundedMovementAndDoNotEmitAINoise()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(1000, -.25f, 1000);
            floor.transform.localScale = new Vector3(50, .5f, 50);
            body.position = new Vector3(1000, 1f, 1000); body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            var source = Field<AudioSource>(audio, "steps");
            yield return new WaitForSeconds(.3f);
            Assert.That(source.isPlaying, Is.False, "Stationary");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            bool played = false;
            Vector3 start = body.position;
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate();
                if (source.isPlaying) { played = true; break; }
            }
            Assert.That(body.position.x, Is.GreaterThan(start.x + 1f));
            Assert.That(played, Is.True, "Grounded travel started footsteps");
            yield return AssertOutput(source);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(.15f);
            Assert.That(source.isPlaying, Is.False, "Stopped");

            // Velocity/input without resolved movement must stay silent against an obstruction.
            body.constraints |= RigidbodyConstraints.FreezePositionX;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            for (int i = 0; i < 30; i++) { yield return new WaitForFixedUpdate(); Assert.That(source.isPlaying, Is.False, "Blocked"); }
            body.constraints &= ~RigidbodyConstraints.FreezePositionX;
            body.useGravity = false; body.position += Vector3.up * 4f; body.linearVelocity = Vector3.zero;
            for (int i = 0; i < 30; i++) { yield return new WaitForFixedUpdate(); Assert.That(source.isPlaying, Is.False, "Airborne"); }
            Assert.That(noises, Is.Zero, "Footsteps do not change AI hearing");
        }

        [UnityTest]
        public IEnumerator CargoPickupDropBlockedSlipAndDeliveryChooseOneCueAndPreserveNoise()
        {
            var item = Object.FindFirstObjectByType<CargoItem>();
            var source = Field<AudioSource>(audio, "cargo");
            var config = Resources.Load<PlayerAudioSettings>("CargoSquadPlayerAudio");
            body.position = item.transform.position + new Vector3(0, .25f, -1.9f);
            yield return new WaitForSeconds(.12f);
            yield return TapCargo();
            Assert.That(carrier.CarriedCargo, Is.SameAs(item));
            Assert.That(source.clip, Is.SameAs(config.pickup)); Assert.That(source.isPlaying, Is.True);
            yield return AssertOutput(source);
            body.position = new Vector3(0, 1, -4);
            yield return new WaitForSeconds(.12f);
            yield return TapCargo();
            Assert.That(carrier.CarriedCargo, Is.Null);
            Assert.That(source.clip, Is.SameAs(config.drop)); Assert.That(source.isPlaying, Is.True);
            Assert.That(noises, Is.Zero);
            yield return TapCargo();
            Assert.That(carrier.CarriedCargo, Is.SameAs(item));
            source.Stop();
            var ground = GameObject.Find("Ground").GetComponent<BoxCollider>();
            ground.enabled = false;
            carrier.Grip.Tick(true, true, 1f, 100f);
            yield return null; yield return null;
            Assert.That(carrier.HasPendingSlip, Is.True);
            Assert.That(source.isPlaying, Is.False, "Blocked slip has not landed");
            Assert.That(noises, Is.Zero);
            ground.enabled = true;
            yield return null; yield return null;
            Assert.That(carrier.CarriedCargo, Is.Null);
            Assert.That(source.clip, Is.SameAs(config.slip), "Slip replaces the normal drop cue");
            Assert.That(source.isPlaying, Is.True);
            Assert.That(carrier.GetComponents<AudioSource>().Count(s => s.isPlaying), Is.EqualTo(1), "One cargo voice");
            Assert.That(noises, Is.EqualTo(1), "Exactly the original slip AI noise event");
            yield return TapCargo();
            Assert.That(carrier.CarriedCargo, Is.SameAs(item));
            source.Stop();
            Assert.That(carrier.TryDeliver(item), Is.True);
            Assert.That(source.isPlaying, Is.False, "Delivery is not a second drop");
            Assert.That(noises, Is.EqualTo(1));
        }
    }
}
#endif
