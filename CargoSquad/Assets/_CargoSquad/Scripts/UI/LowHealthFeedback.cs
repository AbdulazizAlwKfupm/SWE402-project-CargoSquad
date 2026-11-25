using CargoSquad.Combat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CargoSquad.UI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class LowHealthFeedback : MonoBehaviour
    {
        [SerializeField] private Health survivor;
        [SerializeField] private CharacterSelection selection;
        [SerializeField, Min(.1f)] private float fadeTime = .6f;
        [SerializeField] private bool useHalfHealthVignette;
        private Volume effect;
        private VolumeProfile profile;
        private ColorAdjustments grading;
        private Vignette vignette;
        private UniversalAdditionalCameraData cameraData;
        private bool previousPost;
        private LayerMask previousMask;
        private float blend, velocity;
        public float Intensity => blend;
        public float Saturation => grading ? grading.saturation.value : 0f;
        private void Awake()
        {
            cameraData = GetComponent<UniversalAdditionalCameraData>();
            if (!cameraData) cameraData = gameObject.AddComponent<UniversalAdditionalCameraData>();
            previousPost = cameraData.renderPostProcessing; previousMask = cameraData.volumeLayerMask;
            cameraData.renderPostProcessing = true; cameraData.volumeLayerMask |= 1 << 28;
            var host = new GameObject("Low Health Volume"); host.layer = 28; host.transform.SetParent(transform, false);
            effect = host.AddComponent<Volume>(); effect.isGlobal = true; effect.priority = 10000; effect.weight = 0f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>(); effect.sharedProfile = profile;
            if (!useHalfHealthVignette)
            {
                grading = profile.Add<ColorAdjustments>(false);
                grading.saturation.Override(-90f);
                grading.colorFilter.Override(new Color(1f,.70f,.70f));
            }
            vignette = profile.Add<Vignette>(false); vignette.color.Override(new Color(.45f,.015f,.025f));
            vignette.intensity.Override(useHalfHealthVignette ? .23f : .38f); vignette.smoothness.Override(.65f);
        }
        private void Update()
        {
            float threshold = useHalfHealthVignette && survivor ? survivor.Maximum * .5f : 40f;
            float desired = survivor && survivor.Current < threshold && (!selection || selection.HasStarted) ? 1f : 0f;
            blend = Mathf.SmoothDamp(blend, desired, ref velocity, fadeTime, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(blend - desired) < .001f) blend = desired;
            if (grading)
            {
                float severity = survivor ? Mathf.Clamp01((40f - survivor.Current) / 40f) : 0f;
                grading.saturation.value = -Mathf.Lerp(65f,90f,severity);
                vignette.intensity.value = Mathf.Lerp(.25f,.42f,severity) + Mathf.Sin(Time.unscaledTime * 2.5f) * .012f * severity;
            }
            effect.weight = blend;
        }
        private void OnDisable() { if (effect) effect.weight = 0f; }
        private void OnDestroy()
        {
            if (cameraData) { cameraData.renderPostProcessing = previousPost; cameraData.volumeLayerMask = previousMask; }
            if (profile) Destroy(profile);
        }
    }
}
