#if UNITY_EDITOR
using System.Collections;
using System.IO;
using CargoSquad.Cargo;
using TMPro;
using System.Linq;
using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.UI;
using CargoSquad.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CargoSquad.Tests
{
    public sealed class CharacterDeploymentTests
    {
        private CharacterSelection selection;
        private PlayerController player;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode previousInput;
        private InputSettings.BackgroundBehavior previousBackground;
        private readonly System.Collections.Generic.List<GameObject> created = new System.Collections.Generic.List<GameObject>();
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity", new LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null; yield return null;
            selection = Object.FindFirstObjectByType<CharacterSelection>();
            player = Object.FindFirstObjectByType<PlayerController>();
            // Disabled components are excluded by FindFirstObjectByType in some Unity versions.
            if (!player) player = GameObject.Find("PlayerPlaceholder").GetComponent<PlayerController>();
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.zero });
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousInput;
            InputSystem.settings.backgroundBehavior = previousBackground;
            foreach (var obj in created) if (obj) Object.Destroy(obj);
            created.Clear(); yield return null;
        }
        private IEnumerator Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 pos = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos, buttons = 1 }); yield return null; yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }); yield return null; yield return null;
        }
        private IEnumerator PressAbility()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
        private IEnumerator Start(int index)
        {
            selection.SelectCharacter(index); selection.StartSelectedCharacter();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.zero });
            yield return null; yield return null; yield return null;
        }
        private void OpenTestGround()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(floor);
            floor.name = "DeploymentTestGround"; floor.transform.position = new Vector3(1000, -.25f, 1000); floor.transform.localScale = new Vector3(40, .5f, 40);
            var body = player.GetComponent<Rigidbody>(); body.position = new Vector3(1000, 1, 1000); body.linearVelocity = Vector3.zero;
            selection.ActiveVisual.transform.rotation = Quaternion.identity; Physics.SyncTransforms();
        }
        private IEnumerator Drive(float seconds, params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); yield return new WaitForSeconds(seconds);
        }
        private void CaptureSelection() => CaptureInterface("character-selection.png", 1280, 800);
        private void CaptureInterface(string filename, int width, int height)
        {
            Directory.CreateDirectory("ValidationResults");
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.isActiveAndEnabled && c.renderMode != RenderMode.WorldSpace).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var cameras = canvases.Select(c => c.worldCamera).ToArray();
            var distances = canvases.Select(c => c.planeDistance).ToArray();
            var cameraObject = new GameObject("SelectionCaptureCamera");
            var camera = cameraObject.AddComponent<Camera>(); camera.cullingMask = 1 << 5;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .055f, .075f);
            var target = new RenderTexture(width, height, 24); target.Create(); camera.targetTexture = target;
            foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f; }
            Canvas.ForceUpdateCanvases(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            File.WriteAllBytes("ValidationResults/" + filename, image.EncodeToPNG());
            RenderTexture.active = previous;
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = modes[i]; canvases[i].worldCamera = cameras[i]; canvases[i].planeDistance = distances[i]; }
            Object.Destroy(image); camera.targetTexture = null; target.Release(); Object.Destroy(target); Object.Destroy(cameraObject);
        }
        [UnityTest]
        public IEnumerator HologramsUseDifferentActualHeadsRotateWhilePausedAndDisposeOnStart()
        {
            var heads = selection.GetComponentsInChildren<HologramHeadPreview>();
            Assert.That(heads.Length, Is.EqualTo(2));
            var heavyHead = heads.Single(h => h.SourceHeadName == "HV4_J_HEAD");
            var medicHead = heads.Single(h => h.SourceHeadName == "MR1_J_HEAD");
            Assert.That(heavyHead.Texture, Is.Not.SameAs(medicHead.Texture));
            Assert.That(heads.All(h => h.Texture.width >= 1024 && h.Texture.IsCreated()), Is.True);
            foreach (var filter in medicHead.Pivot.GetComponentsInChildren<MeshFilter>())
                Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.EqualTo("Assets/_CargoSquad/Characters/Medic_Redesign_v1.fbx"));
            foreach (var filter in heavyHead.Pivot.GetComponentsInChildren<MeshFilter>())
                Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Is.EqualTo("Assets/_CargoSquad/Characters/Heavy_v4.fbx"));
            Assert.That(medicHead.Pivot.GetComponentsInChildren<Transform>().Any(t => t.name.Contains("CHEST")), Is.False);
            var angle = medicHead.Pivot.rotation;
            Assert.That(Time.timeScale, Is.Zero); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(Quaternion.Angle(angle, medicHead.Pivot.rotation), Is.GreaterThan(3));
            Assert.That(selection.GetComponentsInChildren<TMP_Text>().All(t => t.font != null), Is.True);
            Assert.That(selection.GetComponentsInChildren<UnityEngine.UI.Text>(), Is.Empty);
            Directory.CreateDirectory("ValidationResults"); CaptureSelection();
            File.Copy("ValidationResults/character-selection.png", "ValidationResults/hologram-selection.png", true);
            yield return Start(1); yield return null;
            Assert.That(heads.All(h => h.Texture == null && h.Pivot == null), Is.True);
            Assert.That(player.GetComponentsInChildren<Animator>().Length, Is.EqualTo(1));
            Assert.That(player.GetComponents<Rigidbody>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GameplayHudAndCooldownUseReadableSdfText()
        {
            yield return Start(0); OpenTestGround();
            Assert.That(player.GetComponent<CharacterAbility>().TryActivate(), Is.True);
            yield return new WaitForSeconds(.1f);
            var cooldown = selection.transform.Find("DeploymentCanvas/AbilityStatus/AbilityText").GetComponent<TMP_Text>();
            Assert.That(cooldown.fontSize, Is.GreaterThanOrEqualTo(24));
            Assert.That(cooldown.text, Does.Contain("Ready in")); Assert.That(cooldown.font, Is.Not.Null);
            var mission = Object.FindFirstObjectByType<CargoMissionOverlay>();
            Assert.That(mission.GetComponentsInChildren<TMP_Text>().Count(t => t.gameObject.activeInHierarchy), Is.GreaterThanOrEqualTo(4));
            Assert.That(mission.GetComponentsInChildren<UnityEngine.UI.Text>(), Is.Empty);
            CaptureInterface("hologram-hud-1080.png", 1920, 1080);
        }
        [UnityTest]
        public IEnumerator SelectionPausesAndRealStartClickCannotActivateBash()
        {
            Assert.That(Time.timeScale, Is.Zero); Assert.That(selection.HasStarted, Is.False);
            Assert.That(player.enabled, Is.False); Assert.That(player.GetComponentsInChildren<Animator>(), Is.Empty);
            var before = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(player.transform.position, Is.EqualTo(before));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Directory.CreateDirectory("ValidationResults");
            CaptureSelection();
            yield return null;
            yield return Click(selection.StartButton);
            Assert.That(selection.HasStarted, Is.True); Assert.That(Time.timeScale, Is.EqualTo(1));
            var ability = player.GetComponent<CharacterAbility>();
            Assert.That(ability.Kind, Is.EqualTo(SurvivorKind.Heavy)); Assert.That(ability.IsCharging, Is.False);
            Assert.That(ability.CooldownRemaining, Is.Zero); Assert.That(player.GetComponents<Rigidbody>().Length, Is.EqualTo(1));
            Assert.That(player.GetComponentsInChildren<Animator>().Length, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator MedicCardClickSelectsMedicAndRestoresStaminaWithCooldownAndCap()
        {
            yield return Click(selection.Cards[1]); Assert.That(selection.SelectedIndex, Is.EqualTo(1));
            yield return Click(selection.StartButton); yield return null;
            var stamina = player.GetComponent<PlayerStamina>(); var ability = player.GetComponent<CharacterAbility>();
            Assert.That(stamina.Definition.DisplayName, Does.Contain("Medic"));
            Assert.That(selection.ActiveVisual.name, Is.EqualTo("Medic_redisgned"));
            Assert.That(ability.Kind, Is.EqualTo(SurvivorKind.Medic));
            var tuning = new SerializedObject(ability);
            Assert.That(tuning.FindProperty("medicCooldown").floatValue, Is.EqualTo(12f));
            tuning.FindProperty("medicCooldown").floatValue = .15f; tuning.ApplyModifiedPropertiesWithoutUndo();
            stamina.Tick(true, 1, 1); Assert.That(stamina.Current, Is.LessThan(100));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.zero }); yield return null; yield return null;
            yield return PressAbility(); Assert.That(stamina.Current, Is.EqualTo(100));
            Assert.That(ability.IsCharging, Is.False); Assert.That(ability.TryActivate(), Is.False);
            Assert.That(ability.CooldownRemaining, Is.GreaterThan(0));
            yield return new WaitForSeconds(.2f);
            stamina.Tick(true, 1, 1); Assert.That(ability.TryActivate(), Is.True); Assert.That(stamina.Current, Is.EqualTo(100));
            var definition = ScriptableObject.CreateInstance<CharacterDefinition>();
            var so = new SerializedObject(definition); so.FindProperty("maxStamina").floatValue = 80; so.ApplyModifiedPropertiesWithoutUndo();
            stamina.SetDefinition(definition); stamina.Tick(true, 1, 1);
            yield return new WaitForSeconds(.2f);
            Assert.That(ability.TryActivate(), Is.True); Assert.That(stamina.Current, Is.EqualTo(80)); Object.Destroy(definition);
        }
        private IEnumerator Locomotion(int index, float walk, float run)
        {
            yield return Start(index); OpenTestGround(); yield return new WaitForSeconds(.1f);
            var visual = selection.ActiveVisual; var animator = visual.Animator; var body = player.GetComponent<Rigidbody>();
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True); Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.GetBoneTransform(HumanBodyBones.Hips).name, Does.StartWith(index == 0 ? "HV4_J_" : "MR1_J_"));
            var model = visual.transform.GetChild(0); var scale = model.localScale; var rotation = model.localRotation; var offset = visual.transform.localPosition;
            Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip.name, Is.EqualTo("Idle"));
            yield return Drive(.5f, Key.W); Assert.That(body.linearVelocity.z, Is.EqualTo(walk).Within(.3f));
            var knee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg); var pose = knee.localRotation;
            float largestChange = 0f;
            for (int sample = 0; sample < 12; sample++)
            {
                yield return new WaitForSeconds(.04f);
                largestChange = Mathf.Max(largestChange, Quaternion.Angle(pose, knee.localRotation));
            }
            Assert.That(largestChange, Is.GreaterThan(5), "knee articulates over the walk cycle");
            yield return Drive(.5f, Key.W, Key.LeftShift); Assert.That(body.linearVelocity.z, Is.EqualTo(run).Within(.3f));
            Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip.name, Is.EqualTo("Run"));
            yield return Drive(.6f, Key.A); Assert.That(Vector3.Angle(visual.transform.forward, Vector3.left), Is.LessThan(5));
            yield return Drive(.6f, Key.S); Assert.That(Vector3.Angle(visual.transform.forward, Vector3.back), Is.LessThan(5));
            yield return Drive(.6f, Key.D); Assert.That(Vector3.Angle(visual.transform.forward, Vector3.right), Is.LessThan(5));
            yield return Drive(.5f); Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip.name, Is.EqualTo("Idle"));
            Assert.That(visual.transform.localPosition, Is.EqualTo(offset)); Assert.That(model.localScale, Is.EqualTo(scale));
            Assert.That(Quaternion.Angle(model.localRotation, rotation), Is.LessThan(.01f));
            Assert.That(Vector3.Angle(visual.transform.up, Vector3.up), Is.LessThan(.01f));
        }
        [UnityTest] public IEnumerator HeavyLocomotionIsSlowerAndUpright() { yield return Locomotion(0, 2.8f, 4.8f); }
        [UnityTest] public IEnumerator MedicLocomotionIsFasterAndUpright() { yield return Locomotion(1, 4f, 6.8f); }
        [UnityTest]
        public IEnumerator BashAnimatesAndStopsAtThinWall()
        {
            yield return Start(0); OpenTestGround(); yield return new WaitForSeconds(.1f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); created.Add(wall);
            wall.transform.position = new Vector3(1000, 1, 1001.6f); wall.transform.localScale = new Vector3(4, 2, .05f); Physics.SyncTransforms();
            var arm = selection.ActiveVisual.Animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var armBefore = arm.localRotation;
            float headBefore = selection.ActiveVisual.Animator.GetBoneTransform(HumanBodyBones.Head).position.y;
            var ability = player.GetComponent<CharacterAbility>(); Assert.That(ability.TryActivate(), Is.True);
            yield return new WaitForSeconds(.1f);
            Assert.That(Quaternion.Angle(armBefore, arm.localRotation), Is.GreaterThan(5f));
            Assert.That(selection.ActiveVisual.Animator.GetBoneTransform(HumanBodyBones.Head).position.y,
                Is.GreaterThan(headBefore - .3f), "bash keeps the body above the ground");
            var animator = selection.ActiveVisual.Animator;
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("ShoulderBash"), Is.True);
            Assert.That(animator.GetCurrentAnimatorClipInfo(0)[0].clip.humanMotion, Is.True);
            yield return new WaitForSeconds(.4f);
            Assert.That(player.transform.position.z, Is.LessThanOrEqualTo(1001.11f));
            Assert.That(player.transform.position.z, Is.GreaterThan(1000.5f));
            Assert.That(ability.IsCharging, Is.False); Assert.That(ability.TryActivate(), Is.False);
        }
        [UnityTest]
        public IEnumerator BashTravelsConfiguredDistanceAndDamagesTargetOnce()
        {
            yield return Start(0); OpenTestGround(); yield return new WaitForSeconds(.1f);
            var ability = player.GetComponent<CharacterAbility>();
            yield return PressAbility(); Assert.That(ability.CooldownRemaining, Is.GreaterThan(0f));
            yield return new WaitForSeconds(.5f);
            Assert.That(player.transform.position.z - 1000, Is.EqualTo(ability.ChargeDistance).Within(.2f));
            var so = new SerializedObject(ability); so.FindProperty("chargeCooldown").floatValue = .1f; so.ApplyModifiedPropertiesWithoutUndo();
            yield return new WaitForSeconds(4f);
            player.GetComponent<Rigidbody>().position = new Vector3(1000, 1, 1000);
            var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule); created.Add(enemy);
            enemy.transform.position = new Vector3(1000, 1, 1001.6f); var health = enemy.AddComponent<Health>(); Physics.SyncTransforms();
            Assert.That(ability.TryActivate(), Is.True); yield return new WaitForSeconds(.5f);
            Assert.That(health.Current, Is.EqualTo(health.Maximum - ability.ChargeDamage));
            yield return new WaitForSeconds(.2f); Assert.That(health.Current, Is.EqualTo(health.Maximum - ability.ChargeDamage));
        }
        [UnityTest]
        public IEnumerator RedesignedMedicPicksUpUsesHandIkDropsAndExtractsCargo()
        {
            yield return Start(1);
            var carrier = player.GetComponent<CargoInteractor>();
            var cargo = (CargoItem)new SerializedObject(carrier).FindProperty("cargo").objectReferenceValue;
            var size = cargo.Size;
            cargo.transform.position = player.transform.position + Vector3.forward * 1.2f;
            Physics.SyncTransforms(); yield return Drive(.12f, Key.E); yield return Drive(.1f);
            Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
            var hands = selection.ActiveVisual.GetComponent<FrontCarryVisual>();
            yield return new WaitForSeconds(.8f);
            Assert.That(hands.IkWeight, Is.EqualTo(1f));
            Assert.That(Vector3.Distance(selection.ActiveVisual.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position,
                hands.GripPoint(-1)), Is.LessThan(.04f));
            Assert.That(Vector3.Distance(selection.ActiveVisual.Animator.GetBoneTransform(HumanBodyBones.RightHand).position,
                hands.GripPoint(1)), Is.LessThan(.04f));
            Assert.That(selection.ActiveVisual.Weapon.parent, Is.SameAs(selection.ActiveVisual.StowSocket));
            player.GetComponent<Rigidbody>().position = new Vector3(0, 1, -4);
            yield return Drive(.12f); yield return Drive(.12f, Key.E); yield return Drive(.12f);
            Assert.That(carrier.CarriedCargo, Is.Null); Assert.That(cargo.Collider.enabled, Is.True);
            Assert.That(Vector3.Distance(cargo.Size, size), Is.LessThan(.0001f));
            Assert.That(selection.ActiveVisual.Weapon.parent, Is.SameAs(selection.ActiveVisual.HandSocket));
            yield return Drive(.12f, Key.E); yield return Drive(.1f);
            Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
            var extraction = Object.FindFirstObjectByType<CargoSquad.Missions.CargoExtraction>();
            player.GetComponent<Rigidbody>().position = extraction.transform.position;
            yield return new WaitForSeconds(.15f);
            Assert.That(extraction.IsComplete, Is.True); Assert.That(cargo.IsDelivered, Is.True);
        }
        [UnityTest]
        public IEnumerator AbilitiesAreBlockedByUIHeldStartPressCargoAndDeath()
        {
            yield return Start(1); OpenTestGround();
            var ability = player.GetComponent<CharacterAbility>();
            var hud = selection.transform.Find("DeploymentCanvas/AbilityStatus");
            Vector2 hudPosition = RectTransformUtility.WorldToScreenPoint(null, hud.position);
            // The panel has a top-right pivot; choose a point inside it.
            InputSystem.QueueStateEvent(mouse, new MouseState { position = hudPosition - new Vector2(100, 25) });
            yield return null;
            Assert.That(ability.PointerOverUI(), Is.True); Assert.That(ability.TryActivate(), Is.False);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(40, Screen.height - 60) });
            yield return null;
            Assert.That(ability.PointerOverUI(), Is.True, "legacy stamina HUD blocks abilities");
            Assert.That(ability.TryActivate(), Is.False);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.zero }); yield return null;
            var canvas = new GameObject("BlockingTestUI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)); created.Add(canvas);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var image = new GameObject("ButtonSurface", typeof(RectTransform), typeof(Image)); image.transform.SetParent(canvas.transform, false);
            var rect = image.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases(); yield return null;
            Assert.That(ability.PointerOverUI(), Is.True); Assert.That(ability.TryActivate(), Is.False); Assert.That(ability.CooldownRemaining, Is.Zero);
            Object.Destroy(canvas); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            ability.Configure(SurvivorKind.Medic, selection.ActiveVisual); yield return null; yield return null;
            Assert.That(ability.TryActivate(), Is.False);
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null; yield return null;
            var carrier = player.GetComponent<CargoInteractor>();
            var cargo = (CargoItem)new SerializedObject(carrier).FindProperty("cargo").objectReferenceValue;
            cargo.transform.position = player.transform.position + Vector3.forward * 1.2f; Physics.SyncTransforms();
            yield return Drive(.12f, Key.E); yield return Drive(.05f);
            Assert.That(carrier.CarriedCargo, Is.SameAs(cargo)); Assert.That(ability.TryActivate(), Is.False);
            // Death is independently checked after releasing test cargo via its real drop API/property reset.
            typeof(CargoItem).GetMethod("DropAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(cargo, new object[] { new Vector3(1003, .5f, 1000) });
            typeof(CargoInteractor).GetProperty("CarriedCargo").SetValue(carrier, null);
            player.GetComponent<Health>().ApplyDamage(1000);
            Assert.That(ability.TryActivate(), Is.False); Assert.That(ability.CooldownRemaining, Is.Zero);
        }
    }
}
#endif
