#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Cargo;
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
    public sealed class SpitterDeathCameraTests
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
            var spitter = Object.FindFirstObjectByType<AcidSpitterAI>();
            // Keep unrelated attacks out of the survivor/cargo safety assertions.
            Object.FindFirstObjectByType<MutatedWolfAI>().enabled = false;
            Object.FindFirstObjectByType<ArmoredCreatureAI>().enabled = false;
            var hp = spitter.GetComponent<Health>();
            var skin = spitter.GetComponentInChildren<SkinnedMeshRenderer>();
            var animator = spitter.GetComponentInChildren<Animator>();
            Assert.That(spitter.GetComponent<SpitterDeathFeedback>(), Is.Not.Null);
            Assert.That(spitter.GetComponent<EnemyDamageFeedback>(), Is.Not.Null);
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
            // Keep this capture at the normal zoom and keep the spitter in the shot lane.
            
            Assert.That(Vector3.Distance(spitter.transform.position, new Vector3(0,0,5.8f)), Is.LessThan(.1f));
            Assert.That(spitter.TryStun(15f));
            Vector3 position = spitter.transform.position + Vector3.up - Vector3.forward * 2.65f;
            weapon.GetComponent<Rigidbody>().position = position;
            weapon.transform.position = position;
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.36f);

            var original = skin.sharedMaterials;
            Assert.That(spitter.GetComponent<Collider>().enabled);
            var survivor = weapon.GetComponent<Health>();
            float survivorHP = survivor.Current;
            var cargo = Object.FindFirstObjectByType<CargoCondition>();
            Assert.That(cargo, Is.Not.Null);
            float cargoCondition = cargo.Current;
            Assert.That(Object.FindFirstObjectByType<AcidGlob>(), Is.Null);
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
                Assert.That(weapon.AimAtScreen(camera.WorldToScreenPoint(spitter.transform.position)));
                Assert.That(weapon.TryFire());
                Assert.That(hp.Current, Is.LessThan(previous));
                if (hp.IsDead) break;
                Assert.That(Object.FindFirstObjectByType<SpitterDeathBurst>(), Is.Null);
                Assert.That(spitter.GetComponent<EnemyHitFlash>(), Is.Not.Null);
                Assert.That(spitter.GetComponentInChildren<EnemyDamageBurst>(), Is.Not.Null);
                Assert.That(skin.sharedMaterial.shader.name, Is.EqualTo("CargoSquad/Enemy Hit Flash"));
                yield return new WaitForSecondsRealtime(.85f);
            }

            Assert.That(hp.IsDead, "Heavy must kill the Acid Spitter using shotgun damage.");
            Assert.That(spitter.State, Is.EqualTo(SpitterState.Death));
            // Step Heavy out of the collapse silhouette without changing the gameplay camera.
            var body = weapon.GetComponent<Rigidbody>();
            body.position -= Vector3.forward * 2.5f;
            weapon.transform.position = body.position;
            Physics.SyncTransforms();
            var burst = Object.FindObjectsByType<SpitterDeathBurst>(FindObjectsSortMode.None).Single();
            var systems = burst.GetComponentsInChildren<ParticleSystem>().ToDictionary(p => p.name);
            hp.ApplyDamage(10f);
            Assert.That(Object.FindObjectsByType<SpitterDeathBurst>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(systems["Reservoir Rupture"].particleCount, Is.GreaterThan(0));
            Assert.That(systems["Acid Liquid Spray"].particleCount, Is.GreaterThan(0));
            Assert.That(systems["Toxic Vapor"].particleCount, Is.Zero);
            Capture(camera, "02-reservoir-rupture");
            yield return new WaitForSecondsRealtime(.16f);
            Assert.That(systems["Acid Liquid Spray"].particleCount, Is.GreaterThan(0));
            Capture(camera, "03-liquid-spray");

            float deadline = Time.realtimeSinceStartup + 3f;
            while (systems["Toxic Vapor"].particleCount == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(systems["Toxic Vapor"].particleCount, Is.GreaterThan(0));
            var pose = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(pose.IsName("Death"));
            Assert.That(pose.normalizedTime, Is.GreaterThanOrEqualTo(33f / 65f));
            yield return new WaitForSecondsRealtime(.21f);
            Assert.That(systems["Toxic Vapor"].particleCount, Is.GreaterThan(0));
            Capture(camera, "04-toxic-vapor");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(!burst);
            Assert.That(skin.enabled && skin.gameObject.activeInHierarchy);
            Assert.That(!spitter.GetComponent<Collider>().enabled, "Only the original AI disables the corpse collider.");
            CollectionAssert.AreEqual(original, skin.sharedMaterials);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"));
            Assert.That(survivor.Current, Is.EqualTo(survivorHP));
            Assert.That(cargo.Current, Is.EqualTo(cargoCondition));
            Assert.That(Object.FindFirstObjectByType<AcidGlob>(), Is.Null);
            Capture(camera, "05-after");
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
                const string dir = "ValidationResults/SpitterDeathVFX";
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
