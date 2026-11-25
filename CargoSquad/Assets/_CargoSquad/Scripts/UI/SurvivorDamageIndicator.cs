using UnityEngine;
using UnityEngine.UI;

namespace CargoSquad.UI
{
    public sealed class SurvivorDamageIndicator : MaskableGraphic
    {
        public Vector2 Direction { get; private set; }

        public void Present(Vector2 direction, float opacity)
        {
            Direction = direction;
            color = new Color(1f, .23f, .19f, opacity);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            const int segments = 24;
            bool directional = Direction.sqrMagnitude > .01f;
            float angle = directional ? Mathf.Atan2(Direction.y, Direction.x) : 0f;
            float halfArc = directional ? .48f : Mathf.PI;
            for (int i = 0; i <= segments; i++)
            {
                float angleAt = angle + Mathf.Lerp(-halfArc, halfArc, i / (float)segments);
                Vector2 radial = new Vector2(Mathf.Cos(angleAt), Mathf.Sin(angleAt));
                float thickness = directional ? Mathf.Lerp(3f, 9f, 1f - Mathf.Abs(2f * i / segments - 1f)) : 3f;
                mesh.AddVert(radial * 57f, color, Vector2.zero);
                mesh.AddVert(radial * (57f - thickness), color, Vector2.zero);
                if (i == 0) continue;
                int v = i * 2;
                mesh.AddTriangle(v - 2, v, v - 1);
                mesh.AddTriangle(v, v + 1, v - 1);
            }
        }
    }
}
