using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CargoSquad.Cargo;
using CargoSquad.Visuals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CargoSquad.Editor
{
    /// <summary>
    /// Character art import pipeline: configures the Blender FBX imports (Humanoid), builds the shared Animator
    /// controller, visual-only prefabs and the non-destructive CharacterShowcase scene.
    /// Also exposes a tiny file-based command bridge (Temp/CharacterArt/command.txt) so the pipeline can be run
    /// by automation while the editor is open.
    /// </summary>
    [InitializeOnLoad]
    internal static class CharacterArtPipeline
    {
        private const string Art = "Assets/_CargoSquad/Art";
        private const string Survivors = Art + "/Characters/Survivors";
        private const string Enemies = Art + "/Characters/Enemies";
        private const string AnimFbx = Survivors + "/Navy/Navy.fbx";   // reference rig + all shared humanoid clips
        private const string AnimFolder = Art + "/Animations/Humanoid";
        private const string EnemyAnimFolder = Art + "/Animations/Enemies";
        private const string PrefabFolder = "Assets/_CargoSquad/Prefabs/Characters";
        private const string SourceScene = "Assets/_CargoSquad/Scenes/IndustrialDistrict.unity";
        private const string ShowcaseScene = "Assets/_CargoSquad/Scenes/CharacterShowcase.unity";
        private const string FrontCarryScene = "Assets/_CargoSquad/Scenes/CharacterShowcase_FrontCarry.unity";
        // Size given to the example cargo in the gameplay scene. Cargo size is per object; code reads it from the cargo.
        private static readonly Vector3 FrontCarryCrateSize = new Vector3(0.60f, 0.45f, 0.45f);
        // Cargo_Front marks the centre of that example crate; the carry socket sits on its body-side face.
        private const float SocketBehindCargoFront = 0.225f;

        private static readonly string[] HumanBones =
        {
            "Hips", "Spine", "Chest", "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
            "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes"
        };

        private static readonly string[] LoopClips =
            { "Idle", "Walk", "Run", "CarryIdle", "CarryWalk", "CarryRun", "FrontCarryIdle", "FrontCarryWalk", "FrontCarryRun",
              "Aim", "Move", "Stunned", "Alert" };

        private sealed class Survivor
        {
            public string Name, Prefab, Weapon, Prefix;
        }

        private static readonly Survivor[] SurvivorList =
        {
            new Survivor { Name = "Navy", Prefab = "NavySoldier_Visual", Weapon = "Navy_Rifle", Prefix = "" },
            new Survivor { Name = "Heavy", Prefab = "Heavy_Visual", Weapon = "Heavy_Shotgun", Prefix = "" },
            new Survivor { Name = "Scout", Prefab = "Scout_Visual", Weapon = "Scout_Pistol", Prefix = "Pistol_" },
            new Survivor { Name = "Medic", Prefab = "Medic_Visual", Weapon = "Medic_StunDevice", Prefix = "Stun_" },
        };

        private static readonly string[] EnemyList = { "MutatedWolf", "ArmoredCreature", "AcidSpitter" };

        // ------------------------------------------------------------------ command bridge
        private static readonly string BridgeFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/CharacterArt"));
        private static double nextPoll;

        static CharacterArtPipeline()
        {
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll)
                return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            string commandFile = Path.Combine(BridgeFolder, "command.txt");
            if (!File.Exists(commandFile) || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            string[] parts;
            try
            {
                parts = File.ReadAllText(commandFile).Trim().Split(new[] { ' ' }, 3);
                File.Delete(commandFile);
            }
            catch (IOException)
            {
                return;
            }

            var log = new StringBuilder();
            Application.LogCallback capture = (message, stack, type) =>
                log.AppendLine(type == LogType.Log ? message : $"[{type}] {message}");
            Application.logMessageReceived += capture;
            string status = "DONE";
            try
            {
                MethodInfo method = typeof(CharacterArtPipeline).GetMethod(parts[1],
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                    throw new InvalidOperationException($"Unknown command {parts[1]}");
                object result = method.Invoke(null, method.GetParameters().Length == 0
                    ? null
                    : new object[] { parts.Length > 2 ? parts[2] : string.Empty });
                if (result != null)
                    log.AppendLine(result.ToString());
            }
            catch (Exception exception)
            {
                status = "ERROR";
                log.AppendLine((exception.InnerException ?? exception).ToString());
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            Directory.CreateDirectory(BridgeFolder);
            File.WriteAllText(Path.Combine(BridgeFolder, $"result_{parts[0]}.txt"), $"{status}\n{log}");
        }

        private static string Refresh()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return "refreshed";
        }

        /// <summary>Writes the in-memory state of the loaded scenes to Temp (as copies) so unsaved changes can be diffed.</summary>
        private static string DumpOpenScenes()
        {
            var text = new StringBuilder();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                string copy = $"Assets/_CargoSquad/Scenes/__dump_{scene.name}.unity";
                bool saved = EditorSceneManager.SaveScene(scene, copy, true);
                string target = Path.Combine(BridgeFolder, scene.name + ".dump.unity");
                if (saved)
                {
                    File.Copy(copy, target, true);
                    AssetDatabase.DeleteAsset(copy);
                }
                text.AppendLine($"{scene.path} dirty={scene.isDirty} dumped={saved} -> {target}");
            }
            return text.ToString();
        }

        /// <summary>Reloads the active scene from disk, dropping unsaved changes. Only call after checking what they are.</summary>
        /// <summary>Leaves a test runner scratch scene (InitTestScene*) and reopens a project scene. Refuses other dirty scenes.</summary>
        private static string OpenProjectScene(string path)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.isDirty && !Path.GetFileName(active.path).StartsWith("InitTestScene"))
                throw new InvalidOperationException(active.path + " has unsaved changes.");
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return "opened " + path;
        }

        private static string DiscardSceneChanges()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode first.");
            string path = SceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return $"reloaded {path} dirty={SceneManager.GetActiveScene().isDirty}";
        }

        private static string ProbeClips(string prefab) => CharacterArtProbe.Clips(string.IsNullOrEmpty(prefab) ? "NavySoldier_Visual" : prefab);

        private static string Status()
        {
            var text = new StringBuilder();
            text.AppendLine($"playing={EditorApplication.isPlaying} compiling={EditorApplication.isCompiling}");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                text.AppendLine($"scene {scene.path} dirty={scene.isDirty} loaded={scene.isLoaded}");
            }
            return text.ToString();
        }

        // ------------------------------------------------------------------ tests (bridge lives in the test assembly)
        private static string RunTests(string mode)
        {
            // Entering play mode with an unsaved scene opens a modal Save prompt that blocks the editor.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException($"{SceneManager.GetSceneAt(i).path} has unsaved changes; save or discard them first.");
            Type bridge = Type.GetType("CargoSquad.Tests.CharacterArtTestBridge, CargoSquad.Tests");
            if (bridge == null)
                throw new InvalidOperationException("CharacterArtTestBridge not found.");
            return (string)bridge.GetMethod("Run").Invoke(null, new object[] { mode });
        }

        // ------------------------------------------------------------------ import configuration
        [MenuItem("Cargo Squad/Character Art/1. Configure Model Imports")]
        private static string ConfigureImports()
        {
            var text = new StringBuilder();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            text.AppendLine(CharacterArtMaterials.Build());
            foreach (Survivor survivor in SurvivorList)
            {
                string path = $"{Survivors}/{survivor.Name}/{survivor.Name}.fbx";
                if (!File.Exists(path))
                    continue;
                ConfigureHumanoid(path, path == AnimFbx);
                if (path == AnimFbx)
                    ConfigureClips(path, true);
                text.AppendLine(CharacterArtMaterials.Remap(path));
                text.AppendLine(DescribeModel(path));
            }

            if (AssetDatabase.DeleteAsset(Art + "/Animations/Humanoid/Survivor_Anims.fbx"))
                text.AppendLine("removed superseded Survivor_Anims.fbx (clips now live in Navy.fbx)");

            foreach (string enemy in EnemyList)
            {
                string path = $"{Enemies}/{enemy}/{enemy}.fbx";
                if (!File.Exists(path))
                    continue;
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                Common(importer);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.SaveAndReimport();
                ConfigureClips(path, false);
                text.AppendLine(CharacterArtMaterials.Remap(path));
                text.AppendLine(DescribeModel(path));
            }

            return text.ToString();
        }

        private static void Common(ModelImporter importer)
        {
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.importNormals = ModelImporterNormals.Import;
            importer.optimizeGameObjects = false;
            importer.isReadable = false;
        }

        private static void ConfigureHumanoid(string path, bool animation)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            // Re-exported skeletons must not reuse a stored human description: drop it by importing as Generic first.
            Common(importer);
            importer.importAnimation = animation;
            importer.clipAnimations = Array.Empty<ModelImporterClipAnimation>();
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.humanDescription = new HumanDescription();
            importer.SaveAndReimport();
            importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.SaveAndReimport();
        }

        private static void ConfigureClips(string path, bool humanoid)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string name = clip.takeName.Contains("|") ? clip.takeName.Substring(clip.takeName.LastIndexOf('|') + 1) : clip.takeName;
                clip.name = name;
                string bare = name.Contains("_") ? name.Substring(name.LastIndexOf('_') + 1) : name;
                clip.loopTime = LoopClips.Contains(bare);
                clip.loopPose = false;
                if (!humanoid)
                    clip.name = bare;
                if (!humanoid)
                    continue;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static string DescribeModel(string path)
        {
            var text = new StringBuilder();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            Avatar avatar = assets.OfType<Avatar>().FirstOrDefault();
            text.AppendLine($"{path}");
            text.AppendLine($"  avatar={(avatar ? avatar.name : "none")} valid={(avatar && avatar.isValid)} human={(avatar && avatar.isHuman)}");
            if (model)
            {
                Bounds bounds = default;
                bool first = true;
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                {
                    if (first) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds);
                    first = false;
                    foreach (Material material in renderer.sharedMaterials)
                        if (!material || material.shader.name.Contains("Error") || material.shader.name.Contains("Hidden"))
                            text.AppendLine($"  BAD MATERIAL on {renderer.name}: {(material ? material.shader.name : "null")}");
                }
                var shaders = model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials)
                    .Where(m => m).Select(m => m.shader.name).Distinct();
                text.AppendLine($"  renderers={model.GetComponentsInChildren<Renderer>().Length} skinned={model.GetComponentsInChildren<SkinnedMeshRenderer>().Length} shaders=[{string.Join(", ", shaders)}]");
                text.AppendLine($"  bounds min={bounds.min:F3} max={bounds.max:F3} rootScale={model.transform.localScale} rootRot={model.transform.localEulerAngles}");
                int tris = model.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh)
                    .Concat(model.GetComponentsInChildren<SkinnedMeshRenderer>().Select(s => s.sharedMesh))
                    .Where(m => m).Sum(m => Enumerable.Range(0, m.subMeshCount).Sum(i => (int)m.GetIndexCount(i) / 3));
                text.AppendLine($"  triangles={tris} transforms={model.GetComponentsInChildren<Transform>().Length}");
            }
            foreach (AnimationClip clip in assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                text.AppendLine($"  clip {clip.name} length={clip.length:F3}s loop={clip.isLooping} humanMotion={clip.humanMotion}");
            return text.ToString();
        }

        // ------------------------------------------------------------------ animator + prefabs
        private static Dictionary<string, AnimationClip> LoadClips(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name, c => c);
        }

        private static AnimatorController BuildSurvivorController(string name, Dictionary<string, AnimationClip> clips, string prefix,
            string carry = "")
        {
            AnimationClip Clip(string clip)
            {
                if (clips.TryGetValue(prefix + clip, out AnimationClip specific))
                    return specific;
                if (clips.TryGetValue(clip, out AnimationClip shared))
                    return shared;
                throw new InvalidOperationException($"Missing clip {clip}");
            }

            string path = $"{AnimFolder}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Carrying", AnimatorControllerParameterType.Bool);
            foreach (string trigger in new[] { "Shoot", "Hit", "Death", "Regrip", "CargoSlip" })
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Aiming", AnimatorControllerParameterType.Bool);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Idle"), 0f);
            tree.AddChild(Clip("Walk"), 1.6f);
            tree.AddChild(Clip("Run"), 6f);

            AnimatorState carryState = controller.CreateBlendTreeInController("CarryLocomotion", out BlendTree carryTree, 0);
            carryTree.blendParameter = "Speed";
            carryTree.useAutomaticThresholds = false;
            carryTree.AddChild(Clip(carry + "CarryIdle"), 0f);
            carryTree.AddChild(Clip(carry + "CarryWalk"), 1.4f);
            carryTree.AddChild(Clip(carry + "CarryRun"), 5f);

            AnimatorState pickup = machine.AddState("Pickup");
            pickup.motion = Clip(carry + "Pickup");
            AnimatorState drop = machine.AddState("Drop");
            drop.motion = Clip(carry + "Drop");
            machine.defaultState = locomotion;

            Transition(locomotion, pickup, 0.06f, null, ("Carrying", true));
            Transition(pickup, carryState, 0.1f, 0.85f);
            Transition(pickup, drop, 0.08f, null, ("Carrying", false));
            Transition(carryState, drop, 0.08f, null, ("Carrying", false));
            Transition(drop, locomotion, 0.15f, 0.82f);
            Transition(drop, pickup, 0.08f, null, ("Carrying", true));

            // Optional action states: only wired when the clips exist (Gate B).
            void AddAction(string clipName, string trigger, AnimatorState returnTo, AnimatorState from, string variant = "")
            {
                if (!clips.ContainsKey(prefix + clipName) && !clips.ContainsKey(clipName))
                    return;
                AnimatorState state = machine.AddState(clipName);
                state.motion = Clip(variant + clipName);
                AnimatorStateTransition enter = from.AddTransition(state);
                enter.hasExitTime = false;
                enter.duration = 0.06f;
                enter.AddCondition(AnimatorConditionMode.If, 0f, trigger);
                if (returnTo)
                    Transition(state, returnTo, 0.12f, 0.9f);
            }

            AddAction("Shoot", "Shoot", locomotion, locomotion);
            AddAction("Hit", "Hit", locomotion, locomotion);
            AddAction("Regrip", "Regrip", carryState, carryState, carry);
            AddAction("CargoSlip", "CargoSlip", locomotion, carryState, carry);
            if (clips.ContainsKey(prefix + "Death") || clips.ContainsKey("Death"))
            {
                AnimatorState death = machine.AddState("Death");
                death.motion = Clip("Death");
                AnimatorStateTransition any = machine.AddAnyStateTransition(death);
                any.hasExitTime = false;
                any.duration = 0.08f;
                any.canTransitionToSelf = false;
                any.AddCondition(AnimatorConditionMode.If, 0f, "Death");
            }
            if (clips.ContainsKey(prefix + "Aim") || clips.ContainsKey("Aim"))
            {
                AnimatorState aim = machine.AddState("Aim");
                aim.motion = Clip("Aim");
                Transition(locomotion, aim, 0.12f, null, ("Aiming", true));
                Transition(aim, locomotion, 0.15f, null, ("Aiming", false));
                if (clips.ContainsKey(prefix + "Shoot") || clips.ContainsKey("Shoot"))
                {
                    AnimatorState shootAimed = machine.AddState("ShootAimed");
                    shootAimed.motion = Clip("Shoot");
                    AnimatorStateTransition fire = aim.AddTransition(shootAimed);
                    fire.hasExitTime = false;
                    fire.duration = 0.04f;
                    fire.AddCondition(AnimatorConditionMode.If, 0f, "Shoot");
                    Transition(shootAimed, aim, 0.1f, 0.9f);
                }
            }

            if (carry.Length > 0)
            {
                // The front-carry prototype pins the hands to the crate with humanoid IK.
                AnimatorControllerLayer[] layers = controller.layers;
                layers[0].iKPass = true;
                controller.layers = layers;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void Transition(AnimatorState from, AnimatorState to, float duration, float? exitTime,
            params (string parameter, bool value)[] conditions)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = exitTime.HasValue;
            if (exitTime.HasValue)
                transition.exitTime = exitTime.Value;
            transition.hasFixedDuration = true;
            transition.duration = duration;
            foreach ((string parameter, bool value) in conditions)
                transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameter);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        [MenuItem("Cargo Squad/Character Art/2. Build Visual Prefabs")]
        private static string BuildPrefabs()
        {
            var text = new StringBuilder();
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();
            Dictionary<string, AnimationClip> clips = LoadClips(AnimFbx);
            foreach (Survivor survivor in SurvivorList)
            {
                string path = $"{Survivors}/{survivor.Name}/{survivor.Name}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!model)
                    continue;
                // Front carry is the game's carry style; the overhead set is kept as a reference controller.
                AnimatorController controller = BuildSurvivorController(survivor.Name + "_Survivor", clips, survivor.Prefix, "Front");
                BuildSurvivorController(survivor.Name + "_Survivor_Overhead", clips, survivor.Prefix);
                AssetDatabase.DeleteAsset($"{AnimFolder}/{survivor.Name}_Survivor_FrontCarry.controller");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    instance.name = survivor.Prefab;
                    if (!instance.TryGetComponent(out Animator animator)) animator = instance.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (SkinnedMeshRenderer skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                        skinned.updateWhenOffscreen = true;
                    var visual = instance.AddComponent<SurvivorVisual>();
                    using (var settings = new SerializedObject(visual))
                    {
                        settings.FindProperty("animator").objectReferenceValue = animator;
                        settings.FindProperty("weapon").objectReferenceValue = FindDeep(instance.transform, survivor.Weapon);
                        settings.FindProperty("handSocket").objectReferenceValue = FindDeep(instance.transform, "RightHand_Weapon");
                        settings.FindProperty("stowSocket").objectReferenceValue = FindDeep(instance.transform,
                            survivor.Name == "Scout" || survivor.Name == "Medic" ? "Hip_Sidearm" : "Back_Weapon");
                        settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    // Reusable cargo attachment socket: clean transform under the model root, +Z away from the body.
                    Transform cargoFront = FindDeep(instance.transform, "Cargo_Front");
                    var mount = new GameObject("CargoMount_Front");
                    mount.transform.SetParent(instance.transform, false);
                    mount.transform.localPosition = instance.transform.InverseTransformPoint(cargoFront.position) -
                                                    Vector3.forward * SocketBehindCargoFront;
                    var socket = mount.AddComponent<CargoCarrySocket>();
                    var hands = instance.AddComponent<FrontCarryVisual>();
                    using (var settings = new SerializedObject(hands))
                    {
                        settings.FindProperty("animator").objectReferenceValue = animator;
                        settings.FindProperty("socket").objectReferenceValue = socket;
                        settings.ApplyModifiedPropertiesWithoutUndo();
                    }
                    string prefabPath = $"{PrefabFolder}/{survivor.Prefab}.prefab";
                    PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                    text.AppendLine($"prefab {prefabPath} avatar={(animator.avatar ? animator.avatar.name : "none")} human={animator.isHuman}" +
                                    $" colliders={instance.GetComponentsInChildren<Collider>().Length} bodies={instance.GetComponentsInChildren<Rigidbody>().Length}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            foreach (string enemy in EnemyList)
            {
                string path = $"{Enemies}/{enemy}/{enemy}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!model)
                    continue;
                Dictionary<string, AnimationClip> enemyClips = LoadClips(path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    instance.name = enemy + "_Visual";
                    if (enemyClips.Count > 0)
                    {
                        Directory.CreateDirectory(EnemyAnimFolder);
                        string controllerPath = $"{EnemyAnimFolder}/{enemy}.controller";
                        AssetDatabase.DeleteAsset(controllerPath);
                        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                        AnimatorStateMachine machine = controller.layers[0].stateMachine;
                        foreach (string state in new[] { "Idle", "Move", "Alert", "AttackWindup", "Attack", "Hit", "Stunned", "Death" })
                        {
                            if (!enemyClips.TryGetValue(state, out AnimationClip clip))
                                continue;
                            AnimatorState added = machine.AddState(state);
                            added.motion = clip;
                            if (state == "Idle")
                                machine.defaultState = added;
                        }
                        if (!instance.TryGetComponent(out Animator animator)) animator = instance.AddComponent<Animator>();
                        animator.runtimeAnimatorController = controller;
                        animator.applyRootMotion = false;
                        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    }
                    foreach (SkinnedMeshRenderer skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                        skinned.updateWhenOffscreen = true;
                    string prefabPath = $"{PrefabFolder}/{enemy}_Visual.prefab";
                    PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                    text.AppendLine($"prefab {prefabPath} clips={enemyClips.Count}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            AssetDatabase.SaveAssets();
            return text.ToString();
        }

        // ------------------------------------------------------------------ scenes
        private static Scene Open(string path, out bool wasLoaded)
        {
            Scene already = SceneManager.GetSceneByPath(path);
            wasLoaded = already.IsValid() && already.isLoaded;
            return wasLoaded ? already : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        private static void SaveAndRelease(Scene scene, bool wasLoaded)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Could not save {scene.path}.");
            if (!wasLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        /// <summary>Copies a scene file; an existing destination keeps its .meta and therefore its GUID.</summary>
        private static void CopyScene(string source, string destination)
        {
            if (File.Exists(destination))
            {
                File.Copy(source, destination, true);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
            }
            else if (!AssetDatabase.CopyAsset(source, destination))
            {
                throw new InvalidOperationException($"Could not copy {source}.");
            }
        }

        /// <summary>Puts the Navy visual under the unchanged gameplay root and hides only the capsule renderer.</summary>
        private static GameObject PlacePlayerVisual(Scene scene, StringBuilder text)
        {
            GameObject player = scene.GetRootGameObjects().Single(root => root.name == "PlayerPlaceholder");
            foreach (SurvivorVisual old in player.GetComponentsInChildren<SurvivorVisual>(true))
                UnityEngine.Object.DestroyImmediate(old.gameObject);
            var capsule = player.GetComponent<CapsuleCollider>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/NavySoldier_Visual.prefab");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, capsule.center.y - capsule.height * 0.5f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            var placeholderRenderer = player.GetComponent<MeshRenderer>();
            if (placeholderRenderer)
                placeholderRenderer.enabled = false;
            text.AppendLine($"{scene.name}: player visual at local {visual.transform.localPosition}, capsule renderer hidden");
            return visual;
        }

        /// <summary>Sets the size of the cargo objects in one scene (each cargo owns its size; nothing is global).</summary>
        private static void ResizeCargo(Scene scene, Vector3 size, StringBuilder text)
        {
            foreach (CargoItem cargo in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CargoItem>(true)))
            {
                var box = cargo.GetComponent<BoxCollider>();
                Transform item = cargo.transform;
                float oldHeight = box.size.y * item.lossyScale.y;
                Vector3 parentScale = item.parent ? item.parent.lossyScale : Vector3.one;
                item.localScale = new Vector3(size.x / (box.size.x * parentScale.x), size.y / (box.size.y * parentScale.y),
                    size.z / (box.size.z * parentScale.z));
                item.position += Vector3.down * (oldHeight - size.y) * 0.5f;
                EditorUtility.SetDirty(item);
                text.AppendLine($"{scene.name}: cargo {cargo.name} size {size} at {item.position:F3}");
            }
        }

        /// <summary>Scene-level override that keeps the overhead carry available for reference.</summary>
        private static void MakeOverheadReference(GameObject visual, string survivorName)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{AnimFolder}/{survivorName}_Survivor_Overhead.controller");
            if (!controller)
                throw new InvalidOperationException($"Missing overhead controller for {survivorName}. Build prefabs first.");
            visual.GetComponent<Animator>().runtimeAnimatorController = controller;
            FindDeep(visual.transform, "CargoMount_Front").gameObject.SetActive(false);   // interactor falls back to carryOffset
            visual.GetComponent<FrontCarryVisual>().enabled = false;
        }

        [MenuItem("Cargo Squad/Character Art/3. Integrate Front Carry Into IndustrialDistrict")]
        private static string IntegrateFrontCarry()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode first.");
            var text = new StringBuilder();
            Scene scene = Open(SourceScene, out bool wasLoaded);
            PlacePlayerVisual(scene, text);
            ResizeCargo(scene, FrontCarryCrateSize, text);
            SaveAndRelease(scene, wasLoaded);
            return text.ToString();
        }

        [MenuItem("Cargo Squad/Character Art/4. Build Overhead Reference Scene (CharacterShowcase)")]
        private static string BuildShowcase()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode first.");
            var text = new StringBuilder();
            if (!File.Exists(ShowcaseScene))
                CopyScene(SourceScene, ShowcaseScene);
            Scene scene = Open(ShowcaseScene, out bool wasLoaded);
            MakeOverheadReference(PlacePlayerVisual(scene, text), "Navy");
            ResizeCargo(scene, Vector3.one * 1.5f, text);

            GameObject lineup = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Character Lineup");
            if (lineup)
                UnityEngine.Object.DestroyImmediate(lineup);
            lineup = new GameObject("Character Lineup");
            SceneManager.MoveGameObjectToScene(lineup, scene);
            float x = -9f;
            foreach (string entry in SurvivorList.Select(s => s.Prefab).Concat(EnemyList.Select(e => e + "_Visual")))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{entry}.prefab");
                if (!prefab)
                    continue;
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                item.transform.SetParent(lineup.transform, false);
                item.transform.SetPositionAndRotation(new Vector3(x, 0f, -9.5f), Quaternion.Euler(0f, 180f, 0f));
                Survivor survivor = SurvivorList.FirstOrDefault(s => s.Prefab == entry);
                if (survivor != null)
                    MakeOverheadReference(item, survivor.Name);
                x += entry.Contains("Armored") ? 4f : 3f;
                text.AppendLine($"lineup {entry}");
            }

            SaveAndRelease(scene, wasLoaded);
            return text.ToString();
        }

        [MenuItem("Cargo Squad/Character Art/5. Build Front-Carry Lineup Scene")]
        private static string BuildFrontCarryShowcase()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play mode first.");
            Scene source = SceneManager.GetSceneByPath(SourceScene);
            if (source.IsValid() && source.isDirty)
                throw new InvalidOperationException("Save IndustrialDistrict first.");
            var text = new StringBuilder();
            Scene open = SceneManager.GetSceneByPath(FrontCarryScene);
            if (open.IsValid() && open.isLoaded)
                EditorSceneManager.CloseScene(open, true);
            CopyScene(SourceScene, FrontCarryScene);   // the real gameplay scene plus a lineup of all four survivors
            Scene scene = Open(FrontCarryScene, out bool wasLoaded);
            GameObject player = scene.GetRootGameObjects().Single(root => root.name == "PlayerPlaceholder");
            if (!player.GetComponentInChildren<FrontCarryVisual>())
                throw new InvalidOperationException("IndustrialDistrict has no front-carry visual yet. Run step 3 first.");

            var lineup = new GameObject("Front Carry Lineup");
            SceneManager.MoveGameObjectToScene(lineup, scene);
            float x = -4.5f;
            foreach (Survivor survivor in SurvivorList)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{survivor.Prefab}.prefab");
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                item.transform.SetParent(lineup.transform, false);
                item.transform.SetPositionAndRotation(new Vector3(x, 0f, -9.5f), Quaternion.Euler(0f, 180f, 0f));
                x += 3f;
            }

            SaveAndRelease(scene, wasLoaded);
            text.AppendLine($"saved {FrontCarryScene}");
            return text.ToString();
        }

        [MenuItem("Cargo Squad/Character Art/Run All (Import, Prefabs, Scenes)")]
        private static string BuildAll()
        {
            return ConfigureImports() + BuildPrefabs() + IntegrateFrontCarry() + BuildShowcase() + BuildFrontCarryShowcase();
        }
    }
}
