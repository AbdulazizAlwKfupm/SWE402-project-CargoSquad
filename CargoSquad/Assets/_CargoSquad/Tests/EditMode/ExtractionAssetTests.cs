using CargoSquad.Cargo;
using CargoSquad.Missions;
using CargoSquad.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CargoSquad.Tests
{
    public sealed class ExtractionAssetTests
    {
        [Test]
        public void BeaconPrefabIsDecorativeAndUsesAssignedUrpMaterial()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_CargoSquad/Prefabs/Environment/ExtractionMarker.prefab");
            Assert.That(prefab, Is.Not.Null); Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<CargoExtraction>(true), Is.Empty);
            var settings = new SerializedObject(prefab.GetComponent<ExtractionMarker>());
            Assert.That(settings.FindProperty("markerHeight").floatValue, Is.EqualTo(2.8f));
            Assert.That(settings.FindProperty("markerRadius").floatValue, Is.EqualTo(1.625f));
            Assert.That(settings.FindProperty("transparency").floatValue, Is.EqualTo(.15f));
            var material = settings.FindProperty("hologramMaterial").objectReferenceValue as Material;
            Assert.That(material, Is.Not.Null); Assert.That(material.shader.name, Is.EqualTo("Cargo Squad/Combat Indicator"));
            Assert.That(material.renderQueue, Is.GreaterThanOrEqualTo(3000));
        }
        [Test]
        public void ReusableBrownCrateContainsNoGameplayAndHasItsOwnMatteTexture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_CargoSquad/Prefabs/Environment/CargoCrateVisual.prefab");
            Assert.That(prefab, Is.Not.Null); Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<CargoItem>(true), Is.Empty);
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            var material = prefab.GetComponent<Renderer>().sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(.12f));
            Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(material), Does.EndWith("CargoWeatheredBrown.mat"));
        }
        [Test]
        public void ResultPrefabHasOnlyPresentationAndNoDuplicateMissionController()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_CargoSquad/Prefabs/Environment/MissionResultPanel.prefab");
            Assert.That(prefab, Is.Not.Null); Assert.That(prefab.GetComponent<CargoSquad.UI.MissionResultPanel>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<CargoExtraction>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
    }
}
