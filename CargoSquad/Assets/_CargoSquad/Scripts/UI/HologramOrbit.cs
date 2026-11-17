using UnityEngine;
using UnityEngine.UI;
namespace CargoSquad.UI
{
    /// <summary>Code-drawn orbit around each hologram. Remains sharp at any Canvas scale.</summary>
    public sealed class HologramOrbit : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); const int segments = 96;
            var size = rectTransform.rect.size * .5f;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                mesh.AddVert(new Vector3(p.x * size.x, p.y * size.y), color, Vector2.zero);
                mesh.AddVert(new Vector3(p.x * (size.x - 1.25f), p.y * (size.y - 1.25f)), color, Vector2.zero);
                if (i == 0) continue;
                int n = i * 2; mesh.AddTriangle(n - 2, n, n - 1); mesh.AddTriangle(n, n + 1, n - 1);
            }
        }
    }
}
