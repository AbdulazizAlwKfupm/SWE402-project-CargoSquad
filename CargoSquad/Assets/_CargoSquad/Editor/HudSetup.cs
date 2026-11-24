using CargoSquad.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CargoSquad.Editor
{
    public static class HudSetup
    {
        [MenuItem("Cargo Squad/Scene/Install Ground Edge Collisions")]
        public static void InstallGroundEdgeCollisions()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity");
            var ground = GameObject.Find("Ground").GetComponent<BoxCollider>().bounds;
            var root = GameObject.Find("Play Area Bounds") ?? new GameObject("Play Area Bounds");
            const float thickness = .5f, height = 4f;
            float y = ground.max.y + height * .5f - .5f;
            Edge(root.transform, "South Edge", new Vector3(ground.center.x, y, ground.min.z - thickness * .5f), new Vector3(ground.size.x + thickness * 2, height, thickness));
            Edge(root.transform, "North Edge", new Vector3(ground.center.x, y, ground.max.z + thickness * .5f), new Vector3(ground.size.x + thickness * 2, height, thickness));
            Edge(root.transform, "West Edge", new Vector3(ground.min.x - thickness * .5f, y, ground.center.z), new Vector3(thickness, height, ground.size.z));
            Edge(root.transform, "East Edge", new Vector3(ground.max.x + thickness * .5f, y, ground.center.z), new Vector3(thickness, height, ground.size.z));
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Saved ground-edge collision boxes; no movement or Rigidbody components added.");
        }
        private static void Edge(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var child = parent.Find(name);
            if (!child) { child = new GameObject(name).transform; child.SetParent(parent, false); }
            child.position = center; child.rotation = Quaternion.identity; child.localScale = Vector3.one;
            var box = child.GetComponent<BoxCollider>();
            if (!box) box = child.gameObject.AddComponent<BoxCollider>();
            box.size = size; box.isTrigger = false;
        }

        [MenuItem("Cargo Squad/UI/Install Survivor HUD")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string folder = "Assets/_CargoSquad/Prefabs/UI";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_CargoSquad/Prefabs", "UI");
            const string path = folder + "/SurvivorHud.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
            {
                var root = new GameObject("SurvivorHud", typeof(RectTransform), typeof(SurvivorHud));
                root.layer = 5;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }
            var scene = EditorSceneManager.OpenScene("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity");
            var overlay = Object.FindFirstObjectByType<CargoMissionOverlay>(FindObjectsInactive.Include);
            var settings = new SerializedObject(overlay);
            settings.FindProperty("survivorHudPrefab").objectReferenceValue = prefab.GetComponent<SurvivorHud>();
            settings.FindProperty("selection").objectReferenceValue = Object.FindFirstObjectByType<CharacterSelection>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Survivor HUD prefab and scene references saved; gameplay objects preserved.");
        }
    }
}
