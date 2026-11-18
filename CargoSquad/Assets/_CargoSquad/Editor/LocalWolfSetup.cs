using System;
using System.IO;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Combat;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace CargoSquad.Editor
{
    public static class LocalWolfSetup
    {
        public const string Folder = "Assets/_CargoSquad/Art/LocalMutatedWolfV2";
        public const string ScenePath = "Assets/_CargoSquad/Scenes/IndustrialDistrict.unity";
        public const string PrefabPath = Folder + "/Resources/MutatedWolfV2.prefab";

        [MenuItem("Cargo Squad/Enemies/Install Local Wolf V2")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Install Wolf V2 in Edit Mode.");
            if (!File.Exists(Folder + "/export-manifest.json"))
                throw new FileNotFoundException("Run export_wolf_v2_unity.py with the approved local Blender source first.");
            AssetDatabase.Refresh();
            string[] animations = { "idle_loop", "walk_loop", "run_loop", "bite" };
            foreach (string name in animations.Append("MutatedWolfV2"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "/" + name + ".fbx");
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeBones = false;
                importer.importAnimation = name != "MutatedWolfV2";
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                if (importer.importAnimation)
                {
                    var clips = importer.defaultClipAnimations;
                    if (clips.Length == 0) throw new InvalidOperationException("Missing animation " + name);
                    clips[0].name = name;
                    clips[0].loopTime = name != "bite";
                    clips[0].lockRootPositionXZ = true;
                    clips[0].lockRootHeightY = true;
                    clips[0].lockRootRotation = true;
                    importer.clipAnimations = clips.Take(1).ToArray();
                }
                importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/MutatedWolfV2.fbx");
            if (!model) throw new InvalidOperationException("Approved model failed to import.");
            var root = new GameObject("Mutated Wolf V2");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.transform.SetParent(root.transform, false);
                var animator = visual.GetComponent<Animator>();
                if (!animator) animator = visual.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(Folder + "/MutatedWolfV2.fbx").OfType<Avatar>().FirstOrDefault();
                if (!animator.avatar || !animator.avatar.isValid)
                    throw new InvalidOperationException("The approved rig requires a valid Generic Avatar.");
                string controllerPath = Folder + "/WolfV2.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                foreach (var child in machine.states) machine.RemoveState(child.state);
                string[] states = { "Idle", "Walk", "Run", "Bite" };
                float biteDuration = 1f;
                for (int i = 0; i < states.Length; i++)
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(Folder + "/" + animations[i] + ".fbx")
                        .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
                    var state = machine.AddState(states[i]);
                    state.motion = clip;
                    if (i == 0) machine.defaultState = state;
                    if (i == 3) biteDuration = clip.length;
                }
                animator.runtimeAnimatorController = controller;
                foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    string texturePath = Folder + "/" + renderer.name + "_BaseColor.png";
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (!texture) throw new FileNotFoundException("Baked approved texture missing", texturePath);
                    string materialPath = Folder + "/" + renderer.name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (!material)
                    {
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    material.SetTexture("_BaseMap", texture);
                    string roughnessPath = Folder + "/" + renderer.name + "_Roughness.png";
                    var roughnessImporter = AssetImporter.GetAtPath(roughnessPath) as TextureImporter;
                    if (roughnessImporter)
                    {
                        roughnessImporter.isReadable = true;
                        roughnessImporter.sRGBTexture = false;
                        roughnessImporter.SaveAndReimport();
                        var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath);
                        var packed = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, false, true);
                        Color[] pixels = roughness.GetPixels();
                        for (int p = 0; p < pixels.Length; p++)
                            pixels[p] = new Color(0, 0, 0, 1f - pixels[p].r);
                        packed.SetPixels(pixels);
                        packed.Apply();
                        string packedPath = Folder + "/" + renderer.name + "_MetallicSmoothness.asset";
                        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath);
                        if (existing)
                        {
                            EditorUtility.CopySerialized(packed, existing);
                            UnityEngine.Object.DestroyImmediate(packed);
                            packed = existing;
                        }
                        else AssetDatabase.CreateAsset(packed, packedPath);
                        material.SetTexture("_MetallicGlossMap", packed);
                        material.EnableKeyword("_METALLICSPECGLOSSMAP");
                    }
                    string normalPath = Folder + "/" + renderer.name + "_Normal.png";
                    var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
                    if (normalImporter)
                    {
                        normalImporter.textureType = TextureImporterType.NormalMap;
                        normalImporter.SaveAndReimport();
                        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                        material.EnableKeyword("_NORMALMAP");
                    }
                    var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + renderer.name + "_Emission.png");
                    if (emission)
                    {
                        material.SetTexture("_EmissionMap", emission);
                        material.SetColor("_EmissionColor", Color.white);
                        material.EnableKeyword("_EMISSION");
                    }
                    material.SetFloat("_Smoothness", roughnessImporter ? 1f : 0.18f);
                    material.SetFloat("_Metallic", 0f);
                    renderer.sharedMaterials = new[] { material };
                }
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0, 0.65f, 0);
                capsule.radius = 0.42f;
                capsule.height = 1.3f;
                var body = root.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.45f;
                agent.height = 1.4f;
                agent.baseOffset = 0;
                agent.angularSpeed = 420;
                agent.acceleration = 20;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                root.AddComponent<Health>();
                var ai = root.AddComponent<MutatedWolfAI>();
                var settings = new SerializedObject(ai);
                settings.FindProperty("animator").objectReferenceValue = animator;
                settings.FindProperty("biteDuration").floatValue = biteDuration;
                settings.ApplyModifiedPropertiesWithoutUndo();
                if (!AssetDatabase.IsValidFolder(Folder + "/Resources"))
                    AssetDatabase.CreateFolder(Folder, "Resources");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (!UnityEngine.Object.FindFirstObjectByType<EnemyNavigation>())
            {
                var navigation = new GameObject("Enemy Navigation").AddComponent<EnemyNavigation>();
                var settings = new SerializedObject(navigation);
                settings.FindProperty("ground").objectReferenceValue = GameObject.Find("Ground").GetComponent<BoxCollider>();
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            if (!UnityEngine.Object.FindFirstObjectByType<LocalWolfSpawn>())
            {
                var spawn = new GameObject("Mutated Wolf V2 Spawn").AddComponent<LocalWolfSpawn>();
                spawn.transform.position = new Vector3(8, 0, -7);
                Vector3[] positions = { new Vector3(8, 0, -7), new Vector3(3, 0, -7), new Vector3(3, 0, -3) };
                var settings = new SerializedObject(spawn);
                var points = settings.FindProperty("patrolPoints");
                points.arraySize = positions.Length;
                for (int i = 0; i < positions.Length; i++)
                {
                    var point = new GameObject("Wolf Patrol " + (i + 1)).transform;
                    point.SetParent(spawn.transform);
                    point.position = positions[i];
                    points.GetArrayElementAtIndex(i).objectReferenceValue = point;
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("LOCAL_WOLF_V2_INSTALLED");
        }
    }
}
