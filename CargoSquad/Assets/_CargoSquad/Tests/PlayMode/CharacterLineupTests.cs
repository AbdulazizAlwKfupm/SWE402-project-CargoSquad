#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CargoSquad.Visuals;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CargoSquad.Tests
{
    /// <summary>
    /// Lineup validation in CharacterShowcase: every survivor prefab has a valid shared humanoid setup and plays the
    /// shared clips; colour + flat-black silhouette captures are taken from the real 45-degree gameplay camera
    /// (orthographic size 6, 1280x720) facing the camera, travelling away, carrying, aiming and after death.
    /// </summary>
    public sealed class CharacterLineupTests
    {
        private const string Scene = "Assets/_CargoSquad/Scenes/CharacterShowcase.unity";
        private static readonly string[] Survivors = { "NavySoldier_Visual", "Heavy_Visual", "Scout_Visual", "Medic_Visual" };
        private static readonly string[] Creatures = { "MutatedWolf_Visual", "ArmoredCreature_Visual", "AcidSpitter_Visual" };

        [UnityTest]
        public IEnumerator LineupReadsFromGameplayCamera()
        {
            EditorSceneManager.LoadSceneInPlayMode(Scene, new LoadSceneParameters());
            yield return null;
            yield return null;
            Transform lineup = GameObject.Find("Character Lineup").transform;
            Camera camera = Camera.main;
            camera.GetComponent<TopDownCameraFollow>().enabled = false;
            float gameplaySize = camera.orthographicSize;
            float ground = GameObject.Find("Ground").GetComponent<BoxCollider>().bounds.max.y;

            var survivors = new List<Animator>();
            foreach (string name in Survivors)
            {
                Transform item = lineup.Find(name);
                Assert.That(item, Is.Not.Null, name + " is in the lineup");
                item.GetComponent<SurvivorVisual>().enabled = false;   // the test drives the Animator directly
                var animator = item.GetComponent<Animator>();
                Assert.That(animator.avatar && animator.avatar.isValid && animator.avatar.isHuman, Is.True, name + " humanoid avatar");
                Assert.That(animator.applyRootMotion, Is.False);
                Assert.That(item.GetComponentsInChildren<Collider>(true), Is.Empty);
                foreach (string socket in new[] { "RightHand_Weapon", "LeftHand_Support", "Back_Pack", "Back_Weapon", "Hip_Sidearm",
                             "Chest_Tool", "Cargo_Front", "Cargo_Back", "Cargo_Overhead", "Muzzle" })
                    Assert.That(item.GetComponentsInChildren<Transform>().Any(t => t.name == socket), Is.True, $"{name} socket {socket}");
                foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>())
                    foreach (Material material in renderer.sharedMaterials)
                        Assert.That(material && material.shader.name == "Universal Render Pipeline/Lit", Is.True,
                            $"URP material on {name}/{renderer.name}");
                survivors.Add(animator);
            }

            Animator[] creatures = Creatures.Select(n => lineup.Find(n)).Where(t => t).Select(t => t.GetComponent<Animator>()).ToArray();
            yield return new WaitForSeconds(0.4f);

            // --- measurements at the gameplay camera
            var widths = new Dictionary<string, float>();
            foreach (Animator animator in survivors)
            {
                Bounds bounds = Posed(animator.gameObject, true);
                float pixels = PixelHeight(camera, bounds, 720);
                widths[animator.name] = bounds.size.x;
                Debug.Log($"[lineup] {animator.name} height={bounds.max.y - ground:F2}m shoulderWidth={bounds.size.x:F2}m feetY={bounds.min.y - ground:F3} screenHeight={pixels:F0}px");
                Assert.That(bounds.min.y - ground, Is.EqualTo(0f).Within(0.06f), animator.name + " feet on ground");
                Assert.That(pixels, Is.InRange(64f, 140f), animator.name + " on-screen height at gameplay zoom");
            }
            Assert.That(widths["Heavy_Visual"], Is.GreaterThan(widths["NavySoldier_Visual"] * 1.12f), "Heavy is clearly the widest");
            Assert.That(widths["Scout_Visual"], Is.LessThan(widths["NavySoldier_Visual"] * 0.97f), "Scout is the leanest");

            Transform[] shown = survivors.Select(a => a.transform).Concat(creatures.Select(a => a.transform)).ToArray();
            Vector3 centre = new Vector3((shown.Min(t => t.position.x) + shown.Max(t => t.position.x)) * 0.5f, ground, shown[0].position.z);
            Frame(camera, centre, creatures.Length > 0 ? 6.4f : gameplaySize);
            Shots("20_lineup_front", lineup);

            // --- travelling away from the camera
            foreach (Animator animator in survivors)
            {
                animator.transform.rotation = Quaternion.identity;
                animator.SetFloat("Speed", 4f);
            }
            foreach (Animator creature in creatures)
            {
                creature.transform.rotation = Quaternion.identity;
                if (creature.HasState(0, Animator.StringToHash("Move")))
                    creature.Play("Move");
            }
            yield return new WaitForSeconds(0.55f);
            foreach (Animator animator in survivors)
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), Is.True);
            Shots("21_lineup_away_moving", lineup);

            // --- carrying (test-only stand-in crates at the existing overhead mount; gameplay cargo is not involved)
            var crates = new List<GameObject>();
            foreach (Animator animator in survivors)
            {
                animator.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                animator.SetFloat("Speed", 0f);
                animator.SetBool("Carrying", true);
                GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(crate.GetComponent<Collider>());
                crate.transform.SetParent(lineup, true);
                crate.transform.position = animator.transform.position + Vector3.up * 2.8f;
                crate.transform.localScale = Vector3.one * 1.5f;
                crates.Add(crate);
            }
            foreach (Animator creature in creatures)
            {
                creature.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                if (creature.HasState(0, Animator.StringToHash("Alert")))
                    creature.Play("Alert");
            }
            yield return new WaitForSeconds(1.2f);
            foreach (Animator animator in survivors)
            {
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("CarryLocomotion"), Is.True, animator.name + " carry state");
                float hand = animator.GetBoneTransform(HumanBodyBones.LeftHand).position.y - ground;
                Debug.Log($"[lineup] {animator.name} carry: hand height={hand:F2}m (crate bottom 2.05m)");
            }
            Shots("22_lineup_carry", lineup);
            foreach (Animator animator in survivors)
                animator.SetTrigger("Regrip");
            yield return new WaitForSeconds(0.3f);
            foreach (Animator animator in survivors)
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Regrip") || animator.IsInTransition(0), Is.True, "regrip plays");
            yield return new WaitForSeconds(1.0f);
            foreach (GameObject crate in crates)
                Object.Destroy(crate);
            foreach (Animator animator in survivors)
                animator.SetBool("Carrying", false);
            yield return new WaitForSeconds(1.2f);

            // --- aim / shoot / hit
            foreach (Animator animator in survivors)
                animator.SetBool("Aiming", true);
            foreach (Animator creature in creatures)
                if (creature.HasState(0, Animator.StringToHash("AttackWindup")))
                    creature.Play("AttackWindup");
            yield return new WaitForSeconds(0.6f);
            foreach (Animator animator in survivors)
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Aim"), Is.True, animator.name + " aim state");
            Shots("23_lineup_aim", lineup);
            foreach (Animator animator in survivors)
                animator.SetTrigger("Shoot");
            yield return new WaitForSeconds(0.15f);
            foreach (Animator animator in survivors)
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("ShootAimed") || animator.IsInTransition(0), Is.True, "shoot plays");
            yield return new WaitForSeconds(1.0f);
            foreach (Animator animator in survivors)
                animator.SetBool("Aiming", false);
            yield return new WaitForSeconds(0.4f);
            foreach (Animator animator in survivors)
                animator.SetTrigger("Hit");
            yield return new WaitForSeconds(0.2f);
            foreach (Animator animator in survivors)
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Hit") || animator.IsInTransition(0), Is.True, "hit plays");
            yield return new WaitForSeconds(0.8f);

            // --- death
            foreach (Animator animator in survivors)
                animator.SetTrigger("Death");
            foreach (Animator creature in creatures)
                if (creature.HasState(0, Animator.StringToHash("Death")))
                    creature.Play("Death");
            yield return new WaitForSeconds(1.8f);
            foreach (Animator animator in survivors)
            {
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), Is.True);
                float head = animator.GetBoneTransform(HumanBodyBones.Head).position.y - ground;
                Debug.Log($"[lineup] {animator.name} death: head height={head:F2}m");
                Assert.That(head, Is.LessThan(0.7f), animator.name + " ends on the ground");
            }
            Shots("24_lineup_death", lineup);
        }

        private static void Frame(Camera camera, Vector3 centre, float size)
        {
            camera.orthographicSize = size;
            camera.transform.position = centre + Vector3.up * 0.9f - camera.transform.forward * 30f;
        }

        private static void Shots(string name, Transform lineup)
        {
            CharacterShowcaseTests.Capture(name);
            // flat black silhouettes on white
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var black = new Material(unlit);
            black.SetColor("_BaseColor", Color.black);
            var white = new Material(unlit);
            white.SetColor("_BaseColor", Color.white);
            var saved = new Dictionary<Renderer, Material[]>();
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                saved[renderer] = renderer.sharedMaterials;
                Material swap = renderer.transform.IsChildOf(lineup) ? black : white;
                renderer.sharedMaterials = Enumerable.Repeat(swap, renderer.sharedMaterials.Length).ToArray();
            }
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            LightShadows[] shadows = lights.Select(l => l.shadows).ToArray();
            foreach (Light light in lights)
                light.shadows = LightShadows.None;
            Camera camera = Camera.main;
            Color background = camera.backgroundColor;
            CameraClearFlags flags = camera.clearFlags;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            CharacterShowcaseTests.Capture(name + "_silhouette");
            camera.clearFlags = flags;
            camera.backgroundColor = background;
            for (int i = 0; i < lights.Length; i++)
                lights[i].shadows = shadows[i];
            foreach (KeyValuePair<Renderer, Material[]> pair in saved)
                pair.Key.sharedMaterials = pair.Value;
            Object.Destroy(black);
            Object.Destroy(white);
        }

        private static Bounds Posed(GameObject root, bool bodyOnly)
        {
            var mesh = new Mesh();
            Bounds bounds = default;
            bool first = true;
            foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (bodyOnly && !skinned.name.EndsWith("_Body") && !skinned.name.EndsWith("_Gear"))
                    continue;
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

        private static float PixelHeight(Camera camera, Bounds bounds, int screenHeight)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                float y = camera.WorldToViewportPoint(corner).y;
                min = Mathf.Min(min, y);
                max = Mathf.Max(max, y);
            }
            return (max - min) * screenHeight;
        }
    }
}
#endif
