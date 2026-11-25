#if UNITY_EDITOR
using System.Linq;
using CargoSquad.Player;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CargoSquad.Tests
{
    public sealed class SurvivorDamageFeedbackAssetTests
    {
        [Test]
        public void ExpansionBindsBothSurvivorsToOneExistingHudAndCamera()
        {
            const string path = "Assets/_CargoSquad/Scenes/IndustrialExpansion.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var feedback = roots.SelectMany(x => x.GetComponentsInChildren<SurvivorDamageFeedback>(true)).Single();
                var data = new SerializedObject(feedback);
                var selection = new SerializedObject(data.FindProperty("selection").objectReferenceValue);
                Assert.That(selection.FindProperty("heavy").objectReferenceValue, Is.Not.Null);
                Assert.That(selection.FindProperty("medic").objectReferenceValue, Is.Not.Null);
                Assert.That(data.FindProperty("hud").objectReferenceValue,
                    Is.SameAs(roots.SelectMany(x => x.GetComponentsInChildren<CargoMissionOverlay>(true)).Single()));
                Assert.That(data.FindProperty("cameraFollow").objectReferenceValue, Is.Not.Null);
                Assert.That(((Shader)data.FindProperty("flashShader").objectReferenceValue).isSupported);
                Assert.That(AssetDatabase.LoadAssetAtPath<SurvivorHud>(
                    "Assets/_CargoSquad/Prefabs/UI/SurvivorHud.prefab"), Is.Not.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(
                    "Assets/_CargoSquad/Scripts/UI/LowHealthFeedback.cs"), Is.Not.Null);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
