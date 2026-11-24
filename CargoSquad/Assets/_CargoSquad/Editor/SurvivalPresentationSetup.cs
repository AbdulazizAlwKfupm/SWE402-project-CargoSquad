using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CargoSquad.Editor
{
    public static class SurvivalPresentationSetup
    {
        [MenuItem("Cargo Squad/UI/Install Survival Feedback and Base Preview")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string path = "Assets/_CargoSquad/Prefabs/UI/HealthHologram.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab)
            {
                var obj = new GameObject("Health Hologram", typeof(HealthHologram));
                prefab = PrefabUtility.SaveAsPrefabAsset(obj, path); Object.DestroyImmediate(obj);
            }
            var scene = EditorSceneManager.OpenScene("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity");
            var selection = Object.FindFirstObjectByType<CharacterSelection>();
            var health = GameObject.Find("PlayerPlaceholder").GetComponent<Health>();
            var camera = Camera.main;
            var effect = camera.GetComponent<LowHealthFeedback>(); if (!effect) effect = camera.gameObject.AddComponent<LowHealthFeedback>();
            var data = new SerializedObject(effect); data.FindProperty("survivor").objectReferenceValue = health;
            data.FindProperty("selection").objectReferenceValue = selection; data.ApplyModifiedPropertiesWithoutUndo();
            var spawner = Object.FindFirstObjectByType<HealthHologramSpawner>();
            if (!spawner) spawner = new GameObject("Health Hologram Spawner").AddComponent<HealthHologramSpawner>();
            data = new SerializedObject(spawner); data.FindProperty("ground").objectReferenceValue = GameObject.Find("Ground").GetComponent<BoxCollider>();
            data.FindProperty("survivor").objectReferenceValue = health; data.FindProperty("selection").objectReferenceValue = selection;
            data.FindProperty("pickupPrefab").objectReferenceValue = prefab.GetComponent<HealthHologram>(); data.ApplyModifiedPropertiesWithoutUndo();
            data = new SerializedObject(selection); data.FindProperty("useBaseSelection").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Saved low-health effect, two-slot health hologram spawner, and reversible base selection preview.");
        }
    }
}
