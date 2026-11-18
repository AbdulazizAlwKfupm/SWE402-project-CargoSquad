#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CargoSquad.Tests
{
    internal static class IndustrialExpansionScreenshots
    {
        public static IEnumerator Overview()
        {
            Camera camera = Camera.main;
            var follow = camera.GetComponent<TopDownCameraFollow>();
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float size = camera.orthographicSize;
            bool following = follow.enabled;
            var hud = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace).ToArray();
            var enabled = hud.Select(c => c.enabled).ToArray();
            try
            {
                foreach (var canvas in hud) canvas.enabled = false;
                follow.enabled = false;
                camera.transform.position = new Vector3(14, 24, -25);
                camera.transform.LookAt(new Vector3(0, .7f, 0));
                camera.orthographicSize = 14;
                yield return Capture("02-section-overview-review-camera");
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.orthographicSize = size;
                follow.enabled = following;
                for (int i = 0; i < hud.Length; i++) if (hud[i]) hud[i].enabled = enabled[i];
            }
        }

        public static IEnumerator Capture(string name)
        {
            Camera camera = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var views = canvases.Select(c => c.worldCamera).ToArray();
            var distances = canvases.Select(c => c.planeDistance).ToArray();
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(1280, 800, 24);
            Texture2D image = null;
            try
            {
                camera.targetTexture = target;
                foreach (var canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1;
                }
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)) text.ForceMeshUpdate();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
                const string path = "ValidationResults/IndustrialExpansionPolish";
                Directory.CreateDirectory(path);
                File.WriteAllBytes(path + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                if (image) Object.Destroy(image);
                Object.Destroy(target);
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (!canvases[i]) continue;
                    canvases[i].renderMode = modes[i];
                    canvases[i].worldCamera = views[i];
                    canvases[i].planeDistance = distances[i];
                }
            }
        }
    }
}
#endif
