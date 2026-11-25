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
        [Header("Extraction Feedback")]
        [SerializeField] private Material particleMaterial;
        [SerializeField, Min(0)] private int particlesPerSecond = 5;
        [SerializeField, Min(0)] private int successParticleCount = 14;
        [SerializeField, Range(0f, 1f)] private float particleOpacity = .45f;
        [SerializeField, Range(0f, 2f)] private float successPulseIntensity = 1f;
        [SerializeField, Range(.5f, .8f)] private float successPulseDuration = .68f;
        private ParticleSystem particles;
        private bool successPlayed;
        private float pulseStart;
        private float ambientAccumulator;
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
            if (particleMaterial) CreateParticles();
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
            if (isActiveAndEnabled && mission) mission.ResultFinalized -= OnResult;
            mission = existingMission; survivor = player;
            if (isActiveAndEnabled && mission) mission.ResultFinalized += OnResult;
            zone = mission ? mission.GetComponent<BoxCollider>() : null;
            health = survivor ? survivor.GetComponent<Health>() : null;
            playerCollider = survivor ? survivor.GetComponent<CapsuleCollider>() : null;
        }
        private void CreateParticles()
        {
            var host = new GameObject("Extraction Particles");
            host.SetActive(false);
            host.transform.SetParent(transform, false);
            particles = host.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.35f, 0f), new GradientAlphaKey(1f, .2f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var particleRenderer = host.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = particleMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var colorOverride = new MaterialPropertyBlock();
            colorOverride.SetColor("_BaseColor", Color.white);
            particleRenderer.SetPropertyBlock(colorOverride);
            host.SetActive(true);
        }

        private void EmitParticle(bool success)
        {
            Vector2 radial = Random.insideUnitCircle * (success ? .95f : .82f);
            Color color = Color.Lerp(markerColor, Color.white, .3f);
            color.a = particleOpacity * (success ? 1f : .7f);
            particles.Emit(new ParticleSystem.EmitParams
            {
                position = transform.TransformPoint(new Vector3(radial.x * markerRadius * footprintAspect.x,
                    .15f, radial.y * markerRadius * footprintAspect.y)),
                velocity = Vector3.up * Random.Range(.65f, 1.2f) + new Vector3(radial.x, 0f, radial.y) * .35f,
                startSize = Random.Range(.10f, success ? .24f : .19f),
                startLifetime = success ? successPulseDuration : Random.Range(.9f, 1.45f),
                startColor = color
            }, 1);
        }

        private void OnResult(MissionResult result)
        {
            if (!particles || successPlayed || result.Outcome != MissionOutcome.CargoRecovered) return;
            successPlayed = true;
            pulseStart = Time.unscaledTime;
            particles.Play(false);
            for (int i = 0; i < successParticleCount; i++) EmitParticle(true);
        }

        private void LateUpdate()
        {
            float wave = Mathf.Sin(Time.time * pulseSpeed);
            float progress = successPlayed ? Mathf.Clamp01((Time.unscaledTime - pulseStart) / successPulseDuration) : 1f;
            float successBoost = successPulseIntensity * (1f - progress) * (1f - progress);
            if (particles && Time.timeScale > 0f && (!mission || !mission.IsComplete))
            {
                ambientAccumulator += Time.deltaTime * particlesPerSecond;
                if (ambientAccumulator >= 1f)
                {
                    ambientAccumulator %= 1f;
                    particles.Play(false);
                    EmitParticle(false);
                }
            }
            PulseBrightness = emissionIntensity * (.88f + .12f * wave);
            Color color = markerColor * PulseBrightness; color.a = transparency;
            tint.SetColor("_BaseColor", color); cylinder.SetPropertyBlock(tint);
            Color edge = markerColor * PulseBrightness; edge.a = .85f;
            groundRing.startColor = groundRing.endColor = Color.Lerp(edge, Color.white, Mathf.Clamp01(successBoost * .62f));
            topRing.startColor = topRing.endColor = new Color(edge.r, edge.g, edge.b, .45f);
            scanRing.startColor = scanRing.endColor = new Color(edge.r, edge.g, edge.b, .18f);
            groundRing.transform.localPosition = new Vector3(0, .04f, 0);
            groundRing.transform.localScale = Vector3.one * ringSize * (.985f + .015f * wave);
            topRing.transform.localPosition = Vector3.up * markerHeight;
            scanRing.transform.localPosition = Vector3.up * (.08f + Mathf.Repeat(Time.time * .3f, 1) * (markerHeight - .16f));
            if (progress < 1f)
            {
                scanRing.transform.localPosition = Vector3.up * .09f;
                scanRing.transform.localScale = Vector3.one * Mathf.Lerp(.12f, ringSize, Mathf.SmoothStep(0f, 1f, progress));
                scanRing.widthMultiplier = .11f;
                scanRing.startColor = scanRing.endColor = new Color(edge.r, edge.g, edge.b, Mathf.Clamp01(successBoost * .85f));
            }
            else if (successPlayed)
            {
                scanRing.transform.localScale = Vector3.one;
                scanRing.widthMultiplier = .018f;
            }
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
        private void OnEnable()
        {
            if (!mission) return;
            mission.ResultFinalized -= OnResult;
            mission.ResultFinalized += OnResult;
        }
        private void OnDisable()
        {
            if (mission) mission.ResultFinalized -= OnResult;
            if (particles) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            MissingCargoPrompt = false;
        }
        private void OnDestroy() { if (mesh) Destroy(mesh); }
    }
}
