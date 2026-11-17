using System;
using System.IO;
using System.Linq;
using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.UI;
using CargoSquad.Visuals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CargoSquad.Editor
{
    public static class CharacterDeploymentSetup
    {
        const string Root = "Assets/_CargoSquad/";
        const string ControllerPath = Root + "Art/Animations/Humanoid/Heavy_Player.controller";
        const string BashPath = Root + "Art/Animations/Humanoid/Heavy_ShoulderBash.anim";
        const string MedicPrefab = Root + "Prefabs/Characters/Medic_redisgned.prefab";

        [MenuItem("Cargo Squad/Deployment/Rebuild characters, portraits and selection")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit mode.");
            EnsureTextResources();
            MedicMaterialSetup.Configure();
            BuildBash();
            HeavyVisualSetup.RebuildPrefab();
            string medicController = Root + "Art/Animations/Humanoid/Medic_Player.controller";
            TuneLocomotion(medicController, Root + "ScriptableObjects/Characters/Medic Soldier.asset");
            HeavyVisualSetup.RebuildVisual(Root + "Characters/Medic_Redesign_v1.fbx", MedicPrefab,
                Root + "Characters/Medic_Redesign_v1Avatar.asset", "Medic_redisgned", "MR1_J_", medicController);
            BuildPortrait(Root + "Prefabs/Characters/Heavy_new.prefab", "Heavy", "HV4_J_HEAD");
            BuildPortrait(MedicPrefab, "Medic", "MR1_J_HEAD");
            ConfigureOpenScene();
            AssetDatabase.SaveAssets();
        }
        public static void EnsureTextResources()
        {
            if (AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset")) return;
            string package = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).First();
            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        static void Set(UnityEngine.Object obj, string name, UnityEngine.Object value)
        {
            var so = new SerializedObject(obj); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        }
        static T Ensure<T>(GameObject obj) where T : Component => obj.GetComponent<T>() ?? obj.AddComponent<T>();

        [MenuItem("Cargo Squad/Deployment/Configure open Industrial District")]
        public static void ConfigureOpenScene()
        {
            var scene = SceneManager.GetSceneByPath(Root + "Scenes/IndustrialDistrict.unity");
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open IndustrialDistrict in Edit mode.");
            var player = scene.GetRootGameObjects().Single(g => g.name == "PlayerPlaceholder");
            var existing = player.GetComponentsInChildren<SurvivorVisual>(true);
            var heavySettings = Capture(existing.FirstOrDefault(v => v.name == "Heavy_new" || v.name == "Heavy_v4"));
            var medicSettings = Capture(existing.FirstOrDefault(v => v.name == "Medic_redisgned" || v.name == "Medic_Redesign_v1"));
            // Only these known visual branches are regenerated. Other player children/components are preserved.
            foreach (var child in player.transform.Cast<Transform>().ToArray())
            {
                if (child.name == "Heavy_new" || child.name == "Heavy_v4" || child.name.TrimEnd('.') == "HeavyVisualRoot" ||
                    child.name == "Medic_redisgned" || child.name == "Medic_Redesign_v1") UnityEngine.Object.DestroyImmediate(child.gameObject);
                else if (child.name == "NavySoldier_Visual" || child.name == "Medic_Visual") child.gameObject.SetActive(false);
            }
            var heavy = Visual(Root + "Prefabs/Characters/Heavy_new.prefab", player.transform);
            var medic = Visual(MedicPrefab, player.transform);
            Restore(heavySettings, heavy); Restore(medicSettings, medic);
            MedicMaterialSetup.ApplyToVisual(medic.gameObject);
            Ensure<Health>(player); Ensure<CharacterAbility>(player);
            var selectionObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "CharacterDeployment") ?? new GameObject("CharacterDeployment");
            var selection = Ensure<CharacterSelection>(selectionObject);
            Set(selection, "player", player.GetComponent<PlayerController>());
            Set(selection, "heavy", heavy); Set(selection, "medic", medic);
            Set(selection, "heavyDefinition", AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "ScriptableObjects/Characters/Heavy.asset"));
            Set(selection, "medicDefinition", AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Root + "ScriptableObjects/Characters/Medic Soldier.asset"));
            Set(selection, "gameplayOverlay", UnityEngine.Object.FindFirstObjectByType<CargoMissionOverlay>());
            Set(player.GetComponent<CargoInteractor>(), "carrySocket", heavy.GetComponentInChildren<CargoCarrySocket>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        sealed class SavedVisualSettings
        {
            public float[] facing;
            public float[] carry;
            public Vector3 grip;
            public System.Collections.Generic.Dictionary<string, Material[]> materials;
        }
        static readonly string[] FacingSettings = { "turnSpeed", "fullSpeedReference", "maxPlaybackSpeed", "speedDamping" };
        static readonly string[] CarrySettings = { "lockDelay", "lockSpeed" };
        static SavedVisualSettings Capture(SurvivorVisual visual)
        {
            if (!visual) return null;
            var facing = new SerializedObject(visual); var hands = visual.GetComponent<FrontCarryVisual>();
            var result = new SavedVisualSettings {
                facing = FacingSettings.Select(n => facing.FindProperty(n).floatValue).ToArray(),
                materials = visual.GetComponentsInChildren<Renderer>(true).ToDictionary(
                    r => AnimationUtility.CalculateTransformPath(r.transform, visual.transform), r => r.sharedMaterials)
            };
            if (hands)
            {
                var carry = new SerializedObject(hands);
                result.carry = CarrySettings.Select(n => carry.FindProperty(n).floatValue).ToArray();
                result.grip = carry.FindProperty("grip").vector3Value;
            }
            return result;
        }
        static void Restore(SavedVisualSettings saved, SurvivorVisual visual)
        {
            if (saved == null) return;
            var facing = new SerializedObject(visual);
            for (int i = 0; i < FacingSettings.Length; i++) facing.FindProperty(FacingSettings[i]).floatValue = saved.facing[i];
            facing.ApplyModifiedPropertiesWithoutUndo();
            if (saved.carry != null)
            {
                var carry = new SerializedObject(visual.GetComponent<FrontCarryVisual>());
                for (int i = 0; i < CarrySettings.Length; i++) carry.FindProperty(CarrySettings[i]).floatValue = saved.carry[i];
                carry.FindProperty("grip").vector3Value = saved.grip; carry.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                string path = AnimationUtility.CalculateTransformPath(renderer.transform, visual.transform);
                if (!saved.materials.TryGetValue(path, out var materials)) continue;
                var current = renderer.sharedMaterials;
                var restored = materials.Select((m, i) => m ? m : i < current.Length ? current[i] : null).ToArray();
                if (current.SequenceEqual(restored)) continue;
                renderer.sharedMaterials = restored; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }
        static SurvivorVisual Visual(string path, Transform player)
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), player);
            visual.transform.localPosition = new Vector3(0, -1, 0);
            visual.transform.localRotation = Quaternion.identity; visual.transform.localScale = Vector3.one;
            // Serialized inactive; selection enables exactly one when Start is pressed.
            visual.SetActive(false); return visual.GetComponent<SurvivorVisual>();
        }
        static void BuildBash()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BashPath);
            if (!clip) { clip = new AnimationClip { name = "Heavy_ShoulderBash", frameRate = 60 }; AssetDatabase.CreateAsset(clip, BashPath); }
            clip.ClearCurves();
            // Humanoid muscle curves retarget through Heavy's own Avatar, including real arm/leg motion.
            foreach (string muscle in HumanTrait.MuscleName)
            {
                float brace = 0f;
                switch (muscle)
                {
                    case "Spine Front-Back": brace = .38f; break;
                    case "Spine Twist Left-Right": brace = -.22f; break;
                    case "Head Nod Down-Up": brace = -.12f; break;
                    case "Left Arm Down-Up": brace = -.42f; break;
                    case "Left Arm Front-Back": brace = -.58f; break;
                    case "Left Forearm Stretch": brace = -.78f; break;
                    case "Right Arm Down-Up": brace = -.48f; break;
                    case "Right Arm Front-Back": brace = .18f; break;
                    case "Right Forearm Stretch": brace = -.66f; break;
                    case "Left Upper Leg Front-Back": brace = .28f; break;
                    case "Right Upper Leg Front-Back": brace = -.18f; break;
                    case "Left Lower Leg Stretch": brace = -.3f; break;
                    case "Right Lower Leg Stretch": brace = -.22f; break;
                }
                var curve = new AnimationCurve(new Keyframe(0, brace * .3f), new Keyframe(.09f, brace),
                    new Keyframe(.42f, brace), new Keyframe(.65f, brace * .2f));
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscle), curve);
            }
            var sourceController = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Art/Animations/Humanoid/Navy_Survivor.controller");
            var locomotion = (BlendTree)sourceController.layers[0].stateMachine.states.Single(s => s.state.name == "Locomotion").state.motion;
            var idle = (AnimationClip)locomotion.children[0].motion;
            // Humanoid muscle curves need the body's reference translation as well as limb rotations.
            // An absent RootT.y would put the body centre at the origin and sink the mesh into the floor.
            foreach (var binding in AnimationUtility.GetCurveBindings(idle))
            {
                if (!binding.propertyName.StartsWith("RootT.") && !binding.propertyName.StartsWith("RootQ.")) continue;
                var sourceCurve = AnimationUtility.GetEditorCurve(idle, binding);
                float value = sourceCurve.Evaluate(0f);
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, .65f, value));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(idle);
            settings.loopTime = false; settings.startTime = 0f; settings.stopTime = .65f;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath))
                AssetDatabase.CopyAsset(Root + "Art/Animations/Humanoid/Navy_Survivor.controller", ControllerPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!controller.parameters.Any(p => p.name == "Bash")) controller.AddParameter("Bash", AnimatorControllerParameterType.Trigger);
            if (!controller.parameters.Any(p => p.name == "BashSpeed")) controller.AddParameter("BashSpeed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "ShoulderBash") ?? machine.AddState("ShoulderBash", new Vector3(650, 0));
            state.motion = clip; state.speedParameter = "BashSpeed"; state.speedParameterActive = true; state.writeDefaultValues = true;
            if (!machine.anyStateTransitions.Any(t => t.destinationState == state))
            {
                var enter = machine.AddAnyStateTransition(state); enter.AddCondition(AnimatorConditionMode.If, 0, "Bash");
                enter.hasExitTime = false; enter.duration = .05f; enter.canTransitionToSelf = false;
            }
            if (state.transitions.Length == 0)
            {
                var exit = state.AddTransition(machine.states.Single(s => s.state.name == "Locomotion").state);
                exit.hasExitTime = true; exit.exitTime = 1; exit.duration = .08f;
            }
            TuneLocomotion(ControllerPath, Root + "ScriptableObjects/Characters/Heavy.asset");
            EditorUtility.SetDirty(controller); EditorUtility.SetDirty(clip); AssetDatabase.SaveAssets();
        }
        static void TuneLocomotion(string path, string definitionPath)
        {
            if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(path))
                AssetDatabase.CopyAsset(Root + "Art/Animations/Humanoid/Navy_Survivor.controller", path);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(definitionPath);
            foreach (var state in controller.layers[0].stateMachine.states)
            {
                if (!(state.state.motion is BlendTree tree) || tree.blendParameter != "Speed") continue;
                var children = tree.children;
                if (children.Length != 3) continue;
                float load = state.state.name == "CarryLocomotion" ? .9f : 1f;
                tree.useAutomaticThresholds = false;
                children[0].threshold = 0f;
                // Leave a little headroom for ground contact damping in the existing velocity controller.
                children[1].threshold = Mathf.Max(.1f, definition.WalkSpeed * load - .15f);
                children[2].threshold = Mathf.Max(children[1].threshold + .1f, definition.RunSpeed * load - .15f);
                tree.children = children; EditorUtility.SetDirty(tree);
            }
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        }
        static void BuildPortrait(string prefabPath, string name, string jointName)
        {
            string folder = Root + "Art/UI"; Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            var preview = new PreviewRenderUtility();
            try
            {
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
                preview.AddSingleGO(model);
                var animator = model.GetComponent<Animator>(); animator.Rebind(); animator.Update(.05f);
                var head = model.GetComponentsInChildren<Transform>(true).Single(t => t.name == jointName);
                Bounds bounds = default; bool first = true;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                {
                    renderer.enabled = renderer.transform.IsChildOf(head);
                    if (renderer.enabled) { if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds); }
                }
                if (first) throw new InvalidOperationException(name + " has no head mesh.");
                preview.camera.orthographic = true; preview.camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.16f;
                preview.camera.transform.position = bounds.center + new Vector3(.16f, .035f, 1f).normalized * 3f;
                preview.camera.transform.LookAt(bounds.center); preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 10f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = Color.clear;
                preview.lights[0].intensity = 1.5f; preview.lights[0].transform.rotation = Quaternion.Euler(30, 210, 0);
                preview.lights[1].intensity = 1f; preview.ambientColor = new Color(.55f, .55f, .55f);
                preview.BeginStaticPreview(new Rect(0, 0, 512, 512)); preview.Render(true);
                var texture = preview.EndStaticPreview(); string path = folder + "/" + name + "_Head.png";
                File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            }
            finally { preview.Cleanup(); ShaderUtil.allowAsyncCompilation = async; }
        }
    }
}
