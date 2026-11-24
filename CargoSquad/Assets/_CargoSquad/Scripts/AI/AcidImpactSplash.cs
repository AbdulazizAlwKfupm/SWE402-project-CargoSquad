using UnityEngine;

namespace CargoSquad.AI
{
    public sealed class AcidImpactSplash : MonoBehaviour
    {
        [SerializeField] ParticleSystem droplets, surfaceSplash;
        [SerializeField, Range(6, 12)] int dropletCount = 9;
        [SerializeField, Min(.01f)] float dropletSize = .045f;
        [SerializeField, Min(.01f)] float splashSize = .33f;
        [SerializeField, Range(.3f, .5f)] float dropletDuration = .43f;
        [SerializeField, Range(.2f, .35f)] float splashDuration = .27f;
        [SerializeField] Color liquidTint = new Color(.73f, 1f, .48f);

        [Header("Survivor Impact")]
        [SerializeField, Range(8, 12)] int survivorDropletCount = 10;
        [SerializeField, Min(.01f)] float survivorDropletSize = .065f;
        [SerializeField, Min(0f)] float survivorDropletSpread = 1f;
        [SerializeField, Range(.3f, .5f)] float survivorDropletDuration = .46f;
        [SerializeField, Min(.01f)] float survivorSplashSize = .54f;
        [SerializeField, Range(.5f, 1f)] float survivorSplashDuration = .65f;
        [SerializeField, Min(0f)] float survivorSurfaceOffset = .09f;
        [SerializeField] Color survivorTint = new Color(.9f, 1f, .66f);

        float expiresAt;

        public void Burst(Transform survivorSurface = null)
        {
            bool survivorHit = survivorSurface != null;
            transform.position += transform.up * (survivorHit ? survivorSurfaceOffset : .015f);
            // The surface splash uses local simulation. Airborne droplets use world simulation.
            if (survivorHit) transform.SetParent(survivorSurface, true);
            float surfaceLife = survivorHit ? survivorSplashDuration : splashDuration;
            float airborneLife = survivorHit ? survivorDropletDuration : dropletDuration;
            Color tint = survivorHit ? survivorTint : liquidTint;
            expiresAt = Time.unscaledTime + Mathf.Max(airborneLife, surfaceLife) + .05f;
            surfaceSplash.Play(false);
            surfaceSplash.Emit(new ParticleSystem.EmitParams
            {
                position = Vector3.zero, velocity = Vector3.zero,
                startLifetime = surfaceLife, startSize = survivorHit ? survivorSplashSize : splashSize,
                startColor = tint
            }, 1);

            droplets.Play(false);
            int count = survivorHit ? survivorDropletCount : dropletCount;
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.value) * (2f * Mathf.PI / count);
                Vector3 tangent = transform.right * Mathf.Cos(angle) + transform.forward * Mathf.Sin(angle);
                float spread = survivorHit ? survivorDropletSpread : 1f;
                Vector3 velocity = tangent * (Random.Range(.55f, 1.5f) * spread) +
                    transform.up * Random.Range(.55f, 1.2f);
                if (survivorHit && i % 3 == 0) velocity += Vector3.down * 1.2f;
                droplets.Emit(new ParticleSystem.EmitParams
                {
                    position = transform.position,
                    velocity = velocity,
                    startLifetime = airborneLife * Random.Range(.78f, 1f),
                    startSize = (survivorHit ? survivorDropletSize : dropletSize) * Random.Range(.7f, 1.25f),
                    startColor = tint
                }, 1);
            }
        }

        void Update()
        {
            if (expiresAt > 0f && Time.unscaledTime >= expiresAt) Destroy(gameObject);
        }
    }
}
