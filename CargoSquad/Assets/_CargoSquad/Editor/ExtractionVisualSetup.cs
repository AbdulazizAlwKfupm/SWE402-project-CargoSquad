using System.IO;
using CargoSquad.Cargo;
using CargoSquad.Missions;
using CargoSquad.UI;
using CargoSquad.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSquad.Editor
{
    public static class ExtractionVisualSetup
    {
        private const string Materials = "Assets/_CargoSquad/Art/Materials";
        private const string Prefabs = "Assets/_CargoSquad/Prefabs/Environment";
        [MenuItem("Cargo Squad/Visuals/Install Extraction Beacon and Brown Cargo")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Exit Play Mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Prefabs); AssetDatabase.Refresh();
            string texturePath = Materials + "/CargoWeatheredBrown.png";
            if (!File.Exists(texturePath))
            {
                var texture = new Texture2D(256, 256, TextureFormat.RGB24, false);
                Color main = Hex("79543A"), dark = Hex("4C3527"), light = Hex("997251");
                var random = new System.Random(402);
                for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
                {
                    float grain = Mathf.Sin(x * .18f + Mathf.Sin(y * .035f) * 2) * .045f + (float)random.NextDouble() * .08f;
                    Color color = Color.Lerp(main, light, grain + .12f);
                    if (x < 10 || x > 245 || y < 10 || y > 245 || y % 64 < 3) color = dark * (.85f + grain);
                    // Narrow worn seams and deterministic small abrasions, all original procedural texture.
                    else if (y % 64 < 5) color = light;
                    if (random.NextDouble() < .012) color = Color.Lerp(color, light, .4f);
                    texture.SetPixel(x, y, color);
                }
                texture.Apply(); File.WriteAllBytes(texturePath, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);
            }
            string brownPath = Materials + "/CargoWeatheredBrown.mat";
            var brown = AssetDatabase.LoadAssetAtPath<Material>(brownPath);
            if (!brown)
            {
                brown = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                brown.SetColor("_BaseColor", Color.white); brown.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                brown.SetFloat("_Smoothness", .12f); brown.SetFloat("_Metallic", .02f);
                AssetDatabase.CreateAsset(brown, brownPath);
            }
            string cargoPrefab = Prefabs + "/CargoCrateVisual.prefab";
            if (!File.Exists(cargoPrefab))
            {
                var crate = GameObject.CreatePrimitive(PrimitiveType.Cube); crate.name = "CargoCrateVisual";
                Object.DestroyImmediate(crate.GetComponent<Collider>()); crate.GetComponent<Renderer>().sharedMaterial = brown;
                PrefabUtility.SaveAsPrefabAsset(crate, cargoPrefab); Object.DestroyImmediate(crate);
            }
            string markerPrefab = Prefabs + "/ExtractionMarker.prefab";
            if (!File.Exists(markerPrefab))
            {
                var marker = new GameObject("ExtractionMarker"); var component = marker.AddComponent<ExtractionMarker>();
                var cylinder = new GameObject("HologramCylinder", typeof(MeshFilter), typeof(MeshRenderer)); cylinder.transform.SetParent(marker.transform, false);
                var ring = new GameObject("GroundRing", typeof(LineRenderer)); ring.transform.SetParent(marker.transform, false);
                var glow = new GameObject("GlowEffects"); glow.transform.SetParent(marker.transform, false);
                foreach (string name in new[] { "TopRim", "ScanRing", "EdgeLeft", "EdgeRight" }) new GameObject(name, typeof(LineRenderer)).transform.SetParent(glow.transform, false);
                var label = new GameObject("ExtractLabel", typeof(RectTransform), typeof(Canvas)); label.transform.SetParent(marker.transform, false);
                label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                label.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 32); label.transform.localScale = Vector3.one * .01f;
                var text = new GameObject("Text", typeof(RectTransform)); text.transform.SetParent(label.transform, false);
                var tmp = UiTypography.Apply(text, "EXTRACT", 23, new Color(.35f, 1, .94f)); tmp.fontStyle = TMPro.FontStyles.Bold;
                tmp.rectTransform.sizeDelta = new Vector2(200, 32);
                var settings = new SerializedObject(component);
                settings.FindProperty("hologramMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/CombatIndicator.mat");
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(marker, markerPrefab); Object.DestroyImmediate(marker);
            }
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene("Assets/_CargoSquad/Scenes/IndustrialDistrict.unity");
            var extraction = Object.FindFirstObjectByType<CargoExtraction>();
            extraction.GetComponent<MeshRenderer>().enabled = false; // Keep original trigger and mesh data.
            var existing = Object.FindFirstObjectByType<ExtractionMarker>();
            if (!existing) existing = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(markerPrefab))).GetComponent<ExtractionMarker>();
            Bounds bounds = extraction.GetComponent<Collider>().bounds;
            existing.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var properties = new SerializedObject(existing);
            properties.FindProperty("mission").objectReferenceValue = extraction;
            properties.FindProperty("survivor").objectReferenceValue = Object.FindFirstObjectByType<CargoInteractor>();
            float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
            properties.FindProperty("markerRadius").floatValue = radius;
            properties.FindProperty("footprintAspect").vector2Value = new Vector2(bounds.extents.x / radius, bounds.extents.z / radius);
            properties.ApplyModifiedPropertiesWithoutUndo();
            var cargo = Object.FindFirstObjectByType<CargoItem>();
            cargo.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(brownPath);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static Color Hex(string text) { ColorUtility.TryParseHtmlString("#" + text, out var color); return color; }
    }
}
