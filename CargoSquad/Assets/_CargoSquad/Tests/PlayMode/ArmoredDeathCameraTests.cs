#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Combat;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CargoSquad.Tests
{
    public sealed class ArmoredDeathCameraTests
    {
        Mouse mouse;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ShotgunKillPlaysBothDeathPhasesExactlyOnce()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialExpansion.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            var armored = Object.FindFirstObjectByType<ArmoredCreatureAI>();
            var hp = armored.GetComponent<Health>();
            var skin = armored.GetComponentInChildren<SkinnedMeshRenderer>();
            var animator = armored.GetComponentInChildren<Animator>();
            Assert.That(armored.GetComponent<ArmoredDeathFeedback>(), Is.Not.Null);
            Assert.That(armored.GetComponent<EnemyDamageFeedback>(), Is.Not.Null);
            var selection = Object.FindFirstObjectByType<CharacterSelection>();
            selection.SelectCharacter(0);
            selection.StartSelectedCharacter();
            var weapon = Object.FindFirstObjectByType<PlayerWeapon>();
            var camera = Camera.main;
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = new Vector2(Screen.width * .5f, Screen.height * .5f)
            });
            // Keep this capture at the normal zoom and keep the armored in the shot lane.
            
            Vector3 position = armored.transform.position + Vector3.up - Vector3.forward * 4.35f;
            weapon.GetComponent<Rigidbody>().position = position;
            weapon.transform.position = position;
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.36f);

            float approachDeadline = Time.realtimeSinceStartup + 4f;
            while (Vector3.Distance(position, armored.transform.position) > 2.25f &&
                   Time.realtimeSinceStartup < approachDeadline)
                yield return null;
            Assert.That(armored.TryStun(15f));
            var original = skin.sharedMaterials;
            var groundSmash = armored.GetComponent<GroundSmashFeedback>();
            Assert.That(groundSmash, Is.Not.Null);
            Assert.That(armored.GetComponent<Collider>().enabled);
            Capture(camera, "01-before");
            for (int shots = 0; shots < 12 && !hp.IsDead; shots++)
            {
                if (weapon.Magazine == 0)
                {
                    Assert.That(weapon.TryReload());
                    float reloadDeadline = Time.realtimeSinceStartup + 8f;
                    while (weapon.Reloading && Time.realtimeSinceStartup < reloadDeadline) yield return null;
                    Assert.That(weapon.Magazine, Is.GreaterThan(0));
                }
                float previous = hp.Current;
                Assert.That(weapon.AimAtScreen(camera.WorldToScreenPoint(armored.transform.position)));
                Assert.That(weapon.TryFire());
                Assert.That(hp.Current, Is.LessThan(previous));
                if (hp.IsDead) break;
                Assert.That(Object.FindFirstObjectByType<ArmoredDeathBurst>(), Is.Null);
                Assert.That(armored.GetComponent<EnemyHitFlash>(), Is.Not.Null);
                Assert.That(armored.GetComponentInChildren<EnemyDamageBurst>(), Is.Not.Null);
                Assert.That(skin.sharedMaterial.shader.name, Is.EqualTo("CargoSquad/Enemy Hit Flash"));
                yield return new WaitForSecondsRealtime(.85f);
            }

            Assert.That(hp.IsDead, "Heavy must kill the Armored Creature using shotgun damage.");
            Assert.That(armored.State, Is.EqualTo(ArmoredState.Death));
            // Step Heavy out of the collapse silhouette without changing the gameplay camera.
            var body = weapon.GetComponent<Rigidbody>();
            body.position -= Vector3.forward * 2.5f;
            weapon.transform.position = body.position;
            Physics.SyncTransforms();
            var burst = Object.FindObjectsByType<ArmoredDeathBurst>(FindObjectsSortMode.None).Single();
            var systems = burst.GetComponentsInChildren<ParticleSystem>().ToDictionary(p => p.name);
            hp.ApplyDamage(10f);
            Assert.That(Object.FindObjectsByType<ArmoredDeathBurst>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(systems["Metallic Sparks"].particleCount, Is.GreaterThan(0));
            Assert.That(systems["Armor Fragments"].particleCount, Is.GreaterThan(0));
            Assert.That(systems["Heavy Collapse Dust"].particleCount, Is.Zero);
            Capture(camera, "02-sparks-and-fragments");

            float deadline = Time.realtimeSinceStartup + 3f;
            while (systems["Heavy Collapse Dust"].particleCount == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(systems["Heavy Collapse Dust"].particleCount, Is.GreaterThan(0));
            var pose = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(pose.IsName("Death"));
            Assert.That(pose.normalizedTime, Is.GreaterThanOrEqualTo(44f / 65f));
            yield return new WaitForSecondsRealtime(.21f);
            Assert.That(systems["Heavy Collapse Dust"].particleCount, Is.GreaterThan(0));
            Capture(camera, "03-heavy-collapse-dust");
            yield return new WaitForSecondsRealtime(.90f);
            Assert.That(!burst);
            Assert.That(skin.enabled && skin.gameObject.activeInHierarchy);
            Assert.That(armored.GetComponent<GroundSmashFeedback>(), Is.SameAs(groundSmash));
            Assert.That(!armored.GetComponent<Collider>().enabled, "Only the original AI disables the corpse collider.");
            CollectionAssert.AreEqual(original, skin.sharedMaterials);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"));
            Capture(camera, "04-after");
        }

        static void Capture(Camera camera, string name)
        {
            const int width = 1280, height = 800;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                const string dir = "ValidationResults/ArmoredDeathVFX";
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(image);
            }
        }
    }
}
#endif
