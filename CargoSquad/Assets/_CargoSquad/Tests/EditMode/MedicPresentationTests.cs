using System.Linq;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CargoSquad.Tests
{
    public sealed class MedicPresentationTests
    {
        [Test]
        public void EveryMedicSlotRemapsToItsOwnNamedMaterial()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/_CargoSquad/Characters/Medic_Redesign_v1.fbx");
            var map = importer.GetExternalObjectMap().Where(p => p.Key.type == typeof(Material) && p.Key.name.StartsWith("MR1_")).ToArray();
            Assert.That(map.Length, Is.EqualTo(22));
            foreach (var pair in map)
            {
                Assert.That(pair.Value.name, Is.EqualTo(pair.Key.name));
                Assert.That(AssetDatabase.GetAssetPath(pair.Value), Does.StartWith("Assets/_CargoSquad/Characters/MedicMaterials/"));
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_CargoSquad/Prefabs/Characters/Medic_redisgned.prefab");
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith("Assets/_CargoSquad/Characters/MedicMaterials/"));
        }
        [Test]
        public void HeavyMaterialsRemainValidAfterMedicReimport()
        {
            var heavy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_CargoSquad/Prefabs/Characters/Heavy_new.prefab");
            foreach (var renderer in heavy.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, renderer.name + " has an unresolved material");
                    Assert.That(material.shader.name, Is.Not.EqualTo("Standard"), renderer.name + " uses a built-in shader in URP");
                }
        }
        [Test]
        public void HighResolutionSdfFontIsIncluded()
        {
            Assert.That(UiTypography.Font, Is.Not.Null);
            Assert.That(UiTypography.Font.atlasTexture.width, Is.GreaterThanOrEqualTo(1024));
            Assert.That(UiTypography.Font.material.shader.name, Does.Contain("Distance Field"));
        }
    }
}
