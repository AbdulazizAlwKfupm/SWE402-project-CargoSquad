using System.Linq;
using CargoSquad.Cargo;
using CargoSquad.Missions;
using CargoSquad.UI;
using CargoSquad.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CargoSquad.Editor
{
    public static class MissionResultSetup
    {
        [MenuItem("Cargo Squad/Visuals/Install Wide Extraction and Mission Results")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            const string markerPath = "Assets/_CargoSquad/Prefabs/Environment/ExtractionMarker.prefab";
            var contents = PrefabUtility.LoadPrefabContents(markerPath);
            var tuning = new SerializedObject(contents.GetComponent<ExtractionMarker>());
            tuning.FindProperty("markerRadius").floatValue = 1.625f;
            tuning.FindProperty("footprintAspect").vector2Value = Vector2.one;
            tuning.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(contents, markerPath); PrefabUtility.UnloadPrefabContents(contents);
            const string panelPath = "Assets/_CargoSquad/Prefabs/Environment/MissionResultPanel.prefab";
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(panelPath))
            {
                var obj = new GameObject("Mission Result UI"); obj.AddComponent<MissionResultPanel>();
                PrefabUtility.SaveAsPrefabAsset(obj, panelPath); Object.DestroyImmediate(obj);
            }
            AssetDatabase.SaveAssets();
            const string scenePath = "Assets/_CargoSquad/Scenes/IndustrialDistrict.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);
            var mission = Object.FindFirstObjectByType<CargoExtraction>(); var zone = mission.GetComponent<BoxCollider>();
            Vector3 scale = zone.transform.lossyScale;
            zone.size = new Vector3(3.25f / Mathf.Abs(scale.x), 2 / Mathf.Abs(scale.y), 3.25f / Mathf.Abs(scale.z));
            Physics.SyncTransforms();
            var blockers = Physics.OverlapBox(zone.bounds.center, new Vector3(1.625f, .95f, 1.625f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                .Where(c => !c.attachedRigidbody && !c.GetComponentInParent<CargoItem>()).ToArray();
            if (blockers.Length > 0) throw new System.InvalidOperationException("Wide extraction overlaps: " + string.Join(", ", blockers.Select(c => c.name)));
            var floor = GameObject.Find("Ground").GetComponent<Collider>().bounds;
            if (zone.bounds.min.x < floor.min.x || zone.bounds.max.x > floor.max.x || zone.bounds.min.z < floor.min.z || zone.bounds.max.z > floor.max.z)
                throw new System.InvalidOperationException("Extraction extends beyond the ground.");
            var marker = Object.FindFirstObjectByType<ExtractionMarker>();
            marker.transform.position = new Vector3(zone.bounds.center.x, floor.max.y, zone.bounds.center.z);
            var settings = new SerializedObject(marker);
            settings.FindProperty("markerRadius").floatValue = 1.625f; settings.FindProperty("footprintAspect").vector2Value = Vector2.one;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var panel = Object.FindFirstObjectByType<MissionResultPanel>();
            if (!panel) panel = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(panelPath))).GetComponent<MissionResultPanel>();
            var data = new SerializedObject(panel); data.FindProperty("mission").objectReferenceValue = mission;
            data.FindProperty("survivor").objectReferenceValue = Object.FindFirstObjectByType<CargoInteractor>(); data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            if (!EditorBuildSettings.scenes.Any(s => s.path == scenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(scenePath, true) }).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Extraction footprint {zone.bounds.size}, center {zone.bounds.center}; solid blockers: 0.");
        }
    }
}
