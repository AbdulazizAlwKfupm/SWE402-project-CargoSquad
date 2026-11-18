using System.IO;
using CargoSquad.Combat;
using CargoSquad.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CargoSquad.Editor
{
    public static class WeaponCombatSetup
    {
        private const string Data = "Assets/_CargoSquad/Data/Weapons";
        private const string Prefabs = "Assets/_CargoSquad/Prefabs/Combat";
        [MenuItem("Cargo Squad/Combat/Install Weapons in IndustrialDistrict")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
            Directory.CreateDirectory(Data); Directory.CreateDirectory(Prefabs); AssetDatabase.Refresh();
            const string materialPath = "Assets/_CargoSquad/Art/Materials/CombatIndicator.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Cargo Squad/Combat Indicator"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            string prefabPath = Prefabs + "/AimingConeIndicator.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<AimingConeIndicator>(prefabPath);
            if (!prefab)
            {
                var obj = new GameObject("AimingConeIndicator"); var cone = obj.AddComponent<AimingConeIndicator>();
                var settings = new SerializedObject(cone); settings.FindProperty("material").objectReferenceValue = material;
                settings.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(obj, prefabPath).GetComponent<AimingConeIndicator>();
                Object.DestroyImmediate(obj);
            }
            var heavy = Create("HeavyShotgun", WeaponType.Shotgun, 12, 5, 60, 7, 5, 20, 1.25f, 1.6f, 20, 0, false);
            var medic = Create("MedicStun", WeaponType.Stun, 6, 6, 20, 1, 4, 12, 1.5f, 1.3f, 9, 1.2f, false);
            Create("ScoutPistol", WeaponType.Pistol, 22, 8, 18, 1, 8, 32, 3, 1.3f, 14, 0, false);
            Create("NavyRifle", WeaponType.Rifle, 14, 10, 12, 1, 24, 96, 7, 1.8f, 22, 0, true);
            const string scenePath = "Assets/_CargoSquad/Scenes/IndustrialDistrict.unity";
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene(scenePath);
            var player = Object.FindFirstObjectByType<PlayerController>();
            var weapon = player.GetComponent<PlayerWeapon>(); if (!weapon) weapon = player.gameObject.AddComponent<PlayerWeapon>();
            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("heavyWeapon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Data + "/HeavyShotgun.asset");
            serialized.FindProperty("medicWeapon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(Data + "/MedicStun.asset");
            serialized.FindProperty("conePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AimingConeIndicator>(prefabPath);
            serialized.FindProperty("feedbackMaterial").objectReferenceValue = material;
            serialized.FindProperty("aimCamera").objectReferenceValue = Camera.main;
            serialized.FindProperty("groundHeight").floatValue = GameObject.Find("Ground").GetComponent<Collider>().bounds.max.y;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static WeaponDefinition Create(string name, WeaponType type, float damage, float range, float angle,
            int pellets, int magazine, int reserve, float rate, float reload, float noise, float stun, bool automatic)
        {
            string path = Data + "/" + name + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (data) return data; // Preserve Inspector tuning on subsequent installs.
            data = ScriptableObject.CreateInstance<WeaponDefinition>(); data.type = type;
            data.displayName = type.ToString().ToUpperInvariant(); data.damage = damage; data.range = range;
            data.coneAngle = angle; data.pelletCount = pellets; data.magazineCapacity = magazine;
            data.reserveAmmunition = reserve; data.shotsPerSecond = rate; data.reloadDuration = reload;
            data.noiseRadius = noise; data.stunDuration = stun; data.automatic = automatic;
            if (type != WeaponType.Shotgun) data.endRangeDamageMultiplier = 1;
            if (type == WeaponType.Stun) data.effectColor = new Color(.2f, .9f, 1f);
            AssetDatabase.CreateAsset(data, path); return data;
        }
    }
}
