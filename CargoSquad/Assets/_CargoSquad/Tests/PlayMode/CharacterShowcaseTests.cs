#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using CargoSquad.Cargo;
using CargoSquad.Missions;
using CargoSquad.Visuals;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CargoSquad.Tests
{
    /// <summary>
    /// Validates the Navy Soldier visual on the unchanged gameplay root in the CharacterShowcase scene:
    /// avatar, scale/feet/facing, in-place animation, and the existing movement + cargo pickup/drop/extraction loop.
    /// Screenshots from the real gameplay camera are written to docs/phase2/character_art/validation/unity.
    /// </summary>
    public sealed class CharacterShowcaseTests
    {
        private const string Scene = "Assets/_CargoSquad/Scenes/CharacterShowcase.unity";

        private static string OutputFolder =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/phase2/character_art/validation/unity"));

        [UnityTest]
        public IEnumerator NavyVisualWorksWithExistingMovementAndCargoLoop()
        {
            EditorSceneManager.LoadSceneInPlayMode(Scene, new LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null;
            yield return null;
            var player = Object.FindFirstObjectByType<CargoInteractor>();
            var cargo = Object.FindFirstObjectByType<CargoItem>();
            var extraction = Object.FindFirstObjectByType<CargoExtraction>();
            var body = player.GetComponent<Rigidbody>();
            var visual = player.GetComponentInChildren<SurvivorVisual>();
            Assert.That(visual, Is.Not.Null, "player has a SurvivorVisual child");
            Animator animator = visual.Animator;

            // --- rig / avatar / structure
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True, "valid humanoid avatar");
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, "no colliders on the visual");
            Assert.That(visual.GetComponentsInChildren<Rigidbody>(true), Is.Empty, "no rigidbodies on the visual");
            Assert.That(player.GetComponents<Rigidbody>().Length, Is.EqualTo(1));
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                foreach (Material material in renderer.sharedMaterials)
                    Assert.That(material && material.shader.isSupported && !material.shader.name.Contains("Error"),
                        Is.True, $"material on {renderer.name}");

            yield return new WaitForSeconds(0.3f);
            // --- scale, feet, facing (visual starts unrotated => character must face +Z)
            float ground = GameObject.Find("Ground").GetComponent<BoxCollider>().bounds.max.y;
            Bounds bounds = Measure(visual);
            Debug.Log($"[showcase] bounds min={bounds.min:F3} max={bounds.max:F3} ground={ground:F3}");
            foreach (HumanBodyBones bone in new[] { HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftFoot,
                         HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftHand })
                Debug.Log($"[showcase] {bone} {animator.GetBoneTransform(bone).position - visual.transform.position:F3}");
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                Debug.Log($"[showcase] {renderer.name} minY={renderer.bounds.min.y:F3} maxY={renderer.bounds.max.y:F3}");
            Capture("01_idle");
            CaptureClose("01_idle_close", visual.transform);
            Assert.That(bounds.min.y, Is.EqualTo(ground).Within(0.06f), "feet on the ground");
            Assert.That(bounds.max.y - ground, Is.InRange(1.7f, 2.5f), "adult height in metres (incl. antenna)");
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Assert.That(head.position.y - ground, Is.InRange(1.5f, 1.75f), "head bone height");
            Transform leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Transform leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Assert.That(leftArm.position.x, Is.LessThan(rightArm.position.x), "left side is -X when facing +Z");
            Assert.That(leftToes.position.z, Is.GreaterThan(leftFoot.position.z), "toes point +Z");
            Assert.That(visual.Weapon.parent, Is.SameAs(visual.HandSocket));
            Vector3 visualLocal = visual.transform.localPosition;
            Capture("01_idle");
            CaptureClose("01_idle_close", visual.transform);

            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                // --- movement drives facing + locomotion, visual stays glued to the gameplay root
                Vector3 start = body.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                yield return new WaitForSeconds(0.7f);
                Assert.That(body.position.x, Is.GreaterThan(start.x + 1f));
                Assert.That(animator.GetFloat("Speed"), Is.GreaterThan(2f));
                Assert.That(Vector3.Angle(visual.transform.forward, Vector3.right), Is.LessThan(10f), "faces travel direction");
                Assert.That(Vector3.Distance(visual.transform.localPosition, visualLocal), Is.LessThan(0.001f), "no root motion");
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Vector3 hipOffset = hips.position - visual.transform.position;
                Assert.That(new Vector2(hipOffset.x, hipOffset.z).magnitude, Is.LessThan(0.2f), "in-place clip");
                Capture("02_walk_right");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.LeftShift));
                yield return new WaitForSeconds(0.6f);
                Assert.That(animator.GetFloat("Speed"), Is.GreaterThan(5f));
                Capture("03_run_right");
                CaptureClose("03_run_right_close", visual.transform);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S));
                yield return new WaitForSeconds(0.6f);
                Assert.That(Vector3.Angle(visual.transform.forward, Vector3.back), Is.LessThan(10f));
                Capture("04_walk_toward_camera");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(0.6f);
                Capture("05_walk_away_from_camera");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(0.4f);
                Assert.That(animator.GetFloat("Speed"), Is.LessThan(0.5f));

                // --- existing pickup: cargo stays the single gameplay object on its existing mount
                body.position = cargo.transform.position + new Vector3(0f, 0.25f, -1.9f);
                yield return new WaitForSeconds(0.15f);
                Capture("06_before_pickup");
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.SameAs(cargo));
                Assert.That(Object.FindObjectsByType<CargoItem>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                yield return new WaitForSeconds(0.9f);
                Assert.That(visual.IsCarrying, Is.True);
                Assert.That(animator.GetBool("Carrying"), Is.True);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CarryLocomotion"), Is.True, "carry state");
                Assert.That(cargo.transform.parent, Is.SameAs(player.transform));
                Assert.That(cargo.transform.localPosition, Is.EqualTo(new Vector3(0f, 1.8f, 0f)), "existing cargo mount untouched");
                Assert.That(visual.Weapon.parent, Is.SameAs(visual.StowSocket), "weapon stowed while carrying");
                float cargoBottom = cargo.GetComponent<Renderer>().bounds.min.y;
                float leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position.y;
                float rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand).position.y;
                Debug.Log($"[showcase] cargoBottom={cargoBottom:F3} leftHand={leftHand:F3} rightHand={rightHand:F3}");
                Assert.That(cargoBottom - leftHand, Is.InRange(-0.05f, 0.25f), "left hand under the crate");
                Assert.That(cargoBottom - rightHand, Is.InRange(-0.05f, 0.25f), "right hand under the crate");
                Capture("07_carry_idle");
                CaptureClose("07_carry_idle_close", visual.transform, 2.2f);
                float grip = player.Grip.Percent;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                yield return new WaitForSeconds(0.7f);
                Capture("08_carry_walk_left");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(0.3f);
                Assert.That(player.Grip.Percent, Is.GreaterThanOrEqualTo(grip - 0.01f), "walking does not drain grip more than before");

                // --- existing drop
                body.position = new Vector3(0f, 1f, -4f);
                yield return new WaitForSeconds(0.15f);
                float condition = cargo.Condition.Current;
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.Null);
                Assert.That(cargo.Condition.Current, Is.EqualTo(condition));
                yield return new WaitForSeconds(0.25f);
                Capture("09_drop");
                yield return new WaitForSeconds(0.8f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), Is.True);
                Assert.That(visual.Weapon.parent, Is.SameAs(visual.HandSocket));

                // --- existing extraction
                yield return Tap(keyboard);
                Assert.That(player.CarriedCargo, Is.SameAs(cargo));
                body.position = extraction.transform.position;
                yield return new WaitForSeconds(0.2f);
                Assert.That(extraction.IsComplete, Is.True);
                Assert.That(extraction.RecoveryFailed, Is.False);
                yield return new WaitForSeconds(0.9f);
                Capture("10_after_extraction");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        private static Bounds Measure(SurvivorVisual visual)
        {
            // Exact posed vertex bounds (renderer.bounds of skinned meshes is a loose rotated box).
            var mesh = new Mesh();
            Bounds bounds = default;
            bool first = true;
            foreach (SkinnedMeshRenderer skinned in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinned.BakeMesh(mesh, true);
                Matrix4x4 matrix = skinned.transform.localToWorldMatrix;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(vertex);
                    if (first) bounds = new Bounds(point, Vector3.zero); else bounds.Encapsulate(point);
                    first = false;
                }
            }
            Object.Destroy(mesh);
            return bounds;
        }

        internal static void CaptureClose(string name, Transform target, float size = 1.5f)
        {
            Camera main = Camera.main;
            float previousSize = main.orthographicSize;
            Vector3 previousPosition = main.transform.position;
            main.orthographicSize = size;
            main.transform.position = target.position + Vector3.up * 0.95f - main.transform.forward * 30f;
            Capture(name, 900, 900);
            main.orthographicSize = previousSize;
            main.transform.position = previousPosition;
        }

        private static IEnumerator Tap(Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        internal static void Capture(string name, int width = 1280, int height = 720)
        {
            Camera camera = Camera.main;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Directory.CreateDirectory(OutputFolder);
            File.WriteAllBytes(Path.Combine(OutputFolder, name + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
        }
    }
}
#endif
