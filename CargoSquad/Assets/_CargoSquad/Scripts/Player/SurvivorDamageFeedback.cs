using System.Collections.Generic;
using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.UI;
using CargoSquad.Visuals;
using UnityEngine;

namespace CargoSquad.Player
{
    [DefaultExecutionOrder(300), DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class SurvivorDamageFeedback : MonoBehaviour
    {
        [SerializeField] CharacterSelection selection;
        [SerializeField] CargoMissionOverlay hud;
        [SerializeField] TopDownCameraFollow cameraFollow;
        [SerializeField] Shader flashShader;
        [SerializeField] Color flashColor = new Color(1f, .60f, .59f);
        [SerializeField, Range(.10f, .15f)] float flashDuration = .125f;
        [SerializeField, Min(.05f)] float indicatorDuration = .52f;
        [SerializeField, Min(0f)] float shakeAmplitude = .13f;
        [SerializeField, Min(0f)] float shakeDuration = .18f;

        readonly List<(Renderer renderer, Material[] materials)> originals = new();
        Health health;
        Camera view;
        Material flashMaterial;
        SurvivorDamageIndicator indicator;
        Vector3? source;
        float flashEnd, indicatorEnd, shakeEnd;
        bool shaking;

        void Awake()
        {
            health = GetComponent<Health>();
            if (cameraFollow) view = cameraFollow.GetComponent<Camera>();
        }

        void OnEnable() => health.HitReceived += OnHit;

        void OnDisable()
        {
            health.HitReceived -= OnHit;
            RestoreMaterials();
            ClearShake();
            if (indicator) indicator.enabled = false;
        }

        void OnDestroy()
        {
            if (flashMaterial) Destroy(flashMaterial);
            if (indicator) Destroy(indicator.gameObject);
        }

        void OnHit(Vector3? towardSource, bool powerful)
        {
            if (!selection || !selection.HasStarted) return;
            float now = Time.unscaledTime;
            Flash(selection.ActiveVisual);
            flashEnd = now + flashDuration;
            source = towardSource;
            indicatorEnd = now + indicatorDuration;
            if (powerful && shakeDuration > 0f && shakeAmplitude > 0f)
            {
                shakeEnd = now + shakeDuration;
                shaking = true;
            }
            UpdateIndicator(now);
        }

        void Flash(SurvivorVisual visual)
        {
            if (!visual || !flashShader || originals.Count != 0) return;
            if (!flashMaterial) flashMaterial = new Material(flashShader) { name = "Survivor Hit Flash" };
            flashMaterial.SetColor("_Tint", flashColor);
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is not (MeshRenderer or SkinnedMeshRenderer) ||
                    (visual.Weapon && renderer.transform.IsChildOf(visual.Weapon)) ||
                    renderer.GetComponentInParent<CargoItem>()) continue;
                var original = renderer.sharedMaterials;
                if (original.Length == 0) continue;
                var flashing = new Material[original.Length];
                for (int i = 0; i < flashing.Length; i++) flashing[i] = flashMaterial;
                originals.Add((renderer, original));
                renderer.sharedMaterials = flashing;
            }
        }

        void RestoreMaterials()
        {
            foreach (var item in originals)
                if (item.renderer) item.renderer.sharedMaterials = item.materials;
            originals.Clear();
        }

        void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (originals.Count != 0 && now >= flashEnd) RestoreMaterials();
            if (shaking)
            {
                if (!cameraFollow || !cameraFollow.isActiveAndEnabled || Time.timeScale <= 0f || now >= shakeEnd)
                    ClearShake();
                else
                {
                    float remaining = (shakeEnd - now) / shakeDuration;
                    float phase = (1f - remaining) * 13f;
                    Vector2 offset = new Vector2(Mathf.Sin(phase + .8f), .63f * Mathf.Sin(phase * 1.37f + .5f));
                    cameraFollow.SetDamageOffset(offset * (shakeAmplitude * remaining * remaining));
                }
            }
            UpdateIndicator(now);
        }

        void ClearShake()
        {
            if (!shaking) return;
            if (cameraFollow) cameraFollow.SetDamageOffset(Vector2.zero);
            shaking = false;
        }

        void UpdateIndicator(float now)
        {
            RectTransform root = hud ? hud.DamageIndicatorRoot : null;
            if (!root || !root.gameObject.activeInHierarchy || !view || now >= indicatorEnd)
            {
                if (indicator) indicator.enabled = false;
                return;
            }
            if (!indicator)
            {
                indicator = new GameObject("Survivor Damage Indicator", typeof(RectTransform), typeof(CanvasRenderer))
                    .AddComponent<SurvivorDamageIndicator>();
                indicator.transform.SetParent(root, false);
                indicator.rectTransform.sizeDelta = new Vector2(124f, 124f);
                indicator.raycastTarget = false;
            }
            Vector3 viewport = view.WorldToViewportPoint(transform.position + Vector3.up * .32f);
            indicator.rectTransform.anchorMin = indicator.rectTransform.anchorMax = new Vector2(viewport.x, viewport.y);
            indicator.rectTransform.anchoredPosition = Vector2.zero;
            Vector3 heading = source ?? Vector3.zero;
            Vector2 direction = new Vector2(Vector3.Dot(heading, view.transform.right),
                Vector3.Dot(heading, view.transform.up));
            float fade = Mathf.Clamp01((indicatorEnd - now) / indicatorDuration);
            indicator.enabled = true;
            indicator.Present(direction.normalized, .93f * Mathf.SmoothStep(0f, 1f, fade));
        }
    }
}
