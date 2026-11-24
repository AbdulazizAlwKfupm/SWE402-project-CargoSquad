#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor;
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
    public sealed class WeaponCombatTests
    {
        private CharacterSelection selection;
        private PlayerWeapon weapon;
        private Rigidbody body;
        private CargoInteractor carrier;
        private MutatedWolfAI wolf;
        private Mouse mouse;
        private Keyboard keyboard;
        private readonly List<Object> created = new List<Object>();

        [UnitySetUp]
        public IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity", new LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null; yield return null;
            selection = Object.FindFirstObjectByType<CharacterSelection>();
            weapon = GameObject.Find("PlayerPlaceholder").GetComponent<PlayerWeapon>();
            Assert.That(weapon, Is.Not.Null);
            body = weapon.GetComponent<Rigidbody>(); carrier = weapon.GetComponent<CargoInteractor>();
            wolf = Object.FindFirstObjectByType<MutatedWolfAI>();
            Assert.That(wolf, Is.Not.Null, "The mission spawner must create its configured wolf.");
            mouse = InputSystem.AddDevice<Mouse>(); keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width * .5f, Screen.height * .4f) });
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            foreach (var obj in created) if (obj) Object.Destroy(obj);
            created.Clear(); yield return null;
        }
        private IEnumerator Deploy(int index, bool open = true)
        {
            selection.SelectCharacter(index); selection.StartSelectedCharacter();
            wolf.enabled = false;
            yield return null; yield return null; yield return null;
            // Independent runtime data for tests; never tune the shared asset in Play Mode.
            var copy = Object.Instantiate(weapon.Definition); created.Add(copy);
            var settings = new SerializedObject(weapon);
            settings.FindProperty(index == 0 ? "heavyWeapon" : "medicWeapon").objectReferenceValue = copy;
            settings.ApplyModifiedPropertiesWithoutUndo(); weapon.Equip((SurvivorKind)index, selection.ActiveVisual);
            if (open)
            {
                var floor = Cube(new Vector3(1000, -.25f, 1000), new Vector3(50, .5f, 50)); floor.name = "Combat test ground";
                body.position = new Vector3(1000, 1, 1000); body.linearVelocity = Vector3.zero;
                var camera = Camera.main; camera.GetComponent<TopDownCameraFollow>().enabled = false;
                camera.transform.position = new Vector3(1000, 22, 978);
            }
            Physics.SyncTransforms(); yield return new WaitForSeconds(.12f); yield return null;
            Aim(Vector3.forward);
        }
        private GameObject Cube(Vector3 position, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(obj);
            obj.transform.position = position; obj.transform.localScale = scale; return obj;
        }
        private Health Target(Vector3 position, Vector3 scale)
        {
            var obj = Cube(position, scale); var hp = obj.AddComponent<Health>(); hp.ResetMaximum(1000); return hp;
        }
        private Vector2 Cursor(Vector3 direction) => Camera.main.WorldToScreenPoint(new Vector3(body.position.x, 0, body.position.z) + direction * 4);
        private void Aim(Vector3 direction)
        {
            Vector2 cursor = Cursor(direction);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = cursor });
            Assert.That(weapon.AimAtScreen(cursor), Is.True);
            Assert.That(Vector3.Angle(weapon.AimDirection, direction), Is.LessThan(.01f));
        }
        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }
        private IEnumerator MouseButton(Vector3 direction, ushort buttons)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Cursor(direction), buttons = buttons });
            yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator HeavyCursorConeMouseFirePelletsAmmoAndRate()
        {
            yield return Deploy(0);
            var target = Target(body.position + Vector3.forward * 3, new Vector3(6, 2, .2f)); Physics.SyncTransforms();
            int hits = 0; target.Changed += (_, _) => hits++;
            yield return MouseButton(Vector3.forward, 1);
            Assert.That(weapon.ConeVisible, Is.True); Assert.That(weapon.EffectiveConeAngle, Is.EqualTo(60));
            yield return new WaitForSeconds(.12f);
            Assert.That(Vector3.Angle(selection.ActiveVisual.transform.forward, Vector3.forward), Is.LessThan(2));
            Capture("heavy-cone");
            Assert.That(weapon.Magazine, Is.EqualTo(5)); Assert.That(hits, Is.Zero);
            yield return new WaitForSeconds(.85f); Assert.That(hits, Is.Zero);
            yield return MouseButton(Vector3.forward, 0);
            Assert.That(weapon.Magazine, Is.EqualTo(4)); Assert.That(hits, Is.EqualTo(7));
            Assert.That(target.Current, Is.LessThan(1000)); Assert.That(weapon.TryFire(), Is.False);
            yield return new WaitForSeconds(.85f); // Held LMB must not auto-fire the shotgun.
            Assert.That(weapon.Magazine, Is.EqualTo(4)); Assert.That(hits, Is.EqualTo(7));
            Assert.That(weapon.GetComponent<Health>().Current, Is.EqualTo(130), "No self damage.");
        }
        [UnityTest]
        public IEnumerator WallsRangeAndActualRayIntersectionBlockDamage()
        {
            yield return Deploy(1);
            var target = Target(body.position + Vector3.forward * 4, new Vector3(4, 2, .2f));
            var wall = Cube(body.position + Vector3.forward * 2, new Vector3(5, 3, .3f)); Physics.SyncTransforms();
            Assert.That(weapon.TryFire(), Is.True); Assert.That(target.Current, Is.EqualTo(1000));
            Object.Destroy(wall); yield return new WaitForSeconds(.72f);
            Assert.That(weapon.TryFire(), Is.True); Assert.That(target.Current, Is.EqualTo(994));
            target.transform.position = body.position + Vector3.forward * 7; Physics.SyncTransforms();
            yield return new WaitForSeconds(.72f); Assert.That(weapon.TryFire(), Is.True);
            Assert.That(target.Current, Is.EqualTo(994), "Outside six-metre range.");
            target.transform.position = body.position + new Vector3(1.2f, 0, 4); target.transform.localScale = new Vector3(.1f, 2, .1f);
            Physics.SyncTransforms(); yield return new WaitForSeconds(.72f); Assert.That(weapon.TryFire(), Is.True);
            Assert.That(target.Current, Is.EqualTo(994), "Being near/in the sector alone is not a hit.");
        }
        [UnityTest]
        public IEnumerator EmptyReloadAmmoHudAndDeathCancellation()
        {
            yield return Deploy(1); weapon.Definition.shotsPerSecond = 100; weapon.Definition.reloadDuration = .15f;
            for (int i = 0; i < 4; i++) { Assert.That(weapon.TryFire(), Is.True); yield return new WaitForSeconds(.02f); }
            Assert.That(weapon.Magazine, Is.Zero); Assert.That(weapon.TryFire(), Is.False);
            Assert.That(weapon.HudText, Does.Contain("EMPTY"));
            yield return Press(Key.R); Assert.That(weapon.Reloading, Is.True);
            yield return new WaitForSeconds(.25f);
            Assert.That(weapon.Magazine, Is.EqualTo(4)); Assert.That(weapon.Reserve, Is.EqualTo(8));
            Assert.That(Object.FindFirstObjectByType<CargoMissionOverlay>().GetComponentsInChildren<TMPro.TMP_Text>().Single(t => t.name == "Ammo").text,
                Does.Contain("STUN  4 / 8"));
            Assert.That(weapon.TryFire(), Is.True); Assert.That(weapon.TryReload(), Is.True);
            weapon.GetComponent<Health>().ApplyDamage(999); yield return null;
            Assert.That(weapon.Reloading, Is.False); Assert.That(weapon.TryFire(), Is.False); Assert.That(weapon.TryReload(), Is.False);
            Assert.That(weapon.ConeVisible, Is.False);
        }
        [UnityTest]
        public IEnumerator SelectionPauseUiAndEquipGuardSuppressShooting()
        {
            Assert.That(weapon.TryFire(), Is.False); Assert.That(weapon.ConeVisible, Is.False);
            selection.StartSelectedCharacter();
            Assert.That(weapon.Magazine, Is.EqualTo(5)); Assert.That(weapon.TryFire(), Is.False);
            yield return null; yield return null; yield return null;
            var panel = Object.FindFirstObjectByType<CargoMissionOverlay>().GetComponentsInChildren<RectTransform>().Single(t => t.name == "MissionPanel");
            Vector2 pos = RectTransformUtility.WorldToScreenPoint(null, panel.position) + new Vector2(20, -20);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos, buttons = 3 }); yield return null; yield return null;
            Assert.That(weapon.Magazine, Is.EqualTo(5)); Assert.That(weapon.ConeVisible, Is.False); Assert.That(weapon.TryFire(), Is.False);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Cursor(Vector3.right), buttons = 2 }); yield return null;
            Time.timeScale = 0; yield return null;
            Assert.That(weapon.ConeVisible, Is.False); Assert.That(weapon.TryFire(), Is.False);
        }
        [UnityTest]
        public IEnumerator MedicStunCancelsBiteRecoversResistsRepeatedStunsAndDies()
        {
            yield return Deploy(1, false);
            wolf.enabled = true; wolf.GetComponent<NavMeshAgent>().Warp(new Vector3(7, 0, -7));
            body.position = new Vector3(5.7f, 1, -7); Physics.SyncTransforms();
            float deadline = Time.time + 2;
            while (wolf.State != WolfState.Attack && Time.time < deadline) yield return null;
            Assert.That(wolf.State, Is.EqualTo(WolfState.Attack));
            Aim(Vector3.right); yield return null;
            float hp = weapon.GetComponent<Health>().Current;
            Assert.That(weapon.TryFire(), Is.True); Assert.That(wolf.State, Is.EqualTo(WolfState.Stunned));
            Assert.That(wolf.GetComponent<Health>().Current, Is.LessThan(wolf.GetComponent<Health>().Maximum));
            Assert.That(wolf.TryStun(50), Is.False);
            Vector3 location = wolf.transform.position;
            yield return new WaitForSeconds(.6f);
            Assert.That(wolf.transform.position, Is.EqualTo(location)); Assert.That(weapon.GetComponent<Health>().Current, Is.EqualTo(hp));
            yield return new WaitForSeconds(.7f);
            Assert.That(wolf.State, Is.Not.EqualTo(WolfState.Stunned)); Assert.That(wolf.TryStun(1), Is.False);
            wolf.GetComponent<Health>().ApplyDamage(999);
            Assert.That(wolf.State, Is.EqualTo(WolfState.Death)); Assert.That(wolf.TryStun(1), Is.False);
        }
        [UnityTest]
        public IEnumerator GunshotEmitsExistingNoiseAndWolfInvestigates()
        {
            yield return Deploy(0, false);
            wolf.enabled = true;
            var settings = new SerializedObject(wolf); settings.FindProperty("detectionRange").floatValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            int count = 0; GameplayNoise heard = default; carrier.NoiseEmitted += noise => { count++; heard = noise; };
            Aim(Vector3.back); Assert.That(weapon.TryFire(), Is.True);
            Assert.That(count, Is.EqualTo(1)); Assert.That(heard.Radius, Is.EqualTo(20));
            Assert.That(wolf.State, Is.EqualTo(WolfState.Investigate)); Assert.That(Vector3.Distance(wolf.LastKnownPosition, body.position), Is.LessThan(.01f));
        }
        [UnityTest]
        public IEnumerator CargoAccuracyShotPreservesSocketGripConditionAndExtraction()
        {
            yield return Deploy(0, false);
            var cargo = Object.FindFirstObjectByType<CargoItem>();
            body.position = cargo.transform.position + new Vector3(0, .25f, -1.9f); Physics.SyncTransforms();
            yield return new WaitForSeconds(.12f); yield return Press(Key.E);
            Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
            Transform parent = cargo.transform.parent; float grip = carrier.Grip.Percent, condition = cargo.Condition.Current;
            Aim(Vector3.back); yield return MouseButton(Vector3.back, 1); yield return new WaitForSeconds(.2f);
            Assert.That(weapon.EffectiveConeAngle, Is.EqualTo(90)); Capture("heavy-carry-cone");
            yield return MouseButton(Vector3.back, 0);
            Assert.That(weapon.Magazine, Is.EqualTo(4));
            Assert.That(cargo.transform.parent, Is.SameAs(parent)); Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
            Assert.That(cargo.Condition.Current, Is.EqualTo(condition)); Assert.That(carrier.Grip.Percent, Is.EqualTo(grip));
            Assert.That(weapon.GetComponent<CharacterAbility>().TryActivate(), Is.False);
            var extraction = Object.FindFirstObjectByType<CargoSquad.Missions.CargoExtraction>();
            body.position = extraction.transform.position; Physics.SyncTransforms();
            for (int step = 0; step < 12; step++) yield return new WaitForFixedUpdate();
            yield return null; Assert.That(extraction.IsComplete, Is.True); Assert.That(cargo.IsDelivered, Is.True);
        }
        [UnityTest]
        public IEnumerator MedicConeIsNarrowAndQRestoresStaminaWithoutFiring()
        {
            yield return Deploy(1);
            yield return MouseButton(Vector3.right, 1); yield return new WaitForSeconds(.1f);
            Assert.That(weapon.ConeVisible, Is.True); Assert.That(weapon.EffectiveConeAngle, Is.EqualTo(20)); Capture("medic-cone");
            var stamina = weapon.GetComponent<PlayerStamina>(); stamina.Tick(true, 1, 1);
            yield return Press(Key.Q);
            Assert.That(stamina.Current, Is.EqualTo(100)); Assert.That(weapon.Magazine, Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator FutureRifleSingleReleaseAndReequipReset()
        {
            yield return Deploy(0);
            var rifle = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_CargoSquad/Data/Weapons/NavyRifle.asset"));
            created.Add(rifle); var settings = new SerializedObject(weapon);
            settings.FindProperty("heavyWeapon").objectReferenceValue = rifle; settings.ApplyModifiedPropertiesWithoutUndo();
            weapon.Equip(SurvivorKind.Heavy, selection.ActiveVisual); yield return null; yield return null; yield return null;
            yield return MouseButton(Vector3.forward, 1); yield return new WaitForSeconds(.35f);
            Assert.That(weapon.Magazine, Is.EqualTo(24));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Cursor(Vector3.forward) }); yield return null; yield return null;
            Assert.That(weapon.Magazine, Is.EqualTo(23));
            weapon.Equip(SurvivorKind.Heavy, selection.ActiveVisual);
            Assert.That(weapon.Magazine, Is.EqualTo(24)); Assert.That(weapon.Reserve, Is.EqualTo(96)); Assert.That(weapon.Reloading, Is.False);
        }
        private void Capture(string name)
        {
            var camera = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var cameras = canvases.Select(c => c.worldCamera).ToArray();
            var distances = canvases.Select(c => c.planeDistance).ToArray();
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; }
            Canvas.ForceUpdateCanvases();
            var target = new RenderTexture(1280, 800, 24); camera.targetTexture = target;
            camera.Render(); var old = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory("ValidationResults/Weapons");
            System.IO.File.WriteAllBytes("ValidationResults/Weapons/" + name + ".png", image.EncodeToPNG());
            RenderTexture.active = old; camera.targetTexture = null; Object.Destroy(image); Object.Destroy(target);
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
        }
        private IEnumerator SceneVisual(int index)
        {
            yield return Deploy(index, false);
            body.position = new Vector3(0, 1, -5); Physics.SyncTransforms();
            yield return new WaitForSeconds(.3f);
            yield return MouseButton(Vector3.right, 1);
            yield return new WaitForSeconds(.2f);
            Assert.That(weapon.ConeVisible, Is.True);
            Capture(index == 0 ? "industrial-heavy-cone" : "industrial-medic-cone");
            yield return MouseButton(Vector3.right, 0);
            Assert.That(weapon.Magazine, Is.EqualTo(index == 0 ? 4 : 3));
            Capture(index == 0 ? "industrial-heavy-shot" : "industrial-medic-shot");
            yield return MouseButton(Vector3.right, 0);
            Assert.That(weapon.ConeVisible, Is.False);
        }
        [UnityTest] public IEnumerator IndustrialHeavyVisual() { yield return SceneVisual(0); }
        [UnityTest] public IEnumerator IndustrialMedicVisual() { yield return SceneVisual(1); }

        [UnityTest]
        public IEnumerator HoldTracksCursorAndShortReleaseFiresOnceWithoutRmb()
        {
            yield return Deploy(0);
            yield return MouseButton(Vector3.forward, 1);
            Assert.That(weapon.IsAiming, Is.True); Assert.That(weapon.Magazine, Is.EqualTo(5));
            yield return MouseButton(Vector3.right, 1);
            Assert.That(Vector3.Angle(weapon.AimDirection, Vector3.right), Is.LessThan(.1f));
            float faceDeadline = Time.time + 1;
            while (Vector3.Angle(selection.ActiveVisual.transform.forward, Vector3.right) > 2 && Time.time < faceDeadline) yield return null;
            Assert.That(Vector3.Angle(selection.ActiveVisual.transform.forward, Vector3.right), Is.LessThan(2));
            yield return MouseButton(Vector3.right, 0);
            Assert.That(weapon.Magazine, Is.EqualTo(4)); Assert.That(weapon.ConeVisible, Is.False);
            yield return new WaitForSeconds(.85f);
            Assert.That(weapon.Magazine, Is.EqualTo(4));
            // A click lasting one game frame is still a valid gesture.
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Cursor(Vector3.forward), buttons = 1 }); yield return null;
            Assert.That(weapon.Magazine, Is.EqualTo(4));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Cursor(Vector3.forward) }); yield return null; yield return null;
            Assert.That(weapon.Magazine, Is.EqualTo(3));
        }
        [UnityTest]
        public IEnumerator UiDragPauseFocusReloadAndDeathCancelHeldGesture()
        {
            yield return Deploy(0);
            var panel = Object.FindFirstObjectByType<CargoMissionOverlay>().GetComponentsInChildren<RectTransform>().Single(t => t.name == "MissionPanel");
            Vector2 ui = RectTransformUtility.WorldToScreenPoint(null, panel.position) + new Vector2(20, -20);
            yield return MouseButton(Vector3.right, 1);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = ui, buttons = 1 }); yield return null;
            Assert.That(weapon.ConeVisible, Is.False);
            yield return MouseButton(Vector3.right, 1); yield return MouseButton(Vector3.right, 0);
            Assert.That(weapon.Magazine, Is.EqualTo(5), "World-to-UI drag cannot fire after returning.");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = ui, buttons = 1 }); yield return null;
            yield return MouseButton(Vector3.right, 1); yield return MouseButton(Vector3.right, 0);
            Assert.That(weapon.Magazine, Is.EqualTo(5), "UI-to-world drag cannot arm.");
            yield return MouseButton(Vector3.right, 1); Time.timeScale = 0; yield return null;
            Assert.That(weapon.ConeVisible, Is.False);
            Time.timeScale = 1; yield return MouseButton(Vector3.right, 0); Assert.That(weapon.Magazine, Is.EqualTo(5));
            yield return MouseButton(Vector3.right, 1); weapon.SendMessage("OnApplicationFocus", false); yield return null;
            Assert.That(weapon.ConeVisible, Is.False);
            weapon.SendMessage("OnApplicationFocus", true); yield return MouseButton(Vector3.right, 0);
            Assert.That(weapon.Magazine, Is.EqualTo(5));
            Assert.That(weapon.TryFire(), Is.True);
            yield return MouseButton(Vector3.right, 1); Assert.That(weapon.TryReload(), Is.True);
            yield return MouseButton(Vector3.right, 0); Assert.That(weapon.Magazine, Is.EqualTo(4));
            weapon.GetComponent<Health>().ApplyDamage(999); yield return null;
            Assert.That(weapon.ConeVisible, Is.False); Assert.That(weapon.TryFire(), Is.False);
        }
        [UnityTest]
        public IEnumerator ShotgunAndBashUpdateActualWolfHealthBarAndDeathHidesIt()
        {
            yield return Deploy(0, false);
            wolf.GetComponent<NavMeshAgent>().Warp(new Vector3(7, 0, -7));
            body.position = new Vector3(7, 1, -10); Physics.SyncTransforms(); yield return new WaitForSeconds(.2f);
            var health = wolf.GetComponent<Health>(); var bar = wolf.GetComponent<WolfHealthIndicator>();
            Assert.That(bar.Visible, Is.True); Assert.That(bar.TargetFraction, Is.EqualTo(1));
            float before = health.Current;
            yield return MouseButton(Vector3.forward, 1); yield return MouseButton(Vector3.forward, 0);
            Assert.That(health.Current, Is.LessThan(before));
            Assert.That(bar.TargetFraction, Is.EqualTo(health.Current / health.Maximum).Within(.001f));
            Assert.That(bar.FillColor.r, Is.GreaterThan(.9f));
            yield return new WaitForSeconds(.35f);
            Assert.That(bar.DisplayedFraction, Is.EqualTo(bar.TargetFraction).Within(.01f));
            Capture("wolf-damaged-health");
            body.position = new Vector3(5, 1, -7);
            selection.ActiveVisual.transform.rotation = Quaternion.LookRotation(Vector3.right);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(4, 4) });
            Physics.SyncTransforms();
            before = health.Current; yield return Press(Key.Q);
            Assert.That(weapon.GetComponent<CharacterAbility>().CooldownRemaining, Is.GreaterThan(0), "Q must activate the bash.");
            for (int step = 0; step < 25; step++) yield return new WaitForFixedUpdate();
            Assert.That(health.Current, Is.EqualTo(Mathf.Max(0, before - weapon.GetComponent<CharacterAbility>().ChargeDamage)).Within(.001f));
            Assert.That(bar.TargetFraction, Is.EqualTo(health.Current / health.Maximum).Within(.001f));
            body.position = wolf.transform.position + new Vector3(0, 1, -1.6f);
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            for (int shot = 0; shot < 4 && !health.IsDead; shot++)
            {
                yield return new WaitForSeconds(1f);
                yield return MouseButton(Vector3.forward, 1);
                yield return MouseButton(Vector3.forward, 0);
            }
            Assert.That(health.Current, Is.Zero, "Heavy must be able to finish the wolf with the shotgun.");
            Assert.That(bar.Visible, Is.False);
            Capture("wolf-dead-health-hidden");
        }
        [UnityTest]
        public IEnumerator MedicReleaseDamagesStunsAndIndependentWolfBarsUseThresholds()
        {
            yield return Deploy(1, false);
            wolf.GetComponent<NavMeshAgent>().Warp(new Vector3(7, 0, -7));
            body.position = new Vector3(4.5f, 1, -7); Physics.SyncTransforms(); yield return new WaitForSeconds(.15f);
            wolf.enabled = true;
            var hp = wolf.GetComponent<Health>(); var bar = wolf.GetComponent<WolfHealthIndicator>(); float before = hp.Current;
            yield return MouseButton(Vector3.right, 1); yield return MouseButton(Vector3.right, 0);
            Assert.That(hp.Current, Is.EqualTo(before - 6)); Assert.That(wolf.State, Is.EqualTo(WolfState.Stunned));
            Assert.That(bar.TargetFraction, Is.EqualTo(hp.Current / hp.Maximum).Within(.001f));
            Capture("medic-stun-wolf-health"); wolf.enabled = false;
            var other = new GameObject("Second health view test wolf"); created.Add(other);
            other.transform.position = new Vector3(3, 0, -5); var otherHp = other.AddComponent<Health>();
            var otherBar = other.AddComponent<WolfHealthIndicator>(); yield return null;
            hp.ApplyDamage(hp.Current - hp.Maximum * .5f); yield return new WaitForSeconds(.2f);
            Assert.That(bar.FillColor.g, Is.InRange(.65f, .75f)); Assert.That(otherBar.TargetFraction, Is.EqualTo(1));
            hp.ApplyDamage(hp.Maximum * .25f); yield return new WaitForSeconds(.2f);
            Assert.That(bar.FillColor.g, Is.LessThan(.3f));
            var canvas = wolf.GetComponentsInChildren<Canvas>().Single(c => c.name == "Wolf Health");
            Assert.That(Quaternion.Angle(canvas.transform.rotation, Camera.main.transform.rotation), Is.LessThan(.1f));
            otherHp.ApplyDamage(999); Assert.That(otherBar.Visible, Is.False); Assert.That(bar.Visible, Is.True);
        }
    }
}
#endif
