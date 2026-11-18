using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CargoSquad.Editor
{
    public static class IndustrialExpansionPolish
    {
        public const string ArtPath = "Assets/_CargoSquad/Art/IndustrialExpansion";
        private static Material concrete, floor, asphalt, steel, trim, yellow, dark, red, blue, olive, glass;
        private static Material carton, cartonLight, tape, warm, cyan, rubber, stain, wood;
        private static Mesh bevel, wire, disc;
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        [MenuItem("Cargo Squad/Maps/Polish Industrial Expansion Section")]
        public static void Polish()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before polishing the sample.");
            string path = IndustrialExpansionSetup.ScenePath;
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty)
                throw new InvalidOperationException("Save or revert your IndustrialExpansion edits before rebuilding its generated scenery.");
            if (!File.Exists(path)) throw new FileNotFoundException("Create the IndustrialExpansion sample first.", path);
            Scene previous = SceneManager.GetActiveScene();
            string backupFolder = "Library/IndustrialExpansionBackups";
            Directory.CreateDirectory(backupFolder);
            File.Copy(path, backupFolder + "/IndustrialExpansion-" + DateTime.UtcNow.Ticks + ".unity");
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                if (scene.path != path) throw new InvalidOperationException("Only IndustrialExpansion may be polished.");
                SceneManager.SetActiveScene(scene);
                PrepareArt();
                var old = scene.GetRootGameObjects().Single(g => g.name == "Industrial Expansion Sample");
                Object.DestroyImmediate(old);
                Transform root = Group("Industrial Expansion Sample", null, scene);
                Warehouse(root);
                Yard(root);
                Fences(root);
                Helicopter(root);
                Lighting(root);
                var ground = scene.GetRootGameObjects().Single(g => g.name == "Ground");
                ground.GetComponent<Renderer>().sharedMaterial = asphalt;
                Validate(scene, root);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save the sample scene.");
                Debug.Log("IndustrialExpansion warehouse, yard and extraction scenery polished. Only this scene and Art/IndustrialExpansion were written.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void PrepareArt()
        {
            textures.Clear();
            EnsureFolder(ArtPath);
            EnsureFolder(ArtPath + "/Materials");
            EnsureFolder(ArtPath + "/Textures");
            EnsureFolder(ArtPath + "/Meshes");
            concrete = Surface("Concrete Walls", new Color(.64f, .63f, .61f), .02f, .22f, "Concrete", 1);
            floor = Surface("Warehouse Floor", new Color(.56f, .54f, .51f), 0, .12f, "Floor Concrete", 4);
            asphalt = Surface("Yard Asphalt", new Color(.54f, .53f, .51f), 0, .1f, "Yard Concrete", 12);
            steel = Surface("Dark Galvanized Steel", new Color(.19f, .23f, .27f), .68f, .33f, "Metal", 2);
            trim = Surface("Galvanized Trim", new Color(.48f, .53f, .56f), .72f, .4f, "Metal", 1);
            yellow = Surface("Worn Safety Yellow", new Color(.87f, .59f, .15f), .18f, .26f, "Paint", 1);
            dark = Surface("Seams and Labels", new Color(.065f, .08f, .095f), .12f, .22f);
            red = Surface("Oxide Red Container", new Color(.39f, .15f, .11f), .35f, .24f, "Paint", 1);
            blue = Surface("Petrol Blue Container", new Color(.13f, .26f, .30f), .38f, .27f, "Paint", 1);
            olive = Surface("Helicopter Olive", new Color(.28f, .32f, .27f), .22f, .4f, "Paint", 1);
            glass = Surface("Smoked Cockpit Glass", new Color(.045f, .13f, .17f), .78f, .88f);
            carton = Surface("Packing Carton", new Color(.49f, .34f, .20f), 0, .15f, "Carton", 1);
            cartonLight = Surface("Packing Carton Light", new Color(.62f, .47f, .30f), 0, .14f, "Carton", 1);
            tape = Surface("Packing Tape and Paper", new Color(.76f, .69f, .48f), 0, .26f);
            rubber = Surface("Rubber and Cable", new Color(.065f, .065f, .06f), .05f, .16f);
            stain = Surface("Oil Stains", new Color(.25f, .27f, .29f), .15f, .42f);
            warm = Surface("Warm Lamp Lens", new Color(1, .76f, .4f), .1f, .5f);
            warm.EnableKeyword("_EMISSION"); warm.SetColor("_EmissionColor", new Color(1, .65f, .24f) * 3);
            cyan = Surface("Beacon Accent", new Color(.08f, .65f, .65f), .3f, .5f);
            cyan.EnableKeyword("_EMISSION"); cyan.SetColor("_EmissionColor", new Color(.05f, .7f, .65f) * 2);
            EditorUtility.SetDirty(warm); EditorUtility.SetDirty(cyan);
            AssetDatabase.SaveAssetIfDirty(warm); AssetDatabase.SaveAssetIfDirty(cyan);
            wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_CargoSquad/Art/Materials/CargoWeatheredBrown.mat");
            bevel = SaveMesh("Chamfered Box", BeveledBox());
            wire = SaveMesh("Chain Link 360x170", ChainLink());
            disc = SaveMesh("Oil Patch", PatchMesh());
        }

        private static Material Surface(string name, Color color, float metallic, float smooth,
            string texture = null, float tiles = 1)
        {
            string path = ArtPath + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", smooth);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", Texture(texture, false));
                material.SetTextureScale("_BaseMap", new Vector2(tiles, tiles));
                material.SetTexture("_BumpMap", Texture(texture, true));
                material.SetFloat("_BumpScale", IsFloorTexture(texture) ? .12f : texture == "Asphalt" ? .65f : .35f);
                material.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static Texture2D Texture(string kind, bool normal)
        {
            string path = ArtPath + "/Textures/" + kind + (normal ? " Normal" : " Albedo") + ".png";
            if (textures.TryGetValue(path, out var cached)) return cached;
            const int size = 512;
            var image = new Texture2D(size, size, TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            float Sample(int x, int y)
            {
                if (IsFloorTexture(kind))
                    return .5f + (PeriodicNoise(x, y, .012f, size) - .5f) * .6f +
                        (PeriodicNoise(x, y, .08f, size) - .5f) * .1f;
                float fine = Mathf.PerlinNoise(x * .34f + 5, y * .34f + 13);
                float medium = Mathf.PerlinNoise(x * .055f, y * .055f);
                float broad = Mathf.PerlinNoise(x * .009f + 7, y * .009f + 3);
                return fine * .22f + medium * .3f + broad * .48f;
            }
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float noise = Sample(x, y);
                Color c;
                if (normal)
                {
                    float strength = IsFloorTexture(kind) ? .2f : 1.7f;
                    float dx = (Sample((x + 1) % size, y) - Sample((x + size - 1) % size, y)) * strength;
                    float dy = (Sample(x, (y + 1) % size) - Sample(x, (y + size - 1) % size)) * strength;
                    Vector3 n = new Vector3(-dx, -dy, 1).normalized;
                    c = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f);
                }
                else
                {
                    float value = .9f + noise * .15f;
                    if (kind == "Asphalt") value = .84f + noise * .2f;
                    if (kind == "Metal") value = .88f + noise * .12f + Mathf.Sin(x * 1.3f) * .015f;
                    if (kind == "Paint" && noise < .35f) value *= .65f;
                    if (kind == "Carton") value = .83f + noise * .2f + Mathf.Sin(y * 1.8f) * .015f;
                    if (IsFloorTexture(kind))
                    {
                        // Soft seams share the floor's surface and mip chain; no intersecting, subpixel strips.
                        float distance = Mathf.Min(Mathf.Min(x, size - x), Mathf.Min(y, size - y));
                        float seam = Mathf.Exp(-distance * distance / (2 * 3.2f * 3.2f));
                        value = .955f + (noise - .5f) * .025f - seam * .055f;
                        c = new Color(value, value, value);
                    }
                    else c = new Color(value, value * .99f, value * .97f);
                }
                pixels[y * size + x] = c;
            }
            image.SetPixels(pixels); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG()); Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal; importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8; importer.maxTextureSize = size;
            if (IsFloorTexture(kind))
            {
                importer.mipmapEnabled = true;
                importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 16;
                importer.mipMapBias = 0;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }
            importer.SaveAndReimport();
            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            textures.Add(path, imported);
            return imported;
        }

        private static bool IsFloorTexture(string kind) => kind == "Floor Concrete" || kind == "Yard Concrete";

        private static float PeriodicNoise(float x, float y, float frequency, float size)
        {
            float u = Mathf.SmoothStep(0, 1, x / size);
            float v = Mathf.SmoothStep(0, 1, y / size);
            float a = Mathf.PerlinNoise(x * frequency + 7, y * frequency + 11);
            float b = Mathf.PerlinNoise((x - size) * frequency + 7, y * frequency + 11);
            float c = Mathf.PerlinNoise(x * frequency + 7, (y - size) * frequency + 11);
            float d = Mathf.PerlinNoise((x - size) * frequency + 7, (y - size) * frequency + 11);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        private static void Warehouse(Transform parent)
        {
            Transform g = Group("Warehouse - Open Roof", parent);
            Box(g, "Concrete Floor", V(0, .012f, 6), V(9.8f, .02f, 7.8f), floor);

            Box(g, "Rear Wall", V(0, 1.9f, 10), V(10.3f, 3.8f, .3f), concrete, true);
            Box(g, "West Wall", V(-5, 1.7f, 6), V(.3f, 3.4f, 8), concrete, true);
            Box(g, "East Wall", V(5, 1.7f, 6), V(.3f, 3.4f, 8), concrete, true);
            Box(g, "Entry Wall West", V(-3.7f, .55f, 2), V(2.6f, 1.1f, .34f), concrete, true);
            Box(g, "Entry Wall East", V(3.7f, .55f, 2), V(2.6f, 1.1f, .34f), concrete, true);
            foreach (float x in new[] { -5f, 5f })
            {
                Box(g, "Wall Plinth", V(x, .22f, 6), V(.4f, .44f, 8.2f), dark);
                Box(g, "Steel Roof Coping", V(x, 3.45f, 6), V(.46f, .16f, 8.3f), trim);
                for (float z = 2; z <= 10; z += 2)
                    Box(g, "Structural Pilaster", V(x, 1.7f, z), V(.46f, 3.4f, .22f), trim);
                for (float z = 2.3f; z < 10; z += .28f)
                    Box(g, "Cladding Fold", V(x * 1.035f, 2.12f, z), V(.045f, 2.2f, .045f), trim);
                Box(g, "Wall Service Pipe", V(x * .96f, .75f, 6), V(.07f, .07f, 7.8f), red);
                foreach (float z in new[] { 3.2f, 7.4f })
                {
                    Box(g, "External Window Frame", V(x * 1.036f, 2.5f, z), V(.045f, .9f, 1.5f), steel);
                    Box(g, "External Window Glass", V(x * 1.042f, 2.5f, z), V(.025f, .7f, 1.3f), glass);
                    Box(g, "Window Mullion", V(x * 1.046f, 2.5f, z), V(.03f, .75f, .055f), trim);
                }
            }
            Box(g, "Rear Roof Coping", V(0, 3.87f, 10), V(10.4f, .17f, .48f), trim);
            for (float x = -4.8f; x <= 4.8f; x += .32f)
                Box(g, "Rear Cladding Rib", V(x, 2.1f, 9.82f), V(.035f, 2.9f, .05f), trim);
            for (int i = -1; i <= 1; i++)
            {
                Box(g, "Transom Window Surround", V(i * 2.8f, 3.1f, 9.79f), V(2.25f, .7f, .08f), steel);
                Box(g, "Transom Window", V(i * 2.8f, 3.1f, 9.73f), V(2.05f, .5f, .035f), glass);
                Box(g, "Transom Mullion", V(i * 2.8f, 3.1f, 9.69f), V(.055f, .55f, .04f), trim);
            }
            foreach (float x in new[] { -3.7f, 3.7f })
            {
                Box(g, "Cutaway Wall Cap", V(x, 1.13f, 2), V(2.72f, .12f, .48f), trim);
                Lamp(g, V(x, 2.4f, 2), V(x, 0, .8f), false);
                Bollard(g, V(Mathf.Sign(x) * 2.85f, 0, 1.4f));
                Box(g, "Entry Jamb", V(Mathf.Sign(x) * 2.55f, 1.4f, 2), V(.18f, 2.8f, .24f), steel);
            }
            Sign(g, "WAREHOUSE  /  01", V(-3.72f, .7f, 1.805f), .27f, tape, 2.4f);
            Sign(g, "CARGO HANDLING", V(3.7f, .68f, 1.805f), .22f, tape, 2.35f);
            foreach (float x in new[] { -3.85f, 3.85f })
                foreach (float z in new[] { 4.4f, 7.8f }) Shelf(g, V(x, 0, z));
            foreach (float x in new[] { -2f, 2f })
                for (float z = 2.3f; z < 9; z += 1.25f)
                    Box(g, "Aisle Safety Paint", V(x, .033f, z), V(.07f, .01f, .7f), yellow);
            Box(g, "Entry Threshold", V(0, .03f, 2), V(4.8f, .025f, .16f), trim);
            Hazard(g, V(0, .035f, 2.3f), 4.6f, .35f);
            Sign(g, "01", V(0, .045f, 4.0f), .65f, yellow, 1.5f, true);
            foreach (float x in new[] { -2.7f, 2.7f })
            {
                Box(g, "Overhead Lamp Service Rail", V(x, 4.2f, 6.2f), V(.1f, .12f, 7.7f), steel);
                foreach (float z in new[] { 3f, 9.2f })
                    Rod(g, "Lamp Rail Wall Bracket", V(Mathf.Sign(x) * 4.9f, 3.3f, z), V(x, 4.2f, z), .045f, steel);
            }
            for (int i = 0; i < 3; i++)
            {
                float z = 4 + i * 2.3f;
                Lamp(g, V(-2.7f, 3.5f, z), V(-1.6f, 0, z - .3f), true);
                Lamp(g, V(2.7f, 3.5f, z), V(1.6f, 0, z - .3f), true);
            }
            PipeRun(g, new[] { V(-4.7f, .4f, 9.6f), V(-4.7f, 2.6f, 9.6f), V(4.6f, 2.6f, 9.6f), V(4.6f, 1.4f, 9.6f) }, .1f, steel);
            Box(g, "Electrical Cabinet", V(4.66f, 1.4f, 3.1f), V(.28f, .8f, .7f), steel);
            Box(g, "Cabinet Door", V(4.49f, 1.4f, 3.1f), V(.035f, .69f, .57f), trim);
            Box(g, "Power Indicator", V(4.46f, 1.6f, 3.1f), V(.02f, .08f, .08f), cyan);
            Box(g, "Cargo Staging Paint", V(0, .03f, 7.4f), V(1.3f, .012f, .035f), yellow);
            foreach (float x in new[] { -.65f, .65f }) Box(g, "Cargo Staging Paint", V(x, .03f, 7.8f), V(.035f, .012f, .8f), yellow);
        }

        private static void Shelf(Transform parent, Vector3 position)
        {
            Transform g = At("Steel Storage Shelving", parent, position);
            Box(g, "Shelf Collision", V(0, 1.3f, 0), V(1.65f, 2.6f, 2.45f), steel, true, false);
            foreach (float x in new[] { -.75f, .75f }) foreach (float z in new[] { -1.1f, 1.1f })
                Box(g, "Steel Upright", V(x, 1.4f, z), V(.09f, 2.8f, .09f), steel);
            for (int level = 0; level < 3; level++)
            {
                float y = .18f + level * .85f;
                Box(g, "Shelf Tray", V(0, y, 0), V(1.58f, .08f, 2.3f), trim);
                foreach (float x in new[] { -.77f, .77f }) Box(g, "Orange Shelf Rail", V(x, y - .04f, 0), V(.07f, .13f, 2.32f), yellow);
                Parcel(g, V(-.25f, y + .05f, -.58f), V(.65f, .57f, .83f), level % 2 == 0);
                Parcel(g, V(.13f, y + .05f, .55f), V(.85f, .65f, .9f), level % 2 != 0);
            }
            Rod(g, "Rack Diagonal Brace", V(-.73f, .2f, -1.12f), V(.73f, 2.7f, -1.12f), .023f, trim);
            Rod(g, "Rack Diagonal Brace", V(.73f, .2f, -1.12f), V(-.73f, 2.7f, -1.12f), .023f, trim);
        }

        private static void Parcel(Transform parent, Vector3 bottom, Vector3 size, bool light)
        {
            Box(parent, "Packed Carton", bottom + Vector3.up * size.y * .5f, size, light ? cartonLight : carton);
            Box(parent, "Packing Tape", bottom + V(0, size.y + .006f, 0), V(size.x + .005f, .012f, size.z * .12f), tape);
            Box(parent, "Shipping Label", bottom + V(size.x * .12f, size.y * .6f, -size.z * .5f - .006f),
                V(size.x * .42f, size.y * .28f, .009f), tape);
            for (int i = 0; i < 4; i++) Box(parent, "Label Barcode", bottom + V(size.x * (-.02f + i * .07f), size.y * .6f, -size.z * .5f - .014f),
                V(.015f, size.y * .18f, .008f), dark);
        }

        private static void Yard(Transform parent)
        {
            Transform g = Group("Nearby Industrial Yard", parent);
            Box(g, "Exterior Ground Apron - Visual", V(0, -.065f, 0), V(100, .1f, 100), asphalt);
            Container(g, V(-10, 0, 5.9f), 0, red, "CS  402-18");
            Container(g, V(10.3f, 0, 8.1f), 90, blue, "LOGISTICS  04");
            PalletStack(g, V(-5, 0, -7)); PalletStack(g, V(-11, 0, 9.5f));
            PalletStack(g, V(7.5f, 0, 5.3f));
            Barrier(g, V(-9, 0, -1.5f), 0);
            Barrier(g, V(9.2f, 0, 3.8f), 0);
            foreach (float x in new[] { -2.2f, 2.2f }) for (float z = -6; z < 2; z += 1.3f)
                // Stop at the threshold: the original final dash overlapped the warehouse slab at the same height.
                Box(g, "Loading Lane Dash", V(x, .016f, z), V(.08f, .012f, Mathf.Min(.7f, (2 - z) * 2)), yellow);
            foreach (float z in new[] { -5.4f, -.6f }) Box(g, "Extraction Bay Edge", V(11, .019f, z), V(4.3f, .012f, .1f), yellow);
            Sign(g, "EXTRACTION", V(10.9f, .028f, -6.2f), .4f, yellow, 4, true);
            Sign(g, "KEEP CLEAR", V(5.3f, .02f, -3), .28f, yellow, 3.4f, true);
            foreach (float x in new[] { 8.7f, 13.3f })
                foreach (float z in new[] { -5.5f, -.5f })
                {
                    Box(g, "Inset Beacon Housing", V(x, .06f, z), V(.22f, .12f, .22f), steel);
                    Box(g, "Inset Cyan Lens", V(x, .127f, z), V(.14f, .016f, .14f), cyan);
                }
            foreach (Vector3 p in new[] { V(-12.5f, 0, -4), V(-7.1f, 0, 8.8f), V(7.3f, 0, 8.9f), V(13.4f, 0, -8.5f) }) FloodPole(g, p);
            PipeRun(g, new[] { V(5.5f, .8f, 8.5f), V(5.5f, 2.4f, 8.5f), V(8.2f, 2.4f, 8.5f), V(8.2f, .5f, 8.5f) }, .14f, trim);
            PipeRun(g, new[] { V(5.5f, .8f, 9.1f), V(5.5f, 2.8f, 9.1f), V(8.2f, 2.8f, 9.1f), V(8.2f, .5f, 9.1f) }, .11f, steel);
            for (float z = 8.5f; z < 9.2f; z += .6f) Box(g, "Pipe Support", V(6.7f, 1.2f, z), V(.13f, 2.4f, .14f), steel);
            Drain(g, V(0, .027f, 1.3f), 4.4f);
            Drain(g, V(10.8f, .02f, -7.6f), 3);
            foreach (Vector3 p in new[] { V(-7.2f, .011f, 2.5f), V(5.6f, .011f, -6.3f), V(11.8f, .012f, 1.7f), V(-3.8f, .012f, -8.3f) })
            {
                var patch = MeshObject(g, "Oil and Water Wear", disc, stain);
                patch.transform.localPosition = p; patch.transform.localScale = V(.7f, 1, .45f);
            }
            var random = new System.Random(402);
            for (int i = 0; i < 28; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * (13.4f + (float)random.NextDouble() * .6f);
                float z = -10 + (float)random.NextDouble() * 20;
                Box(g, "Boundary Rubble", V(x, .045f, z), V(.1f + (float)random.NextDouble() * .18f, .09f, .12f), concrete);
            }
        }

        private static void Container(Transform parent, Vector3 position, float yaw, Material paint, string label)
        {
            Transform g = At("Corrugated Shipping Container", parent, position);
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(g, "Container Body", V(0, 1.25f, 0), V(2.5f, 2.5f, 5), paint, true);
            foreach (float x in new[] { -1.27f, 1.27f })
            {
                foreach (float y in new[] { .1f, 2.45f }) Box(g, "Container Side Rail", V(x, y, 0), V(.12f, .14f, 5.15f), steel);
                for (float z = -2.3f; z < 2.4f; z += .22f) Box(g, "Corrugated Side Fold", V(x, 1.25f, z), V(.06f, 2.25f, .07f), paint);
            }
            for (float z = -2.3f; z < 2.4f; z += .25f) Box(g, "Roof Corrugation", V(0, 2.53f, z), V(2.45f, .06f, .07f), paint);
            foreach (float x in new[] { -.63f, .63f })
            {
                Box(g, "End Door", V(x, 1.25f, -2.53f), V(1.2f, 2.27f, .08f), paint);
                Rod(g, "Door Lock Rod", V(x, .3f, -2.61f), V(x, 2.25f, -2.61f), .025f, trim);
                Box(g, "Door Handle", V(x + .12f, 1, -2.65f), V(.28f, .05f, .05f), trim);
                foreach (float y in new[] { .2f, 2.35f }) Box(g, "Corner Casting", V(x * 1.9f, y, -2.56f), V(.16f, .16f, .14f), trim);
            }
            Sign(g, label, V(0, 1.8f, -2.585f), .18f, tape, 2.2f);
        }

        private static void PalletStack(Transform parent, Vector3 position)
        {
            Transform g = At("Palletized Freight", parent, position);
            Box(g, "Freight Collision", V(0, .7f, 0), V(1.9f, 1.4f, 1.5f), wood, true, false);
            foreach (float x in new[] { -.65f, 0, .65f }) Box(g, "Pallet Foot", V(x, .1f, 0), V(.16f, .2f, 1.5f), wood);
            for (float z = -.65f; z <= .7f; z += .26f) Box(g, "Pallet Deck Board", V(0, .23f, z), V(1.9f, .08f, .2f), wood);
            Parcel(g, V(-.47f, .28f, 0), V(.85f, .85f, 1.23f), true);
            Parcel(g, V(.47f, .28f, 0), V(.85f, .85f, 1.23f), false);
            Parcel(g, V(-.22f, 1.14f, 0), V(1.1f, .65f, 1), true);
        }

        private static void Barrier(Transform parent, Vector3 p, float yaw)
        {
            Transform g = At("Concrete Traffic Barrier", parent, p); g.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(g, "Concrete Cover", V(0, .55f, 0), V(2.8f, 1.1f, .65f), concrete, true);
            Box(g, "Barrier Foot", V(0, .12f, 0), V(3, .24f, .85f), concrete);
            for (int i = -2; i <= 2; i++)
            {
                var stripe = Box(g, "Barrier Yellow Stripe", V(i * .48f, .65f, -.34f), V(.21f, .55f, .018f), yellow);
                stripe.transform.localRotation = Quaternion.Euler(0, 0, -22);
            }
            foreach (float x in new[] { -1.1f, 1.1f }) Box(g, "Reflector", V(x, .9f, -.355f), V(.11f, .08f, .02f), warm);
        }

        private static void Fences(Transform parent)
        {
            Transform g = Group("Boundary Chain Link Fencing", parent);
            Fence(g, V(-14.6f, 0, -11.6f), V(14.6f, 0, -11.6f));
            Fence(g, V(-14.6f, 0, 11.6f), V(14.6f, 0, 11.6f));
            Fence(g, V(-14.6f, 0, -11.6f), V(-14.6f, 0, 11.6f));
            Fence(g, V(14.6f, 0, -11.6f), V(14.6f, 0, 11.6f));
        }

        private static void Fence(Transform parent, Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            int count = Mathf.CeilToInt(delta.magnitude / 3.8f);
            Transform g = At("Chain Link Boundary", parent, a);
            g.rotation = Quaternion.LookRotation(Vector3.Cross(delta.normalized, Vector3.up));
            float length = delta.magnitude;
            Box(g, "Continuous Boundary Collision", V(length * .5f, 1.15f, 0), V(length, 2.3f, .18f), steel, true, false);
            Box(g, "Concrete Fence Curb", V(length * .5f, .13f, 0), V(length, .26f, .35f), concrete);
            for (int i = 0; i <= count; i++)
            {
                float x = i * length / count;
                Rod(g, "Galvanized Fence Post", V(x, .1f, 0), V(x, 2.3f, 0), .048f, trim);
                Box(g, "Post Footing", V(x, .16f, 0), V(.4f, .32f, .45f), concrete);
                Rod(g, "Angled Security Arm", V(x, 2.28f, 0), V(x, 2.58f, -.18f), .025f, steel);
                if (i == count) continue;
                float width = length / count;
                var panel = MeshObject(g, "Diamond Chain Link Mesh", wire, trim);
                panel.transform.localPosition = V(x + .06f, .4f, 0);
                panel.transform.localScale = V((width - .12f) / 3.6f, 1, 1);
                foreach (float y in new[] { .36f, 2.14f }) Rod(g, "Panel Frame Rail", V(x, y, 0), V(x + width, y, 0), .028f, steel);
            }
            foreach (float y in new[] { 2.37f, 2.55f }) Rod(g, "Security Wire", V(0, y, -.13f), V(length, y, -.13f), .009f, steel);
        }

        private static void Helicopter(Transform parent)
        {
            Transform heli = At("Helicopter Placeholder - Visual Only", parent, V(10.2f, 5.8f, -4.2f));
            heli.localRotation = Quaternion.Euler(0, 65, 0);
            Mesh bodyMesh = SaveMesh("Utility Helicopter Fuselage", Fuselage(false));
            MeshObject(heli, "Faceted Utility Fuselage", bodyMesh, olive);
            MeshObject(heli, "Fitted Cockpit and Cabin Glazing", SaveMesh("Helicopter Glazing", Fuselage(true)), glass);
            Box(heli, "Engine Fairing", V(0, .84f, .35f), V(1.35f, .5f, 1.8f), olive);
            foreach (float x in new[] { -.7f, .7f })
            {
                Rod(heli, "Turbine Exhaust", V(x, .8f, .75f), V(x, .82f, 1.6f), .19f, steel);
                Rod(heli, "Dark Exhaust Opening", V(x, .82f, 1.57f), V(x, .82f, 1.65f), .145f, rubber);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Rod(heli, "Door Frame Vertical", V(side * 1.03f, -.45f, -.22f), V(side * .92f, .65f, -.22f), .023f, trim);
                Rod(heli, "Sliding Door Lower Rail", V(side * .99f, -.42f, -.2f), V(side * .95f, -.42f, 1.02f), .024f, trim);
                Box(heli, "Door Handle", V(side * 1.04f, -.02f, .67f), V(.045f, .055f, .23f), trim);
                Rod(heli, "Landing Skid", V(side * 1.2f, -1.12f, -1.8f), V(side * 1.2f, -1.12f, 1.8f), .065f, steel);
                Rod(heli, "Raised Skid Nose", V(side * 1.2f, -1.12f, -1.8f), V(side * 1.2f, -.92f, -2.05f), .065f, steel);
                foreach (float z in new[] { -.85f, .85f }) Rod(heli, "Skid Support", V(side * .7f, -.4f, z), V(side * 1.2f, -1.1f, z), .055f, steel);
                Rod(heli, "Rope Attachment", V(side * .85f, -.5f, .1f), V(side * 1.45f, -.5f, .1f), .055f, steel);
                Rod(heli, "Hanging Rope", V(side * 1.4f, -.55f, .1f), V(side * 1.4f, -5.12f, .1f), .025f, rubber);
                Mesh hook = SaveMesh("Rope Hook", ArcMesh(.13f, .026f, 25, 320));
                var h = MeshObject(heli, "Rope Hook", hook, trim); h.transform.localPosition = V(side * 1.4f, -5.22f, .1f);
                Box(heli, "Navigation Lamp", V(side * 1.04f, .08f, .78f), V(.06f, .08f, .12f), side < 0 ? warm : cyan);
            }
            Sign(heli, "CS - 04", V(1.025f, -.22f, .35f), .16f, tape, 1.1f);
            heli.Find("CS - 04").localRotation = Quaternion.Euler(0, -90, 0);
            Rod(heli, "Tail Boom", V(0, .05f, 1.25f), V(0, .68f, 5.25f), .18f, olive);
            Polygon(heli, "Vertical Tail Fin", new[] { V(0, .5f, 4.7f), V(0, 2.1f, 5.15f), V(0, 2.15f, 5.7f), V(0, .5f, 5.35f) }, olive);
            Box(heli, "Tail Stabilizer", V(0, .7f, 4.5f), V(2.4f, .08f, .55f), olive);
            Rod(heli, "Tail Rotor Axle", V(-.15f, 1.05f, 5.15f), V(.38f, 1.05f, 5.15f), .08f, steel);
            var tailRotor = At("Tail Rotor", heli, V(.42f, 1.05f, 5.15f));
            tailRotor.localRotation = Quaternion.Euler(25, 0, 0);
            Box(tailRotor, "Tail Rotor Blade", Vector3.zero, V(.055f, 1.6f, .12f), dark);
            Box(tailRotor, "Tail Rotor Blade", Vector3.zero, V(.055f, .12f, 1.6f), dark);
            Rod(heli, "Main Rotor Mast", V(0, .9f, .1f), V(0, 1.8f, .1f), .09f, trim);
            var rotor = At("Four Blade Main Rotor", heli, V(0, 1.8f, .1f));
            rotor.localRotation = Quaternion.Euler(0, 24, 0);
            for (int i = 0; i < 4; i++)
            {
                var blade = At("Rotor Blade", rotor, Vector3.zero); blade.localRotation = Quaternion.Euler(0, i * 90, 0);
                Box(blade, "Composite Blade", V(0, 0, 2.25f), V(.25f, .045f, 4.15f), dark);
                Box(blade, "Blade Tip Stripe", V(0, .012f, 4.13f), V(.26f, .05f, .25f), yellow);
                Rod(rotor, "Rotor Pitch Link", V(0, -.15f, 0), blade.TransformPoint(V(0, 0, .7f)) - rotor.position, .035f, steel);
            }
            Box(heli, "Underside Search Lamp", V(0, -.69f, -1.15f), V(.45f, .22f, .4f), dark);
            Box(heli, "Search Lamp Lens", V(0, -.815f, -1.15f), V(.3f, .025f, .27f), warm);
            var extractionLight = new GameObject("Extraction Cyan Spill").AddComponent<Light>();
            extractionLight.transform.SetParent(parent, false); extractionLight.transform.localPosition = V(11, 1.5f, -3);
            extractionLight.type = LightType.Point; extractionLight.color = new Color(.12f, .9f, .88f);
            extractionLight.intensity = 3.5f; extractionLight.range = 5; extractionLight.shadows = LightShadows.None;
        }

        private static void Lighting(Transform parent)
        {
            Scene scene = parent.gameObject.scene;
            Light sun = scene.GetRootGameObjects().Single(g => g.name == "Directional Light").GetComponent<Light>();
            sun.color = new Color(.93f, .95f, 1); sun.intensity = .62f;
            sun.transform.rotation = Quaternion.Euler(48, -32, 0); sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .45f;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.53f, .535f, .55f);
            RenderSettings.ambientIntensity = 1;
            string reflectionPath = ArtPath + "/Industrial Ambient Reflection.asset";
            var reflection = AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath);
            if (!reflection)
            {
                reflection = new Cubemap(32, TextureFormat.RGB24, true);
                AssetDatabase.CreateAsset(reflection, reflectionPath);
            }
            foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX,
                CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
            {
                Color color = face == CubemapFace.PositiveY ? new Color(.64f, .65f, .68f) :
                    face == CubemapFace.NegativeY ? new Color(.24f, .24f, .245f) : new Color(.46f, .47f, .5f);
                reflection.SetPixels(Enumerable.Repeat(color, 32 * 32).ToArray(), face);
            }
            reflection.Apply(); EditorUtility.SetDirty(reflection); AssetDatabase.SaveAssetIfDirty(reflection);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection;
            RenderSettings.reflectionIntensity = .55f;
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.25f, .26f, .28f);
            RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .006f;
            var camera = scene.GetRootGameObjects().Single(g => g.name == "Main Camera").GetComponent<Camera>();
            camera.orthographicSize = 6.8f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.25f, .26f, .28f);
            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (!cameraData) cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            string path = ArtPath + "/Industrial Atmosphere.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            T Effect<T>() where T : VolumeComponent
            {
                if (profile.TryGet<T>(out var effect)) return effect;
                effect = profile.Add<T>(); AssetDatabase.AddObjectToAsset(effect, profile); return effect;
            }
            Effect<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var bloom = Effect<Bloom>(); bloom.intensity.Override(.23f); bloom.threshold.Override(1.15f); bloom.scatter.Override(.5f);
            var grading = Effect<ColorAdjustments>(); grading.postExposure.Override(.1f); grading.contrast.Override(3); grading.saturation.Override(-5);
            var vignette = Effect<Vignette>(); vignette.intensity.Override(.06f); vignette.smoothness.Override(.4f);
            foreach (var effect in profile.components) EditorUtility.SetDirty(effect);
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
            var volume = Group("Industrial Atmosphere", parent).gameObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;
        }

        private static void Lamp(Transform parent, Vector3 p, Vector3 target, bool pendant, bool yard = false)
        {
            Transform g = At(pendant ? "Warehouse Pendant Lamp" : "Entry Wall Lamp", parent, p);
            if (pendant) Rod(g, "Lamp Suspension", V(0, .7f, 0), V(0, .05f, 0), .017f, steel);
            else Rod(g, "Wall Lamp Bracket", V(0, -.4f, .1f), V(0, 0, 0), .035f, steel);
            Box(g, "Lamp Housing", Vector3.zero, V(.66f, .18f, .4f), steel);
            Box(g, "Warm Lamp Diffuser", V(0, -.103f, 0), V(.53f, .025f, .3f), warm);
            Box(g, "Visible Lamp Lens Edge", V(0, -.06f, -.205f), V(.5f, .055f, .015f), warm);
            var light = new GameObject("Warm Pool of Light").AddComponent<Light>();
            light.transform.SetParent(g, false); light.transform.localPosition = V(0, -.18f, 0);
            light.transform.rotation = Quaternion.LookRotation(parent.TransformPoint(target) - light.transform.position);
            light.type = LightType.Spot; light.color = new Color(1, .74f, .44f);
            light.intensity = pendant ? 9 : yard ? 8 : 7;
            light.range = pendant ? 6.5f : 6; light.spotAngle = 78; light.innerSpotAngle = 45;
            light.shadows = pendant && Mathf.Abs(p.z - 6.3f) < .2f ? LightShadows.Soft : LightShadows.None;
            using var lightData = new SerializedObject(light.GetUniversalAdditionalLightData());
            lightData.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
            lightData.ApplyModifiedPropertiesWithoutUndo();
            light.shadowStrength = .7f; light.shadowBias = .03f;
        }

        private static void FloodPole(Transform parent, Vector3 p)
        {
            Transform g = At("Yard Floodlight Pole", parent, p);
            Box(g, "Concrete Pole Base", V(0, .22f, 0), V(.65f, .44f, .65f), concrete);
            Rod(g, "Floodlight Mast", V(0, .3f, 0), V(0, 4.2f, 0), .07f, steel);
            Box(g, "Twin Lamp Crossbar", V(0, 4.2f, 0), V(1.05f, .1f, .1f), steel);
            Lamp(g, V(-.3f, 4.2f, 0), V(-.3f, 0, -1.8f), false, true);
            Lamp(g, V(.3f, 4.2f, 0), V(.3f, 0, 1.8f), false, true);
        }

        private static void Bollard(Transform parent, Vector3 p)
        {
            Rod(parent, "Safety Bollard", p + V(0, .08f, 0), p + V(0, 1.05f, 0), .09f, yellow);
            Rod(parent, "Bollard Dark Band", p + V(0, .64f, 0), p + V(0, .81f, 0), .092f, dark);
            Box(parent, "Bollard Base", p + V(0, .06f, 0), V(.3f, .12f, .3f), steel);
        }

        private static void PipeRun(Transform parent, Vector3[] points, float radius, Material material)
        {
            for (int i = 1; i < points.Length; i++)
            {
                Rod(parent, "Industrial Service Pipe", points[i - 1], points[i], radius, material);
                var joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                joint.name = "Pipe Elbow"; SceneManager.MoveGameObjectToScene(joint, parent.gameObject.scene);
                joint.transform.SetParent(parent, false); joint.transform.localPosition = points[i - 1];
                joint.transform.localScale = Vector3.one * radius * 2.04f; Object.DestroyImmediate(joint.GetComponent<Collider>());
                joint.GetComponent<Renderer>().sharedMaterial = material;
                Vector3 direction = (points[i] - points[i - 1]).normalized;
                Rod(parent, "Pipe Flange", points[i - 1] + direction * .23f, points[i - 1] + direction * .3f, radius * 1.4f, trim);
            }
        }

        private static void Drain(Transform parent, Vector3 p, float length)
        {
            Box(parent, "Recessed Drain Channel", p, V(length, .015f, .25f), dark);
            for (float x = -length * .5f; x <= length * .5f; x += .11f)
                Box(parent, "Drain Grate Bar", p + V(x, .01f, 0), V(.025f, .015f, .23f), steel);
        }

        private static void Hazard(Transform parent, Vector3 p, float width, float depth)
        {
            Box(parent, "Threshold Dark Hazard Band", p, V(width, .012f, depth), dark);
            for (float x = -width * .5f + .2f; x < width * .5f - .2f; x += .36f)
            {
                var stripe = Box(parent, "Threshold Hazard Stripe", p + V(x, .01f, 0), V(.15f, .012f, depth), yellow);
                stripe.transform.localRotation = Quaternion.Euler(0, -28, 0);
            }
        }

        private static void Sign(Transform parent, string text, Vector3 p, float size, Material ink, float width, bool ground = false)
        {
            var item = new GameObject(text, typeof(TextMeshPro)); SceneManager.MoveGameObjectToScene(item, parent.gameObject.scene);
            item.transform.SetParent(parent, false); item.transform.localPosition = p;
            if (ground) item.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var label = item.GetComponent<TextMeshPro>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            label.text = text; label.fontSize = size * 10; label.color = ink.GetColor("_BaseColor");
            label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(width, ground ? .8f : .6f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static Transform Group(string name, Transform parent, Scene scene = default)
        {
            var item = new GameObject(name);
            SceneManager.MoveGameObjectToScene(item, parent ? parent.gameObject.scene : scene);
            if (parent) item.transform.SetParent(parent, false);
            return item.transform;
        }

        private static Transform At(string name, Transform parent, Vector3 position)
        {
            Transform group = Group(name, parent); group.localPosition = position; return group;
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        private static GameObject Box(Transform parent, string name, Vector3 p, Vector3 size,
            Material material, bool solid = false, bool visible = true)
        {
            var item = visible ? MeshObject(parent, name, bevel, material) : Group(name, parent).gameObject;
            item.transform.localPosition = p; item.transform.localScale = size;
            if (solid) item.AddComponent<BoxCollider>();
            return item;
        }

        private static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material)
        {
            var item = Group(name, parent).gameObject;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            return item;
        }

        private static void Rod(Transform parent, string name, Vector3 a, Vector3 b, float radius, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cylinder); item.name = name;
            SceneManager.MoveGameObjectToScene(item, parent.gameObject.scene); item.transform.SetParent(parent, false);
            item.transform.localPosition = (a + b) * .5f;
            item.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            item.transform.localScale = V(radius * 2, Vector3.Distance(a, b) * .5f, radius * 2);
            Object.DestroyImmediate(item.GetComponent<Collider>()); item.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Polygon(Transform parent, string name, Vector3[] points, Material material)
        {
            string key = name.Replace(" ", "") + "-" + string.Join("_", points.Select(p => p.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)));
            var builder = new Geometry(); builder.Face(points, true);
            MeshObject(parent, name, SaveMesh(key, builder.Mesh(name)), material);
        }

        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = ArtPath + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing); return existing;
            }
            mesh.name = name; AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }

        private static Mesh BeveledBox()
        {
            var g = new Geometry();
            const float inner = .477f, outer = .5f;
            for (int axis = 0; axis < 3; axis++) foreach (float sign in new[] { -1f, 1f })
            {
                Vector3[] face = { V(-inner, -inner, 0), V(inner, -inner, 0), V(inner, inner, 0), V(-inner, inner, 0) };
                for (int i = 0; i < face.Length; i++)
                {
                    Vector3 p = face[i]; face[i] = axis == 0 ? V(sign * outer, p.x, p.y) : axis == 1 ? V(p.y, sign * outer, p.x) : V(p.x, p.y, sign * outer);
                }
                g.Outward(face);
            }
            for (int axis = 0; axis < 3; axis++) foreach (float a in new[] { -1f, 1f }) foreach (float b in new[] { -1f, 1f })
            {
                Vector3 P(float l, float u, float v) => axis == 0 ? V(l, a * u, b * v) : axis == 1 ? V(b * v, l, a * u) : V(a * u, b * v, l);
                g.Outward(new[] { P(-inner, inner, outer), P(inner, inner, outer), P(inner, outer, inner), P(-inner, outer, inner) });
            }
            foreach (float x in new[] { -1f, 1f }) foreach (float y in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                g.Outward(new[] { V(x * outer, y * inner, z * inner), V(x * inner, y * outer, z * inner), V(x * inner, y * inner, z * outer) });
            return g.Mesh("Chamfered Box");
        }

        private static Mesh ChainLink()
        {
            var g = new Geometry();
            const float width = 3.6f, height = 1.7f;
            foreach (int slope in new[] { -1, 1 }) for (float intercept = -width; intercept < width + height; intercept += .19f)
            {
                var points = new List<Vector3>();
                float y0 = intercept, y1 = slope * width + intercept;
                if (y0 >= 0 && y0 <= height) points.Add(V(0, y0, 0));
                if (y1 >= 0 && y1 <= height) points.Add(V(width, y1, 0));
                float x0 = -intercept / slope, x1 = (height - intercept) / slope;
                if (x0 > 0 && x0 < width) points.Add(V(x0, 0, 0));
                if (x1 > 0 && x1 < width) points.Add(V(x1, height, 0));
                if (points.Count >= 2) g.Tube(points[0], points[1], .009f, 5);
            }
            return g.Mesh("Diamond Chain Link");
        }

        private static Mesh PatchMesh()
        {
            var g = new Geometry(); var points = new Vector3[20];
            for (int i = 0; i < points.Length; i++)
            {
                float t = i * Mathf.PI * 2 / points.Length;
                float r = .65f + Mathf.Sin(i * 2.1f) * .15f;
                points[i] = V(Mathf.Cos(t) * r, 0, Mathf.Sin(t) * r);
            }
            g.Face(points, true); return g.Mesh("Irregular Surface Wear");
        }

        private static Mesh Fuselage(bool windows)
        {
            var g = new Geometry();
            float[] zs = { -2.2f, -1.65f, -.7f, .75f, 1.45f, 1.75f };
            float[] widths = { .34f, .82f, 1.02f, .96f, .58f, .2f };
            float[] heights = { .39f, .65f, .78f, .72f, .49f, .23f };
            Vector3 Point(int row, int i)
            {
                float angle = (i % 8) * Mathf.PI / 4 + Mathf.PI / 8;
                return V(Mathf.Cos(angle) * widths[row], Mathf.Sin(angle) * heights[row], zs[row]);
            }
            for (int row = 0; row < zs.Length - 1; row++) for (int i = 0; i < 8; i++)
            {
                Vector3[] face = { Point(row, i), Point(row, i + 1), Point(row + 1, i + 1), Point(row + 1, i) };
                bool glazed = row == 0 && i <= 2 || (row == 1 || row == 2) && (i == 0 || i == 2);
                if (glazed)
                {
                    Vector3 centre = face.Aggregate(Vector3.zero, (sum, p) => sum + p) / 4;
                    var inner = face.Select(p => Vector3.Lerp(p, centre, .12f)).ToArray();
                    if (windows) g.Outward(inner);
                    else for (int edge = 0; edge < 4; edge++)
                        g.Outward(new[] { face[edge], face[(edge + 1) % 4], inner[(edge + 1) % 4], inner[edge] });
                }
                else if (!windows) g.Outward(face);
            }
            if (!windows)
            {
                g.Face(Enumerable.Range(0, 8).Select(i => Point(0, 7 - i)).ToArray(), true);
                g.Face(Enumerable.Range(0, 8).Select(i => Point(zs.Length - 1, i)).ToArray(), true);
            }
            return g.Mesh("Utility Helicopter Fuselage");
        }

        private static Mesh ArcMesh(float radius, float tube, float from, float to)
        {
            var g = new Geometry();
            for (int i = 0; i < 22; i++)
            {
                float a = Mathf.Lerp(from, to, i / 22f) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(from, to, (i + 1) / 22f) * Mathf.Deg2Rad;
                g.Tube(V(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0), V(Mathf.Cos(b) * radius, Mathf.Sin(b) * radius, 0), tube, 6);
            }
            return g.Mesh("Cargo Rope Hook");
        }

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();
            public void Outward(Vector3[] points)
            {
                Vector3 centre = points.Aggregate(Vector3.zero, (sum, p) => sum + p) / points.Length;
                if (Vector3.Dot(Vector3.Cross(points[1] - points[0], points[2] - points[0]), centre) < 0) Array.Reverse(points);
                Face(points);
            }
            public void Face(Vector3[] points, bool both = false)
            {
                int first = vertices.Count;
                Vector3 normal = Vector3.Cross(points[1] - points[0], points[2] - points[0]).normalized;
                foreach (Vector3 p in points)
                {
                    vertices.Add(p);
                    uv.Add(Mathf.Abs(normal.y) > .5f ? new Vector2(p.x + .5f, p.z + .5f) :
                        Mathf.Abs(normal.x) > .5f ? new Vector2(p.z + .5f, p.y + .5f) : new Vector2(p.x + .5f, p.y + .5f));
                }
                for (int i = 1; i < points.Length - 1; i++)
                {
                    triangles.Add(first); triangles.Add(first + i); triangles.Add(first + i + 1);
                }
                if (both) { var reverse = (Vector3[])points.Clone(); Array.Reverse(reverse); Face(reverse); }
            }
            public void Tube(Vector3 a, Vector3 b, float radius, int sides)
            {
                Vector3 direction = (b - a).normalized;
                Vector3 right = Vector3.Cross(direction, Mathf.Abs(direction.y) > .9f ? Vector3.right : Vector3.up).normalized;
                Vector3 up = Vector3.Cross(direction, right);
                for (int i = 0; i < sides; i++)
                {
                    Vector3 R(int j) => (right * Mathf.Cos(j * Mathf.PI * 2 / sides) + up * Mathf.Sin(j * Mathf.PI * 2 / sides)) * radius;
                    Face(new[] { a + R(i), a + R(i + 1), b + R(i + 1), b + R(i) });
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
            }
        }

        private static void Validate(Scene scene, Transform root)
        {
            T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
            if (All<Player.PlayerController>().Length != 1 || All<Cargo.CargoItem>().Length != 1 ||
                All<Missions.CargoExtraction>().Length != 1 || All<UI.CharacterSelection>().Length != 1)
                throw new InvalidOperationException("The sample must retain exactly one of each gameplay authority.");
            if (root.Find("Helicopter Placeholder - Visual Only").GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Helicopter scenery must have no collision.");
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (!renderer.sharedMaterial || !renderer.GetComponent<MeshFilter>().sharedMesh)
                    throw new InvalidOperationException("Missing scenery mesh or material on " + renderer.name);
        }
    }
}
