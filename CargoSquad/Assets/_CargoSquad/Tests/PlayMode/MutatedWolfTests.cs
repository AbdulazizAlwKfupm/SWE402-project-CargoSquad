#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Combat;
using CargoSquad.Cargo;
using CargoSquad.Player;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CargoSquad.Tests
{
    public sealed class MutatedWolfTests
    {
        private MutatedWolfAI wolf;
        private PlayerController player;
        private CharacterSelection selection;
        private static readonly string[] Missions = { "IndustrialDistrict", "IndustrialExpansion" };

        [UnityTest]
        public IEnumerator MissionSpawnsConfiguredCC0Wolf([ValueSource(nameof(Missions))] string mission)
        {
            EditorSceneManager.LoadSceneInPlayMode($"Assets/_CargoSquad/Scenes/{mission}.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            var wolves = Object.FindObjectsByType<MutatedWolfAI>(FindObjectsSortMode.None);
            Assert.That(wolves, Has.Length.EqualTo(1));
            var activeWolf = wolves[0];
            var spawn = Object.FindFirstObjectByType<LocalWolfSpawn>();
            var expected = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_CargoSquad/Art/PixelMutatedWolf_CC0_v2/PixelMutatedWolf_CC0_v2.prefab");
            Assert.That(activeWolf.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh,
                Is.EqualTo(expected.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh));
            Assert.That(activeWolf.GetComponentInChildren<Animator>().runtimeAnimatorController,
                Is.EqualTo(expected.GetComponentInChildren<Animator>().runtimeAnimatorController));
            Assert.That(Vector3.Distance(activeWolf.transform.position, spawn.transform.position), Is.LessThan(.2f));
            Assert.That(activeWolf.GetComponent<NavMeshAgent>().isOnNavMesh);
            Assert.That(activeWolf.GetComponent<WolfHealthIndicator>().Visible);
        }

        [UnitySetUp]
        public IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null;
            yield return null;
            wolf = Object.FindFirstObjectByType<MutatedWolfAI>();
            player = Object.FindFirstObjectByType<PlayerController>();
            selection = Object.FindFirstObjectByType<CharacterSelection>();
            Assert.That(wolf, Is.Not.Null);
            Assert.That(wolf.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True);
            var intersecting = Physics.OverlapCapsule(wolf.transform.position + Vector3.up * 0.45f,
                wolf.transform.position + Vector3.up * 0.85f, 0.4f, ~0, QueryTriggerInteraction.Ignore)
                .Where(collider => !collider.transform.IsChildOf(wolf.transform)).ToArray();
            Assert.That(intersecting, Is.Empty, "The installed spawn must be clear of scene collision geometry.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PatrolNoiseSightBiteCooldownAndDeath()
        {
            Vector3 paused = wolf.transform.position;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(wolf.transform.position, Is.EqualTo(paused));
            selection.StartSelectedCharacter();
            yield return new WaitForSeconds(2f);
            Assert.That(Vector3.Distance(wolf.transform.position, paused), Is.GreaterThan(0.4f), "Wolf must actually patrol.");

            var hearingPoint = new Vector3(1f, 0f, -7f);
            wolf.HearNoise(new GameplayNoise(hearingPoint, 30f));
            Assert.That(wolf.State, Is.EqualTo(WolfState.Investigate));
            Assert.That(wolf.LastKnownPosition, Is.EqualTo(hearingPoint));
            Vector3 hearingStart = wolf.transform.position;
            yield return new WaitForSeconds(0.4f);
            Assert.That(Vector3.Distance(wolf.transform.position, hearingPoint),
                Is.LessThan(Vector3.Distance(hearingStart, hearingPoint)));

            var agent = wolf.GetComponent<NavMeshAgent>();
            Assert.That(agent.Warp(new Vector3(7, 0, -7)), Is.True);
            var body = player.GetComponent<Rigidbody>();
            body.position = new Vector3(4.5f, 1, -7);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.1f);
            Assert.That(wolf.State, Is.EqualTo(WolfState.Chase));
            var health = player.GetComponent<Health>();
            float initialHealth = health.Current;
            float guard = Time.time + 3f;
            while (wolf.State != WolfState.Attack && Time.time < guard) yield return null;
            Assert.That(wolf.State, Is.EqualTo(WolfState.Attack));
            Assert.That(health.Current, Is.EqualTo(initialHealth), "Damage must wait for the bite impact.");
            guard = Time.time + 1.2f;
            while (health.Current == initialHealth && Time.time < guard) yield return null;
            Assert.That(health.Current, Is.EqualTo(initialHealth - wolf.BiteDamage));
            float firstHitAt = Time.time;
            float afterBite = health.Current;
            yield return new WaitForSeconds(0.35f);
            Assert.That(health.Current, Is.EqualTo(afterBite), "One bite must not hit on multiple frames.");
            guard = Time.time + 3f;
            while (health.Current == afterBite && Time.time < guard) yield return null;
            Assert.That(health.Current, Is.EqualTo(afterBite - wolf.BiteDamage));
            Assert.That(Time.time - firstHitAt, Is.GreaterThanOrEqualTo(wolf.AttackCooldown - 0.06f));
            afterBite = health.Current;

            var enemyHealth = wolf.GetComponent<Health>();
            enemyHealth.ApplyDamage(1000);
            Assert.That(wolf.State, Is.EqualTo(WolfState.Death));
            Vector3 dead = wolf.transform.position;
            yield return new WaitForSeconds(1.95f);
            Assert.That(health.Current, Is.EqualTo(afterBite));
            Assert.That(Vector3.Distance(wolf.transform.position, dead), Is.LessThan(0.02f));
            Assert.That(wolf.GetComponent<Collider>().enabled, Is.False);
            var deathPose = wolf.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0);
            Assert.That(deathPose.IsName("Death") && deathPose.normalizedTime >= 1f);
            var skin = wolf.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked = new Mesh();
            try
            {
                skin.BakeMesh(baked);
                // BakeMesh already includes the import scale. Rotate its vertices into world axes.
                var pose = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                var heights = baked.vertices.Select(v => pose.MultiplyPoint3x4(v).y - dead.y).ToArray();
                Assert.That(heights.Min(), Is.InRange(-.025f, .04f), "The final pose must contact the floor.");
                Assert.That(heights.Max(), Is.LessThan(1f), "The corpse must collapse.");
            }
            finally { Object.Destroy(baked); }
        }

        [UnityTest]
        public IEnumerator NavigationFindsRouteAroundObstacle()
        {
            selection.StartSelectedCharacter();
            // A physical box is included in a separate local navigation surface by the test fixture.
            var agent = wolf.GetComponent<NavMeshAgent>();
            var ground = GameObject.Find("Ground").GetComponent<BoxCollider>();
            var obstacles = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)
                .Where(box => box != ground && !box.isTrigger && !box.attachedRigidbody &&
                    box.bounds.size.y > 1.4f && box.bounds.size.x < 6f && box.bounds.size.z < 6f);
            bool found = false;
            foreach (var obstacle in obstacles)
            {
                Vector3 center = obstacle.bounds.center;
                Vector3 a = new Vector3(center.x - obstacle.bounds.extents.x - 1.2f, 0, center.z);
                Vector3 b = new Vector3(center.x + obstacle.bounds.extents.x + 1.2f, 0, center.z);
                if (!NavMesh.SamplePosition(a, out var start, 0.6f, NavMesh.AllAreas) ||
                    !NavMesh.SamplePosition(b, out var end, 0.6f, NavMesh.AllAreas)) continue;
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 3) continue;
                found = true;
                Assert.That(agent.Warp(start.position), Is.True);
                wolf.HearNoise(new GameplayNoise(end.position, 50));
                player.GetComponent<Rigidbody>().position = new Vector3(-12, 1, -8);
                float guard = Time.time + 12f;
                while (Vector3.Distance(wolf.transform.position, end.position) > 0.6f && Time.time < guard)
                    yield return null;
                Assert.That(Vector3.Distance(wolf.transform.position, end.position), Is.LessThan(0.6f));
                break;
            }
            Assert.That(found, Is.True, "A complete route around an actual scene obstacle must be tested.");
        }

        [UnityTest]
        public IEnumerator HeavyBashHitsOnceAndMedicRestoresStamina()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(4, 4) });
                selection.SelectCharacter(0);
                selection.StartSelectedCharacter();
                yield return null;
                yield return null;
                var body = player.GetComponent<Rigidbody>();
                body.position = new Vector3(5, 1, -7);
                selection.ActiveVisual.transform.rotation = Quaternion.LookRotation(Vector3.right);
                var agent = wolf.GetComponent<NavMeshAgent>();
                agent.Warp(new Vector3(7, 0, -7));
                wolf.enabled = false;
                agent.isStopped = true;
                Physics.SyncTransforms();
                var ability = player.GetComponent<CharacterAbility>();
                var health = wolf.GetComponent<Health>();
                float initial = health.Current;
                Assert.That(ability.TryActivate(), Is.True);
                yield return new WaitForSeconds(0.5f);
                Assert.That(health.Current, Is.EqualTo(initial - ability.ChargeDamage));
                yield return new WaitForSeconds(0.3f);
                Assert.That(health.Current, Is.EqualTo(initial - ability.ChargeDamage));

                // Reuse the existing character configuration and stamina operation.
                ability.Configure(SurvivorKind.Medic, selection.ActiveVisual);
                yield return null;
                yield return null;
                var stamina = player.GetComponent<PlayerStamina>();
                stamina.Tick(true, 1f, 100f);
                Assert.That(stamina.Current, Is.Zero);
                Assert.That(ability.TryActivate(), Is.True);
                Assert.That(stamina.Current, Is.GreaterThanOrEqualTo(100f));
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator SelectedMedicRestoresStaminaAndReceivesBites()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(4, 4) });
                selection.SelectCharacter(1);
                selection.StartSelectedCharacter();
                yield return null;
                yield return null;
                var ability = player.GetComponent<CharacterAbility>();
                Assert.That(ability.Kind, Is.EqualTo(SurvivorKind.Medic));
                var stamina = player.GetComponent<PlayerStamina>();
                stamina.Tick(true, 1f, 100f);
                Assert.That(ability.TryActivate(), Is.True);
                Assert.That(stamina.Current, Is.EqualTo(100f));
                Assert.That(ability.TryActivate(), Is.False);
                wolf.GetComponent<NavMeshAgent>().Warp(new Vector3(7, 0, -7));
                player.GetComponent<Rigidbody>().position = new Vector3(5.7f, 1, -7);
                Physics.SyncTransforms();
                var health = player.GetComponent<Health>();
                float initial = health.Current;
                float deadline = Time.time + 3f;
                while (health.Current == initial && Time.time < deadline) yield return null;
                Assert.That(health.Current, Is.EqualTo(initial - wolf.BiteDamage));
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator ActualCargoSlipIsHeardAndSearchReturnsToPatrol()
        {
            selection.StartSelectedCharacter();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var cargo = Object.FindFirstObjectByType<CargoItem>();
                var carrier = player.GetComponent<CargoInteractor>();
                var body = player.GetComponent<Rigidbody>();
                body.position = cargo.transform.position + new Vector3(0, 0.25f, -1.9f);
                yield return new WaitForSeconds(0.12f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(carrier.CarriedCargo, Is.SameAs(cargo));
                body.position = new Vector3(0, 1, -4);
                var settings = new SerializedObject(wolf);
                settings.FindProperty("detectionRange").floatValue = 0.05f;
                settings.FindProperty("searchDuration").floatValue = 0.2f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                wolf.GetComponent<NavMeshAgent>().Warp(new Vector3(5, 0, -4));
                yield return new WaitForSeconds(0.12f);
                carrier.Grip.Tick(true, true, 1f, 100f);
                yield return null;
                yield return null;
                Assert.That(carrier.CarriedCargo, Is.Null);
                Assert.That(wolf.State, Is.EqualTo(WolfState.Investigate));
                float guard = Time.time + 9f;
                bool searched = false;
                bool returned = false;
                while (Time.time < guard)
                {
                    searched |= wolf.State == WolfState.Search;
                    returned |= wolf.State == WolfState.Return;
                    if (searched && returned) break;
                    yield return null;
                }
                Assert.That(searched && returned, Is.True, "Investigation should end in search and a return to patrol.");
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator ApprovedRigAnimatesAndRenders()
        {
            selection.StartSelectedCharacter();
            wolf.enabled = false;
            var animator = wolf.GetComponentInChildren<Animator>();
            Assert.That(animator.avatar && animator.avatar.isValid, Is.True);
            var clips = animator.runtimeAnimatorController.animationClips;
            var states = new[] { "Idle", "Walk", "Run", "Bite", "Hurt", "Stunned", "Death" };
            Assert.That(clips.Select(clip => clip.name), Is.EquivalentTo(states));
            var bite = clips.Single(clip => clip.name == "Bite");
            Assert.That(bite.length, Is.EqualTo(1f).Within(.001f));
            Assert.That(bite.events, Is.Empty, "Only the AI should apply bite damage.");
            var bones = wolf.GetComponentsInChildren<SkinnedMeshRenderer>()
                .SelectMany(renderer => renderer.bones).Distinct().ToArray();
            Assert.That(bones, Is.Not.Empty);
            Assert.That(bones.All(bone => bone), Is.True);
            foreach (string state in states)
            {
                animator.Play(state, 0, 0f);
                animator.Update(0f);
                var rotations = bones.Select(bone => bone.localRotation).ToArray();
                animator.Update(0.23f);
                Assert.That(bones.Where((bone, i) => Quaternion.Angle(rotations[i], bone.localRotation) > 0.1f).Any(),
                    Is.True, state + " must move the approved rig.");
            }
            animator.Play("Idle", 0, 0.1f);
            animator.Update(0f);
            var renderers = wolf.GetComponentsInChildren<SkinnedMeshRenderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                bounds.Encapsulate(renderer.bounds);
            }
            Assert.That(bounds.size.y, Is.InRange(1f, 2f));
            Assert.That(bounds.size.z, Is.InRange(1.7f, 3.6f));
            var cameraObject = new GameObject("Wolf validation camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = bounds.center + new Vector3(3.2f, 1.5f, 3.5f);
            camera.transform.LookAt(bounds.center);
            camera.orthographic = true;
            camera.orthographicSize = 1.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.10f);
            camera.cullingMask = 1 << 31;
            foreach (Transform part in wolf.GetComponentsInChildren<Transform>()) part.gameObject.layer = 31;
            var target = new RenderTexture(960, 600, 24);
            camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(960, 600, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 600), 0, 0);
            image.Apply();
            System.IO.Directory.CreateDirectory("ValidationResults/IntegratedWolf");
            System.IO.File.WriteAllBytes("ValidationResults/IntegratedWolf/wolf-idle.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.Destroy(image);
            Object.Destroy(target);
            Object.Destroy(cameraObject);
            yield return null;
        }
    }
}
#endif
