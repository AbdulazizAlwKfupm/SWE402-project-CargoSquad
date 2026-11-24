using UnityEngine;

namespace CargoSquad.AI
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SmashGroundShape : MonoBehaviour
    {
        Mesh mesh;
        MaterialPropertyBlock properties;

        public void Build(float halfAngle, float innerRadius)
        {
            const int segments = 48;
            var vertices = new Vector3[(segments + 1) * 2];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, i / (float)segments) * Mathf.Deg2Rad;
                Vector3 radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                int v = i * 2;
                vertices[v] = radial * innerRadius;
                vertices[v + 1] = radial;
                colors[v] = new Color(1, 1, 1, innerRadius == 0f ? .3f : 0f);
                colors[v + 1] = Color.white;
                if (i == segments) break;
                int t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 3;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            mesh = new Mesh { name = "Smash Ground Shape" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            properties = new MaterialPropertyBlock();
        }

        public void Tint(Color tint)
        {
            properties.SetColor("_BaseColor", tint);
            GetComponent<MeshRenderer>().SetPropertyBlock(properties);
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
        }
    }
}
