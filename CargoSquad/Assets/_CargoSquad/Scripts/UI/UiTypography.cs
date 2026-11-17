using TMPro;
using UnityEngine;

namespace CargoSquad.UI
{
    public static class UiTypography
    {
        public static TMP_FontAsset Font => Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        public static TextMeshProUGUI Apply(GameObject host, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = host.AddComponent<TextMeshProUGUI>();
            label.font = Font; label.text = text; label.fontSize = size; label.color = color;
            label.alignment = alignment; label.raycastTarget = false; label.extraPadding = true;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }
    }
}
