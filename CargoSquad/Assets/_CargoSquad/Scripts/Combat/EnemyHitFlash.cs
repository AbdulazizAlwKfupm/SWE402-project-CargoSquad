using System;
using System.Collections.Generic;
using UnityEngine;

namespace CargoSquad.Combat
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class EnemyHitFlash : MonoBehaviour
    {
        [Serializable]
        public struct Settings
        {
            public Color color;
            [Range(0f, 1.5f)] public float intensity;
            [Range(.02f, .25f)] public float duration;
        }

        sealed class Surface
        {
            public Renderer renderer;
            public Material[] original;
        }

        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        readonly List<Surface> surfaces = new();
        readonly Dictionary<Material, Material> variants = new();
        Health health;
        Color peak;
        float started, duration;
        bool active;

        void Awake() => health = GetComponent<Health>();
        void OnEnable() => health.Died += Clear;

        void OnDisable()
        {
            health.Died -= Clear;
            Clear();
        }

        public void Play(Settings settings)
        {
            if (!isActiveAndEnabled || health.IsDead || settings.intensity <= 0f || settings.duration <= 0f)
                return;

            if (!active)
            {
                surfaces.Clear();
                foreach (var renderer in GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer))
                        continue;

                    var original = renderer.sharedMaterials;
                    Material[] highlighted = null;
                    for (int i = 0; i < original.Length; i++)
                    {
                        var material = original[i];
                        if (!material || !material.HasProperty(EmissionColor) || !material.HasProperty(EmissionMap))
                            continue;
                        if (!variants.TryGetValue(material, out var variant))
                        {
                            // URP Lit needs an emission-enabled material variant. Keep the skinned mesh and base texture.
                            variant = new Material(material) { name = material.name + " (Hit Flash)" };
                            variant.EnableKeyword("_EMISSION");
                            variant.SetTexture(EmissionMap, Texture2D.whiteTexture);
                            variants.Add(material, variant);
                        }
                        highlighted ??= (Material[])original.Clone();
                        highlighted[i] = variant;
                    }

                    if (highlighted == null) continue;
                    surfaces.Add(new Surface { renderer = renderer, original = original });
                    renderer.sharedMaterials = highlighted;
                }
                active = surfaces.Count > 0;
            }

            peak = settings.color * settings.intensity;
            duration = settings.duration;
            started = Time.unscaledTime;
            Apply(1f);
        }

        void LateUpdate()
        {
            if (!active) return;
            float progress = (Time.unscaledTime - started) / duration;
            if (progress >= 1f) Clear();
            else Apply(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.22f, 1f, progress)));
        }

        void Apply(float strength)
        {
            foreach (var entry in variants)
                entry.Value.SetColor(EmissionColor,
                    Color.Lerp(entry.Key.IsKeywordEnabled("_EMISSION") ? entry.Key.GetColor(EmissionColor) : Color.black,
                        peak, strength));
        }

        public void Clear()
        {
            if (!active) return;
            foreach (var surface in surfaces)
                if (surface.renderer) surface.renderer.sharedMaterials = surface.original;
            surfaces.Clear();
            active = false;
        }

        void OnDestroy()
        {
            Clear();
            foreach (var material in variants.Values) Destroy(material);
        }
    }
}
