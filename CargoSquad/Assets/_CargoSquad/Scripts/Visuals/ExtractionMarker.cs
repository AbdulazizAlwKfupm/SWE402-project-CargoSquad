using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Missions;
using CargoSquad.UI;
using TMPro;
using UnityEngine;

namespace CargoSquad.Visuals
{
    /// <summary>Decorative view of an existing extraction zone. Never completes or changes a mission.</summary>
    [DisallowMultipleComponent]
    public sealed class ExtractionMarker : MonoBehaviour
    {
        [Header("Existing gameplay references (optional on reusable prefab)")]
        [SerializeField] private CargoExtraction mission;
        [SerializeField] private CargoInteractor survivor;
        [Header("Beacon")]
        [SerializeField, Min(.2f)] private float markerHeight = 2.8f;
        [SerializeField, Min(.1f)] private float markerRadius = 1;
        [SerializeField] private Vector2 footprintAspect = Vector2.one;
        [SerializeField] private Color markerColor = new Color(.18f, .88f, .86f);
        [SerializeField, Range(0, .4f)] private float transparency = .15f;
        [SerializeField, Range(.1f, 3)] private float emissionIntensity = 1.25f;
        [SerializeField, Min(0)] private float pulseSpeed = 1.4f;
        [SerializeField, Range(.9f, 1.05f)] private float ringSize = 1;
        [SerializeField] private bool labelVisibility = true;
        [SerializeField] private Material hologramMaterial;
        [SerializeField, Range(24, 96)] private int segments = 64;
        private Mesh mesh;
        private MeshRenderer cylinder;
        private LineRenderer groundRing, topRing, scanRing, leftEdge, rightEdge;
        private TMP_Text label;
        private Camera view;
        private BoxCollider zone;
        private CapsuleCollider playerCollider;
        private Health health;
        private MaterialPropertyBlock tint;
        private Vector3[] circle;
        public float PulseBrightness { get; private set; }
        public bool MissingCargoPrompt { get; private set; }
        public float MarkerHeight => markerHeight;
        public Vector2 Footprint => footprintAspect * (markerRadius * 2);

        private void Awake()
        {
            view = Camera.main; tint = new MaterialPropertyBlock();
            cylinder = transform.Find("HologramCylinder").GetComponent<MeshRenderer>();
            mesh = new Mesh { name = "Extraction hologram cylinder" };
            segments = Mathf.Clamp(segments, 24, 96);
            var vertices = new Vector3[(segments + 1) * 3];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 12];
            circle = new Vector3[segments];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2;
                Vector3 radial = new Vector3(Mathf.Cos(angle) * markerRadius * footprintAspect.x, 0,
                    Mathf.Sin(angle) * markerRadius * footprintAspect.y);
                if (i < segments) circle[i] = radial;
                for (int row = 0; row < 3; row++)
                {
                    int index = i * 3 + row;
                    vertices[index] = radial + Vector3.up * (row * markerHeight * .5f);
                    // Brightest at the base, transparent near eye level, a faint upper edge.
                    colors[index] = new Color(1, 1, 1, row == 0 ? 1 : row == 1 ? .4f : .35f);
                }
                if (i == segments) continue;
                for (int row = 0; row < 2; row++)
                {
                    int t = i * 12 + row * 6, a = i * 3 + row, b = a + 3;
                    triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = a + 1;
                    triangles[t + 3] = a + 1; triangles[t + 4] = b; triangles[t + 5] = b + 1;
                }
            }
            mesh.vertices = vertices; mesh.colors = colors; mesh.triangles = triangles; mesh.RecalculateBounds();
            transform.Find("HologramCylinder").GetComponent<MeshFilter>().sharedMesh = mesh;
            cylinder.sharedMaterial = hologramMaterial;
            cylinder.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; cylinder.receiveShadows = false;
            groundRing = ConfigureRing("GroundRing", .045f);
            topRing = ConfigureRing("GlowEffects/TopRim", .025f);
            scanRing = ConfigureRing("GlowEffects/ScanRing", .018f);
            leftEdge = ConfigureEdge("EdgeLeft"); rightEdge = ConfigureEdge("EdgeRight");
            label = transform.Find("ExtractLabel").GetComponentInChildren<TMP_Text>();
            Bind(mission, survivor);
        }
        private LineRenderer ConfigureRing(string path, float width)
        {
            var line = transform.Find(path).GetComponent<LineRenderer>();
            line.sharedMaterial = hologramMaterial; line.useWorldSpace = false; line.loop = true;
            line.positionCount = segments; line.SetPositions(circle); line.widthMultiplier = width;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        private LineRenderer ConfigureEdge(string name)
        {
            var parent = transform.Find("GlowEffects");
            var host = parent.Find(name);
            if (!host) { host = new GameObject(name).transform; host.SetParent(parent, false); }
            var line = host.GetComponent<LineRenderer>(); if (!line) line = host.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = hologramMaterial; line.useWorldSpace = false; line.positionCount = 2;
            line.widthMultiplier = .018f; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false; return line;
        }
        public void Bind(CargoExtraction existingMission, CargoInteractor player)
        {
            mission = existingMission; survivor = player;
            zone = mission ? mission.GetComponent<BoxCollider>() : null;
            health = survivor ? survivor.GetComponent<Health>() : null;
            playerCollider = survivor ? survivor.GetComponent<CapsuleCollider>() : null;
        }
        private void LateUpdate()
        {
            float wave = Mathf.Sin(Time.time * pulseSpeed);
            PulseBrightness = emissionIntensity * (.88f + .12f * wave);
            Color color = markerColor * PulseBrightness; color.a = transparency;
            tint.SetColor("_BaseColor", color); cylinder.SetPropertyBlock(tint);
            Color edge = markerColor * PulseBrightness; edge.a = .85f;
            groundRing.startColor = groundRing.endColor = edge;
            topRing.startColor = topRing.endColor = new Color(edge.r, edge.g, edge.b, .45f);
            scanRing.startColor = scanRing.endColor = new Color(edge.r, edge.g, edge.b, .18f);
            groundRing.transform.localPosition = new Vector3(0, .04f, 0);
            groundRing.transform.localScale = Vector3.one * ringSize * (.985f + .015f * wave);
            topRing.transform.localPosition = Vector3.up * markerHeight;
            scanRing.transform.localPosition = Vector3.up * (.08f + Mathf.Repeat(Time.time * .3f, 1) * (markerHeight - .16f));
            MissingCargoPrompt = zone && zone.enabled && survivor && playerCollider && health && !health.IsDead &&
                !mission.IsComplete && !mission.CargoDestroyed && !mission.IsPrimaryCargo(survivor.CarriedCargo) &&
                Physics.ComputePenetration(zone, zone.transform.position, zone.transform.rotation,
                    playerCollider, playerCollider.transform.position, playerCollider.transform.rotation, out _, out _);
            label.gameObject.SetActive(labelVisibility);
            if (!view) view = Camera.main;
            if (view)
            {
                Vector3 up = view.transform.up, right = view.transform.right;
                float screenExtent = markerRadius * new Vector2(footprintAspect.x * up.x, footprintAspect.y * up.z).magnitude;
                label.transform.parent.position = transform.position + Vector3.up * (markerHeight + (screenExtent + .3f) / Mathf.Max(.25f, up.y));
                label.transform.parent.rotation = view.transform.rotation;
                float divisor = Mathf.Max(.001f, new Vector2(footprintAspect.x * right.x, footprintAspect.y * right.z).magnitude);
                Vector3 silhouette = markerRadius * new Vector3(footprintAspect.x * footprintAspect.x * right.x, 0,
                    footprintAspect.y * footprintAspect.y * right.z) / divisor;
                leftEdge.SetPosition(0, -silhouette + Vector3.up * .04f); leftEdge.SetPosition(1, -silhouette + Vector3.up * markerHeight);
                rightEdge.SetPosition(0, silhouette + Vector3.up * .04f); rightEdge.SetPosition(1, silhouette + Vector3.up * markerHeight);
                leftEdge.startColor = leftEdge.endColor = rightEdge.startColor = rightEdge.endColor = new Color(edge.r, edge.g, edge.b, .38f);
            }
            label.text = "EXTRACT";
        }
        private void OnDisable() => MissingCargoPrompt = false;
        private void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
