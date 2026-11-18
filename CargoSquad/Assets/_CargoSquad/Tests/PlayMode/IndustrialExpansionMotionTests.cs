#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using CargoSquad.AI;
using CargoSquad.Player;
using CargoSquad.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CargoSquad.Tests
{
    public sealed class IndustrialExpansionMotionTests
    {
        [UnityTest]
        public IEnumerator RecordEightDirectionWalkWithNormalGameplayCamera()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_CargoSquad/Scenes/IndustrialExpansion.unity", new LoadSceneParameters());
            yield return null;
            yield return null;
            Object.FindFirstObjectByType<CharacterSelection>().StartSelectedCharacter();
            foreach (var wolf in Object.FindObjectsByType<MutatedWolfAI>(FindObjectsSortMode.None)) wolf.enabled = false;
            Camera camera = Camera.main;
            var follow = camera.GetComponent<TopDownCameraFollow>();
            var body = Object.FindFirstObjectByType<PlayerController>().GetComponent<Rigidbody>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var target = new RenderTexture(1280, 720, 24);
            var frame = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.renderMode != RenderMode.WorldSpace).ToArray();
            var modes = canvases.Select(c => c.renderMode).ToArray();
            var views = canvases.Select(c => c.worldCamera).ToArray();
            var distances = canvases.Select(c => c.planeDistance).ToArray();
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            float previousCaptureDelta = Time.captureDeltaTime;
            float previousSize = camera.orthographicSize;
            Quaternion previousRotation = camera.transform.rotation;
            string label = Environment.GetEnvironmentVariable("CARGOSQUAD_MOTION_LABEL") ?? "fixed";
            string path = "ValidationResults/IndustrialExpansionComfort/" + label;
            Directory.CreateDirectory(path + "/frames");
            using var report = new StreamWriter(path + "/motion.csv");
            report.WriteLine("frame,direction,player_x,player_z,camera_x,camera_z,seam_contrast,seam_luma,reference_luma");
            var keys = new[] { new[] { Key.D }, new[] { Key.W }, new[] { Key.A }, new[] { Key.S },
                new[] { Key.D, Key.W }, new[] { Key.A, Key.W }, new[] { Key.A, Key.S }, new[] { Key.D, Key.S } };
            string[] names = { "east", "north", "west", "south", "north-east", "north-west", "south-west", "south-east" };
            int counter = 0;
            float cameraTravel = 0;
            try
            {
                camera.targetTexture = target;
                foreach (var canvas in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1;
                }
                Time.captureDeltaTime = 1f / 30;
                body.position = new Vector3(0, 1, -3.5f);
                body.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                for (int i = 0; i < 20; i++) yield return null;
                Vector3 previousCamera = camera.transform.position;
                for (int direction = 0; direction < keys.Length; direction++)
                {
                    Vector3 start = body.position;
                    for (int step = 0; step < 45; step++)
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[direction]));
                        yield return null;
                        Assert.That(follow.enabled, Is.True);
                        Assert.That(camera.orthographicSize, Is.EqualTo(previousSize));
                        Assert.That(Quaternion.Angle(camera.transform.rotation, previousRotation), Is.LessThan(.001f));
                        Canvas.ForceUpdateCanvases();
                        camera.Render();
                        RenderTexture.active = target;
                        frame.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); frame.Apply();
                        File.WriteAllBytes(path + "/frames/" + counter.ToString("D4") + ".png", frame.EncodeToPNG());
                        float seam = 0, reference = 0;
                        for (int point = 0; point < 9; point++)
                        {
                            float x = 3.35f + point * .15f;
                            seam += Luminance(frame, camera.WorldToScreenPoint(new Vector3(x, .01f, -4)));
                            reference += .5f * (Luminance(frame, camera.WorldToScreenPoint(new Vector3(x, .01f, -3.9f))) +
                                Luminance(frame, camera.WorldToScreenPoint(new Vector3(x, .01f, -4.1f))));
                        }
                        seam /= 9; reference /= 9;
                        float contrast = (reference - seam) / Mathf.Max(reference, .001f);
                        report.WriteLine(string.Join(",", counter, names[direction], F(body.position.x), F(body.position.z),
                            F(camera.transform.position.x), F(camera.transform.position.z), F(contrast), F(seam), F(reference)));
                        cameraTravel += Vector3.Distance(previousCamera, camera.transform.position);
                        previousCamera = camera.transform.position;
                        counter++;
                    }
                    Assert.That(Vector3.Distance(start, body.position), Is.GreaterThan(2), "Walking was blocked toward " + names[direction]);
                }
                Assert.That(cameraTravel, Is.GreaterThan(3), "The recorded test must include actual following-camera movement.");
                Debug.Log("Recorded " + counter + " frames at 30 Hz across eight walking directions; camera travel " + F(cameraTravel) + " m. Output: " + path);
            }
            finally
            {
                Time.captureDeltaTime = previousCaptureDelta;
                InputSystem.RemoveDevice(keyboard);
                RenderTexture.active = previousActive;
                camera.targetTexture = previousTarget;
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (!canvases[i]) continue;
                    canvases[i].renderMode = modes[i]; canvases[i].worldCamera = views[i]; canvases[i].planeDistance = distances[i];
                }
                Object.Destroy(frame); Object.Destroy(target);
                Time.timeScale = 1;
            }
        }

        private static string F(float value) => value.ToString("F6", CultureInfo.InvariantCulture);

        private static float Luminance(Texture2D image, Vector3 pixel)
        {
            Color color = image.GetPixelBilinear((pixel.x - .5f) / image.width, (pixel.y - .5f) / image.height);
            return color.r * .2126f + color.g * .7152f + color.b * .0722f;
        }
    }
}
#endif
