#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Combat;
using CargoSquad.Cargo;
using CargoSquad.Player;
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
    public sealed class SurvivorDamageFeedbackTests
    {
        [UnityTest] public IEnumerator HeavyWolfBite() => Verify<MutatedWolfAI>(0, "Heavy-Wolf", false, 1.45f);
        [UnityTest] public IEnumerator HeavyArmorSwipe() => Verify<ArmoredCreatureAI>(0, "Heavy-Armor", true, 1.66f);
        [UnityTest] public IEnumerator HeavyAcidSpit() => Verify<AcidSpitterAI>(0, "Heavy-Acid", false, 5f);
        [UnityTest] public IEnumerator MedicWolfBite() => Verify<MutatedWolfAI>(1, "Medic-Wolf", false, 1.45f);
        [UnityTest] public IEnumerator MedicArmorSwipe() => Verify<ArmoredCreatureAI>(1, "Medic-Armor", true, 1.66f);
        [UnityTest] public IEnumerator MedicAcidSpit() => Verify<AcidSpitterAI>(1, "Medic-Acid", false, 5f);

        [UnityTest]
        public IEnumerator LowHealthVignetteUsesStrictHalfHealthThreshold()
        {
            for (int character = 0; character < 2; character++)
            {
                EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialExpansion.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                yield return null;
                Object.FindFirstObjectByType<MutatedWolfAI>().enabled = false;
                Object.FindFirstObjectByType<ArmoredCreatureAI>().enabled = false;
                Object.FindFirstObjectByType<AcidSpitterAI>().enabled = false;
                var selection = Object.FindFirstObjectByType<CharacterSelection>();
                selection.SelectCharacter(character);
                selection.StartSelectedCharacter();
                var health = Object.FindFirstObjectByType<SurvivorDamageFeedback>().GetComponent<Health>();
                var effect = Camera.main.GetComponent<LowHealthFeedback>();
                Assert.That(effect, Is.Not.Null);

                health.ApplyDamageFrom(health.Current - health.Maximum * .51f, Camera.main.transform.right);
                Assert.That(Object.FindFirstObjectByType<SurvivorDamageIndicator>().Direction.x, Is.GreaterThan(.99f));
                Assert.That(selection.ActiveVisual.GetComponentsInChildren<Renderer>()
                    .Any(r => r.sharedMaterial && r.sharedMaterial.name == "Survivor Hit Flash"));
                yield return null;
                Assert.That(effect.Intensity, Is.Zero, "51% HP");

                health.ApplyDamage(health.Current - health.Maximum * .50f);
                yield return null;
                Assert.That(effect.Intensity, Is.Zero, "50% HP");

                health.ApplyDamage(health.Current - health.Maximum * .49f);
                yield return null;
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(effect.Intensity, Is.GreaterThan(.9f), "49% HP");
                Assert.That(effect.Saturation, Is.Zero);
                if (character == 0)
                    yield return IndustrialExpansionScreenshots.Capture("step6-heavy-49-percent");

                health.Restore(health.Maximum * .50f - health.Current);
                yield return null;
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(effect.Intensity, Is.LessThan(.05f), "Healing to 50% fades the vignette");
                health.Restore(health.Maximum * .51f - health.Current);
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.That(effect.Intensity, Is.Zero, "Healing above 50% clears the vignette");
            }
        }

        [UnityTest]
        public IEnumerator CargoPickupDustPlaysOnlyAtSuccessfulPickup()
        {
            var background = InputSystem.settings.backgroundBehavior;
            var inputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                for (int character = 0; character < 2; character++)
                {
                    EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialExpansion.unity",
                        new LoadSceneParameters(LoadSceneMode.Single));
                    yield return null;
                    yield return null;
                    Object.FindFirstObjectByType<MutatedWolfAI>().enabled = false;
                    Object.FindFirstObjectByType<ArmoredCreatureAI>().enabled = false;
                    Object.FindFirstObjectByType<AcidSpitterAI>().gameObject.SetActive(false);
                    var selection = Object.FindFirstObjectByType<CharacterSelection>();
                    selection.SelectCharacter(character);
                    selection.StartSelectedCharacter();

                    var carrier = Object.FindFirstObjectByType<CargoInteractor>();
                    var cargo = Object.FindFirstObjectByType<CargoItem>();
                    var dust = carrier.GetComponentInChildren<ParticleSystem>();
                    Assert.That(dust && dust.name == "Cargo Pickup Dust Particles");
                    var body = carrier.GetComponent<Rigidbody>();

                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    Assert.That(carrier.CarriedCargo, Is.Null);
                    Assert.That(dust.particleCount, Is.Zero);

                    Bounds originalBounds = cargo.Collider.bounds;
                    Vector3 position = new Vector3(originalBounds.min.x - .95f, body.position.y, originalBounds.center.z);
                    body.position = position;
                    carrier.transform.position = position;
                    Physics.SyncTransforms();
                    yield return null;
                    int pickupEvents = 0;
                    carrier.CargoPickedUp += bounds =>
                    {
                        Assert.That(Vector3.Distance(bounds.center, originalBounds.center), Is.LessThan(.05f));
                        pickupEvents++;
                    };
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
                    Assert.That(cargo.IsCarried, Is.True);
                    Assert.That(pickupEvents, Is.EqualTo(1));
                    Assert.That(dust.particleCount, Is.EqualTo(10));
                    var particles = new ParticleSystem.Particle[10];
                    dust.GetParticles(particles);
                    Assert.That(particles.All(p => Vector3.Distance(p.position, originalBounds.center) < 1.1f));
                    if (character == 0)
                    {
                        yield return new WaitForSeconds(.06f);
                        yield return IndustrialExpansionScreenshots.Capture("step7-cargo-pickup-dust");
                    }
                    yield return new WaitForSeconds(.45f);
                    Assert.That(dust.particleCount, Is.Zero);
                    Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = background;
                InputSystem.settings.editorInputBehaviorInPlayMode = inputBehavior;
            }
        }

        static IEnumerator Verify<T>(int character, string label, bool powerful, float range) where T : MonoBehaviour
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialExpansion.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            var enemy = Object.FindFirstObjectByType<T>();
            foreach (var other in new MonoBehaviour[] {
                Object.FindFirstObjectByType<MutatedWolfAI>(), Object.FindFirstObjectByType<ArmoredCreatureAI>(),
                Object.FindFirstObjectByType<AcidSpitterAI>() })
                if (other != enemy) other.enabled = false;

            var selection = Object.FindFirstObjectByType<CharacterSelection>();
            var player = Object.FindFirstObjectByType<SurvivorDamageFeedback>();
            var body = player.GetComponent<Rigidbody>();
            var health = player.GetComponent<Health>();
            var camera = Camera.main;
            var follow = camera.GetComponent<TopDownCameraFollow>();
            int hits = 0;
            bool receivedPower = false;
            Vector3? receivedDirection = null;
            health.HitReceived += (direction, power) => { hits++; receivedPower = power; receivedDirection = direction; };
            Vector3 spot = FindPosition(enemy.transform, range);
            body.position = spot;
            player.transform.position = spot;
            Physics.SyncTransforms();
            selection.SelectCharacter(character);
            selection.StartSelectedCharacter();
            float hp = health.Current;
            var original = selection.ActiveVisual.GetComponentsInChildren<Renderer>()
                .Where(r => r.enabled && r is MeshRenderer or SkinnedMeshRenderer)
                .ToDictionary(r => r, r => r.sharedMaterials);
            health.ApplyDamage(0f);
            Assert.That(hits, Is.Zero);
            Assert.That(Object.FindFirstObjectByType<SurvivorDamageIndicator>(), Is.Null);
            yield return IndustrialExpansionScreenshots.Capture(label + "-before");

            float limit = Time.realtimeSinceStartup + 8f;
            while (hits == 0 && Time.realtimeSinceStartup < limit) yield return null;
            Assert.That(hits, Is.EqualTo(1), label);
            Assert.That(health.Current, Is.LessThan(hp));
            Assert.That(receivedDirection.HasValue);
            Assert.That(receivedPower, Is.EqualTo(powerful));
            Vector3 direction = receivedDirection.Value;
            Assert.That(Vector3.Dot(direction.normalized, (enemy.transform.position - player.transform.position).normalized),
                Is.GreaterThan(.65f));
            var indicator = Object.FindFirstObjectByType<SurvivorDamageIndicator>();
            Assert.That(indicator && indicator.enabled && !indicator.raycastTarget);
            Assert.That(indicator.GetComponent<CanvasRenderer>(), Is.Not.Null);
            var hud = camera.GetComponent<CargoMissionOverlay>();
            Assert.That(indicator.transform.parent, Is.EqualTo(hud.DamageIndicatorRoot));
            var projected = new Vector2(Vector3.Dot(direction, camera.transform.right),
                Vector3.Dot(direction, camera.transform.up)).normalized;
            Assert.That(Vector2.Dot(indicator.Direction, projected), Is.GreaterThan(.98f));
            Assert.That(original.Keys.Any(r => r.sharedMaterial && r.sharedMaterial.name == "Survivor Hit Flash"));
            if (enemy is AcidSpitterAI)
                Assert.That(player.GetComponentInChildren<AcidImpactSplash>(), Is.Not.Null);
            enemy.enabled = false; // Isolate one hit through its visual cleanup.
            float maxShake = Vector3.Distance(camera.transform.position, follow.FollowPosition);
            var rotation = camera.transform.rotation;
            yield return IndustrialExpansionScreenshots.Capture(label + "-hit");
            for (int frame = 0; frame < 4; frame++)
            {
                maxShake = Mathf.Max(maxShake, Vector3.Distance(camera.transform.position, follow.FollowPosition));
                yield return null;
            }
            Assert.That(maxShake, powerful ? Is.InRange(.015f, .15f) : Is.EqualTo(0f).Within(.001f));
            Debug.Log($"{label}: peak sampled camera offset {maxShake:F3} m");
            yield return new WaitForSecondsRealtime(.55f);
            foreach (var entry in original) CollectionAssert.AreEqual(entry.Value, entry.Key.sharedMaterials);
            Assert.That(indicator.enabled, Is.False);
            Assert.That(Object.FindObjectsByType<SurvivorDamageIndicator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Vector3.Distance(camera.transform.position, follow.FollowPosition), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, rotation), Is.LessThan(.01f));
            yield return IndustrialExpansionScreenshots.Capture(label + "-after");

            health.ApplyDamage(1f);
            Assert.That(indicator.enabled && indicator.Direction == Vector2.zero, "Unknown source uses a generic ring.");
            player.enabled = false;
            Assert.That(indicator.enabled, Is.False);
            foreach (var entry in original) CollectionAssert.AreEqual(entry.Value, entry.Key.sharedMaterials);
        }

        static Vector3 FindPosition(Transform enemy, float range)
        {
            for (int i = 0; i < 16; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.back * range;
                if (!NavMesh.SamplePosition(enemy.position + offset, out var hit, .25f, NavMesh.AllAreas)) continue;
                Vector3 spot = hit.position + Vector3.up;
                if (!Physics.Linecast(spot, enemy.position + Vector3.up, out var obstruction,
                        ~0, QueryTriggerInteraction.Ignore) || obstruction.collider.transform.IsChildOf(enemy)) return spot;
            }
            Assert.Fail("No clear attack lane for " + enemy.name);
            return default;
        }
    }
}
#endif
