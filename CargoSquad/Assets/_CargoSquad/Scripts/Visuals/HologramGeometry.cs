using UnityEngine;
using UnityEngine.Rendering;

namespace CargoSquad.Visuals
{
    /// <summary>Small shared geometry helpers. Materials belong to the caller, never imported model assets.</summary>
    public static class HologramGeometry
    {
        public static Material Material(Color color, bool transparent = false)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)BlendMode.One);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            }
            return material;
        }
        public static Transform Shape(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            var collider = obj.GetComponent<Collider>(); collider.enabled = false; Object.Destroy(collider);
            var renderer = obj.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; return obj.transform;
        }
        public static LineRenderer Ring(string name, Transform parent, float radius, float y, Material material, float width = .025f)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            var line = obj.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true;
            line.sharedMaterial = material; line.widthMultiplier = width; line.positionCount = 80;
            for (int i = 0; i < 80; i++) { float a = i * Mathf.PI * 2 / 80; line.SetPosition(i, new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius)); }
            line.shadowCastingMode = ShadowCastingMode.Off; return line;
        }
    }
}
