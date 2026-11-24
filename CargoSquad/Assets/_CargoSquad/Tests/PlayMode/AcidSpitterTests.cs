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
    public sealed class AcidSpitterTests
    {
        const string ScenePath = "Assets/_CargoSquad/Art/PixelAcidSpitter_CC0/SpitterReview.unity";
        private AcidSpitterAI creature;
        private Health health;
        private PlayerWeapon weapon;
        private CharacterSelection selection;
        private Mouse mouse;

        [UnitySetUp]
        public IEnumerator OpenReview()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            creature = Object.FindFirstObjectByType<AcidSpitterAI>();
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
        public IEnumerator NineTakesAndGroundedDeath()
        {
            yield return Deploy(0);
            var animator = creature.GetComponentInChildren<Animator>();
            Assert.That(animator.avatar.isValid && !animator.applyRootMotion);
            var clips = animator.runtimeAnimatorController.animationClips;
            CollectionAssert.AreEquivalent(System.Enum.GetNames(typeof(SpitterState)), clips.Select(c => c.name));
            foreach (var clip in clips)
            {
                Assert.That(clip.isLooping, Is.EqualTo(clip.name is "Idle" or "Walk" or "Run"));
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

        IEnumerator PositionPlayer(Vector3 point)
        {
            weapon.GetComponent<Rigidbody>().position = point;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        IEnumerator AwaitState(SpitterState expected)
        {
            float deadline = Time.time + 8f;
            while (creature.State != expected && Time.time < deadline) yield return null;
            Assert.That(creature.State, Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator ClosesDistanceAndFiresOneMovingGlob()
        {
            var start = creature.transform.position;
            var far = start + new Vector3(-8,1,-3);
            weapon.transform.position = far;
            weapon.GetComponent<Rigidbody>().position = far;
            Physics.SyncTransforms();
            yield return Deploy(0);
            yield return AwaitState(SpitterState.Run);
            yield return AwaitState(SpitterState.SpitWindup);
            Assert.That(Vector3.Distance(start,creature.transform.position),Is.GreaterThan(.5f));
            var hp = weapon.GetComponent<Health>();
            float initial = hp.Current;
            int hits = 0;
            hp.Changed += (_,_) => hits++;
            yield return AwaitState(SpitterState.SpitAttack);
            float deadline = Time.time + 2;
            AcidGlob glob = null;
            while (!glob && Time.time < deadline)
            {
                glob = Object.FindFirstObjectByType<AcidGlob>();
                yield return null;
            }
            Assert.That(glob, Is.Not.Null);
            Assert.That(hp.Current,Is.EqualTo(initial));
            Vector3 launch = glob.transform.position;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Vector3.Distance(launch,glob.transform.position),Is.GreaterThan(.05f));
            while (hits == 0 && Time.time < deadline + 1) yield return null;
            Assert.That(hp.Current,Is.EqualTo(initial-14));
            yield return new WaitForSeconds(.35f);
            Assert.That(hits,Is.EqualTo(1));
            Assert.That(!glob);
        }

        [UnityTest]
        public IEnumerator CloseSwipeDealsOneHit()
        {
            yield return Deploy(0);
            yield return PositionPlayer(creature.transform.position + creature.transform.forward * 1.5f + Vector3.up);
            yield return AwaitState(SpitterState.MeleeSwipe);
            var hp = weapon.GetComponent<Health>();
            float initial = hp.Current;
            yield return new WaitForSeconds(1.55f);
            Assert.That(hp.Current, Is.EqualTo(initial - 11));
            Assert.That(Object.FindFirstObjectByType<AcidGlob>(),Is.Null);
        }

        [UnityTest]
        public IEnumerator RetreatsToFiringDistance()
        {
            Vector3 start = creature.transform.position;
            Vector3 near = start + creature.transform.forward * 2.8f + Vector3.up;
            weapon.transform.position = near;
            weapon.GetComponent<Rigidbody>().position = near;
            Physics.SyncTransforms();
            yield return Deploy(0);
            yield return AwaitState(SpitterState.Walk);
            yield return AwaitState(SpitterState.SpitWindup);
            Vector3 gap = Vector3.ProjectOnPlane(weapon.transform.position - creature.transform.position, Vector3.up);
            Assert.That(gap.magnitude, Is.GreaterThanOrEqualTo(3.95f));
        }

        [UnityTest]
        public IEnumerator SolidCoverInterceptsAirborneGlob()
        {
            yield return Deploy(0);
            yield return AwaitState(SpitterState.SpitAttack);
            var hp=weapon.GetComponent<Health>();
            float initial=hp.Current;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position=Vector3.Lerp(creature.transform.position,weapon.transform.position,.65f)+Vector3.up;
                wall.transform.LookAt(new Vector3(weapon.transform.position.x,wall.transform.position.y,weapon.transform.position.z));
                wall.transform.localScale=new Vector3(3,3,.25f);
                Physics.SyncTransforms();
                float deadline=Time.time+2;
                while (!Object.FindFirstObjectByType<AcidGlob>() && Time.time<deadline) yield return null;
                Assert.That(Object.FindFirstObjectByType<AcidGlob>(),Is.Not.Null);
                yield return new WaitForSeconds(.95f);
                Assert.That(hp.Current,Is.EqualTo(initial));
                Assert.That(Object.FindFirstObjectByType<AcidGlob>(),Is.Null);
            }
            finally { Object.Destroy(wall); }
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
            yield return PositionPlayer(creature.transform.position + new Vector3(0,1,-4.8f));
            float deadline = Time.time + 9;
            while (creature.State != SpitterState.SpitWindup && Time.time < deadline) yield return null;
            Assert.That(creature.State, Is.EqualTo(SpitterState.SpitWindup));
            var playerHealth = weapon.GetComponent<Health>();
            float hp = playerHealth.Current, enemyHP = health.Current;
            Assert.That(FireAtCreature());
            Assert.That(creature.State, Is.EqualTo(SpitterState.Stunned));
            Assert.That(health.Current, Is.EqualTo(enemyHP - weapon.Definition.damage).Within(.001f));
            Assert.That(creature.TryStun(3), Is.False);
            yield return new WaitForSeconds(weapon.Definition.stunDuration + .12f);
            Assert.That(creature.State, Is.Not.EqualTo(SpitterState.Stunned));
            Assert.That(playerHealth.Current, Is.EqualTo(hp));
            Assert.That(creature.TryStun(3), Is.False);
        }
    }
}
#endif
