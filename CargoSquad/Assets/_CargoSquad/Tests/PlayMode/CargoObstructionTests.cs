#if UNITY_EDITOR
using System.Collections;
using CargoSquad.Cargo;
using CargoSquad.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CargoSquad.Tests
{
    /// <summary>
    /// Carried cargo must not pass through solid geometry in the real gameplay scene. The tests add their own
    /// walls on open ground so the distances are exact, and measure how deep the carried cargo's box is inside
    /// any solid collider on every frame.
    /// </summary>
    public sealed class CargoObstructionTests
    {
        private const string GameScene = "Assets/_CargoSquad/Scenes/IndustrialDistrict.unity";
        private const string PrefabFolder = "Assets/_CargoSquad/Prefabs/Characters/";
        private const float Transient = 0.05f;   // deepest momentary overlap tolerated while physics resolves a contact
        private const float Settled = 0.01f;

        private sealed class Rig
        {
            public CargoInteractor Player;
            public CargoItem Cargo;
            public Rigidbody Body;
            public SurvivorVisual Visual;
            public CargoCarrySocket Socket;
            public Keyboard Keyboard;
            public float Ground;
            public float Deepest;
            private readonly Collider[] hits = new Collider[16];

            /// <summary>Depth (m) of the carried cargo's box inside solid geometry right now.</summary>
            public float Penetration()
            {
                BoxCollider box = Socket.Obstruction;
                if (!box || !box.enabled)
                    return 0f;
                Transform pose = box.transform;
                int count = Physics.OverlapBoxNonAlloc(pose.position, box.size * 0.5f, hits, pose.rotation, ~0, QueryTriggerInteraction.Ignore);
                float depth = 0f;
                for (int i = 0; i < count; i++)
                {
                    if (hits[i].attachedRigidbody == Body)
                        continue;
                    if (Physics.ComputePenetration(box, pose.position, pose.rotation, hits[i], hits[i].transform.position,
                            hits[i].transform.rotation, out _, out float distance))
                        depth = Mathf.Max(depth, distance);
                }
                return depth;
            }

            public IEnumerator Drive(float seconds, params Key[] keys)
            {
                InputSystem.QueueStateEvent(Keyboard, new KeyboardState(keys));
                float end = Time.time + seconds;
                while (Time.time < end)
                {
                    yield return new WaitForFixedUpdate();
                    Deepest = Mathf.Max(Deepest, Penetration());
                }
            }

            public IEnumerator Release(float seconds = 0.25f) => Drive(seconds);

            public float TakeDeepest()
            {
                float value = Deepest;
                Deepest = 0f;
                return value;
            }

            public Vector3 CargoCentre => Cargo.transform.TransformPoint(Cargo.Collider.center);

            /// <summary>Furthest point of the carried cargo along a world direction.</summary>
            public float CargoReach(Vector3 direction)
            {
                Vector3 half = Cargo.Size * 0.5f;
                Quaternion rotation = Cargo.transform.rotation;
                float reach = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y, (i & 4) == 0 ? -half.z : half.z);
                    reach = Mathf.Max(reach, Vector3.Dot(CargoCentre + rotation * corner, direction));
                }
                return reach;
            }

            public IEnumerator PickUpAt(Vector3 spot)
            {
                Vector3 size = Cargo.Size;
                Cargo.transform.position += new Vector3(spot.x, Ground + size.y * 0.5f, spot.z + 1.1f) - CargoCentre;
                Body.position = new Vector3(spot.x, Ground + 1f, spot.z);
                Body.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(0.2f);
                yield return Tap();
                Assert.That(Player.CarriedCargo, Is.SameAs(Cargo), "picked up");
                Socket = Visual.GetComponentInChildren<CargoCarrySocket>();
                Assert.That(Cargo.transform.parent, Is.SameAs(Socket.transform));
                Assert.That(Socket.IsObstructing, Is.True, "obstruction box is active while carrying");
                Assert.That(Vector3.Distance(Socket.Obstruction.size, size), Is.LessThan(1e-4f), "obstruction box matches the cargo size");
                Assert.That(Cargo.Collider.enabled, Is.False, "the cargo's own collider stays off");
                yield return new WaitForSeconds(0.6f);
            }

            public IEnumerator Tap()
            {
                InputSystem.QueueStateEvent(Keyboard, new KeyboardState(Key.E));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(Keyboard, new KeyboardState());
                yield return null;
            }

            public void Teleport(float x, float z)
            {
                Body.position = new Vector3(x, Ground + 1f, z);
                Body.linearVelocity = Vector3.zero;
            }
        }

        private static IEnumerator Load(Rig rig)
        {
            EditorSceneManager.LoadSceneInPlayMode(GameScene, new LoadSceneParameters());
            yield return null; MissionResultTestSupport.KeepSimulationRunning();
            yield return null;
            Object.FindFirstObjectByType<CargoSquad.UI.CharacterSelection>()?.StartSelectedCharacter();
            yield return null;
            rig.Player = Object.FindFirstObjectByType<CargoInteractor>();
            rig.Cargo = Object.FindFirstObjectByType<CargoItem>();
            rig.Body = rig.Player.GetComponent<Rigidbody>();
            rig.Visual = rig.Player.GetComponentInChildren<SurvivorVisual>();
            rig.Ground = GameObject.Find("Ground").GetComponent<BoxCollider>().bounds.max.y;
        }

        private static Collider Wall(Vector3 centre, Vector3 size)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestWall";
            wall.transform.position = centre;
            wall.transform.localScale = size;
            Physics.SyncTransforms();
            return wall.GetComponent<Collider>();
        }

        /// <summary>
        /// Returns a patch of the real scene big enough for the test walls. The level is small and dressed with
        /// scenery, so the emptiest patch is used and any scenery inside it is switched off for this play session
        /// only (logged). The mission's extraction zone and the cargo depot are never touched.
        /// </summary>
        private static Vector3 FindOpenGround(Rig rig)
        {
            Bounds floor = GameObject.Find("Ground").GetComponent<BoxCollider>().bounds;
            var half = new Vector3(4.3f, 1.4f, 6.6f);
            Vector3 best = default;
            var bestBlockers = new System.Collections.Generic.List<GameObject>();
            int bestCount = int.MaxValue;
            for (float x = floor.min.x + half.x + 0.5f; x <= floor.max.x - half.x - 0.5f; x += 1f)
            {
                for (float z = floor.min.z + half.z + 2.5f; z <= floor.max.z - half.z + 1.5f; z += 1f)
                {
                    var blockers = new System.Collections.Generic.List<GameObject>();
                    bool usable = true;
                    foreach (Collider collider in Physics.OverlapBox(new Vector3(x, rig.Ground + 1.6f, z - 2f), half, Quaternion.identity,
                                 ~0, QueryTriggerInteraction.Collide))
                    {
                        if (collider.attachedRigidbody == rig.Body)
                            continue;
                        if (collider.GetComponentInParent<CargoSquad.Missions.CargoExtraction>() || collider.GetComponentInParent<CargoItem>())
                            usable = false;
                        else if (!collider.isTrigger && !blockers.Contains(collider.gameObject))
                            blockers.Add(collider.gameObject);
                    }
                    if (usable && blockers.Count < bestCount)
                    {
                        bestCount = blockers.Count;
                        bestBlockers = blockers;
                        best = new Vector3(x, rig.Ground, z);
                    }
                }
            }

            Assert.That(bestCount, Is.LessThan(int.MaxValue), $"no usable test patch (floor {floor.min:F0}..{floor.max:F0})");
            foreach (GameObject blocker in bestBlockers)
                blocker.SetActive(false);
            Physics.SyncTransforms();
            Debug.Log($"[wall] test patch at ({best.x:F0}, {best.z:F0}); scenery switched off for this run: " +
                      (bestBlockers.Count == 0 ? "none" : string.Join(", ", bestBlockers.ConvertAll(b => b.name))));
            return best;
        }

        [UnityTest]
        public IEnumerator IndustrialDistrict_CarriedCargoIsBlockedByWallsCorridorsAndTurns()
        {
            var rig = new Rig();
            yield return Load(rig);
            Vector3 origin = FindOpenGround(rig);
            rig.Teleport(origin.x, origin.z);
            yield return new WaitForSeconds(0.1f);
            rig.Keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return rig.PickUpAt(origin);
                Vector3 size = rig.Cargo.Size;
                Vector3 heldLocal = rig.Cargo.transform.localPosition;
                float condition = rig.Cargo.Condition.Current;
                float capsule = rig.Player.GetComponent<CapsuleCollider>().radius;
                Collider north = Wall(new Vector3(origin.x, rig.Ground + 1.5f, origin.z + 3.25f), new Vector3(8.4f, 3f, 0.5f));
                float face = north.bounds.min.z;

                // --- 1. walking straight into a wall: the cargo stops at the wall, the body further back than its capsule alone
                yield return rig.Drive(1.6f, Key.W);
                float reach = rig.CargoReach(Vector3.forward);
                float deepest = rig.TakeDeepest();
                Debug.Log($"[wall] head-on: cargo front {reach - face:F3} m past the wall face, body {face - rig.Body.position.z:F3} m back, deepest {deepest:F3} m");
                Assert.That(reach, Is.LessThan(face + Settled), "cargo does not enter the wall");
                Assert.That(deepest, Is.LessThan(Transient), "no deep overlap on impact");
                Assert.That(face - rig.Body.position.z, Is.GreaterThan(capsule + 0.1f), "body is held back by the cargo, not by its capsule");
                Assert.That(rig.Player.CarriedCargo, Is.SameAs(rig.Cargo), "blocked movement does not drop the cargo");
                Assert.That(rig.Cargo.Condition.Current, Is.EqualTo(condition), "blocked movement does not damage the cargo");
                Assert.That(Vector3.Distance(rig.Cargo.transform.localPosition, heldLocal), Is.LessThan(1e-5f), "cargo stays on its socket");
                CharacterShowcaseTests.Capture("70_wall_blocked");
                CharacterShowcaseTests.CaptureClose("70_wall_blocked_close", rig.Visual.transform, 1.8f);

                // --- 2. pushing diagonally: slides along the wall instead of sticking
                float x = rig.Body.position.x;
                yield return rig.Drive(1.0f, Key.W, Key.D);
                deepest = rig.TakeDeepest();
                Debug.Log($"[wall] slide: moved {rig.Body.position.x - x:F2} m along the wall, deepest {deepest:F3} m");
                Assert.That(rig.Body.position.x - x, Is.GreaterThan(1.2f), "slides along the wall");
                Assert.That(deepest, Is.LessThan(Transient));
                // A settled-contact assertion needs released input, rather than another driven impact.
                yield return rig.Release(0.1f);
                Assert.That(rig.Penetration(), Is.LessThan(Settled));

                // --- 3. never trapped: walking away is immediate
                float z = rig.Body.position.z;
                yield return rig.Drive(0.6f, Key.S);
                Assert.That(z - rig.Body.position.z, Is.GreaterThan(1.2f), "can walk away from the wall");
                Assert.That(rig.TakeDeepest(), Is.LessThan(Transient));

                // --- 4. turning next to the wall: travel east just clear of it, then turn into it
                rig.Teleport(origin.x - 3f, face - capsule - 0.24f);
                yield return rig.Release(0.1f);
                yield return rig.Drive(0.5f, Key.D);
                Assert.That(Vector3.Angle(rig.Visual.transform.forward, Vector3.right), Is.LessThan(15f), "faces east beside the wall");
                yield return rig.Drive(0.7f, Key.W);
                yield return rig.Release();
                deepest = rig.TakeDeepest();
                Debug.Log($"[wall] turn: facing {Vector3.Angle(rig.Visual.transform.forward, Vector3.forward):F0} deg from the wall normal, " +
                          $"cargo front {rig.CargoReach(Vector3.forward) - face:F3} m, deepest {deepest:F3} m");
                Assert.That(deepest, Is.LessThan(Transient), "turning does not swing the cargo into the wall");
                Assert.That(rig.Penetration(), Is.LessThan(Settled));
                Assert.That(rig.CargoReach(Vector3.forward), Is.LessThan(face + Settled));
                CharacterShowcaseTests.CaptureClose("71_wall_turn_close", rig.Visual.transform, 1.8f);

                // --- 5. safe drop while pressed against the wall
                yield return rig.Drive(0.6f, Key.W);
                yield return rig.Release();
                float carryingStop = face - rig.Body.position.z;
                yield return rig.Tap();
                Assert.That(rig.Player.CarriedCargo, Is.Null, "can drop next to a wall");
                Assert.That(rig.Socket.IsObstructing, Is.False, "obstruction box is off after the drop");
                Assert.That(rig.Cargo.Collider.enabled, Is.True);
                Assert.That(rig.Cargo.Condition.Current, Is.EqualTo(condition));
                Assert.That(rig.Cargo.Collider.bounds.min.y - rig.Ground, Is.EqualTo(0.02f).Within(0.03f), "dropped on the ground");
                foreach (Collider other in Physics.OverlapBox(rig.Cargo.Collider.bounds.center, size * 0.49f, rig.Cargo.transform.rotation,
                             ~0, QueryTriggerInteraction.Ignore))
                    Assert.That(other, Is.SameAs(rig.Cargo.Collider), "dropped cargo is not inside " + other.name);
                yield return rig.Drive(0.6f, Key.W);
                yield return rig.Release();
                Debug.Log($"[wall] stop distance carrying {carryingStop:F2} m, empty-handed {face - rig.Body.position.z:F2} m");
                Assert.That(face - rig.Body.position.z, Is.LessThan(capsule + 0.05f), "without cargo the capsule reaches the wall again");
                Object.Destroy(north.gameObject);
                yield return null;

                // --- 6. narrow corridor (1.1 m: wide enough for the capsule, too narrow to stand sideways with the cargo)
                yield return rig.PickUpAt(origin);
                const float width = 1.1f;
                float middle = origin.x;
                Collider west = Wall(new Vector3(middle - width * 0.5f - 0.25f, rig.Ground + 1.5f, origin.z - 3.5f), new Vector3(0.5f, 3f, 5f));
                Collider east = Wall(new Vector3(middle + width * 0.5f + 0.25f, rig.Ground + 1.5f, origin.z - 3.5f), new Vector3(0.5f, 3f, 5f));
                float entrance = west.bounds.min.z, exit = west.bounds.max.z;
                rig.Teleport(middle, entrance - 1.2f);
                yield return rig.Release(0.1f);
                yield return rig.Drive(3.0f, Key.W);
                deepest = rig.TakeDeepest();
                Debug.Log($"[corridor] walked from z={entrance - 1.2f:F1} to z={rig.Body.position.z:F1} (exit {exit:F1}), deepest {deepest:F3} m");
                Assert.That(rig.Body.position.z, Is.GreaterThan(exit), "walks through the corridor carrying cargo");
                Assert.That(deepest, Is.LessThan(Transient));

                rig.Teleport(middle, (entrance + exit) * 0.5f);
                yield return rig.Release(0.1f);
                yield return rig.Drive(0.3f, Key.W);
                yield return rig.Drive(0.8f, Key.D);          // try to turn and walk into the side wall
                yield return rig.Drive(0.8f, Key.A);
                deepest = rig.TakeDeepest();
                Debug.Log($"[corridor] sideways attempts: deepest {deepest:F3} m, facing {rig.Visual.transform.eulerAngles.y:F0} deg");
                Assert.That(deepest, Is.LessThan(Transient), "cannot swing the cargo into the corridor walls");
                Assert.That(rig.CargoReach(Vector3.right), Is.LessThan(east.bounds.min.x + Settled));
                Assert.That(-rig.CargoReach(Vector3.left), Is.GreaterThan(west.bounds.max.x - Settled));
                CharacterShowcaseTests.CaptureClose("72_corridor_close", rig.Visual.transform, 2.2f);
                z = rig.Body.position.z;
                yield return rig.Drive(1.2f, Key.S);          // and can still leave
                Assert.That(z - rig.Body.position.z, Is.GreaterThan(2f), "not trapped in the corridor");
                Assert.That(rig.TakeDeepest(), Is.LessThan(Transient));

                // --- 7. safe drop inside the corridor
                rig.Teleport(middle, (entrance + exit) * 0.5f);
                yield return rig.Release(0.15f);
                yield return rig.Tap();
                if (rig.Player.CarriedCargo)
                {
                    Assert.That(rig.Player.Prompt, Does.Contain("No room"), "a refused drop keeps the cargo and says why");
                    Assert.That(rig.Socket.IsObstructing, Is.True, "a refused drop keeps the obstruction box");
                    Debug.Log("[corridor] drop refused: no clear spot");
                }
                else
                {
                    foreach (Collider other in Physics.OverlapBox(rig.Cargo.Collider.bounds.center, size * 0.49f, rig.Cargo.transform.rotation,
                                 ~0, QueryTriggerInteraction.Ignore))
                        Assert.That(other, Is.SameAs(rig.Cargo.Collider), "dropped cargo is not inside " + other.name);
                    Debug.Log($"[corridor] dropped at {rig.Cargo.transform.position:F2}");
                }
                Assert.That(rig.Cargo.Condition.Current, Is.EqualTo(condition), "none of this damaged the cargo");
            }
            finally
            {
                InputSystem.RemoveDevice(rig.Keyboard);
            }
        }

        [UnityTest]
        public IEnumerator IndustrialDistrict_EverySurvivorAndCargoSizeIsBlocked()
        {
            var rig = new Rig();
            yield return Load(rig);
            Vector3 origin = FindOpenGround(rig);
            Vector3 visualLocal = rig.Visual.transform.localPosition;
            Collider wall = Wall(new Vector3(origin.x, rig.Ground + 1.5f, origin.z + 3.25f), new Vector3(8.4f, 3f, 0.5f));
            float face = wall.bounds.min.z;
            BoxCollider cargoBox = rig.Cargo.Collider;
            rig.Keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var cases = new (string prefab, Vector3 size)[]
                {
                    ("Heavy_Visual", new Vector3(0.80f, 0.30f, 0.60f)), ("Scout_Visual", new Vector3(0.60f, 0.45f, 0.45f)),
                    ("Medic_Visual", new Vector3(0.40f, 0.40f, 0.90f)), ("NavySoldier_Visual", new Vector3(0.70f, 0.50f, 0.30f))
                };
                foreach ((string prefab, Vector3 size) in cases)
                {
                    Object.Destroy(rig.Visual.gameObject);
                    yield return null;
                    GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + prefab + ".prefab"), rig.Player.transform);
                    model.transform.localPosition = visualLocal;
                    rig.Visual = model.GetComponent<SurvivorVisual>();
                    rig.Cargo.transform.localScale = new Vector3(size.x / cargoBox.size.x, size.y / cargoBox.size.y, size.z / cargoBox.size.z);
                    yield return rig.PickUpAt(origin);
                    yield return rig.Drive(1.6f, Key.W);
                    float deepest = rig.TakeDeepest();
                    float reach = rig.CargoReach(Vector3.forward);
                    Debug.Log($"[wall] {prefab} with {size:F2} cargo: front {reach - face:F3} m past the wall, body {face - rig.Body.position.z:F2} m back, deepest {deepest:F3} m");
                    Assert.That(reach, Is.LessThan(face + Settled), prefab + ": cargo does not enter the wall");
                    Assert.That(reach, Is.GreaterThan(face - 0.05f), prefab + ": cargo reaches the wall");
                    Assert.That(deepest, Is.LessThan(Transient), prefab);
                    yield return rig.Drive(0.8f, Key.W, Key.A);
                    Assert.That(rig.TakeDeepest(), Is.LessThan(Transient), prefab + " sliding");
                    yield return rig.Drive(0.5f, Key.S);
                    yield return rig.Release(0.2f);
                    yield return rig.Tap();
                    Assert.That(rig.Player.CarriedCargo, Is.Null, prefab + " dropped");
                    Assert.That(rig.Socket.IsObstructing, Is.False);
                    yield return new WaitForSeconds(0.2f);
                }
            }
            finally
            {
                InputSystem.RemoveDevice(rig.Keyboard);
            }
        }
    }
}
#endif
