using CargoSquad.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSquad.UI
{
    /// <summary>Per-wolf view of existing Health. Owns no damage or AI state.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class WolfHealthIndicator : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(0, 2.8f, 0);
        [SerializeField, Min(.01f)] private float smoothTime = .08f;
        [SerializeField, Min(.01f)] private float flashDuration = .16f;
        [SerializeField, Min(.1f)] private float width = 1.35f;
        [SerializeField, Min(.03f)] private float height = .16f;
        private Health health;
        private Camera view;
        private GameObject bar;
        private Image fill;
        private float target, displayed, velocity, previous, flashUntil;
        public float TargetFraction => target;
        public float DisplayedFraction => displayed;
        public bool Visible => bar && bar.activeSelf;
        public Color FillColor => fill ? fill.color : Color.clear;

        private void Awake()
        {
            health = GetComponent<Health>(); view = Camera.main;
            bar = new GameObject("Wolf Health", typeof(RectTransform), typeof(Canvas));
            bar.transform.SetParent(transform, false);
            var canvas = bar.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = view;
            var rect = bar.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(100, 12);
            var background = new GameObject("Border", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(rect, false);
            var border = background.GetComponent<Image>(); border.raycastTarget = false;
            border.color = new Color(.025f, .04f, .05f, .95f);
            Stretch(border.rectTransform);
            var track = new GameObject("Track", typeof(RectTransform), typeof(Image)); track.transform.SetParent(background.transform, false);
            var trackImage = track.GetComponent<Image>(); trackImage.color = new Color(.16f, .2f, .22f, .95f);
            trackImage.raycastTarget = false; Stretch(trackImage.rectTransform);
            trackImage.rectTransform.offsetMin = new Vector2(2, 2); trackImage.rectTransform.offsetMax = new Vector2(-2, -2);
            var foreground = new GameObject("HP Fill", typeof(RectTransform), typeof(Image)); foreground.transform.SetParent(track.transform, false);
            fill = foreground.GetComponent<Image>(); fill.raycastTarget = false; Stretch(fill.rectTransform);
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private void OnEnable()
        {
            previous = health.Current; target = displayed = health.Current / health.Maximum;
            health.Changed += OnChanged; Draw();
        }
        private void Start()
        {
            // All Health.Awake calls have completed, even when installed by an enemy's Awake.
            previous = health.Current; target = displayed = health.Current / health.Maximum; Draw();
        }
        private void OnDisable() { health.Changed -= OnChanged; if (bar) bar.SetActive(false); }
        private void OnChanged(float current, float maximum)
        {
            if (current < previous) flashUntil = Time.unscaledTime + flashDuration;
            previous = current; target = Mathf.Clamp01(current / maximum);
            if (health.IsDead) displayed = 0;
            Draw();
        }
        private void Draw()
        {
            bar.SetActive(!health.IsDead && isActiveAndEnabled);
            fill.rectTransform.anchorMax = new Vector2(displayed, 1);
            fill.color = Time.unscaledTime < flashUntil ? new Color(1, .35f, .3f) :
                target > .6f ? new Color(.35f, .9f, .48f) : target > .3f ? new Color(1, .7f, .2f) : new Color(1, .22f, .2f);
        }
        private void LateUpdate()
        {
            if (!view) view = Camera.main;
            if (!view || health.IsDead) return;
            displayed = Mathf.SmoothDamp(displayed, target, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(displayed - target) < .001f) displayed = target;
            bar.transform.position = transform.position + offset;
            bar.transform.rotation = view.transform.rotation;
            // Constant orthographic screen size; perspective size follows distance to the camera.
            float size = view.orthographic ? view.orthographicSize / 8f : Vector3.Distance(view.transform.position, bar.transform.position) / 25f;
            Vector3 parentScale = transform.lossyScale;
            bar.transform.localScale = new Vector3(width / 100f / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                height / 12f / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1) * Mathf.Clamp(size, .5f, 3f);
            Draw();
        }
    }
}
