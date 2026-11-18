#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CargoSquad.Cargo;
using CargoSquad.Missions;
using CargoSquad.Player;
using CargoSquad.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CargoSquad.Tests
{
    /// <summary>
    /// Regression tests for the front carry in the real gameplay scene: CargoInteractor parents the cargo to the
    /// survivor model's CargoCarrySocket, and pickup, manual drop, automatic slip and extraction keep working.
    /// </summary>
    public sealed class FrontCarryTests
    {
        private const string GameScene = "Assets/_CargoSquad/Scenes/IndustrialDistrict.unity";
        private const string LineupScene = "Assets/_CargoSquad/Scenes/CharacterShowcase_FrontCarry.unity";
        private const string PrefabFolder = "Assets/_CargoSquad/Prefabs/Characters/";

        [UnityTest]
        public IEnumerator IndustrialDistrict_PickupFrontCarryDropSlipAndExtraction()
        {
            EditorSceneManager.LoadSceneInPlayMode(GameScene, new LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null;
            // This long visual/cargo regression predates combat. Bite integration is covered separately.
            foreach (var enemy in Object.FindObjectsByType<CargoSquad.AI.MutatedWolfAI>(FindObjectsSortMode.None))
                enemy.enabled = false;
            Object.FindFirstObjectByType<CargoSquad.UI.CharacterSelection>()?.StartSelectedCharacter();
            yield return null;
            var player = Object.FindFirstObjectByType<CargoInteractor>();
            var cargo = Object.FindFirstObjectByType<CargoItem>();
            var extraction = Object.FindFirstObjectByType<CargoExtraction>();
            var body = player.GetComponent<Rigidbody>();
            var visual = player.GetComponentInChildren<SurvivorVisual>();
            var hands = player.GetComponentInChildren<FrontCarryVisual>();
            var groundCollider = GameObject.Find("Ground").GetComponent<BoxCollider>();
            float ground = groundCollider.bounds.max.y;

            // --- the gameplay root is intact; only the capsule renderer is hidden
            Assert.That(visual, Is.Not.Null, "Navy visual is in the real scene");
            Assert.That(hands, Is.Not.Null);
            Assert.That(player.GetComponent<PlayerController>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerStamina>(), Is.Not.Null);
            Assert.That(player.GetComponent<CargoGrip>(), Is.Not.Null);
            Assert.That(player.GetComponent<CapsuleCollider>().enabled, Is.True);
            Assert.That(player.GetComponents<Rigidbody>().Length, Is.EqualTo(1));
            Assert.That(player.GetComponent<MeshRenderer>().enabled, Is.False, "capsule renderer hidden");
            Assert.That(player.GetComponent<MeshFilter>(), Is.Not.Null, "capsule mesh kept");
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Animator animator = visual.Animator;
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);

            Vector3 size = cargo.Size;
            Transform storage = cargo.transform.parent;
            Quaternion groundRotation = cargo.transform.rotation;
            Debug.Log($"[game] cargo size {size:F2}, resting bottom {cargo.Collider.bounds.min.y - ground:F3}");
            Assert.That(cargo.Collider.bounds.min.y - ground, Is.EqualTo(0f).Within(0.05f), "crate rests on the ground");
            yield return new WaitForSeconds(0.3f);
            CharacterShowcaseTests.Capture("50_game_idle");

            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                // --- movement
                Vector3 start = body.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                // Count simulated physics steps so screenshot stalls do not shorten the movement interval.
                for (int step = 0; step < 60; step++) yield return new WaitForFixedUpdate();
                Assert.That(body.position.x, Is.GreaterThan(start.x + 1f));
                Assert.That(Vector3.Angle(visual.transform.forward, Vector3.right), Is.LessThan(10f));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), Is.True);
                CharacterShowcaseTests.Capture("51_game_walk");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());

                // --- pickup: the interactor parents the cargo to the model's socket
                body.position = Beside(cargo, ground, -1.3f);
                yield return new WaitForSeconds(0.15f);
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.SameAs(cargo));
                Assert.That(cargo.transform.parent, Is.SameAs(hands.Socket.transform), "cargo is attached to the carry socket");
                Assert.That(cargo.Collider.enabled, Is.False, "collision off while carried");
                Assert.That(cargo.GetComponent<Rigidbody>(), Is.Null);
                Assert.That(Object.FindObjectsByType<CargoItem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Vector3 heldLocal = cargo.transform.localPosition;
                yield return new WaitForSeconds(1.6f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CarryLocomotion"), Is.True);
                Assert.That(visual.Weapon.parent, Is.SameAs(visual.StowSocket));
                Assert.That(Vector3.Distance(cargo.Size, size), Is.LessThan(1e-4f), "carrying does not rescale the cargo");
                AssertHeld(hands, animator, cargo.transform, Centre(cargo), size, "Navy idle");
                CharacterShowcaseTests.Capture("52_game_carry_idle");
                CharacterShowcaseTests.CaptureClose("52_game_carry_idle_close", visual.transform, 1.6f);

                // --- the cargo follows position and facing through the hierarchy while moving and turning
                // (done on open ground: inside the depot the scenery blocks hide the player from the camera)
                body.position = new Vector3(2f, ground + 1f, -3f);
                yield return new WaitForSeconds(0.15f);
                foreach ((Key key, Vector3 direction, string shot) in new[]
                         {
                             (Key.A, Vector3.left, "53_game_carry_left"), (Key.S, Vector3.back, "54_game_carry_toward_camera"),
                             (Key.D, Vector3.right, "55_game_carry_right"), (Key.W, Vector3.forward, "56_game_carry_away")
                         })
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    float turnSpeed = new SerializedObject(visual).FindProperty("turnSpeed").floatValue;
                    yield return new WaitForSeconds(Mathf.Max(.7f, 180f / Mathf.Max(1f, turnSpeed) + .15f));
                    Assert.That(Vector3.Angle(visual.transform.forward, direction), Is.LessThan(10f), shot + " facing");
                    Vector3 toCrate = Centre(cargo) - visual.transform.position;
                    toCrate.y = 0f;
                    Assert.That(Vector3.Angle(toCrate, direction), Is.LessThan(5f), shot + " crate is in front");
                    Assert.That(Vector3.Distance(cargo.transform.localPosition, heldLocal), Is.LessThan(1e-5f), shot + " no per-frame repositioning");
                    Assert.That(Quaternion.Angle(cargo.transform.localRotation, Quaternion.identity), Is.LessThan(0.01f));
                    AssertHeld(hands, animator, cargo.transform, Centre(cargo), size, shot);
                    AssertHeadNotHidden(animator, cargo, size, shot);
                    CharacterShowcaseTests.Capture(shot);
                }

                // --- sprinting while carrying still drains grip (existing mechanic)
                float gripBefore = player.Grip.Percent;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift));
                yield return new WaitForSeconds(0.6f);
                Assert.That(player.Grip.Percent, Is.LessThan(gripBefore), "sprint drains grip");
                AssertHeld(hands, animator, cargo.transform, Centre(cargo), size, "run");
                CharacterShowcaseTests.CaptureClose("57_game_carry_run_close", visual.transform, 1.6f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(0.3f);

                // --- manual drop restores parent, orientation, size and collision
                body.position = new Vector3(0f, ground + 1f, -4f);
                yield return new WaitForSeconds(0.15f);
                float condition = cargo.Condition.Current;
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.Null);
                AssertOnGround(cargo, storage, groundRotation, size, ground, "manual drop");
                Assert.That(cargo.Condition.Current, Is.EqualTo(condition));
                yield return new WaitForSeconds(1.0f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), Is.True);
                Assert.That(visual.Weapon.parent, Is.SameAs(visual.HandSocket));
                CharacterShowcaseTests.Capture("58_game_after_drop");

                // --- the dropped crate is solid again: the player cannot walk through it
                body.position = Beside(cargo, ground, -1.6f);
                yield return new WaitForSeconds(0.15f);
                Vector3 crateBefore = cargo.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(1.0f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Debug.Log($"[game] blocked at z={body.position.z:F2} y={body.position.y:F2}, crate near face z={Centre(cargo).z - size.z * 0.5f:F2}");
                Assert.That(body.position.z, Is.LessThan(Centre(cargo).z - size.z * 0.5f), "player is stopped by the dropped crate");
                Assert.That(body.position.y, Is.EqualTo(ground + 1f).Within(0.1f), "player does not climb the crate");
                Assert.That(cargo.transform.position, Is.EqualTo(crateBefore), "crate is not pushed");
                yield return new WaitForSeconds(0.2f);

                // --- automatic slip: grip runs out, cargo waits for clear ground, then drops with damage and noise
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.SameAs(cargo));
                int noises = 0;
                player.NoiseEmitted += _ => noises++;
                groundCollider.enabled = false;
                player.Grip.Tick(true, true, 1f, 100f);
                yield return null;
                yield return null;
                Assert.That(player.HasPendingSlip, Is.True);
                Assert.That(cargo.transform.parent, Is.SameAs(hands.Socket.transform), "still held while no ground is clear");
                Assert.That(noises, Is.Zero);
                groundCollider.enabled = true;
                body.position = new Vector3(0f, ground + 1f, -4f);
                yield return new WaitForSeconds(0.15f);
                Assert.That(player.CarriedCargo, Is.Null, "slip dropped the cargo");
                Assert.That(noises, Is.EqualTo(1));
                Assert.That(cargo.Condition.Current, Is.EqualTo(condition - cargo.Definition.SlipDamage));
                AssertOnGround(cargo, storage, groundRotation, size, ground, "slip");
                yield return new WaitForSeconds(0.3f);

                // --- extraction
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.SameAs(cargo));
                Assert.That(player.Grip.Percent, Is.EqualTo(100f));
                body.position = extraction.transform.position;
                yield return new WaitForSeconds(0.2f);
                Assert.That(extraction.IsComplete, Is.True);
                Assert.That(extraction.RecoveryFailed, Is.False);
                Assert.That(cargo.IsDelivered, Is.True);
                Assert.That(cargo.transform.parent, Is.SameAs(storage), "delivered cargo leaves the socket");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator IndustrialDistrict_EverySurvivorCarriesOnItsOwnSocket()
        {
            EditorSceneManager.LoadSceneInPlayMode(GameScene, new LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null;
            Object.FindFirstObjectByType<CargoSquad.UI.CharacterSelection>()?.StartSelectedCharacter();
            yield return null;
            var player = Object.FindFirstObjectByType<CargoInteractor>();
            var cargo = Object.FindFirstObjectByType<CargoItem>();
            var body = player.GetComponent<Rigidbody>();
            float ground = GameObject.Find("Ground").GetComponent<BoxCollider>().bounds.max.y;
            Vector3 size = cargo.Size;
            Vector3 visualLocal = player.GetComponentInChildren<SurvivorVisual>().transform.localPosition;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                foreach (string name in new[] { "Heavy_Visual", "Scout_Visual", "Medic_Visual", "NavySoldier_Visual" })
                {
                    // swap the model only; the gameplay root and its components stay as they are
                    Object.Destroy(player.GetComponentInChildren<SurvivorVisual>().gameObject);
                    yield return null;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
                    GameObject model = Object.Instantiate(prefab, player.transform);
                    model.name = name;
                    model.transform.localPosition = visualLocal;
                    var hands = model.GetComponent<FrontCarryVisual>();
                    var animator = model.GetComponent<Animator>();
                    body.position = Beside(cargo, ground, -1.3f);
                    yield return new WaitForSeconds(0.2f);
                    yield return Tap(keyboard);
                    Assert.That(player.CarriedCargo, Is.SameAs(cargo), name + " picked up");
                    Assert.That(cargo.transform.parent, Is.SameAs(hands.Socket.transform), name + " uses its own socket");
                    yield return new WaitForSeconds(1.6f);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CarryLocomotion"), Is.True, name);
                    AssertHeld(hands, animator, cargo.transform, Centre(cargo), size, name);
                    float head = animator.GetBoneTransform(HumanBodyBones.Head).position.y;
                    Debug.Log($"[game] {name}: crate centre {Centre(cargo).y - ground:F2} m, head {head - ground:F2} m");
                    Assert.That(Centre(cargo).y + size.y * 0.5f, Is.LessThan(head - 0.1f), name + " head clear of the crate");
                    CharacterShowcaseTests.CaptureClose("60_game_carry_" + name, model.transform, 1.6f);
                    yield return Tap(keyboard);
                    Assert.That(player.CarriedCargo, Is.Null, name + " dropped");
                    Assert.That(cargo.Collider.enabled, Is.True);
                    yield return new WaitForSeconds(0.2f);
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator Lineup_AllSurvivorsHoldTheSameCargoWithHandIk()
        {
            EditorSceneManager.LoadSceneInPlayMode(LineupScene, new LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null;
            yield return null;
            Transform lineup = GameObject.Find("Front Carry Lineup").transform;
            Camera camera = Camera.main;
            camera.GetComponent<TopDownCameraFollow>().enabled = false;
            var size = new Vector3(0.60f, 0.45f, 0.45f);
            var wide = new Vector3(0.80f, 0.30f, 0.60f);   // a second cargo shape, to show nothing is tied to one size
            var carriers = new List<(Animator animator, FrontCarryVisual hands, Transform crate)>();
            foreach (Transform item in lineup)
            {
                item.GetComponent<SurvivorVisual>().enabled = false;   // the test drives the Animator directly
                GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(crate.GetComponent<Collider>());
                crate.transform.localScale = size;
                var hands = item.GetComponent<FrontCarryVisual>();
                hands.SetPreviewCargo(crate.transform, size);
                var animator = item.GetComponent<Animator>();
                animator.SetBool("Carrying", true);
                carriers.Add((animator, hands, crate.transform));
            }
            Assert.That(carriers.Count, Is.EqualTo(4));
            yield return new WaitForSeconds(1.4f);

            float ground = GameObject.Find("Ground").GetComponent<BoxCollider>().bounds.max.y;
            foreach ((Animator animator, FrontCarryVisual hands, Transform crate) in carriers)
            {
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CarryLocomotion"), Is.True, animator.name);
                AssertHeld(hands, animator, crate, crate.position, size, animator.name);
            }

            Vector3 centre = new Vector3(carriers.Average(c => c.animator.transform.position.x), ground, carriers[0].animator.transform.position.z);
            camera.transform.position = centre + Vector3.up * 0.9f - camera.transform.forward * 30f;
            CharacterShowcaseTests.Capture("39_front_lineup_facing_camera");
            foreach ((Animator animator, _, _) in carriers)
            {
                animator.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                animator.SetFloat("Speed", 4f);
            }
            yield return new WaitForSeconds(0.6f);
            foreach ((Animator animator, FrontCarryVisual hands, Transform crate) in carriers)
                AssertHeld(hands, animator, crate, crate.position, size, animator.name + " walking");
            CharacterShowcaseTests.Capture("40_front_lineup_walking_side");
            foreach ((Animator animator, _, _) in carriers)
                animator.transform.rotation = Quaternion.identity;
            yield return new WaitForSeconds(0.3f);
            CharacterShowcaseTests.Capture("41_front_lineup_walking_away");
            camera.orthographicSize = 3.3f;
            foreach ((Animator animator, _, _) in carriers)
            {
                animator.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                animator.SetFloat("Speed", 0f);
            }
            yield return new WaitForSeconds(0.5f);
            CharacterShowcaseTests.Capture("42_front_lineup_close");

            // --- a differently sized cargo sits against the body and the hands follow its sides
            foreach ((Animator animator, FrontCarryVisual hands, Transform crate) in carriers)
            {
                crate.localScale = wide;
                hands.SetPreviewCargo(crate, wide);
            }
            yield return new WaitForSeconds(0.6f);
            foreach ((Animator animator, FrontCarryVisual hands, Transform crate) in carriers)
                AssertHeld(hands, animator, crate, crate.position, wide, animator.name + " wide cargo", 0.06f);
            CharacterShowcaseTests.Capture("43_front_lineup_wide_cargo");
        }

        private static Vector3 Centre(CargoItem cargo) => cargo.transform.TransformPoint(cargo.Collider.center);

        private static Vector3 Beside(CargoItem cargo, float ground, float z)
        {
            Vector3 centre = Centre(cargo);
            return new Vector3(centre.x, ground + 1f, centre.z + z);
        }

        private static void AssertHeld(FrontCarryVisual hands, Animator animator, Transform crate, Vector3 centre, Vector3 size,
            string label, float tolerance = 0.03f)
        {
            Transform socket = hands.Socket.transform;
            Vector3 expected = socket.TransformPoint(hands.Socket.LocalCenterFor(size));
            Assert.That(Vector3.Distance(centre, expected), Is.LessThan(0.005f), label + ": cargo rests against the socket");
            Assert.That(Quaternion.Angle(crate.rotation, hands.transform.rotation), Is.LessThan(0.5f), label + ": cargo faces with the model");
            foreach ((HumanBodyBones bone, int side) in new[] { (HumanBodyBones.LeftHand, -1), (HumanBodyBones.RightHand, 1) })
            {
                Vector3 wrist = animator.GetBoneTransform(bone).position;
                float error = Vector3.Distance(wrist, hands.GripPoint(side));
                Vector3 local = Quaternion.Inverse(crate.rotation) * (wrist - centre);
                Debug.Log($"[front] {label} {bone}: grip error {error * 100f:F1} cm, wrist {Mathf.Abs(local.x) - size.x * 0.5f:F3} m outside the side face");
                Assert.That(error, Is.LessThan(tolerance), $"{label}: {bone} on its grip point");
            }
        }

        private static void AssertOnGround(CargoItem cargo, Transform storage, Quaternion rotation, Vector3 size, float ground, string label)
        {
            Assert.That(cargo.IsCarried, Is.False, label);
            Assert.That(cargo.transform.parent, Is.SameAs(storage), label + ": parent restored");
            Assert.That(Quaternion.Angle(cargo.transform.rotation, rotation), Is.LessThan(0.01f), label + ": ground orientation restored");
            Assert.That(Vector3.Distance(cargo.Size, size), Is.LessThan(1e-4f), label + ": size unchanged");
            Assert.That(cargo.Collider.enabled, Is.True, label + ": collision restored");
            Assert.That(cargo.Collider.bounds.min.y - ground, Is.EqualTo(0.02f).Within(0.03f), label + ": on the ground");
            Assert.That(Physics.CheckBox(cargo.Collider.bounds.center, size * 0.45f), Is.True, label + ": physics sees the crate");
        }

        /// <summary>The carried cargo must not cover the survivor's head from the gameplay camera.</summary>
        private static void AssertHeadNotHidden(Animator animator, CargoItem cargo, Vector3 size, string label)
        {
            Camera camera = Camera.main;
            Vector3 head = camera.WorldToScreenPoint(animator.GetBoneTransform(HumanBodyBones.Head).position + Vector3.up * 0.08f);
            Vector3 centre = Centre(cargo);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            float nearest = float.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
                Vector3 point = camera.WorldToScreenPoint(centre + cargo.transform.rotation * Vector3.Scale(corner, size));
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
                nearest = Mathf.Min(nearest, point.z);
            }
            bool inside = head.x > min.x && head.x < max.x && head.y > min.y && head.y < max.y;
            Assert.That(inside && nearest < head.z, Is.False, label + ": cargo does not cover the head");
        }

        private static IEnumerator Tap(Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }
    }
}
#endif
