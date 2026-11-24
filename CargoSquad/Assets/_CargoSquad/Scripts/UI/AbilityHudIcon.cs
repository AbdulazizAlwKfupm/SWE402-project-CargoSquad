using CargoSquad.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSquad.UI
{
    /// <summary>Resolution-independent role glyph and cooldown sweep, drawn directly as UI geometry.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AbilityHudIcon : MaskableGraphic
    {
        public SurvivorKind Kind { get; private set; }
        public float RemainingFraction { get; private set; }
        public bool Ready { get; private set; }
        public void SetState(SurvivorKind kind, float remaining, bool ready)
        {
            remaining = Mathf.Clamp01(remaining);
            if (Kind == kind && Mathf.Approximately(RemainingFraction, remaining) && Ready == ready) return;
            Kind = kind; RemainingFraction = remaining; Ready = ready; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .48f;
            Disc(mesh, radius, 1f, new Color(.04f,.10f,.13f));
            Ring(mesh, radius, 1f, new Color(.18f,.28f,.31f));
            var accent = Ready ? new Color(.52f,.94f,.72f) : new Color(.30f,.72f,.83f);
            Ring(mesh, radius, 1f - RemainingFraction, accent);
            float scale = radius / 32f;
            if (Kind == SurvivorKind.Medic)
            {
                Rect(mesh, -6 * scale, -17 * scale, 12 * scale, 34 * scale, Color.white);
                Rect(mesh, -17 * scale, -6 * scale, 34 * scale, 12 * scale, Color.white);
            }
            else
            {
                Poly(mesh, new[] { new Vector2(-17,17), new Vector2(17,17), new Vector2(14,-6), new Vector2(0,-19), new Vector2(-14,-6) }, scale, Color.white);
                Poly(mesh, new[] { new Vector2(-7,7), new Vector2(1,7), new Vector2(1,12), new Vector2(12,0), new Vector2(1,-12), new Vector2(1,-7), new Vector2(-7,-7) }, scale, new Color(.05f,.15f,.19f));
            }
            if (RemainingFraction > 0f) Disc(mesh, radius - 4, RemainingFraction, new Color(.01f,.035f,.045f,.72f));
            else if (!Ready) Disc(mesh, radius - 4, 1f, new Color(.01f,.035f,.045f,.45f));
        }
        static void Disc(VertexHelper mesh, float radius, float fraction, Color c)
        {
            int segments = Mathf.CeilToInt(64 * fraction); if (segments == 0) return;
            int start = mesh.currentVertCount; mesh.AddVert(Vector3.zero, c, Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * .5f - fraction * Mathf.PI * 2 * i / segments;
                mesh.AddVert(new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius, c, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
        static void Ring(VertexHelper mesh, float radius, float fraction, Color c)
        {
            int segments = Mathf.CeilToInt(80 * fraction); if (segments == 0) return;
            int start = mesh.currentVertCount;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * .5f - fraction * Mathf.PI * 2 * i / segments;
                var p = new Vector3(Mathf.Cos(a), Mathf.Sin(a));
                mesh.AddVert(p * radius, c, Vector2.zero); mesh.AddVert(p * (radius - 2.5f), c, Vector2.zero);
                if (i == 0) continue;
                int n = start + i * 2;
                mesh.AddTriangle(n - 2, n, n - 1); mesh.AddTriangle(n, n + 1, n - 1);
            }
        }
        static void Rect(VertexHelper mesh, float x, float y, float width, float height, Color c) =>
            Poly(mesh, new[] { new Vector2(x,y), new Vector2(x+width,y), new Vector2(x+width,y+height), new Vector2(x,y+height) }, 1, c);
        static void Poly(VertexHelper mesh, Vector2[] vertices, float scale, Color c)
        {
            // A centroid fan also handles the simple concave arrow as separate visible wedges.
            int start = mesh.currentVertCount;
            Vector2 center = Vector2.zero; foreach (var p in vertices) center += p; center /= vertices.Length;
            mesh.AddVert(center * scale, c, Vector2.zero);
            foreach (var p in vertices) mesh.AddVert(p * scale, c, Vector2.zero);
            for (int i = 0; i < vertices.Length; i++) mesh.AddTriangle(start, start + i + 1, start + (i + 1) % vertices.Length + 1);
        }
    }
}
