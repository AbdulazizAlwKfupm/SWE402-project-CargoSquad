#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CargoSquad.Tests
{
    public sealed class PixelWolfAnimationTests
    {
        private const string Asset = "Assets/_CargoSquad/Art/PixelMutatedWolf/PixelMutatedWolf.prefab";

        [Test]
        public void OriginalPrefabHasSevenTakesAndPointFilteredAtlases()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Asset);
            var animator = prefab.GetComponentInChildren<Animator>();
            Assert.That(animator.avatar.isValid && !animator.applyRootMotion);
            var clips = animator.runtimeAnimatorController.animationClips;
            CollectionAssert.AreEquivalent(new[] { "Idle", "Walk", "Run", "Bite", "Hurt", "Stunned", "Death" },
                clips.Select(c => c.name));
            foreach (var clip in clips)
            {
                Assert.That(clip.isLooping, Is.EqualTo(clip.name is "Idle" or "Walk" or "Run"));
                Assert.That(clip.events, Is.Empty);
            }
            Assert.That(clips.Single(c => c.name == "Bite").length, Is.EqualTo(1f).Within(.001f));
            var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(renderer.bones.All(b => b));
            var material = renderer.sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            foreach (string slot in new[] { "_BaseMap", "_EmissionMap" })
                Assert.That(material.GetTexture(slot).filterMode, Is.EqualTo(FilterMode.Point));
        }

        [UnityTest]
        public IEnumerator DamageStunRecoveryAndGroundedDeathUseTheNewClips()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            MissionResultTestSupport.KeepSimulationRunning();
            yield return null;
            foreach (var existing in Object.FindObjectsByType<MutatedWolfAI>(FindObjectsSortMode.None))
                existing.gameObject.SetActive(false);
            Object.FindFirstObjectByType<CharacterSelection>().StartSelectedCharacter();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Asset);
            var instance = Object.Instantiate(prefab, new Vector3(7, 0, -7), Quaternion.identity);
            try
            {
                yield return new WaitForSeconds(.15f);
                var wolf = instance.GetComponent<MutatedWolfAI>();
                var hp = instance.GetComponent<Health>();
                var anim = instance.GetComponentInChildren<Animator>();
                var bar = instance.GetComponent<WolfHealthIndicator>();
                hp.ApplyDamage(6);
                yield return new WaitForSeconds(.11f);
                Assert.That(anim.GetCurrentAnimatorStateInfo(0).IsName("Hurt"));
                Assert.That(bar.TargetFraction, Is.EqualTo(.94f).Within(.001f));
                Assert.That(wolf.TryStun(.5f));
                yield return new WaitForSeconds(.13f);
                Assert.That(anim.GetCurrentAnimatorStateInfo(0).IsName("Stunned"));
                yield return new WaitForSeconds(.55f);
                Assert.That(wolf.State, Is.Not.EqualTo(WolfState.Stunned));
                Assert.That(anim.GetCurrentAnimatorStateInfo(0).IsName("Stunned"), Is.False);
                hp.ApplyDamage(hp.Current);
                yield return new WaitForSeconds(1.25f);
                Assert.That(wolf.State, Is.EqualTo(WolfState.Death));
                Assert.That(anim.GetCurrentAnimatorStateInfo(0).IsName("Death"));
                Assert.That(anim.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.GreaterThanOrEqualTo(1));
                Assert.That(bar.Visible, Is.False);
                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                var baked = new Mesh();
                try
                {
                    renderer.BakeMesh(baked);
                    // BakeMesh returns metre-sized vertices in renderer axes. TransformPoint
                    // would apply the FBX centimetre conversion a second time.
                    var pose = Matrix4x4.TRS(renderer.transform.position, renderer.transform.rotation, Vector3.one);
                    var heights = baked.vertices.Select(v => pose.MultiplyPoint3x4(v).y - instance.transform.position.y).ToArray();
                    Assert.That(heights.Min(), Is.InRange(-.025f, .04f));
                    Assert.That(heights.Max(), Is.LessThan(1f));
                }
                finally { Object.Destroy(baked); }
            }
            finally { Object.Destroy(instance); }
        }
    }
}
#endif

