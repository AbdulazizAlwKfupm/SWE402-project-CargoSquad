using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CargoSquad.Editor
{
    public static class MedicMaterialSetup
    {
        const string Folder = "Assets/_CargoSquad/Characters/MedicMaterials";
        static readonly string[] Slots = {
            "ArmorOffWhite", "BareSteel", "BootLeather", "Brow", "CanvasOffWhite", "CanvasSage", "ClothCharcoal",
            "ContainerYellow", "DarkSteel", "EnergyCyan", "EyeWhite", "Glove", "Iris", "LampWarm", "PaintTeal",
            "RedPolymer", "Rubber", "Skin", "UniformOlive", "VestOlive", "WebbingTan", "WebbingTeal"
        };
        [MenuItem("Cargo Squad/Deployment/Repair Medic material remaps")]
        public static void Configure()
        {
            PreserveHeavyMaterialDependencies();
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/_CargoSquad/Characters/Medic_Redesign_v1.fbx");
            foreach (var slot in Slots)
            {
                string path = Folder + "/MR1_" + slot + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                {
                    string template = slot switch {
                        "ArmorOffWhite" => "Medic_Headgear", "CanvasOffWhite" => "Medic_Pack", "CanvasSage" => "Medic_Jacket",
                        "UniformOlive" => "Medic_Pants", "VestOlive" => "Medic_Gear", "PaintTeal" => "Medic_Accent",
                        "WebbingTeal" => "Medic_Accent", "DarkSteel" => "Medic_device", "BareSteel" => "CS_Gunmetal",
                        "Glove" => "CS_Glove", "Skin" => "CS_Skin_A", _ => null
                    };
                    var source = template == null ? null : AssetDatabase.LoadAssetAtPath<Material>("Assets/_CargoSquad/Art/Shared/Materials/" + template + ".mat");
                    material = source ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.name = "MR1_" + slot;
                    if (!source)
                    {
                        // These detail colours follow the Medic Blender viewport palette, in sRGB.
                        Color color = slot switch {
                            "BootLeather" => new Color(.118f,.106f,.094f), "Brow" => new Color(.114f,.086f,.071f),
                            "ClothCharcoal" => new Color(.102f,.106f,.114f), "ContainerYellow" => new Color(.612f,.498f,.141f),
                            "EnergyCyan" => new Color(.184f,.847f,.902f), "EyeWhite" => new Color(.851f,.831f,.800f),
                            "Iris" => new Color(.227f,.165f,.094f), "LampWarm" => new Color(1f,.886f,.690f),
                            "RedPolymer" => new Color(.557f,.165f,.133f), "Rubber" => new Color(.071f,.071f,.075f),
                            "WebbingTan" => new Color(.490f,.435f,.298f), _ => Color.gray
                        };
                        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .22f);
                        if (slot == "EnergyCyan" || slot == "LampWarm")
                        {
                            material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .7f);
                        }
                    }
                    AssetDatabase.CreateAsset(material, path);
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "MR1_" + slot), material);
            }
            AssetDatabase.SaveAssets(); importer.SaveAndReimport();
        }

        static void PreserveHeavyMaterialDependencies()
        {
            // The old Heavy remap referenced a material subasset inside Medic's FBX. Reimporting Medic
            // removes that subasset. Give these existing white slots a stable, independent URP material.
            const string path = "Assets/_CargoSquad/Characters/Heavy_ImportedWhite.mat";
            var heavy = (ModelImporter)AssetImporter.GetAtPath("Assets/_CargoSquad/Characters/Heavy_v4.fbx");
            var map = heavy.GetExternalObjectMap();
            bool changed = false;
            foreach (string slot in new[] { "HV4_EyeWhite", "ammo_box" })
            {
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), slot);
                if (map.TryGetValue(id, out var current) && current &&
                    AssetDatabase.GetAssetPath(current) != "Assets/_CargoSquad/Characters/Medic_Redesign_v1.fbx") continue;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Heavy_ImportedWhite" };
                    material.SetColor("_BaseColor", new Color(.8f, .8f, .8f, 1f)); material.SetFloat("_Smoothness", .25f);
                    AssetDatabase.CreateAsset(material, path);
                }
                heavy.AddRemap(id, material); changed = true;
            }
            if (changed) { AssetDatabase.SaveAssets(); heavy.SaveAndReimport(); }
        }

        public static void ApplyToVisual(GameObject root)
        {
            // Rigid prefabs contain explicit renderer materials, so importer remaps alone cannot update old instances.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_CargoSquad/Characters/Medic_Redesign_v1.fbx");
            var sources = model.GetComponentsInChildren<Renderer>(true).ToDictionary(r => r.name);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    // Mesh submesh order is retained. Match the FBX source renderer by its unique MR1 object name.
                    if (!sources.TryGetValue(renderer.name, out var source) || i >= source.sharedMaterials.Length) continue;
                    materials[i] = source.sharedMaterials[i]; changed = true;
                }
                if (!changed) continue;
                renderer.sharedMaterials = materials; PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }
    }
}
