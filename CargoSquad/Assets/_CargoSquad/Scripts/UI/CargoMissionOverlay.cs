using CargoSquad.Cargo;
using CargoSquad.Missions;
using CargoSquad.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSquad.UI
{
    [DisallowMultipleComponent]
    public sealed class CargoMissionOverlay : MonoBehaviour
    {
        [SerializeField] private CargoInteractor player;
        [SerializeField] private CargoExtraction mission;
        [SerializeField] private PlayerStamina stamina;
        private GameObject canvasObject;
        private RectTransform panel;
        private TMP_Text objective, staminaLabel, conditionLabel, gripLabel, warning, prompt;
        private float staminaFraction;
        private void OnEnable()
        {
            if (stamina) stamina.Changed += UpdateStamina;
            EnsureInterface(); canvasObject.SetActive(true);
            if (stamina) UpdateStamina(stamina.Current, stamina.Maximum);
        }
        private void OnDisable()
        {
            if (stamina) stamina.Changed -= UpdateStamina;
            if (canvasObject) canvasObject.SetActive(false);
        }
        private void UpdateStamina(float current, float maximum) => staminaFraction = current / maximum;
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (!isActiveAndEnabled || !player || !mission || !panel) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(panel, screenPoint) ||
                (prompt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(prompt.rectTransform, screenPoint));
        }
        private void Update()
        {
            if (!player || !mission || !objective) return;
            CargoItem carried = player.CarriedCargo;
            objective.text = mission.IsComplete ? (mission.RecoveryFailed ? "RECOVERY FAILED / ESCAPED" : "MISSION COMPLETE") :
                mission.CargoDestroyed ? "ESCAPE ALIVE" : mission.IsPrimaryCargo(carried) ? "DELIVER CARGO" : "FIND THE CARGO";
            staminaLabel.text = stamina ? $"STAMINA   {staminaFraction * 100f:0}%" : "";
            var condition = carried ? carried.Condition : mission.PrimaryCondition;
            float fraction = condition ? condition.Current / condition.Maximum : 0f;
            conditionLabel.text = condition && !mission.IsComplete ? $"CARGO CONDITION   {fraction * 100f:0}%" : "";
            gripLabel.text = carried && player.Grip ? $"GRIP   {player.Grip.Percent:0}%" : "";
            warning.text = mission.IsComplete && !mission.RecoveryFailed ? "Cargo secured. Mission success." :
                condition && fraction > 0f && fraction <= .25f ? "CRITICAL CARGO CONDITION" :
                carried && player.Grip && player.Grip.Percent <= 25f ? "CRITICAL GRIP" : "";
            prompt.text = !mission.IsComplete ? player.Prompt : "";
            prompt.gameObject.SetActive(!string.IsNullOrEmpty(prompt.text));
        }
        private TMP_Text Row(string name, string text, int size, float y, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.layer = 5;
            var rect = obj.GetComponent<RectTransform>(); rect.SetParent(panel, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(16, -y); rect.sizeDelta = new Vector2(334, 30);
            return UiTypography.Apply(obj, text, size, color, TextAlignmentOptions.MidlineLeft);
        }
        private void EnsureInterface()
        {
            if (canvasObject) return;
            canvasObject = new GameObject("MissionHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = 5; canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; canvas.pixelPerfect = true;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var box = new GameObject("MissionPanel", typeof(RectTransform), typeof(Image)); box.layer = 5;
            panel = box.GetComponent<RectTransform>(); panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0, 1); panel.anchoredPosition = new Vector2(14, -14);
            panel.sizeDelta = new Vector2(370, 180); box.GetComponent<Image>().color = new Color(.035f, .055f, .075f, .96f);
            var mint = new Color(.52f, .94f, .72f); var muted = new Color(.78f, .84f, .87f);
            objective = Row("Objective", "", 22, 10, Color.white);
            staminaLabel = Row("Stamina", "", 20, 43, mint);
            conditionLabel = Row("Condition", "", 18, 76, muted);
            gripLabel = Row("Grip", "", 18, 107, muted);
            warning = Row("Warning", "", 15, 140, new Color(1f, .76f, .47f));
            var obj = new GameObject("CargoPrompt", typeof(RectTransform)); obj.layer = 5;
            var rect = obj.GetComponent<RectTransform>(); rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 20); rect.sizeDelta = new Vector2(620, 40);
            prompt = UiTypography.Apply(obj, "", 22, Color.white); prompt.gameObject.SetActive(false);
        }
        private void OnDestroy() { if (canvasObject) Destroy(canvasObject); }
    }
}
