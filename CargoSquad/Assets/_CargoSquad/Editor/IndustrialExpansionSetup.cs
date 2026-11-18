using System;
using System.IO;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Cargo;
using CargoSquad.Missions;
using CargoSquad.Player;
using CargoSquad.UI;
using CargoSquad.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CargoSquad.Editor
{
    public static class IndustrialExpansionSetup
    {
        public const string ScenePath = "Assets/_CargoSquad/Scenes/IndustrialExpansion.unity";
        private const string SourcePath = "Assets/_CargoSquad/Scenes/IndustrialDistrict.unity";
        private const string MenuPath = "Cargo Squad/Maps/Create Industrial Expansion Sample";
        private const string SharedMaterials = "Assets/_CargoSquad/Art/Shared/Materials/";

        [MenuItem(MenuPath)]
        public static void CreateSample()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before creating the sample.");
            if (File.Exists(ScenePath) || File.Exists(ScenePath + ".meta"))
            {
                Debug.Log("IndustrialExpansion already exists; no files were changed. Open " + ScenePath);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                return;
            }

            // Copy the saved scene so every existing local gameplay reference stays together.
            // The source scene, shared assets, open scenes and build settings are never saved here.
            Scene previous = SceneManager.GetActiveScene();
            Scene sample = default;
            bool copied = false;
            bool complete = false;
            try
            {
                if (!AssetDatabase.CopyAsset(SourcePath, ScenePath))
                    throw new IOException("Could not copy IndustrialDistrict to " + ScenePath);
                copied = true;
                sample = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(sample);

                Material concrete = Material("CS_FilterGrey");
                Material steel = Material("CS_Gunmetal");
                Material rust = Material("Armored_PlateRust");
                Material paint = Material("Heavy_Accent");
                Material wood = AssetDatabase.LoadAssetAtPath<Material>(
                    "Assets/_CargoSquad/Art/Materials/CargoWeatheredBrown.mat");
                if (!wood) throw new InvalidOperationException("The existing brown cargo material is missing.");

                Object.DestroyImmediate(Root(sample, "Exploration Graybox"));
                var ground = Root(sample, "Ground");
                ground.transform.position = new Vector3(0, -.5f, 0);
                ground.transform.localScale = new Vector3(30, 1, 24);
                ground.GetComponent<Renderer>().sharedMaterial = concrete;
                Root(sample, "PlayerPlaceholder").transform.SetPositionAndRotation(
                    new Vector3(-10, 1, -7), Quaternion.identity);
                Root(sample, "CargoPlaceholder").transform.position = new Vector3(0, .225f, 7.4f);

                Transform yard = Group(sample, "Industrial Expansion Sample");
                BuildFences(yard, steel);
                BuildWarehouse(yard, concrete, rust, steel, paint, wood);
                BuildCover(yard, concrete, steel, wood);
                BuildMarkings(yard, paint);
                BuildHelicopter(yard, steel, rust, concrete);

                // Keep the current optional local enemy loader and its references, on clear yard routes.
                Transform spawn = Root(sample, "Mutated Wolf V2 Spawn").transform;
                spawn.position = new Vector3(8, 0, -7);
                spawn.Find("Wolf Patrol 1").position = new Vector3(8, 0, -7);
                spawn.Find("Wolf Patrol 2").position = new Vector3(3, 0, -7);
                spawn.Find("Wolf Patrol 3").position = new Vector3(7.5f, 0, 2);

                Validate(sample);
                if (!EditorSceneManager.SaveScene(sample, ScenePath))
                    throw new IOException("Could not save " + ScenePath);
                complete = true;
                Debug.Log("Industrial Expansion sample created at " + ScenePath +
                    ". Open it, press Play, select Heavy or Medic, and carry the warehouse cargo to EXTRACT.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (sample.IsValid() && sample.isLoaded) EditorSceneManager.CloseScene(sample, true);
                if (copied && !complete) AssetDatabase.DeleteAsset(ScenePath);
            }
        }

        private static void BuildFences(Transform parent, Material steel)
        {
            Transform fences = Group(parent.gameObject.scene, "Boundary Fences", parent);
            Fence(fences, "South", new Vector3(-14.6f, 0, -11.6f), new Vector3(14.6f, 0, -11.6f), steel);
            Fence(fences, "North", new Vector3(-14.6f, 0, 11.6f), new Vector3(14.6f, 0, 11.6f), steel);
            Fence(fences, "West", new Vector3(-14.6f, 0, -11.6f), new Vector3(-14.6f, 0, 11.6f), steel);
            Fence(fences, "East", new Vector3(14.6f, 0, -11.6f), new Vector3(14.6f, 0, 11.6f), steel);
        }

        private static void Fence(Transform parent, string name, Vector3 from, Vector3 to, Material material)
        {
            Transform edge = Group(parent.gameObject.scene, name + " Fence", parent);
            Vector3 delta = to - from;
            bool alongX = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);
            var barrier = edge.gameObject.AddComponent<BoxCollider>();
            barrier.center = (from + to) * .5f + Vector3.up;
            barrier.size = alongX ? new Vector3(delta.magnitude, 2, .18f) : new Vector3(.18f, 2, delta.magnitude);
            for (int i = 0; i <= Mathf.CeilToInt(delta.magnitude / 2); i++)
            {
                int count = Mathf.CeilToInt(delta.magnitude / 2);
                Box(edge, "Fence Post " + i, Vector3.Lerp(from, to, i / (float)count) + Vector3.up,
                    new Vector3(.12f, 2, .12f), material, false);
            }
            foreach (float height in new[] { .35f, 1.05f, 1.8f })
                Box(edge, "Fence Rail", (from + to) * .5f + Vector3.up * height,
                    alongX ? new Vector3(delta.magnitude, .07f, .07f) : new Vector3(.07f, .07f, delta.magnitude), material, false);
        }

        private static void BuildWarehouse(Transform parent, Material concrete, Material rust, Material steel,
            Material paint, Material wood)
        {
            Transform warehouse = Group(parent.gameObject.scene, "Warehouse - Open Roof", parent);
            Box(warehouse, "Rear Wall", new Vector3(0, 1.4f, 10), new Vector3(10.3f, 2.8f, .3f), rust);
            Box(warehouse, "West Wall", new Vector3(-5, 1.4f, 6), new Vector3(.3f, 2.8f, 8), rust);
            Box(warehouse, "East Wall", new Vector3(5, 1.4f, 6), new Vector3(.3f, 2.8f, 8), rust);
            // Low camera-facing walls expose the cargo; the central opening is 4.8 metres wide.
            Box(warehouse, "Entry Wall West", new Vector3(-3.7f, .6f, 2), new Vector3(2.6f, 1.2f, .3f), concrete);
            Box(warehouse, "Entry Wall East", new Vector3(3.7f, .6f, 2), new Vector3(2.6f, 1.2f, .3f), concrete);
            foreach (float x in new[] { -5f, 5f })
                foreach (float z in new[] { 2f, 6f, 10f })
                    Box(warehouse, "Steel Column", new Vector3(x, 1.5f, z), new Vector3(.24f, 3, .24f), steel);
            Box(warehouse, "Rear Roof Edge", new Vector3(0, 2.95f, 10), new Vector3(10.3f, .18f, .35f), steel, false);
            Box(warehouse, "Entry Sill Paint", new Vector3(0, .012f, 2), new Vector3(4.8f, .02f, .15f), paint, false);
            Crate(warehouse, new Vector3(-3.5f, 0, 5.6f), wood, steel);
            Crate(warehouse, new Vector3(3.5f, 0, 8), wood, steel);
        }

        private static void BuildCover(Transform parent, Material concrete, Material steel, Material wood)
        {
            Transform cover = Group(parent.gameObject.scene, "Yard Crates and Cover", parent);
            Box(cover, "West Concrete Cover", new Vector3(-9, .55f, -1.5f), new Vector3(3, 1.1f, .65f), concrete);
            Box(cover, "East Concrete Cover", new Vector3(9.2f, .55f, 4), new Vector3(2.8f, 1.1f, .65f), concrete);
            Crate(cover, new Vector3(-11, 0, 5.5f), wood, steel);
            Crate(cover, new Vector3(-11, 0, 7.4f), wood, steel);
            Crate(cover, new Vector3(-5.5f, 0, -7), wood, steel);
            Crate(cover, new Vector3(-3.5f, 0, -7), wood, steel);
        }

        private static void Crate(Transform parent, Vector3 basePosition, Material wood, Material steel)
        {
            Transform crate = Group(parent.gameObject.scene, "Cover Crate", parent);
            Box(crate, "Crate Body", basePosition + Vector3.up * .65f, new Vector3(1.5f, 1.3f, 1.3f), wood);
            foreach (float x in new[] { -.5f, .5f })
                Box(crate, "Crate Strap", basePosition + new Vector3(x, 1.31f, 0), new Vector3(.07f, .03f, 1.32f), steel, false);
        }

        private static void BuildMarkings(Transform parent, Material paint)
        {
            Transform markings = Group(parent.gameObject.scene, "Loading Lane Paint - No Collision", parent);
            foreach (float x in new[] { -2.2f, 2.2f })
                for (int z = -5; z <= 8; z += 2)
                    Box(markings, "Lane Dash", new Vector3(x, .012f, z), new Vector3(.09f, .02f, .85f), paint, false);
            foreach (float z in new[] { -5.4f, -.6f })
                Box(markings, "Extraction Bay Edge", new Vector3(11, .012f, z), new Vector3(4.3f, .02f, .1f), paint, false);
        }

        private static void BuildHelicopter(Transform parent, Material steel, Material rust, Material concrete)
        {
            Transform heli = Group(parent.gameObject.scene, "Helicopter Placeholder - Visual Only", parent);
            // Hover behind the marker, outside its ground-plane camera sightline.
            Vector3 origin = new Vector3(10.5f, 6, 6);
            Primitive(heli, "Fuselage", PrimitiveType.Sphere, origin, new Vector3(2.3f, 1.45f, 3.3f), steel, false);
            Primitive(heli, "Cockpit", PrimitiveType.Sphere, origin + new Vector3(0, .1f, -1.15f), new Vector3(1.9f, 1.1f, 1.15f), concrete, false);
            Box(heli, "Tail Boom", origin + new Vector3(0, .1f, 2.5f), new Vector3(.35f, .4f, 3.6f), steel, false);
            Box(heli, "Tail Fin", origin + new Vector3(0, .65f, 4.1f), new Vector3(.15f, 1.3f, .8f), rust, false);
            Box(heli, "Rotor Mast", origin + Vector3.up * 1.1f, new Vector3(.14f, .8f, .14f), steel, false);
            Box(heli, "Main Rotor X", origin + Vector3.up * 1.5f, new Vector3(6, .07f, .2f), steel, false);
            Box(heli, "Main Rotor Z", origin + Vector3.up * 1.5f, new Vector3(.2f, .07f, 6), steel, false);
            foreach (float x in new[] { -1f, 1f })
            {
                Box(heli, "Landing Skid", origin + new Vector3(x, -1, 0), new Vector3(.12f, .12f, 3.3f), steel, false);
                Box(heli, "Skid Strut", origin + new Vector3(x, -.7f, 0), new Vector3(.1f, .65f, .1f), steel, false);
                Primitive(heli, "Hanging Rope", PrimitiveType.Cylinder, new Vector3(origin.x + x, 2.9f, origin.z),
                    new Vector3(.045f, 2.35f, .045f), rust, false);
            }
        }

        private static Material Material(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterials + name + ".mat");
            if (!material) throw new InvalidOperationException("Missing existing material " + name);
            return material;
        }

        private static GameObject Root(Scene scene, string name) => scene.GetRootGameObjects().Single(root => root.name == name);

        private static Transform Group(Scene scene, string name, Transform parent = null)
        {
            var group = new GameObject(name);
            SceneManager.MoveGameObjectToScene(group, scene);
            if (parent) group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size,
            Material material, bool solid = true) => Primitive(parent, name, PrimitiveType.Cube, position, size, material, solid);

        private static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position,
            Vector3 size, Material material, bool solid)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            SceneManager.MoveGameObjectToScene(item, parent.gameObject.scene);
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.transform.localScale = size;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(item.GetComponent<Collider>());
            return item;
        }

        private static void Validate(Scene scene)
        {
            T[] All<T>() where T : Component => scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
            if (All<PlayerController>().Length != 1 || All<CargoItem>().Length != 1 ||
                All<CargoExtraction>().Length != 1 || All<ExtractionMarker>().Length != 1 ||
                All<CharacterSelection>().Length != 1 || All<CargoMissionOverlay>().Length != 1 ||
                All<MissionResultPanel>().Length != 1 || All<EnemyNavigation>().Length != 1)
                throw new InvalidOperationException("Expected one of each existing gameplay system in the sample.");
            Transform heli = Root(scene, "Industrial Expansion Sample").transform.Find("Helicopter Placeholder - Visual Only");
            if (heli.GetComponentsInChildren<Collider>(true).Length != 0 || heli.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("The helicopter must remain visual only.");
            foreach (Component component in All<Component>())
            {
                if (!component) throw new InvalidOperationException("Missing script in sample scene.");
                using var data = new SerializedObject(component);
                var property = data.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        !property.objectReferenceValue && property.objectReferenceInstanceIDValue != 0)
                        throw new InvalidOperationException("Missing reference: " + component.name + "." + property.propertyPath);
            }
        }
    }
}
