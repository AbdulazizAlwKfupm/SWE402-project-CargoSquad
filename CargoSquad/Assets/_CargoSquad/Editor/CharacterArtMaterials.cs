using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CargoSquad.Editor
{
    /// <summary>
    /// Creates URP/Lit materials from the palette manifest written by the Blender build (cs_materials.json) and
    /// remaps the FBX material slots to them. FBX-embedded colours are not used because they import too dark/glossy.
    /// </summary>
    internal static class CharacterArtMaterials
    {
        private const string Folder = "Assets/_CargoSquad/Art/Shared/Materials";
        private const string Manifest = Folder + "/cs_materials.json";

        [Serializable]
        private sealed class Entry
        {
            public string name;
            public string hex;
            public float rough;
            public float metal;
            public string emit;
        }

        [Serializable]
        private sealed class Library
        {
            public Entry[] materials;
        }

        public static string Build()
        {
            if (!File.Exists(Manifest))
                return "no material manifest";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader)
                throw new InvalidOperationException("URP Lit shader not found.");
            Library library = JsonUtility.FromJson<Library>(File.ReadAllText(Manifest));
            foreach (Entry entry in library.materials)
            {
                string path = $"{Folder}/{entry.name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                {
                    material = new Material(shader) { name = entry.name };
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = shader;
                ColorUtility.TryParseHtmlString(entry.hex, out Color color);
                material.SetColor("_BaseColor", color);
                material.SetColor("_Color", color);
                material.SetFloat("_Metallic", entry.metal);
                material.SetFloat("_Smoothness", Mathf.Clamp01((1f - entry.rough) * 0.6f));
                if (!string.IsNullOrEmpty(entry.emit) && ColorUtility.TryParseHtmlString(entry.emit, out Color emission))
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                    material.SetColor("_EmissionColor", emission * 1.6f);
                }
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
            return $"materials: {library.materials.Length} URP/Lit assets in {Folder}";
        }

        public static string Remap(string modelPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            IEnumerable<string> names = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().Select(m => m.name).Distinct();
            int mapped = 0;
            var missing = new List<string>();
            foreach (string name in names)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/{name}.mat");
                if (!material)
                {
                    missing.Add(name);
                    continue;
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
                mapped++;
            }

            importer.SaveAndReimport();
            return $"  remapped {mapped} materials" + (missing.Count > 0 ? $" (no palette entry: {string.Join(", ", missing)})" : string.Empty);
        }
    }
}
