using UnityEngine;

namespace CargoSquad.Cargo
{
    [DisallowMultipleComponent, RequireComponent(typeof(CargoInteractor))]
    public sealed class CargoPickupDust : MonoBehaviour
    {
        [SerializeField] private Material dustMaterial;
        [SerializeField, Range(1, 24)] private int particleCount = 10;
        [SerializeField, Min(.05f)] private float particleSize = .82f;
        [SerializeField, Min(.05f)] private float lifetime = .36f;

        [Header("Drop Dust")]
        [SerializeField, Range(1, 24)] private int dropParticleCount = 12;
        [SerializeField, Min(.05f)] private float dropParticleSize = .90f;
        [SerializeField, Range(0f, 1f)] private float dropOpacity = .90f;
        [SerializeField, Range(.41f, .435f)] private float dropLifetime = .43f;

        private CargoInteractor interactor;
        private ParticleSystem dust;

        private void Awake()
        {
            interactor = GetComponent<CargoInteractor>();
            if (!dustMaterial) return;

            var emitter = new GameObject("Cargo Pickup Dust Particles");
            emitter.SetActive(false);
            emitter.transform.SetParent(transform, false);
            dust = emitter.AddComponent<ParticleSystem>();

            var main = dust.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;
            var emission = dust.emission;
            emission.enabled = false;
            var shape = dust.shape;
            shape.enabled = false;
            var fade = dust.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.55f, 0f), new GradientAlphaKey(1f, .18f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var growth = dust.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .65f, 1f, 1.2f));
            var renderer = emitter.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = dustMaterial;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", new Color(.72f, .66f, .58f, .78f));
            renderer.SetPropertyBlock(tint);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            emitter.SetActive(true);
        }

        private void OnEnable()
        {
            interactor.CargoPickedUp += EmitPickup;
            interactor.CargoDropped += EmitDrop;
        }
        private void OnDisable()
        {
            interactor.CargoPickedUp -= EmitPickup;
            interactor.CargoDropped -= EmitDrop;
            if (dust) dust.Clear();
        }

        private void EmitPickup(Bounds bounds) =>
            Emit(bounds, particleCount, particleSize, lifetime, 1f, .34f, .90f, 1f, .32f);

        private void EmitDrop(CargoItem cargo) =>
            Emit(cargo.Collider.bounds, dropParticleCount, dropParticleSize, dropLifetime,
                dropOpacity, .23f, 1.25f, 1.4f, .19f);

        private void Emit(Bounds bounds, int count, float size, float duration, float opacity,
            float height, float footprint, float speed, float lift)
        {
            if (!dust || opacity <= 0f) return;
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + height, bounds.center.z);
            dust.transform.position = origin;
            dust.Play(false);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + .5f) * Mathf.PI * 2f / count;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var edge = Vector3.Scale(direction, bounds.extents) * footprint;
                dust.Emit(new ParticleSystem.EmitParams
                {
                    position = origin + edge,
                    velocity = direction * (Random.Range(.75f, 1.35f) * speed) + Vector3.up * lift,
                    startLifetime = duration * Random.Range(.85f, 1.15f),
                    startSize = size * Random.Range(.8f, 1.2f),
                    startColor = new Color(1f, 1f, 1f, opacity),
                    rotation = Random.Range(0f, 360f)
                }, 1);
            }
        }
    }
}
