using UnityEngine;

namespace CargoSquad.Combat
{
    /// <summary>Fixed-size reusable pool of short tracers, muzzle stars, and impact crosses.</summary>
    public sealed class WeaponFeedback : MonoBehaviour
    {
        private readonly LineRenderer[] lines = new LineRenderer[64];
        private readonly float[] expires = new float[64];
        private readonly Color[] colors = new Color[64];
        private int next;
        public void Initialize(Material material)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var obj = new GameObject("Shot feedback"); obj.transform.SetParent(transform, false);
                var line = obj.AddComponent<LineRenderer>(); lines[i] = line;
                line.sharedMaterial = material; line.positionCount = 2; line.useWorldSpace = true;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false; line.enabled = false;
            }
        }
        public void Segment(Vector3 a, Vector3 b, Color color, float width = .025f)
        {
            int i = next++ % lines.Length; var line = lines[i];
            line.SetPosition(0, a); line.SetPosition(1, b); line.widthMultiplier = width;
            colors[i] = color; expires[i] = Time.time + .10f;
            line.startColor = line.endColor = color; line.enabled = true;
        }
        public void Spark(Vector3 point, Color color, float size)
        {
            Segment(point - Vector3.right * size, point + Vector3.right * size, color, .05f);
            Segment(point - Vector3.forward * size, point + Vector3.forward * size, color, .05f);
            Segment(point - Vector3.up * size, point + Vector3.up * size, color, .05f);
        }
        public void Electric(Vector3 a, Vector3 b, Color color)
        {
            Vector3 side = Vector3.Cross((b - a).normalized, Vector3.up);
            Vector3 prior = a;
            for (int i = 1; i <= 6; i++)
            {
                Vector3 point = Vector3.Lerp(a, b, i / 6f) + (i == 6 ? Vector3.zero : side * (i % 2 == 0 ? -.07f : .07f));
                Segment(prior, point, color); prior = point;
            }
        }
        private void Update()
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i] || !lines[i].enabled) continue;
                float alpha = Mathf.Clamp01((expires[i] - Time.time) / .10f);
                if (alpha <= 0) { lines[i].enabled = false; continue; }
                var color = colors[i]; color.a *= alpha; lines[i].startColor = lines[i].endColor = color;
            }
        }
        public void Clear() { foreach (var line in lines) if (line) line.enabled = false; }
    }
}
