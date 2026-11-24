using CargoSquad.Cargo;
using CargoSquad.Combat;
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
        [SerializeField] private SurvivorHud survivorHudPrefab;
        [SerializeField] private CharacterSelection selection;
        private SurvivorHud survivorHud;
        private GameObject canvasObject;
        private RectTransform panel;
        private TMP_Text objective, conditionLabel, gripLabel, warning, prompt;
        private Health health;
        private CargoGrip grip;
        private CargoCondition observedCondition;
        private float previousCondition, previousGrip;
        private float conditionFlashUntil, gripFlashUntil, dropNoticeUntil;
        private static readonly Color Muted = new Color(.78f, .84f, .87f);
        private static readonly Color DamageRed = new Color(1f, .28f, .28f);
        private static readonly Color GripOrange = new Color(1f, .64f, .23f);
        private CargoSquad.Visuals.ExtractionMarker extractionMarker;
        private void OnEnable()
        {
            EnsureInterface(); canvasObject.SetActive(true);
            health = player ? player.GetComponent<Health>() : null;
            extractionMarker = FindFirstObjectByType<CargoSquad.Visuals.ExtractionMarker>();
            grip = player ? player.GetComponent<CargoGrip>() : null;
            if (!selection) selection = FindFirstObjectByType<CharacterSelection>();
            survivorHud.Bind(health, stamina, player ? player.GetComponent<CharacterAbility>() : null, player, selection);
            if (player)
            {
                player.CarriedCargoChanged += OnCarriedCargoChanged;
                player.CargoDropped += OnCargoDropped;
            }
            if (grip)
            {
                previousGrip = grip.Percent;
                grip.Changed += UpdateGrip;
            }
            BindCondition(CurrentCondition);
            RefreshCargoLabels();
        }
        private void OnDisable()
        {
            if (grip) grip.Changed -= UpdateGrip;
            if (player)
            {
                player.CarriedCargoChanged -= OnCarriedCargoChanged;
                player.CargoDropped -= OnCargoDropped;
            }
            if (observedCondition) observedCondition.Changed -= UpdateCondition;
            observedCondition = null;
            if (canvasObject) canvasObject.SetActive(false);
        }
        private CargoCondition CurrentCondition => player && player.CarriedCargo
            ? player.CarriedCargo.Condition : mission ? mission.PrimaryCondition : null;

        private void BindCondition(CargoCondition condition)
        {
            if (observedCondition == condition) return;
            if (observedCondition) observedCondition.Changed -= UpdateCondition;
            observedCondition = condition;
            if (!observedCondition) return;
            previousCondition = observedCondition.Current;
            observedCondition.Changed += UpdateCondition;
        }

        private void UpdateCondition(float current, float maximum)
        {
            if (current < previousCondition) conditionFlashUntil = Time.unscaledTime + .20f;
            previousCondition = current;
            RefreshCargoLabels();
            conditionLabel.color = DamageRed;
        }

        private void UpdateGrip(float percent)
        {
            if (percent < previousGrip) gripFlashUntil = Time.unscaledTime + .20f;
            previousGrip = percent;
            RefreshCargoLabels();
            gripLabel.color = GripOrange;
        }

        private void OnCarriedCargoChanged(CargoItem cargo)
        {
            BindCondition(CurrentCondition);
            RefreshCargoLabels();
        }

        private void OnCargoDropped(CargoItem cargo) => dropNoticeUntil = Time.unscaledTime + 1.2f;

        private void RefreshCargoLabels()
        {
            var condition = CurrentCondition;
            float fraction = condition && condition.Maximum > 0 ? condition.Current / condition.Maximum : 0;
            conditionLabel.text = condition && mission && !mission.IsComplete ? $"CARGO CONDITION   {fraction * 100f:0}%" : "";
            gripLabel.text = player && player.CarriedCargo && grip ? $"GRIP   {grip.Percent:0}%" : "";
        }
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (!isActiveAndEnabled || !player || !mission || !panel) return false;
            return survivorHud.ContainsScreenPoint(screenPoint) || RectTransformUtility.RectangleContainsScreenPoint(panel, screenPoint) ||
                (prompt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(prompt.rectTransform, screenPoint));
        }
        private void Update()
        {
            if (!player || !mission || !objective) return;
            CargoItem carried = player.CarriedCargo;
            objective.text = mission.SurvivorLost ? "MISSION FAILED" :
                mission.IsComplete ? (mission.RecoveryFailed ? "RECOVERY FAILED / ESCAPED" : "MISSION COMPLETE") :
                mission.CargoDestroyed ? "ESCAPE ALIVE" : mission.IsPrimaryCargo(carried) ? "DELIVER CARGO" : "FIND THE CARGO";
            var condition = CurrentCondition;
            BindCondition(condition);
            float fraction = condition ? condition.Current / condition.Maximum : 0f;
            RefreshCargoLabels();
            conditionLabel.color = Time.unscaledTime < conditionFlashUntil ? DamageRed : Muted;
            gripLabel.color = Time.unscaledTime < gripFlashUntil ? GripOrange : Muted;
            warning.text = mission.SurvivorLost ? "Survivor lost. Mission failed." :
                mission.IsComplete && !mission.RecoveryFailed ? "Cargo secured. Mission success." :
                mission.CargoDestroyed && !mission.IsComplete ? "Cargo destroyed. Escape alive." :
                extractionMarker && extractionMarker.MissingCargoPrompt ? "REQUIRED CARGO MISSING" :
                Time.unscaledTime < dropNoticeUntil ? "CARGO DROPPED" :
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
            panel.sizeDelta = new Vector2(370, 225); box.GetComponent<Image>().color = new Color(.035f, .055f, .075f, .96f);
            var mint = new Color(.52f, .94f, .72f); var muted = new Color(.78f, .84f, .87f);
            objective = Row("Objective", "", 22, 10, Color.white);
            conditionLabel = Row("Condition", "", 18, 42, muted);
            gripLabel = Row("Grip", "", 18, 70, muted);
            warning = Row("Warning", "", 15, 98, new Color(1f, .76f, .47f));
            panel.sizeDelta = new Vector2(370, 138);
            survivorHud = survivorHudPrefab ? Instantiate(survivorHudPrefab, canvasObject.transform) :
                new GameObject("SurvivorHud", typeof(RectTransform), typeof(SurvivorHud)).GetComponent<SurvivorHud>();
            if (!survivorHudPrefab) survivorHud.transform.SetParent(canvasObject.transform, false);
            var obj = new GameObject("CargoPrompt", typeof(RectTransform)); obj.layer = 5;
            var rect = obj.GetComponent<RectTransform>(); rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 178); rect.sizeDelta = new Vector2(620, 40);
            prompt = UiTypography.Apply(obj, "", 22, Color.white); prompt.gameObject.SetActive(false);
        }
        private void OnDestroy() { if (canvasObject) Destroy(canvasObject); }
    }
}
