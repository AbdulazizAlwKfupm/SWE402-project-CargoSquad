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
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CargoSquad.Tests
{
    public sealed class EnemyHitFlashCameraTests
    {
        Mouse mouse;

        [UnityTearDown]
        public IEnumerator RemoveTestMouse()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            yield return null;
        }

        [UnityTest] public IEnumerator WolfFlashesOnShot() => Check<MutatedWolfAI>("Wolf");
        [UnityTest] public IEnumerator ArmorFlashesOnShot() => Check<ArmoredCreatureAI>("Armored");
        [UnityTest] public IEnumerator SpitterFlashesOnShot() => Check<AcidSpitterAI>("Spitter");

        IEnumerator Check<T>(string label) where T : MonoBehaviour
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialExpansion.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            var enemy = Object.FindFirstObjectByType<T>();
            Assert.That(enemy, Is.Not.Null);
            enemy.enabled = false; // Keep the shot and before/after crops repeatable.
            var health = enemy.GetComponent<Health>();
            var selection = Object.FindFirstObjectByType<CharacterSelection>();
            selection.SelectCharacter(1);
            selection.StartSelectedCharacter();
            var weapon = Object.FindFirstObjectByType<PlayerWeapon>();
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState
            {
                position = new Vector2(Screen.width * .5f, Screen.height * .5f)
            });
            var body = weapon.GetComponent<Rigidbody>();
            var camera = Camera.main;
            var origin = FindFiringPosition(enemy.transform);
            body.position = origin;
            weapon.transform.position = origin;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.36f);
            var skin = enemy.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(skin, Is.Not.Null);
            var original = skin.sharedMaterials;
            health.ApplyDamage(0f);
            Assert.That(enemy.GetComponent<EnemyHitFlash>(), Is.Null);
            var before = Capture(camera, skin.bounds.center, label + "-before");
            Assert.That(weapon.AimAtScreen(camera.WorldToScreenPoint(enemy.transform.position)));
            float hp = health.Current;
            Assert.That(weapon.CanUse, "Medic must be deployed and able to use the weapon.");
            Assert.That(weapon.TryFire(), "Successful shot from the world rather than the HUD.");
            Assert.That(health.Current, Is.LessThan(hp), "The real Medic shot must hit the target.");
            var flash = enemy.GetComponent<EnemyHitFlash>();
            Assert.That(flash, Is.Not.Null, "Confirmed HP loss must reach the flash.");
            var shells = flash.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(r => r.name == "Hit Flash Outline").ToArray();
            Assert.That(shells, Is.Not.Empty);
            var during = Capture(camera, skin.bounds.center, label + "-flash");
            Assert.That(during.white - before.white, Is.GreaterThan(70), "Rendered white fill.");
            Assert.That(during.cyan - before.cyan, Is.GreaterThan(10), "Rendered cyan outline.");
            yield return new WaitForSecondsRealtime(.17f);
            CollectionAssert.AreEqual(original, skin.sharedMaterials);
            Assert.That(shells.All(r => !r.enabled));
            Capture(camera, skin.bounds.center, label + "-restored");
            health.ApplyDamage(1f);
            Assert.That(shells.Count(r => r.enabled), Is.GreaterThan(0));
            health.ApplyDamage(health.Current);
            CollectionAssert.AreEqual(original, skin.sharedMaterials);
            Assert.That(shells.All(r => !r.enabled), "Death clears an active outline.");
        }

        static Vector3 FindFiringPosition(Transform enemy)
        {
            Vector3 chest = enemy.position + Vector3.up;
            for (int i = 0; i < 16; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.back * 2.55f;
                if (!NavMesh.SamplePosition(enemy.position + offset, out var spot, .25f, NavMesh.AllAreas))
                    continue;
                Vector3 position = spot.position + Vector3.up;
                if (Physics.Linecast(position, chest, out var hit, ~0, QueryTriggerInteraction.Ignore) &&
                    hit.collider.transform.IsChildOf(enemy)) return position;
            }
            Assert.Fail("No clear firing position for " + enemy.name);
            return default;
        }

        static (int white, int cyan) Capture(Camera camera, Vector3 focus, string name)
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
                string dir = "ValidationResults/EnemyHitFlash";
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), image.EncodeToPNG());
                Vector3 center = camera.WorldToScreenPoint(focus);
                var pixels = image.GetPixels32();
                int white = 0, cyan = 0;
                for (int y = Mathf.Max(0, (int)center.y - 75); y < Mathf.Min(height, (int)center.y + 75); y++)
                for (int x = Mathf.Max(0, (int)center.x - 75); x < Mathf.Min(width, (int)center.x + 75); x++)
                {
                    var p = pixels[y * width + x];
                    // The scene's camera maps the unlit white fill to about 213 in the captured RGB target.
                    if (p.r >= 205 && p.g >= 205 && p.b >= 205 &&
                        Mathf.Abs(p.r - p.g) <= 8 && Mathf.Abs(p.r - p.b) <= 8) white++;
                    if (p.g > 150 && p.b > 205 && p.b - p.r > 27 && p.g - p.r > 18) cyan++;
                }
                Debug.Log($"{name}: white={white}, cyan={cyan}");
                return (white, cyan);
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
