#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CargoSquad.Tests
{
    /// <summary>One focused check of the enemy sound triggers on the real IndustrialExpansion enemies.</summary>
    public sealed class EnemyAudioTests
    {
        [UnityTest]
        public IEnumerator EnemySoundsFollowMovementAttacksDamageAndDeath()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialExpansion.unity", new LoadSceneParameters());
            yield return null;
            yield return null;
            Object.FindFirstObjectByType<CharacterSelection>().StartSelectedCharacter();
            var player = Object.FindFirstObjectByType<PlayerController>();
            var body = player.GetComponent<Rigidbody>();
            player.GetComponent<Health>().ResetMaximum(100000f); // survive the whole check
            MutatedWolfAI wolf = null;
            for (int i = 0; i < 30 && !wolf; i++) { yield return null; wolf = Object.FindFirstObjectByType<MutatedWolfAI>(); }
            var armored = Object.FindFirstObjectByType<ArmoredCreatureAI>();
            var spitter = Object.FindFirstObjectByType<AcidSpitterAI>();
            Assert.That(wolf, Is.Not.Null, "spawned wolf");
            Assert.That(armored, Is.Not.Null, "placed armored creature");
            Assert.That(spitter, Is.Not.Null, "placed acid spitter");

            foreach (var enemy in new MonoBehaviour[] { wolf, armored, spitter })
            {
                var audio = enemy.GetComponent<EnemyAudio>();
                Assert.That(audio, Is.Not.Null, enemy.name + " has EnemyAudio");
                var p = audio.Profile;
                Assert.That(p, Is.Not.Null, enemy.name + " profile");
                Assert.That(p.footstepRecording && p.stepStarts.Length > 0 && p.primaryAttack.clip && p.hurt.clip && p.death.clip,
                    Is.True, enemy.name + " clips assigned");
                if (!(enemy is MutatedWolfAI)) Assert.That(p.secondaryAttack.clip, Is.Not.Null, enemy.name + " second attack");
                var sources = enemy.GetComponents<AudioSource>();
                Assert.That(sources.Length, Is.EqualTo(3), enemy.name + " uses exactly three voices");
                Assert.That(sources.All(s => s.spatialBlend == 1f), Is.True, enemy.name + " 3D audio");
            }

            var wolfAudio = wolf.GetComponent<EnemyAudio>();
            var armoredAudio = armored.GetComponent<EnemyAudio>();
            var spitterAudio = spitter.GetComponent<EnemyAudio>();

            // Wolf patrols away from the survivor: footsteps while walking.
            yield return Until(() => wolfAudio.StepsPlayed > 0, 8f, "wolf footsteps while patrolling");

            // Armored creature: charge (footsteps), then swipe and smash at their contact frames.
            Place(body, armored.transform.position + new Vector3(0, 1, -6f));
            yield return Until(() => armoredAudio.StepsPlayed > 0, 6f, "armored footsteps while charging");
            yield return Until(() => armoredAudio.AttacksPlayed >= 2, 12f, "armored swipe and smash");

            // Acid spitter: approach from range (footsteps), spit, then melee swipe up close.
            Place(body, spitter.transform.position + new Vector3(0, 1, -9.5f));
            yield return Until(() => spitterAudio.StepsPlayed > 0, 6f, "spitter footsteps while running");
            yield return Until(() => spitterAudio.AttacksPlayed >= 1, 10f, "spitter acid launch");
            int spits = spitterAudio.AttacksPlayed;
            Place(body, spitter.transform.position + new Vector3(0, 1, -1.3f));
            yield return Until(() => spitterAudio.AttacksPlayed > spits, 10f, "spitter claw swipe");
            Place(body, new Vector3(-12, 1, -9));

            // One shotgun-style volley (several hits in one frame) gives one hurt sound per enemy.
            foreach (var audio in new[] { wolfAudio, armoredAudio, spitterAudio })
            {
                var health = audio.GetComponent<Health>();
                int before = audio.HurtsPlayed;
                for (int pellet = 0; pellet < 6; pellet++) health.ApplyDamage(1f);
                Assert.That(audio.HurtsPlayed, Is.EqualTo(before + 1), audio.name + " one hurt sound per volley");
            }
            yield return new WaitForSeconds(.4f);

            // Death: exactly once, on a detached voice that outlives the enemy's components.
            foreach (var audio in new[] { wolfAudio, armoredAudio, spitterAudio })
            {
                var health = audio.GetComponent<Health>();
                string voice = audio.name + " Death Audio";
                health.ApplyDamage(100000f);
                Assert.That(audio.DeathPlayed, Is.True, audio.name + " death sound");
                Debug.Log("[enemy-audio] " + audio.name + " death clip length " + audio.Profile.death.clip.length + " same-frame voices " +
                    Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(s => s.name == voice) + " timeScale " + Time.timeScale);
                health.ApplyDamage(100000f);
                yield return null;
                var voices = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(s => s.name == voice).ToArray();
                Assert.That(voices.Length, Is.EqualTo(1), audio.name + " exactly one death voice; voices with Death: " +
                    string.Join(", ", Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(s => s.name.Contains("Death")).Select(s => s.name + (s.isPlaying ? "(playing)" : "")))
                    + " | sources on enemy: " + audio.GetComponents<AudioSource>().Length + " died=" + audio.DeathPlayed);
                Assert.That(voices[0].isPlaying && voices[0].spatialBlend == 1f, Is.True, audio.name + " death voice playing in 3D");
                int steps = audio.StepsPlayed, attacks = audio.AttacksPlayed;
                foreach (var behaviour in audio.GetComponents<MonoBehaviour>().Where(b => !(b is EnemyAudio))) behaviour.enabled = false;
                yield return new WaitForSeconds(.6f);
                Assert.That(voices[0] && voices[0].isPlaying, Is.True, audio.name + " death audio continues with AI disabled");
                Assert.That(audio.StepsPlayed, Is.EqualTo(steps), audio.name + " no footsteps when dead");
                Assert.That(audio.AttacksPlayed, Is.EqualTo(attacks), audio.name + " no attacks when dead");
            }
            Debug.Log($"[enemy-audio] wolf steps {wolfAudio.StepsPlayed}, armored steps {armoredAudio.StepsPlayed} attacks {armoredAudio.AttacksPlayed}, " +
                      $"spitter steps {spitterAudio.StepsPlayed} attacks {spitterAudio.AttacksPlayed}");
        }

        private static void Place(Rigidbody body, Vector3 position)
        {
            body.position = position;
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.time + seconds;
            while (!condition())
            {
                Assert.That(Time.time, Is.LessThan(end), "timed out waiting for " + what);
                yield return null;
            }
        }
    }
}
#endif
