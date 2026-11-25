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
    public sealed class WolfDeathCameraTests
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
            var wolf = Object.FindFirstObjectByType<MutatedWolfAI>();
            var hp = wolf.GetComponent<Health>();
            var skin = wolf.GetComponentInChildren<SkinnedMeshRenderer>();
            var animator = wolf.GetComponentInChildren<Animator>();
            Assert.That(wolf.GetComponent<WolfDeathFeedback>(), Is.Not.Null);
            Assert.That(wolf.GetComponent<EnemyDamageFeedback>(), Is.Not.Null);
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
            // Keep this capture at the normal zoom and keep the wolf in the shot lane.
            Assert.That(wolf.TryStun(6f));
            Vector3 position = wolf.transform.position + Vector3.up - Vector3.right * 2.55f;
            weapon.GetComponent<Rigidbody>().position = position;
            weapon.transform.position = position;
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.36f);

            var original = skin.sharedMaterials;
            Capture(camera, "01-before");
            for (int shots = 0; shots < 5 && !hp.IsDead; shots++)
            {
                float previous = hp.Current;
                Assert.That(weapon.AimAtScreen(camera.WorldToScreenPoint(wolf.transform.position)));
                Assert.That(weapon.TryFire());
                Assert.That(hp.Current, Is.LessThan(previous));
                if (hp.IsDead) break;
                Assert.That(Object.FindFirstObjectByType<WolfDeathBurst>(), Is.Null);
                Assert.That(wolf.GetComponent<EnemyHitFlash>(), Is.Not.Null);
                Assert.That(wolf.GetComponentInChildren<EnemyDamageBurst>(), Is.Not.Null);
                Assert.That(skin.sharedMaterial.shader.name, Is.EqualTo("CargoSquad/Enemy Hit Flash"));
                yield return new WaitForSecondsRealtime(.85f);
            }

            Assert.That(hp.IsDead, "Heavy must kill the wolf using shotgun damage.");
            Assert.That(wolf.State, Is.EqualTo(WolfState.Death));
            // Step Heavy out of the collapse silhouette without changing the gameplay camera.
            var body = weapon.GetComponent<Rigidbody>();
            body.position -= Vector3.right * 1.6f;
            weapon.transform.position = body.position;
            Physics.SyncTransforms();
            var burst = Object.FindObjectsByType<WolfDeathBurst>(FindObjectsSortMode.None).Single();
            var systems = burst.GetComponentsInChildren<ParticleSystem>().ToDictionary(p => p.name);
            hp.ApplyDamage(10f);
            Assert.That(Object.FindObjectsByType<WolfDeathBurst>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(systems["Crimson Burst"].particleCount, Is.GreaterThan(0));
            Assert.That(systems["Infected Fragments"].particleCount, Is.GreaterThan(0));
            Assert.That(systems["Ground Contact Dust"].particleCount, Is.Zero);
            Capture(camera, "02-crimson-and-fragments");

            float deadline = Time.realtimeSinceStartup + 3f;
            while (systems["Ground Contact Dust"].particleCount == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(systems["Ground Contact Dust"].particleCount, Is.GreaterThan(0));
            var pose = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(pose.IsName("Death"));
            Assert.That(pose.normalizedTime, Is.GreaterThanOrEqualTo(36f / 51f));
            yield return new WaitForSecondsRealtime(.21f);
            Assert.That(systems["Mutated Mist"].particleCount, Is.GreaterThan(0));
            Capture(camera, "03-ground-contact-mist");
            yield return new WaitForSecondsRealtime(.90f);
            Assert.That(!burst);
            Assert.That(skin.enabled && skin.gameObject.activeInHierarchy);
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
                const string dir = "ValidationResults/WolfDeathVFX";
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
