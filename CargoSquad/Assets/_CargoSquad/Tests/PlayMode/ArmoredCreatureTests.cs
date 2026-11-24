#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CargoSquad.Tests
{
    public sealed class ArmoredCreatureTests
    {
        const string ScenePath = "Assets/_CargoSquad/Art/PixelArmoredCreature_CC0/ArmoredCreatureReview.unity";
        private ArmoredCreatureAI creature;
        private Health health;
        private PlayerWeapon weapon;
        private CharacterSelection selection;
        private Mouse mouse;

        [UnitySetUp]
        public IEnumerator OpenReview()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            creature = Object.FindFirstObjectByType<ArmoredCreatureAI>();
            selection = Object.FindFirstObjectByType<CharacterSelection>();
            weapon = Object.FindFirstObjectByType<PlayerWeapon>();
            health = creature.GetComponent<Health>();
            mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(Screen.width / 2f, Screen.height / 2f) });
            yield return null;
            Assert.That(creature.GetComponent<NavMeshAgent>().isOnNavMesh);
            Assert.That(Object.FindFirstObjectByType<MutatedWolfAI>(), Is.Null);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            Time.timeScale = 1;
            yield return null;
        }

        private IEnumerator Deploy(int index)
        {
            selection.SelectCharacter(index);
            selection.StartSelectedCharacter();
            yield return null;
            yield return null;
            yield return null;
        }

        private bool FireAtCreature()
        {
            Vector3 p = creature.transform.position;
            p.y = 0;
            return weapon.AimAtScreen(Camera.main.WorldToScreenPoint(p)) && weapon.TryFire();
        }

        [UnityTest]
        public IEnumerator EightTakesAndGroundedDeath()
        {
            yield return Deploy(0);
            var animator = creature.GetComponentInChildren<Animator>();
            Assert.That(animator.avatar.isValid && !animator.applyRootMotion);
            var clips = animator.runtimeAnimatorController.animationClips;
            CollectionAssert.AreEquivalent(System.Enum.GetNames(typeof(ArmoredState)), clips.Select(c => c.name));
            foreach (var clip in clips)
            {
                Assert.That(clip.isLooping, Is.EqualTo(clip.name is "Idle" or "Walk" or "Charge"));
                Assert.That(clip.events, Is.Empty);
            }
            var skin = creature.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(skin.bones.All(b => b));
            Assert.That(skin.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(skin.sharedMaterial.GetTexture("_BaseMap").filterMode, Is.EqualTo(FilterMode.Point));
            health.ApplyDamage(health.Maximum);
            yield return new WaitForSeconds(clips.Single(c => c.name == "Death").length + .25f);
            var pose = animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(pose.IsName("Death") && pose.normalizedTime >= 1);
            Assert.That(creature.GetComponent<WolfHealthIndicator>().Visible, Is.False);
            Assert.That(creature.GetComponent<CapsuleCollider>().enabled, Is.False);
            Assert.That(creature.TryStun(1), Is.False);
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh);
                var world = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                var heights = mesh.vertices.Select(v => world.MultiplyPoint3x4(v).y - creature.transform.position.y);
                Assert.That(heights.Min(), Is.InRange(-.05f, .09f));
                Assert.That(heights.Max(), Is.LessThan(1.6f));
            }
            finally { Object.Destroy(mesh); }
        }

        [UnityTest]
        public IEnumerator PursuitAndBothContactsApplyDamageOnce()
        {
            Vector3 spawn = creature.transform.position;
            yield return Deploy(0);
            var playerHealth = weapon.GetComponent<Health>();
            int hits = 0;
            playerHealth.Changed += (_, _) => hits++;
            var animator = creature.GetComponentInChildren<Animator>();
            foreach (var attack in new[] { ArmoredState.HeavySwipe, ArmoredState.GroundSmash })
            {
                int before = hits;
                float hp = playerHealth.Current;
                float deadline = Time.time + 9;
                while (creature.State != attack && Time.time < deadline) yield return null;
                Assert.That(creature.State, Is.EqualTo(attack));
                Assert.That(Vector3.Distance(spawn, creature.transform.position), Is.GreaterThan(.5f));
                Assert.That(hits, Is.EqualTo(before));
                while (hits == before && Time.time < deadline + 2) yield return null;
                Assert.That(hits, Is.EqualTo(before + 1));
                Assert.That(playerHealth.Current, Is.EqualTo(hp - (attack == ArmoredState.HeavySwipe ? 22 : 32)));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,
                    Is.InRange(attack == ArmoredState.HeavySwipe ? .54f : .59f, .72f));
                while (creature.State == attack && Time.time < deadline + 3) yield return null;
                Assert.That(hits, Is.EqualTo(before + 1));
            }
        }

        [UnityTest]
        public IEnumerator HeavyShotgunAndBashDamageSharedHealth()
        {
            yield return Deploy(0);
            var body = weapon.GetComponent<Rigidbody>();
            body.position = creature.transform.position + new Vector3(0,1,-2.4f);
            Physics.SyncTransforms();
            // The player uses an interpolated Rigidbody. Aim after its transform has caught up.
            yield return new WaitForFixedUpdate();
            yield return null;
            float hp = health.Current;
            Assert.That(FireAtCreature());
            Assert.That(health.Current, Is.LessThan(hp));
            Assert.That(creature.GetComponent<WolfHealthIndicator>().TargetFraction,
                Is.EqualTo(health.Current / health.Maximum).Within(.001f));
            hp = health.Current;
            selection.ActiveVisual.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var ability = weapon.GetComponent<CharacterAbility>();
            Assert.That(ability.TryActivate());
            yield return new WaitForSeconds(.55f);
            Assert.That(health.Current, Is.EqualTo(hp - ability.ChargeDamage).Within(.001f));
        }

        [UnityTest]
        public IEnumerator MedicShotInterruptsWindupAndRecovers()
        {
            yield return Deploy(1);
            float deadline = Time.time + 9;
            while (creature.State != ArmoredState.HeavySwipe && Time.time < deadline) yield return null;
            Assert.That(creature.State, Is.EqualTo(ArmoredState.HeavySwipe));
            var playerHealth = weapon.GetComponent<Health>();
            float hp = playerHealth.Current, enemyHP = health.Current;
            Assert.That(FireAtCreature());
            Assert.That(creature.State, Is.EqualTo(ArmoredState.Stunned));
            Assert.That(health.Current, Is.EqualTo(enemyHP - weapon.Definition.damage).Within(.001f));
            Assert.That(creature.TryStun(3), Is.False);
            yield return new WaitForSeconds(weapon.Definition.stunDuration + .12f);
            Assert.That(creature.State, Is.Not.EqualTo(ArmoredState.Stunned));
            Assert.That(playerHealth.Current, Is.EqualTo(hp));
            Assert.That(creature.TryStun(3), Is.False);
        }
    }
}
#endif
