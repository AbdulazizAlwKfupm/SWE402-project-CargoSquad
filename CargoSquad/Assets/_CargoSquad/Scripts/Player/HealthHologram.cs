using CargoSquad.Combat;
using CargoSquad.Visuals;
using TMPro;
using UnityEngine;

namespace CargoSquad.Player
{
    [DisallowMultipleComponent]
    public sealed class HealthHologram : MonoBehaviour
    {
        public const float HealAmount = 25f;
        private Transform symbol;
        private TMP_Text label;
        private Material glow, glass;
        public event System.Action Collected;
        public bool Available { get; private set; }
        private void Awake()
        {
            glow = HologramGeometry.Material(new Color(.18f,1f,.75f));
            glass = HologramGeometry.Material(new Color(.08f,.8f,.65f,.09f), true);
            HologramGeometry.Ring("Ground Halo", transform, .55f, .035f, glow);
            HologramGeometry.Ring("Projector Ring", transform, .38f, .12f, glow, .013f);
            HologramGeometry.Shape("Projection", transform, PrimitiveType.Cylinder, new Vector3(0,.7f,0), new Vector3(.5f,.65f,.5f), glass);
            symbol = new GameObject("Health Cross").transform; symbol.SetParent(transform, false); symbol.localPosition = Vector3.up * 1.1f;
            HologramGeometry.Shape("Vertical", symbol, PrimitiveType.Cube, Vector3.zero, new Vector3(.16f,.65f,.12f), glow);
            HologramGeometry.Shape("Horizontal", symbol, PrimitiveType.Cube, Vector3.zero, new Vector3(.65f,.16f,.12f), glow);
            var text = new GameObject("Health Amount"); text.transform.SetParent(transform, false); text.transform.localPosition = Vector3.up * 1.7f;
            label = text.AddComponent<TextMeshPro>(); label.font = UI.UiTypography.Font; label.text = "+25 HP"; label.fontSize = 2;
            label.alignment = TextAlignmentOptions.Center; label.color = new Color(.55f,1f,.85f); label.rectTransform.sizeDelta = new Vector2(2,.5f);
        }
        public void Spawn(Vector3 position) { transform.position = position; Available = true; gameObject.SetActive(true); }
        public bool TryCollect(Health survivor)
        {
            if (!Available || !survivor || survivor.IsDead || survivor.Current >= survivor.Maximum) return false;
            if (survivor.Restore(HealAmount) <= 0f) return false;
            Available = false; gameObject.SetActive(false); Collected?.Invoke(); return true;
        }
        private void Update()
        {
            symbol.Rotate(Vector3.up, 35f * Time.deltaTime, Space.Self);
            symbol.localPosition = Vector3.up * (1.1f + Mathf.Sin(Time.time * 1.7f) * .06f);
            if (Camera.main) label.transform.rotation = Camera.main.transform.rotation;
        }
        private void OnDestroy() { if (glow) Destroy(glow); if (glass) Destroy(glass); }
    }
}
