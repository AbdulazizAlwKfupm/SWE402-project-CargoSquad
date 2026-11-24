using System;
using System.Linq;
using CargoSquad.Cargo;
using CargoSquad.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CargoSquad.Editor
{
    // Heavy v4 is a rigid, articulated model: its mesh parts are parented to empties.
    // Humanoid retargeting can animate these transforms without adding an armature or skinning.
    public static class HeavyVisualSetup
    {
        const string ModelPath = "Assets/_CargoSquad/Characters/Heavy_v4.fbx";
        const string PrefabPath = "Assets/_CargoSquad/Prefabs/Characters/Heavy_new.prefab";
        const string AvatarPath = "Assets/_CargoSquad/Characters/Heavy_v4Avatar.asset";
        const string ScenePath = "Assets/_CargoSquad/Scenes/IndustrialDistrict.unity";
        static readonly (string human, string joint)[] Mapping = {
            ("Hips", "PELVIS"), ("Spine", "CHEST"), ("Head", "HEAD"),
            ("LeftUpperArm", "UARM_L"), ("LeftLowerArm", "FARM_L"), ("LeftHand", "HAND_L"),
            ("RightUpperArm", "UARM_R"), ("RightLowerArm", "FARM_R"), ("RightHand", "HAND_R"),
            ("LeftUpperLeg", "THIGH_L"), ("LeftLowerLeg", "SHIN_L"), ("LeftFoot", "FOOT_L"),
            ("RightUpperLeg", "THIGH_R"), ("RightLowerLeg", "SHIN_R"), ("RightFoot", "FOOT_R")
        };

        static Transform Joint(GameObject root, string prefix, string suffix) => root.GetComponentsInChildren<Transform>(true)
            .Single(t => t.name == prefix + suffix);
        static void Reference(UnityEngine.Object component, string property, UnityEngine.Object value)
        {
            var so = new SerializedObject(component);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static Transform Socket(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation;
            return t;
        }

        [MenuItem("Cargo Squad/Heavy v4/Rebuild animated visual prefab")]
        public static void RebuildPrefab()
        {
            string controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_CargoSquad/Art/Animations/Humanoid/Heavy_Player.controller")
                ? "Assets/_CargoSquad/Art/Animations/Humanoid/Heavy_Player.controller"
                : "Assets/_CargoSquad/Art/Animations/Humanoid/Navy_Survivor.controller";
            RebuildVisual(ModelPath, PrefabPath, AvatarPath, "Heavy_new", "HV4_J_", controller);
        }

        public static void RebuildVisual(string modelPath, string prefabPath, string avatarPath, string rootName,
            string prefix, string controllerPath = "Assets/_CargoSquad/Art/Animations/Humanoid/Navy_Survivor.controller")
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit mode.");
            var root = new GameObject(rootName);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                model.transform.SetParent(root.transform, false);
                // Keep the FBX axis/unit correction below an identity Animator/facing root.
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                model.transform.localScale = Vector3.one * 100;
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var a in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(a);
                foreach (var pair in Mapping) Joint(root, prefix, pair.joint).localRotation = Quaternion.identity;
                // Source geometry was made with arms down. Rotate its actual pivots to a T reference pose.
                Joint(root, prefix, "UARM_L").localRotation = Quaternion.Euler(0, 90, 0);
                Joint(root, prefix, "UARM_R").localRotation = Quaternion.Euler(0, -90, 0);
                var description = new HumanDescription {
                    human = Mapping.Select(p => new HumanBone { humanName = p.human, boneName = prefix + p.joint,
                        limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                    skeleton = root.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone {
                        name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                    upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
                    armStretch = 0.05f, legStretch = 0.05f
                };
                var avatar = AvatarBuilder.BuildHumanAvatar(root, description);
                if (!avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Heavy's own Humanoid Avatar is invalid.");
                avatar.name = System.IO.Path.GetFileNameWithoutExtension(avatarPath);
                var existing = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
                if (existing) { EditorUtility.CopySerialized(avatar, existing); UnityEngine.Object.DestroyImmediate(avatar); avatar = existing; }
                else AssetDatabase.CreateAsset(avatar, avatarPath);
                var animator = root.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    controllerPath);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var hand = Joint(root, prefix, "HAND_R");
                var gun = Joint(root, prefix, "GUN");
                // Align the gun with the wrist while preserving its authored grip offset and orientation.
                // Capture the offset in the original posed FBX, not the reference T-pose.
                var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                var sourceHand = Joint(source, prefix, "HAND_R"); var sourceGun = Joint(source, prefix, "GUN");
                var handPosition = sourceHand.InverseTransformPoint(sourceGun.position);
                var handRotation = Quaternion.Inverse(sourceHand.rotation) * sourceGun.rotation;
                UnityEngine.Object.DestroyImmediate(source);
                var handSocket = Socket("RightHand_Weapon", hand, handPosition, handRotation);
                gun.SetParent(handSocket, false); gun.localPosition = Vector3.zero; gun.localRotation = Quaternion.identity;
                var back = Socket("Back_Weapon", Joint(root, prefix, "CHEST"), new Vector3(0.001f, 0.0025f, 0.002f), Quaternion.Euler(0, 0, 155));
                // Cargo sockets must use world metres and forward +Z, never the 100x/Z-up model space.
                var cargo = Socket("CargoMount_Front", root.transform, new Vector3(0, 1.1f, 0.3f), Quaternion.identity)
                    .gameObject.AddComponent<CargoCarrySocket>();
                var visual = root.AddComponent<SurvivorVisual>();
                if (prefix == "HV4_J_" || prefix == "MR1_J_")
                    Reference(visual, "definition", AssetDatabase.LoadAssetAtPath<CargoSquad.Player.CharacterDefinition>(
                        "Assets/_CargoSquad/ScriptableObjects/Characters/" + (prefix == "HV4_J_" ? "Heavy" : "Medic Soldier") + ".asset"));
                Reference(visual, "animator", animator); Reference(visual, "weapon", gun);
                Reference(visual, "handSocket", handSocket); Reference(visual, "stowSocket", back);
                // Preserve the visual timing/tuning from the existing Heavy scene instance.
                var tuning = new SerializedObject(visual);
                tuning.FindProperty("turnSpeed").floatValue = 600f;
                tuning.FindProperty("fullSpeedReference").floatValue = 3f;
                tuning.FindProperty("maxPlaybackSpeed").floatValue = 1f;
                tuning.FindProperty("speedDamping").floatValue = 0.06f;
                tuning.ApplyModifiedPropertiesWithoutUndo();
                var carry = root.AddComponent<FrontCarryVisual>();
                Reference(carry, "animator", animator); Reference(carry, "socket", cargo);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("Cargo Squad/Heavy v4/Configure open Industrial District")]
        public static void ConfigureScene()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open IndustrialDistrict in Edit mode.");
            if (scene.GetRootGameObjects().Any(g => g.name == "CharacterDeployment"))
            {
                CharacterDeploymentSetup.ConfigureOpenScene();
                return;
            }
            var player = scene.GetRootGameObjects().Single(g => g.name == "PlayerPlaceholder");
            // Replace only this visual branch. Physics and gameplay components stay on the player.
            foreach (Transform child in player.transform.Cast<Transform>().ToArray())
            {
                if (child.name == "Heavy_new" || child.name == "Heavy_v4" || child.name.TrimEnd('.') == "HeavyVisualRoot") UnityEngine.Object.DestroyImmediate(child.gameObject);
                else if (child.name == "NavySoldier_Visual") child.gameObject.SetActive(false);
            }
            var heavy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), player.transform);
            heavy.transform.localPosition = new Vector3(0, -1, 0);
            heavy.transform.localRotation = Quaternion.identity;
            heavy.transform.localScale = Vector3.one;
            Reference(player.GetComponent<CargoInteractor>(), "carrySocket", heavy.GetComponentInChildren<CargoCarrySocket>());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
