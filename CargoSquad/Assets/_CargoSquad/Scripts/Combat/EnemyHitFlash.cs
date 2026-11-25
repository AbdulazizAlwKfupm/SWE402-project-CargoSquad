using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CargoSquad.Combat
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class EnemyHitFlash : MonoBehaviour
    {
        [Serializable]
        public struct Settings
        {
            public Shader shader;
            public Color color;
            [Range(0f, 1.5f)] public float intensity;
            [Range(.02f, .25f)] public float duration;
            [Range(0f, .09f)] public float outlineWidth;
        }

        sealed class Surface
        {
            public Renderer body, outline;
            public Material[] original, flash;
        }

        readonly List<Surface> surfaces = new();
        Material bodyMaterial, outlineMaterial;
        Health health;
        float expiresAt;
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
            if (!isActiveAndEnabled || health.IsDead || !settings.shader ||
                settings.intensity <= 0f || settings.duration <= 0f) return;

            if (!bodyMaterial) BuildSurfaces(settings.shader);
            if (surfaces.Count == 0) return;

            bodyMaterial.SetColor("_Tint", settings.color * settings.intensity);
            outlineMaterial.SetColor("_Tint", new Color(.34f, .79f, 1f) * Mathf.Min(settings.intensity, 1f));
            outlineMaterial.SetFloat("_Width", settings.outlineWidth);

            foreach (var surface in surfaces)
            {
                if (!surface.body || !surface.body.enabled) continue;
                if (surface.original == null)
                {
                    surface.original = surface.body.sharedMaterials;
                    surface.body.sharedMaterials = surface.flash;
                }
                surface.outline.enabled = settings.outlineWidth > 0f;
            }
            active = true;
            expiresAt = Time.unscaledTime + settings.duration;
        }

        void BuildSurfaces(Shader shader)
        {
            bodyMaterial = new Material(shader) { name = "Hit Flash Body" };
            outlineMaterial = new Material(shader) { name = "Hit Flash Outline", renderQueue = 2501 };
            outlineMaterial.SetFloat("_Cull", (float)CullMode.Front);
            outlineMaterial.SetFloat("_DepthWrite", 0f);

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                int slots = renderer.sharedMaterials.Length;
                if (slots == 0) continue;
                Renderer outline;
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh)
                {
                    var shell = CreateShell(renderer).AddComponent<SkinnedMeshRenderer>();
                    shell.sharedMesh = skin.sharedMesh;
                    shell.bones = skin.bones;
                    shell.rootBone = skin.rootBone;
                    shell.quality = skin.quality;
                    shell.localBounds = skin.localBounds;
                    outline = shell;
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh)
                {
                    var shell = CreateShell(renderer);
                    shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    outline = shell.AddComponent<MeshRenderer>();
                }
                else continue;

                outline.shadowCastingMode = ShadowCastingMode.Off;
                outline.receiveShadows = false;
                var flash = new Material[slots];
                var edges = new Material[slots];
                for (int i = 0; i < slots; i++)
                {
                    flash[i] = bodyMaterial;
                    edges[i] = outlineMaterial;
                }
                outline.sharedMaterials = edges;
                outline.enabled = false;
                surfaces.Add(new Surface { body = renderer, outline = outline, flash = flash });
            }
        }

        static GameObject CreateShell(Renderer renderer)
        {
            var shell = new GameObject("Hit Flash Outline") { layer = renderer.gameObject.layer };
            shell.transform.SetParent(renderer.transform, false);
            return shell;
        }

        void LateUpdate()
        {
            if (active && Time.unscaledTime >= expiresAt) Clear();
        }

        public void Clear()
        {
            if (!active) return;
            foreach (var surface in surfaces)
            {
                if (surface.body && surface.original != null) surface.body.sharedMaterials = surface.original;
                if (surface.outline) surface.outline.enabled = false;
                surface.original = null;
            }
            active = false;
        }

        void OnDestroy()
        {
            Clear();
            foreach (var surface in surfaces)
                if (surface.outline) Destroy(surface.outline.gameObject);
            if (bodyMaterial) Destroy(bodyMaterial);
            if (outlineMaterial) Destroy(outlineMaterial);
        }
    }
}
