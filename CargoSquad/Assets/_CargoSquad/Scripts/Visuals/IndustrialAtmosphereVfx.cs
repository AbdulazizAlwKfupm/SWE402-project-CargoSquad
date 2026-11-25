using System;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace CargoSquad.Visuals
{
    [DisallowMultipleComponent]
    public sealed class IndustrialAtmosphereVfx : MonoBehaviour
    {
        private enum Effect { Steam, Dust, Sparks }

        [Serializable]
        private sealed class Emitter
        {
            public string label;
            public bool enabled = true;
            public Effect effect;
            public Vector3 localPosition;
            public Vector3 localDrift = Vector3.up;
            [Range(1, 12)] public int count = 4;
            public Vector2 interval = new Vector2(3f, 5f);
            [Min(.05f)] public float lifetime = 1f;
            [Range(0, 1)] public float opacity = .3f;
            [NonSerialized] public ParticleSystem particles;
            [NonSerialized] public float nextBurst;
        }

        [Serializable]
        private sealed class Flicker
        {
            public Light light;
            public bool enabled = true;
            [Range(0f, .3f)] public float intensityDip = .16f;
            public Vector2 interval = new Vector2(5f, 10f);
            [Min(.15f)] public float duration = .55f;
            [NonSerialized] public float normalIntensity;
            [NonSerialized] public float nextDip;
        }

        [SerializeField] private Material softParticleMaterial;
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private Emitter[] emitters = Array.Empty<Emitter>();
        [SerializeField] private Flicker[] lights = Array.Empty<Flicker>();

        private void OnEnable()
        {
            float now = Time.time;
            foreach (var item in emitters)
            {
                var material = item.effect == Effect.Sparks ? sparkMaterial : softParticleMaterial;
                if (!item.particles && material) item.particles = CreateEmitter(item, material);
                item.nextBurst = now + Delay(item.interval) * .25f;
            }
            foreach (var item in lights)
            {
                if (!item.light) continue;
                item.normalIntensity = item.light.intensity;
                item.nextDip = now + Delay(item.interval);
            }
        }

        private void OnDisable()
        {
            foreach (var item in emitters)
                if (item.particles) item.particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var item in lights)
                if (item.light) item.light.intensity = item.normalIntensity;
        }

        private void Update()
        {
            float now = Time.time;
            foreach (var item in emitters)
            {
                if (!item.enabled || !item.particles || now < item.nextBurst) continue;
                Burst(item);
                item.nextBurst = now + Delay(item.interval);
            }

            foreach (var item in lights)
            {
                if (!item.light) continue;
                float elapsed = now - item.nextDip;
                if (!item.enabled || elapsed < 0f)
                {
                    item.light.intensity = item.normalIntensity;
                    continue;
                }

                float progress = Mathf.Clamp01(elapsed / item.duration);
                float dip = Mathf.Sin(Mathf.PI * progress);
                item.light.intensity = item.normalIntensity * (1f - item.intensityDip * dip * dip);
                if (progress >= 1f) item.nextDip = now + Delay(item.interval);
            }
        }

        private static float Delay(Vector2 interval) =>
            Random.Range(Mathf.Max(.2f, interval.x), Mathf.Max(.2f, interval.x, interval.y));

        private ParticleSystem CreateEmitter(Emitter item, Material material)
        {
            var host = new GameObject(item.label);
            host.SetActive(false);
            host.transform.SetParent(transform, false);
            host.transform.localPosition = item.localPosition;
            var particles = host.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;
            main.gravityModifier = item.effect == Effect.Sparks ? .55f : 0f;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;

            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(item.effect == Effect.Sparks ? 1f : 0f, 0f),
                    new GradientAlphaKey(1f, .2f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            if (item.effect == Effect.Steam)
            {
                var growth = particles.sizeOverLifetime;
                growth.enabled = true;
                growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .45f, 1f, 1.2f));
            }

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = item.effect == Effect.Sparks ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = .18f;
            renderer.lengthScale = 1.1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", Color.white);
            renderer.SetPropertyBlock(properties);
            host.SetActive(true);
            return particles;
        }

        private void Burst(Emitter item)
        {
            bool steam = item.effect == Effect.Steam;
            bool sparks = item.effect == Effect.Sparks;
            var particles = item.particles;
            particles.Play(false);
            for (int i = 0; i < item.count; i++)
            {
                Color color = sparks ? Color.Lerp(new Color(1f, .38f, .08f), new Color(1f, .89f, .42f), Random.value)
                    : steam ? new Color(.88f, .9f, .9f) : new Color(.78f, .69f, .57f);
                color.a = item.opacity;
                particles.Emit(new ParticleSystem.EmitParams
                {
                    position = particles.transform.position + Random.insideUnitSphere * (sparks ? .035f : steam ? .14f : 1.25f),
                    velocity = transform.TransformDirection(item.localDrift) * Random.Range(.7f, 1.3f) +
                        Random.insideUnitSphere * (sparks ? 1.5f : steam ? .2f : .04f),
                    startLifetime = item.lifetime * Random.Range(.82f, 1.12f),
                    startSize = Random.Range(.75f, 1.25f) * (sparks ? .085f : steam ? .72f : .12f),
                    startColor = color,
                    rotation = Random.Range(0f, 360f)
                }, 1);
            }
        }
    }
}
