#if UNITY_EDITOR
using System.Collections;
using CargoSquad.AI;
using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CargoSquad.Tests
{
    public sealed class ExpansionEnemyIntegrationTests
    {
        const string ScenePath = "Assets/_CargoSquad/Scenes/IndustrialExpansion.unity";

        [UnityTest]
        public IEnumerator ApprovedEnemiesDeployAndReloadExactlyOnce()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            AssertEnemies();

            var selection = Object.FindFirstObjectByType<CharacterSelection>();
            selection.StartSelectedCharacter();
            var health = Object.FindFirstObjectByType<PlayerController>().GetComponent<Health>();
            yield return new WaitForSeconds(1.25f);
            Assert.That(health.Current, Is.EqualTo(health.Maximum), "Deployment should provide time to orient.");

            foreach (var enemy in new MonoBehaviour[] {
                Object.FindFirstObjectByType<MutatedWolfAI>(),
                Object.FindFirstObjectByType<ArmoredCreatureAI>(),
                Object.FindFirstObjectByType<AcidSpitterAI>() })
            {
                var body = health.GetComponent<Rigidbody>();
                body.position = enemy.transform.position + Vector3.up - Vector3.forward * 2.8f;
                Physics.SyncTransforms();
                yield return new WaitForSecondsRealtime(.43f);
                yield return IndustrialExpansionScreenshots.Capture("expansion-" + enemy.GetType().Name);
                var enemyHealth = enemy.GetComponent<Health>();
                enemyHealth.ApplyDamage(enemyHealth.Maximum);
                Assert.That(enemyHealth.IsDead);
                Assert.That(((IStunnable)enemy).TryStun(1f), Is.False);
            }

            Assert.That(SceneUtility.GetBuildIndexByScenePath(ScenePath), Is.GreaterThanOrEqualTo(0));
            health.ApplyDamage(health.Maximum);
            yield return null;
            var result = Object.FindFirstObjectByType<MissionResultPanel>();
            Assert.That(result.Visible);
            result.RestartButton.onClick.Invoke();
            yield return null;
            yield return null;
            yield return null;
            AssertEnemies();
            Assert.That(Object.FindFirstObjectByType<CharacterSelection>().HasStarted, Is.False);
        }

        static void AssertEnemies()
        {
            Assert.That(Object.FindObjectsByType<MutatedWolfAI>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<ArmoredCreatureAI>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<AcidSpitterAI>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            foreach (var agent in Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
                Assert.That(agent.isOnNavMesh, agent.name);
        }
    }
}
#endif
