#if UNITY_EDITOR
using CargoSquad.Combat;
using NUnit.Framework;
using UnityEditor;

namespace CargoSquad.Tests
{
    public sealed class EnemyHitFlashAssetTests
    {
        [TestCase("Wolf")]
        [TestCase("Armored")]
        [TestCase("Spitter")]
        public void ImpactPrefabHasSupportedFlashAndOriginalParticleSystems(string enemy)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<EnemyDamageBurst>(
                $"Assets/_CargoSquad/VFX/Combat/EnemyDamage/{enemy}DamageImpact.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.HitFlash.shader, Is.Not.Null);
            Assert.That(prefab.HitFlash.shader.isSupported);
            Assert.That(ShaderUtil.ShaderHasError(prefab.HitFlash.shader), Is.False);
            Assert.That(prefab.HitFlash.duration, Is.EqualTo(.12f).Within(.001f));
            Assert.That(prefab.GetComponentsInChildren<UnityEngine.ParticleSystem>(true).Length, Is.EqualTo(2));
        }
    }
}
#endif
