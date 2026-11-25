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
    public sealed class WolfDeathAssetTests
    {
        [Test]
        public void ExpansionSpawnerReferencesTheWolfAndBothEffects()
        {
            const string path = "Assets/_CargoSquad/Scenes/IndustrialExpansion.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var spawn = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<LocalWolfSpawn>()).Single();
                var data = new SerializedObject(spawn);
                var death = data.FindProperty("deathEffect").objectReferenceValue as WolfDeathBurst;
                Assert.That(death, Is.Not.Null);
                Assert.That(data.FindProperty("damageEffect").objectReferenceValue, Is.Not.Null);
                var wolf = (GameObject)data.FindProperty("wolfPrefab").objectReferenceValue;
                Assert.That(wolf.GetComponent<PixelWolfAnimation>(), Is.Not.Null);
                var systems = death.GetComponentsInChildren<ParticleSystem>();
                Assert.That(systems.Length, Is.EqualTo(4));
                Assert.That(systems.All(p => !p.main.playOnAwake && p.main.useUnscaledTime && !p.emission.enabled));
                Assert.That(death.GetComponentsInChildren<Collider>(), Is.Empty);
                Assert.That(new SerializedObject(death).FindProperty("groundContactPhase").floatValue,
                    Is.EqualTo(36f / 51f).Within(.001f));
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
