#if UNITY_EDITOR
using System.Linq;
using CargoSquad.AI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CargoSquad.Tests
{
    public sealed class ArmoredDeathAssetTests
    {
        [Test]
        public void ExpansionArmorHasIndependentDeathAndGroundSmashPrefabs()
        {
            const string path = "Assets/_CargoSquad/Scenes/IndustrialExpansion.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var ai = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<ArmoredCreatureAI>()).Single();
                var feedback = new SerializedObject(ai.GetComponent<ArmoredDeathFeedback>());
                var death = feedback.FindProperty("effectPrefab").objectReferenceValue as ArmoredDeathBurst;
                Assert.That(death, Is.Not.Null);
                var smash = new SerializedObject(ai.GetComponent<GroundSmashFeedback>());
                Assert.That(smash.FindProperty("impactPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(smash.FindProperty("warningPrefab").objectReferenceValue, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(death),
                    Does.EndWith("/ArmoredCreatureDeath.prefab"));
                var systems = death.GetComponentsInChildren<ParticleSystem>();
                Assert.That(systems.Length, Is.EqualTo(3));
                Assert.That(systems.All(p => !p.main.playOnAwake && !p.emission.enabled &&
                    p.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported));
                Assert.That(death.GetComponentsInChildren<Collider>(), Is.Empty);
                Assert.That(death.GetComponentsInChildren<Rigidbody>(), Is.Empty);
                Assert.That(new SerializedObject(death).FindProperty("groundContactPhase").floatValue,
                    Is.EqualTo(44f / 65f).Within(.001f));
                var armor = new SerializedObject(ai);
                Assert.That(armor.FindProperty("smashDamage").floatValue, Is.EqualTo(40f));
                Assert.That(armor.FindProperty("swipeDamage").floatValue, Is.EqualTo(27.5f));
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
