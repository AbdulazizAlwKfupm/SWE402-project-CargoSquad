using System;
using System.Linq;
using CargoSquad.Cargo;
using CargoSquad.Missions;
using CargoSquad.Player;
using CargoSquad.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CargoSquad.Editor
{
    internal static class IndustrialDistrictSetup
    {
        private const string ScenePath = "Assets/_CargoSquad/Scenes/IndustrialDistrict.unity";

        [MenuItem("Cargo Squad/Set Up Industrial District Cargo Loop")]
        private static void SetUpCargoLoop()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open IndustrialDistrict in Edit mode.");

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var player = FindRoot(scene, "PlayerPlaceholder");
            var ground = FindRoot(scene, "Ground").GetComponent<BoxCollider>();
            var cargoObject = FindRoot(scene, "CargoPlaceholder");
            var extractionObject = FindRoot(scene, "ExtractionPlaceholder");
            var zone = extractionObject.GetComponent<BoxCollider>();
            var camera = FindRoot(scene, "Main Camera");
            if (!actions || actions.FindAction("Player/Interact") == null || !player.GetComponent<Rigidbody>() ||
                !ground || !cargoObject.GetComponent<BoxCollider>() || !zone)
            {
                throw new InvalidOperationException("Check the interaction action, player body, ground, cargo, and extraction colliders.");
            }

            var cargo = GetOrAdd<CargoItem>(cargoObject);
            var condition = GetOrAdd<CargoCondition>(cargoObject);
            var stamina = GetOrAdd<PlayerStamina>(player);
            GetOrAdd<CargoGrip>(player);
            var navy = CreateDefinition<CharacterDefinition>("Characters", "Navy Soldier", asset =>
                ConfigureCharacter(asset, "Navy Soldier", 105, 4f, 7f, 105, 17, 22, 1.15f, 1.15f, 1f, 1f));
            CreateDefinition<CharacterDefinition>("Characters", "Heavy", asset =>
                ConfigureCharacter(asset, "Heavy", 130, 3.5f, 6f, 110, 15, 23, 1.6f, 1.3f, 0.8f, 1f));
            CreateDefinition<CharacterDefinition>("Characters", "Scout", asset =>
                ConfigureCharacter(asset, "Scout", 85, 4.5f, 8f, 125, 21, 20, 0.85f, 0.95f, 1.25f, 1.15f));
            CreateDefinition<CharacterDefinition>("Characters", "Medic Soldier", asset =>
                ConfigureCharacter(asset, "Medic Soldier", 95, 4f, 6.8f, 100, 18, 21, 0.95f, 1.1f, 1.05f, 1.25f));
            var example = CreateDefinition<CargoDefinition>("Cargo", "Medical Supplies", asset =>
            {
                asset.FindProperty("displayName").stringValue = "Medical Supplies";
                asset.FindProperty("value").enumValueIndex = (int)CargoValue.Valuable;
            });
            AssignDefaultDefinition(stamina, navy);
            AssignDefaultDefinition(condition, example);
            var interactor = GetOrAdd<CargoInteractor>(player);
            var extraction = GetOrAdd<CargoExtraction>(extractionObject);
            var overlay = GetOrAdd<CargoMissionOverlay>(camera);
            Undo.RecordObject(zone, "Make extraction nonblocking");
            zone.isTrigger = true;

            using var interactionSettings = new SerializedObject(interactor);
            interactionSettings.FindProperty("inputActions").objectReferenceValue = actions;
            interactionSettings.FindProperty("cargo").objectReferenceValue = cargo;
            interactionSettings.FindProperty("ground").objectReferenceValue = ground;
            interactionSettings.ApplyModifiedProperties();

            using var extractionSettings = new SerializedObject(extraction);
            extractionSettings.FindProperty("primaryCargo").objectReferenceValue = cargo;
            extractionSettings.ApplyModifiedProperties();

            using var overlaySettings = new SerializedObject(overlay);
            overlaySettings.FindProperty("player").objectReferenceValue = interactor;
            overlaySettings.FindProperty("mission").objectReferenceValue = extraction;
            overlaySettings.FindProperty("stamina").objectReferenceValue = stamina;
            overlaySettings.ApplyModifiedProperties();

            ValidateReferences(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Could not save {ScenePath}.");

            Debug.Log("Industrial District cargo loop saved and references validated.");
        }

        private const string DefinitionsPath = "Assets/_CargoSquad/ScriptableObjects";

        private static T CreateDefinition<T>(string folder, string name, Action<SerializedObject> configure)
            where T : ScriptableObject
        {
            string folderPath = $"{DefinitionsPath}/{folder}";
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder(DefinitionsPath, folder);

            string path = $"{folderPath}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing)
                return existing;

            var definition = ScriptableObject.CreateInstance<T>();
            using var settings = new SerializedObject(definition);
            configure(settings);
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        private static void ConfigureCharacter(SerializedObject asset, string name, float health, float walk,
            float run, float stamina, float recovery, float consumption, float strength, float stability,
            float stealth, float scavenging)
        {
            asset.FindProperty("displayName").stringValue = name;
            asset.FindProperty("maxHealth").floatValue = health;
            asset.FindProperty("walkSpeed").floatValue = walk;
            asset.FindProperty("runSpeed").floatValue = run;
            asset.FindProperty("maxStamina").floatValue = stamina;
            asset.FindProperty("staminaRecovery").floatValue = recovery;
            asset.FindProperty("sprintConsumption").floatValue = consumption;
            asset.FindProperty("carryStrength").floatValue = strength;
            asset.FindProperty("cargoStability").floatValue = stability;
            asset.FindProperty("stealthModifier").floatValue = stealth;
            asset.FindProperty("scavengingModifier").floatValue = scavenging;
        }

        private static void AssignDefaultDefinition(Component component, ScriptableObject definition)
        {
            using var settings = new SerializedObject(component);
            var property = settings.FindProperty("definition");
            if (!property.objectReferenceValue)
                property.objectReferenceValue = definition;
            settings.ApplyModifiedProperties();
        }

        [MenuItem("Cargo Squad/Set Up Industrial District Playtest")]
        private static void SetUpPlaytest()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open IndustrialDistrict in Edit mode.");

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var player = FindRoot(scene, "PlayerPlaceholder");
            var ground = FindRoot(scene, "Ground").GetComponent<BoxCollider>();
            var camera = FindRoot(scene, "Main Camera").GetComponent<Camera>();
            var capsule = player.GetComponent<CapsuleCollider>();
            if (!actions || actions.FindAction("Player/Move") == null || actions.FindAction("Player/Sprint") == null ||
                !ground || !camera || !capsule || capsule.isTrigger || ground.isTrigger)
            {
                throw new InvalidOperationException("Check the input actions, player capsule, ground, and camera.");
            }

            var body = GetOrAdd<Rigidbody>(player);
            Undo.RecordObject(body, "Configure player physics");
            body.isKinematic = false;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            using var controller = new SerializedObject(GetOrAdd<PlayerController>(player));
            controller.FindProperty("inputActions").objectReferenceValue = actions;
            controller.ApplyModifiedProperties();

            Undo.RecordObject(camera, "Frame playable camera");
            camera.orthographic = true;
            camera.orthographicSize = 6f;

            var follow = GetOrAdd<TopDownCameraFollow>(camera.gameObject);
            using var settings = new SerializedObject(follow);
            settings.FindProperty("target").objectReferenceValue = player.transform;
            settings.FindProperty("playArea").objectReferenceValue = ground;
            settings.ApplyModifiedProperties();

            ValidateReferences(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Could not save {ScenePath}.");

            Debug.Log("Industrial District player physics and camera references saved and validated.");
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            return gameObject.TryGetComponent<T>(out var component) ? component : Undo.AddComponent<T>(gameObject);
        }

        [MenuItem("Cargo Squad/Update Industrial District Graybox")]
        private static void UpdateGraybox()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run Industrial District setup in Edit mode.");

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open IndustrialDistrict before running graybox setup.");

            GameObject ground = FindRoot(scene, "Ground");
            GameObject player = FindRoot(scene, "PlayerPlaceholder");
            GameObject cargo = FindRoot(scene, "CargoPlaceholder");
            GameObject extraction = FindRoot(scene, "ExtractionPlaceholder");
            Camera camera = FindRoot(scene, "Main Camera").GetComponent<Camera>();
            Light light = FindRoot(scene, "Directional Light").GetComponent<Light>();
            if (!camera || !light || light.type != LightType.Directional ||
                !ground.GetComponent<BoxCollider>() || !player.GetComponent<CapsuleCollider>() ||
                !cargo.GetComponent<BoxCollider>() || !extraction.GetComponent<BoxCollider>())
            {
                throw new InvalidOperationException("Check the existing camera, light, and graybox colliders.");
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update Industrial District Graybox");

            Undo.RecordObjects(new UnityEngine.Object[] { ground.transform, player.transform, cargo.transform,
                extraction.transform, camera, camera.transform }, "Update terrain, markers, and camera");
            ground.transform.localScale = new Vector3(30f, 1f, 24f);
            player.transform.SetPositionAndRotation(new Vector3(-11f, 1f, -6f), Quaternion.identity);
            cargo.transform.position = new Vector3(0f, 0.75f, 6.5f);
            extraction.transform.position = new Vector3(11f, 1f, -3f);

            camera.orthographic = true;
            if (!camera.GetComponent<TopDownCameraFollow>())
                camera.orthographicSize = 11f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 22f, -22f), Quaternion.Euler(45f, 0f, 0f));

            Transform graybox = GetGroup(scene, null, "Exploration Graybox");
            Transform depot = GetGroup(scene, graybox, "Cargo Depot");
            Transform walls = GetGroup(scene, depot, "Sightline Walls");
            Box(walls, "Rear Wall", new Vector3(0f, 1.3f, 9f), new Vector3(7f, 2.6f, 0.4f));
            Box(walls, "West Wall", new Vector3(-3.3f, 1.3f, 6f), new Vector3(0.4f, 2.6f, 5.6f));
            Box(walls, "East Wall", new Vector3(3.3f, 1.3f, 6f), new Vector3(0.4f, 2.6f, 5.6f));
            Box(walls, "Entry Wall West", new Vector3(-2.4f, 1.3f, 3f), new Vector3(2.2f, 2.6f, 0.4f));
            Box(walls, "Entry Wall East", new Vector3(2.4f, 1.3f, 3f), new Vector3(2.2f, 2.6f, 0.4f));
            GameObject roof = Box(depot, "Roof Placeholder (Hidden)", new Vector3(0f, 2.75f, 6f),
                new Vector3(7f, 0.3f, 6.4f));
            Undo.RecordObject(roof, "Hide depot roof");
            roof.SetActive(false);

            Transform ruins = GetGroup(scene, graybox, "Ruined Structure");
            Box(ruins, "Standing Wall", new Vector3(-10f, 1.5f, 6f), new Vector3(5f, 3f, 0.45f));
            Box(ruins, "Broken Return", new Vector3(-12.3f, 0.7f, 4.4f), new Vector3(0.45f, 1.4f, 2.8f));
            Box(ruins, "Rubble", new Vector3(-9.3f, 0.25f, 4.6f), new Vector3(1.3f, 0.5f, 0.8f), 25f);

            Transform cover = GetGroup(scene, graybox, "Cover");
            Box(cover, "Concrete Barrier West", new Vector3(-6.5f, 0.55f, -2f), new Vector3(3f, 1.1f, 0.65f), 15f);
            Box(cover, "Concrete Barrier East", new Vector3(7f, 0.55f, 3.5f), new Vector3(0.65f, 1.1f, 3f));
            Box(cover, "Crate Stack", new Vector3(5f, 0.8f, -5.5f), new Vector3(1.8f, 1.6f, 1.3f), -12f);

            Transform hiding = GetGroup(scene, graybox, "Hiding Zones");
            AddBush(hiding, "West Bush", new Vector3(-11f, 0.55f, 0f));
            AddBush(hiding, "East Bush", new Vector3(10f, 0.55f, 7.5f));

            Transform markers = GetGroup(scene, graybox, "Exploration Markers");
            Box(markers, "Resource Cache Placeholder", new Vector3(-10.7f, 0.4f, 5f), new Vector3(0.9f, 0.8f, 0.9f));
            Box(markers, "Mission Objective Placeholder", new Vector3(11.5f, 0.6f, -0.7f), new Vector3(0.8f, 1.2f, 0.8f));

            ValidateReferences(scene);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Could not save {ScenePath}.");

            Debug.Log($"Industrial District angled graybox saved and validated at {ScenePath}.");
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().Single(root => root.name == name);
        }

        private static Transform GetGroup(Scene scene, Transform parent, string name)
        {
            GameObject group = parent
                ? parent.Find(name)?.gameObject
                : scene.GetRootGameObjects().SingleOrDefault(root => root.name == name);
            if (group)
                return group.transform;

            group = new GameObject(name);
            SceneManager.MoveGameObjectToScene(group, scene);
            Undo.RegisterCreatedObjectUndo(group, $"Create {name}");
            if (parent)
                Undo.SetTransformParent(group.transform, parent, $"Parent {name}");
            return group.transform;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, float yaw = 0f)
        {
            return SetPrimitive(parent, name, PrimitiveType.Cube, position, scale, yaw);
        }

        private static void AddBush(Transform parent, string name, Vector3 position)
        {
            GameObject bush = SetPrimitive(parent, name, PrimitiveType.Sphere, position,
                new Vector3(2.6f, 1.2f, 2f), 0f);
            Collider collider = bush.GetComponent<Collider>();
            Undo.RecordObject(collider, "Set bush trigger volume");
            collider.isTrigger = true;
        }

        private static GameObject SetPrimitive(Transform parent, string name, PrimitiveType primitive,
            Vector3 position, Vector3 scale, float yaw)
        {
            GameObject item = parent.Find(name)?.gameObject;
            if (!item)
            {
                item = GameObject.CreatePrimitive(primitive);
                item.name = name;
                SceneManager.MoveGameObjectToScene(item, parent.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(item, $"Create {name}");
                Undo.SetTransformParent(item.transform, parent, $"Parent {name}");
            }

            Undo.RecordObject(item.transform, $"Place {name}");
            item.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            item.transform.localScale = scale;
            return item;
        }

        private static void ValidateReferences(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (!component)
                        throw new InvalidOperationException($"Missing script in the hierarchy under {root.name}.");

                    using var serialized = new SerializedObject(component);
                    SerializedProperty property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType == SerializedPropertyType.ObjectReference &&
                            !property.objectReferenceValue && property.objectReferenceInstanceIDValue != 0)
                        {
                            throw new InvalidOperationException(
                                $"Missing reference on {component.name} ({component.GetType().Name}.{property.propertyPath}).");
                        }
                    }
                }
            }
        }
    }
}

