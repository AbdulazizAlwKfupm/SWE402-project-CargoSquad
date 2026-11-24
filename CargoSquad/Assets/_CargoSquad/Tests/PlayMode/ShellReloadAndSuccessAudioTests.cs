#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CargoSquad.AI;
using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Missions;
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
    public sealed class ShellReloadAndSuccessAudioTests
    {
        private PlayerWeapon weapon;
        private CharacterSelection selection;
        private CargoInteractor carrier;
        private CargoExtraction mission;
        private MissionSuccessAudio success;
        private Keyboard keyboard;
        private Mouse mouse;

        private static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private void Ammo(int magazine, int reserve)
        {
            typeof(PlayerWeapon).GetField("<Magazine>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, magazine);
            typeof(PlayerWeapon).GetField("<Reserve>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(weapon, reserve);
        }

        [UnitySetUp]
        public IEnumerator Load()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity", new LoadSceneParameters());
            yield return null; yield return null;
            foreach (var wolf in Object.FindObjectsByType<MutatedWolfAI>(FindObjectsSortMode.None)) wolf.enabled = false;
            selection = Object.FindFirstObjectByType<CharacterSelection>(); selection.StartSelectedCharacter();
            weapon = Object.FindFirstObjectByType<PlayerWeapon>(); carrier = weapon.GetComponent<CargoInteractor>();
            mission = Object.FindFirstObjectByType<CargoExtraction>(); success = weapon.GetComponent<MissionSuccessAudio>();
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * .5f, Screen.height * .4f) });
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f; AudioListener.pause = false;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptyPartialFullLowReserveAndNoReserveMatchActualShellSoundCounts()
        {
            var insertion = weapon.Definition.shellInsertClip; var rack = weapon.Definition.reloadFinishClip;
            Assert.That(insertion && rack, Is.True);
            var source = Field<AudioSource>(weapon, "shellReloadSource");
            var cues = new List<AudioClip>();
            int expectedMagazine = 0, expectedReserve = 0, noises = 0;
            weapon.ReloadCuePlayed += clip =>
            {
                cues.Add(clip);
                if (clip == insertion) { expectedMagazine++; expectedReserve--; }
                Assert.That(weapon.Magazine, Is.EqualTo(expectedMagazine), "Ammo and sound commit in the same callback");
                Assert.That(weapon.Reserve, Is.EqualTo(expectedReserve));
                Assert.That(source.isPlaying, Is.True);
            };
            carrier.NoiseEmitted += _ => noises++;
            float emptyDuration = 0f, partialDuration = 0f;
            foreach (var test in new[] { (0, 20, 5), (2, 20, 3), (5, 20, 0), (0, 2, 2), (2, 0, 0), (4, 1, 1) })
            {
                weapon.Equip(SurvivorKind.Heavy, selection.ActiveVisual);
                Ammo(test.Item1, test.Item2); expectedMagazine = test.Item1; expectedReserve = test.Item2; cues.Clear();
                float started = Time.time;
                Assert.That(weapon.TryReload(), Is.EqualTo(test.Item3 > 0));
                Assert.That(weapon.TryReload(), Is.False, "Repeated R cannot overlap reloads");
                Assert.That(cues, Is.Empty, "No insertion before its scheduled ammo commit");
                float deadline = Time.realtimeSinceStartup + 8f;
                while (weapon.Reloading && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(weapon.Reloading, Is.False);
                Assert.That(cues.Count(c => c == insertion), Is.EqualTo(test.Item3));
                Assert.That(cues.Count(c => c == rack), Is.EqualTo(test.Item3 > 0 ? 1 : 0));
                Assert.That(weapon.Magazine, Is.EqualTo(test.Item1 + test.Item3));
                Assert.That(weapon.Reserve, Is.EqualTo(test.Item2 - test.Item3));
                if (test.Item1 == 0 && test.Item2 == 20) emptyDuration = Time.time - started;
                if (test.Item1 == 2 && test.Item2 == 20) partialDuration = Time.time - started;
            }
            Assert.That(emptyDuration, Is.GreaterThan(partialDuration + 1f));
            Assert.That(noises, Is.Zero, "Audible reloads never emit AI noise");
        }

        [UnityTest]
        public IEnumerator InterruptionKeepsOnlyCommittedShellsAndNeverFinishesOrResumes()
        {
            var source = Field<AudioSource>(weapon, "shellReloadSource");
            foreach (string interruption in new[] { "cancel", "disable", "focus", "pause", "equip", "fire", "mouse" })
            {
                weapon.enabled = true;
                weapon.SendMessage("OnApplicationFocus", true);
                weapon.Equip(SurvivorKind.Heavy, selection.ActiveVisual);
                Ammo(0, 5);
                var cues = new List<AudioClip>(); System.Action<AudioClip> record = clip => cues.Add(clip);
                weapon.ReloadCuePlayed += record;
                Assert.That(weapon.TryReload(), Is.True);
                float deadline = Time.realtimeSinceStartup + 2f;
                while (weapon.Magazine == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(weapon.Magazine, Is.EqualTo(1)); Assert.That(cues.Count, Is.EqualTo(1));
                if (interruption == "cancel") weapon.CancelReload();
                else if (interruption == "disable") weapon.enabled = false;
                else if (interruption == "focus") weapon.SendMessage("OnApplicationFocus", false);
                else if (interruption == "pause") Time.timeScale = 0f;
                else if (interruption == "equip") weapon.Equip(SurvivorKind.Heavy, selection.ActiveVisual);
                else if (interruption == "fire")
                {
                    string state = $"CanUse={weapon.CanUse}, waiting={Field<bool>(weapon, "waitForRelease")}, UI={weapon.GetComponent<CharacterAbility>().PointerOverUI()}, focus={Field<bool>(weapon, "hasFocus")}";
                    Assert.That(weapon.TryFire(), Is.True, "Loaded shell can interrupt reload and fire: " + state);
                }
                else
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = mouse.position.ReadValue(), buttons = 1 }); yield return null;
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = mouse.position.ReadValue() }); yield return null;
                    Assert.That(weapon.Magazine, Is.Zero, "Real press/release interrupts and fires the committed shell");
                }
                yield return null;
                Assert.That(weapon.Reloading, Is.False); Assert.That(source.isPlaying, Is.False);
                int magazine = weapon.Magazine, reserve = weapon.Reserve;
                Time.timeScale = 1f; weapon.enabled = true; weapon.SendMessage("OnApplicationFocus", true);
                yield return new WaitForSeconds(.8f);
                Assert.That(weapon.Magazine, Is.EqualTo(magazine)); Assert.That(weapon.Reserve, Is.EqualTo(reserve));
                Assert.That(cues.Count, Is.EqualTo(1), "No extra insertions or rack after interruption");
                weapon.ReloadCuePlayed -= record;
            }
        }

        [UnityTest]
        public IEnumerator DeathAfterOneShellKeepsThatShellAndStopsAllFurtherReloadAudio()
        {
            Ammo(0, 5); int cues = 0; weapon.ReloadCuePlayed += _ => cues++;
            Assert.That(weapon.TryReload(), Is.True);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (weapon.Magazine == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(weapon.Magazine, Is.EqualTo(1)); Assert.That(cues, Is.EqualTo(1));
            weapon.GetComponent<Health>().ApplyDamage(999);
            Assert.That(Field<AudioSource>(weapon, "shellReloadSource").isPlaying, Is.False);
            yield return new WaitForSecondsRealtime(.9f);
            Assert.That(weapon.Magazine, Is.EqualTo(1)); Assert.That(weapon.Reserve, Is.EqualTo(4));
            Assert.That(cues, Is.EqualTo(1)); Assert.That(weapon.Reloading, Is.False);
        }

        [UnityTest]
        public IEnumerator DeathBeforeFirstInsertionAndSceneUnloadCancelAllPendingAmmoAndAudio()
        {
            Ammo(0, 5); int cues = 0; weapon.ReloadCuePlayed += _ => cues++;
            Assert.That(weapon.TryReload(), Is.True);
            weapon.GetComponent<Health>().ApplyDamage(999);
            Assert.That(weapon.Reloading, Is.False);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(weapon.Magazine, Is.Zero); Assert.That(weapon.Reserve, Is.EqualTo(5)); Assert.That(cues, Is.Zero);
            // Reset runtime health only, then unload the owning scene during another pending insertion.
            Time.timeScale = 1f; weapon.GetComponent<Health>().ResetMaximum(100);
            weapon.GetComponent<PlayerController>().enabled = true; weapon.enabled = true;
            // Mission completion correctly blocks reload; a fresh scene is needed for unload coverage.
            yield return Load();
            Ammo(0, 5); int unloadedCues = 0; weapon.ReloadCuePlayed += _ => unloadedCues++;
            Assert.That(weapon.TryReload(), Is.True);
            var oldWeapon = weapon; var oldSource = Field<AudioSource>(weapon, "shellReloadSource");
            var oldScene = weapon.gameObject.scene; var blank = SceneManager.CreateScene("Audio unload regression");
            SceneManager.SetActiveScene(blank); yield return SceneManager.UnloadSceneAsync(oldScene);
            yield return new WaitForSecondsRealtime(.8f);
            Assert.That(oldWeapon == null && oldSource == null, Is.True); Assert.That(unloadedCues, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OtherWeaponsKeepSingleTimedMagazineReload()
        {
            foreach (string name in new[] { "MedicStun", "ScoutPistol", "NavyRifle" })
            {
                var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_CargoSquad/Data/Weapons/" + name + ".asset");
                var serialized = new SerializedObject(weapon); serialized.FindProperty("heavyWeapon").objectReferenceValue = definition;
                serialized.ApplyModifiedPropertiesWithoutUndo(); weapon.Equip(SurvivorKind.Heavy, selection.ActiveVisual);
                Ammo(0, 2); var cues = new List<AudioClip>(); System.Action<AudioClip> record = c => cues.Add(c);
                weapon.ReloadCuePlayed += record;
                float started = Time.time; Assert.That(weapon.TryReload(), Is.True);
                yield return new WaitForSeconds(definition.reloadDuration * .5f);
                Assert.That(weapon.Magazine, Is.Zero, "No shell-style increments for other weapons");
                while (weapon.Reloading) yield return null;
                // Subtracting float timestamps can round a completed deadline a fraction of a microsecond low.
                Assert.That(Time.time - started, Is.GreaterThanOrEqualTo(definition.reloadDuration - .001f));
                Assert.That(weapon.Magazine, Is.EqualTo(2)); Assert.That(weapon.Reserve, Is.Zero);
                Assert.That(cues, Is.EqualTo(new[] { definition.reloadClip })); weapon.ReloadCuePlayed -= record;
            }
        }

        private IEnumerator Pickup()
        {
            var cargo = Object.FindFirstObjectByType<CargoItem>(); var body = carrier.GetComponent<Rigidbody>();
            body.position = cargo.transform.position + new Vector3(0, .775f, -1.9f); Physics.SyncTransforms();
            yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
        }

        [UnityTest]
        public IEnumerator ConfirmedDeliveryPlaysSuccessOnceDuringPausedResultPanel()
        {
            int plays = 0; success.Played += () => plays++;
            var source = Field<AudioSource>(success, "source");
            Assert.That(source.clip, Is.Null);
            carrier.GetComponent<Rigidbody>().position = mission.transform.position; Physics.SyncTransforms();
            yield return new WaitForSeconds(.15f);
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.None)); Assert.That(plays, Is.Zero);
            yield return Pickup();
            carrier.GetComponent<Rigidbody>().position = mission.transform.position; Physics.SyncTransforms();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!mission.IsComplete && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.CargoRecovered));
            Assert.That(Object.FindFirstObjectByType<MissionResultPanel>().Visible, Is.True);
            Assert.That(Time.timeScale, Is.Zero); Assert.That(plays, Is.EqualTo(1));
            Assert.That(source.isPlaying, Is.True); Assert.That(source.clip.length, Is.InRange(1f, 2f));
            AudioListener.pause = true;
            var samples = new float[256]; bool nonzero = false;
            for (int i = 0; i < 20; i++)
            {
                source.GetOutputData(samples, 0); if (samples.Any(v => Mathf.Abs(v) > .00001f)) { nonzero = true; break; }
                yield return new WaitForSecondsRealtime(.01f);
            }
            Assert.That(nonzero, Is.True, "Success produces DSP output under time/listener pause");
            float playhead = source.time; yield return new WaitForSecondsRealtime(.15f);
            Assert.That(source.time, Is.GreaterThan(playhead));
            // Listener reconnection and repeated zone callbacks must not replay a finalized result.
            success.enabled = false; success.enabled = true;
            mission.SendMessage("OnTriggerStay", carrier.GetComponent<CapsuleCollider>()); yield return null;
            Assert.That(plays, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CargoLossEscapeAndDeathNeverPlaySuccess()
        {
            int plays = 0; success.Played += () => plays++;
            var source = Field<AudioSource>(success, "source");
            Object.FindFirstObjectByType<CargoItem>().Condition.ApplyDamage(999);
            carrier.GetComponent<Rigidbody>().position = mission.transform.position; Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.EscapedCargoLost));
            Assert.That(source.clip, Is.Null); Assert.That(plays, Is.Zero);
            // A fresh mission death is another distinct terminal path, with no success playback.
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); yield return Load();
            plays = 0; success.Played += () => plays++;
            carrier.GetComponent<Health>().ApplyDamage(999); yield return null;
            Assert.That(mission.Result.Outcome, Is.EqualTo(MissionOutcome.SurvivorKilled));
            Assert.That(Field<AudioSource>(success, "source").clip, Is.Null); Assert.That(plays, Is.Zero);
        }
    }
}
#endif
