#if UNITY_EDITOR
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Combat;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CargoSquad.Tests
{
    public sealed class SpitterDeathAssetTests
    {
        [Test]
        public void ExpansionSpitterHasDeathAndAttackVfxAssigned()
        {
            const string path = "Assets/_CargoSquad/Scenes/IndustrialExpansion.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var ai = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<AcidSpitterAI>()).Single();
                var feedback = new SerializedObject(ai.GetComponent<SpitterDeathFeedback>());
                var death = feedback.FindProperty("effectPrefab").objectReferenceValue as SpitterDeathBurst;
                Assert.That(death, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(death), Does.EndWith("/AcidSpitterDeath.prefab"));
                var systems = death.GetComponentsInChildren<ParticleSystem>();
                Assert.That(systems.Length, Is.EqualTo(3));
                Assert.That(systems.All(p => !p.main.playOnAwake && !p.emission.enabled &&
                    p.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported));
                Assert.That(death.GetComponentsInChildren<Collider>(), Is.Empty);
                Assert.That(death.GetComponentsInChildren<Rigidbody>(), Is.Empty);
                Assert.That(death.GetComponentsInChildren<AcidGlob>(), Is.Empty);
                Assert.That(new SerializedObject(death).FindProperty("vaporPhase").floatValue,
                    Is.EqualTo(33f / 65f).Within(.001f));
                var data = new SerializedObject(ai);
                var shot = data.FindProperty("projectile").objectReferenceValue as AcidGlob;
                Assert.That(shot, Is.Not.Null);
                Assert.That(new SerializedObject(shot).FindProperty("impactSplash").objectReferenceValue, Is.Not.Null);
                Assert.That(shot.GetComponentInChildren<ParticleSystem>(), Is.Not.Null);
                Assert.That(data.FindProperty("spitDamage").floatValue, Is.EqualTo(14f));
                Assert.That(data.FindProperty("swipeDamage").floatValue, Is.EqualTo(11f));
                Assert.That(new SerializedObject(ai.GetComponent<EnemyDamageFeedback>())
                    .FindProperty("effectPrefab").objectReferenceValue, Is.Not.Null);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
