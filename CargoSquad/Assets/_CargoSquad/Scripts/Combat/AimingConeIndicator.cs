using UnityEngine;

namespace CargoSquad.Combat
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class AimingConeIndicator : MonoBehaviour
    {
        [SerializeField, Range(8, 96)] private int segments = 40;
        [SerializeField] private Material material;
        [SerializeField] private Color color = new Color(.18f, .85f, .86f, .19f);
        [SerializeField, Min(.001f)] private float outlineWidth = .025f;
        private Mesh mesh;
        private LineRenderer outline;
        private Vector3[] vertices;
        private Color[] colors;
        private Vector3[] edge;
        private float lastRange = -1, lastAngle;
        public bool Visible => gameObject.activeSelf;

        private void Awake()
        {
            segments = Mathf.Clamp(segments, 8, 96);
            mesh = new Mesh { name = "Cargo Squad aiming sector" }; mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            vertices = new Vector3[segments + 2]; colors = new Color[vertices.Length];
            var triangles = new int[segments * 3];
            for (int i = 0; i < segments; i++) { triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2; }
            mesh.vertices = vertices; mesh.triangles = triangles;
            outline = gameObject.AddComponent<LineRenderer>(); outline.sharedMaterial = material;
            outline.useWorldSpace = false; outline.loop = true; outline.widthMultiplier = outlineWidth;
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.startColor = outline.endColor = new Color(color.r, color.g, color.b, .72f);
            edge = new Vector3[segments + 2]; outline.positionCount = edge.Length;
        }

        public void Show(Vector3 origin, Vector3 direction, float range, float angle)
        {
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, Vector3.up));
            if (Mathf.Approximately(range, lastRange) && Mathf.Approximately(angle, lastAngle)) return;
            lastRange = range; lastAngle = angle;
            colors[0] = new Color(color.r, color.g, color.b, color.a * .3f);
            for (int i = 0; i <= segments; i++)
            {
                float yaw = Mathf.Lerp(-angle * .5f, angle * .5f, i / (float)segments);
                vertices[i + 1] = Quaternion.Euler(0, yaw, 0) * Vector3.forward * range;
                colors[i + 1] = color; edge[i + 1] = vertices[i + 1];
            }
            mesh.vertices = vertices; mesh.colors = colors; mesh.RecalculateBounds();
            outline.SetPositions(edge);
        }
        public void Hide() => gameObject.SetActive(false);
        private void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
