using System;
using System.Linq;
using CargoSquad.Visuals;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSquad.UI
{
    /// <summary>Independent, head-only model render. Uses unscaled time while deployment pauses gameplay.</summary>
    public sealed class HologramHeadPreview : MonoBehaviour
    {
        private const int PreviewLayer = 30;
        private GameObject world;
        private Transform pivot;
        private RenderTexture texture;
        private RawImage output;
        private RectTransform scan;
        private float phase;
        public string SourceHeadName { get; private set; }
        public Transform Pivot => pivot;
        public RenderTexture Texture => texture;
        public string[] MeshNames { get; private set; }

        public void Initialize(SurvivorVisual visual, string headName, RawImage image, int slot, RectTransform scanLine)
        {
            if (world) Release();
            SourceHeadName = headName; output = image; scan = scanLine; phase = slot * 1.3f;
            var head = visual.GetComponentsInChildren<Transform>(true).SingleOrDefault(t => t.name == headName);
            if (!head) throw new InvalidOperationException(visual.name + " must contain its own " + headName + " head.");
            // No Animator, SurvivorVisual, physics or ability components are cloned into the preview.
            world = new GameObject("Hologram_" + headName) { hideFlags = HideFlags.DontSave };
            Vector3 anchor = new Vector3(5000 + slot * 20, 5000, 5000);
            world.transform.position = anchor;
            pivot = new GameObject("HeadPivot").transform; pivot.SetParent(world.transform, false);
            var clone = Instantiate(head.gameObject);
            clone.name = headName + "_Preview"; clone.SetActive(true);
            clone.transform.SetParent(pivot, false);
            clone.transform.SetPositionAndRotation(anchor, head.rotation);
            clone.transform.localScale = head.lossyScale;
            foreach (var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PreviewLayer;
            var renderers = clone.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) { r.enabled = true; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; bounds.Encapsulate(r.bounds); }
            clone.transform.position += anchor - bounds.center;
            MeshNames = clone.GetComponentsInChildren<MeshFilter>().Select(m => m.sharedMesh.name).ToArray();
            var cameraObject = new GameObject("HeadCamera"); cameraObject.transform.SetParent(world.transform, false);
            var camera = cameraObject.AddComponent<Camera>(); camera.cullingMask = 1 << PreviewLayer;
            camera.transform.SetPositionAndRotation(anchor + Vector3.forward * 3, Quaternion.Euler(0, 180, 0));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .07f, .085f, 1f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.y, new Vector2(bounds.extents.x, bounds.extents.z).magnitude) * 1.14f;
            camera.nearClipPlane = .05f; camera.farClipPlane = 6f; camera.allowHDR = false; camera.allowMSAA = true;
            texture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) {
                name = headName + "_LivePortrait", antiAliasing = 4, filterMode = FilterMode.Bilinear, useMipMap = false
            };
            texture.Create(); camera.targetTexture = texture; image.texture = texture;
            AddLight("Key", anchor + new Vector3(.8f, 1.2f, 1.6f), new Color(.87f, .96f, 1f), 4f);
            AddLight("CyanRim", anchor + new Vector3(-.8f, -.15f, -.8f), new Color(.13f, .85f, 1f), 3.5f);
            pivot.localRotation = Quaternion.Euler(0, slot == 0 ? -18 : 18, 0);
        }
        private void AddLight(string name, Vector3 position, Color color, float intensity)
        {
            var obj = new GameObject(name); obj.transform.SetParent(world.transform, false); obj.transform.position = position;
            var light = obj.AddComponent<Light>(); light.type = LightType.Point; light.range = 4f;
            light.color = color; light.intensity = intensity; light.shadows = LightShadows.None; light.cullingMask = 1 << PreviewLayer;
        }
        private void Update()
        {
            if (!pivot) return;
            pivot.Rotate(Vector3.up, 22f * Time.unscaledDeltaTime, Space.Self);
            phase += Time.unscaledDeltaTime;
            if (scan) scan.anchoredPosition = new Vector2(0, 58 + Mathf.Sin(phase * .9f) * 82);
        }
        private void Release()
        {
            if (output) output.texture = null;
            if (world) { world.SetActive(false); Destroy(world); world = null; }
            pivot = null;
            if (texture) { texture.Release(); Destroy(texture); texture = null; }
        }
        private void OnDisable() => Release();
        private void OnDestroy() => Release();
    }

}
