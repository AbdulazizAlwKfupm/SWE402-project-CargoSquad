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
        private GameObject canvasObject;
        private RectTransform panel;
        private TMP_Text objective, healthLabel, staminaLabel, conditionLabel, gripLabel, warning, prompt;
        private Image healthFill;
        private Health health;
        private CargoGrip grip;
        private CargoCondition observedCondition;
        private float targetHealthFraction, displayedHealthFraction, healthVelocity;
        private float previousHealth, previousCondition, previousGrip;
        private float healthFlashUntil, conditionFlashUntil, gripFlashUntil, dropNoticeUntil;
        private static readonly Color Muted = new Color(.78f, .84f, .87f);
        private static readonly Color DamageRed = new Color(1f, .28f, .28f);
        private static readonly Color GripOrange = new Color(1f, .64f, .23f);
        private float staminaFraction;
        private CargoSquad.Combat.PlayerWeapon weapon;
        private TMP_Text ammoLabel;
        private CargoSquad.Visuals.ExtractionMarker extractionMarker;
        private void OnEnable()
        {
            EnsureInterface(); canvasObject.SetActive(true);
            if (stamina) stamina.Changed += UpdateStamina;
            if (stamina) UpdateStamina(stamina.Current, stamina.Maximum);
            health = player ? player.GetComponent<Health>() : null;
            weapon = player ? player.GetComponent<CargoSquad.Combat.PlayerWeapon>() : null;
            extractionMarker = FindFirstObjectByType<CargoSquad.Visuals.ExtractionMarker>();
            if (weapon) weapon.Changed += UpdateAmmo;
            UpdateAmmo();
            grip = player ? player.GetComponent<CargoGrip>() : null;
            if (health)
            {
                previousHealth = health.Current;
                health.Changed += UpdateHealth;
                UpdateHealth(health.Current, health.Maximum);
                displayedHealthFraction = targetHealthFraction;
                DrawHealth();
            }
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
            if (stamina) stamina.Changed -= UpdateStamina;
            if (health) health.Changed -= UpdateHealth;
            if (weapon) weapon.Changed -= UpdateAmmo;
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
        private void UpdateStamina(float current, float maximum) => staminaFraction = current / maximum;
        private void UpdateAmmo() { if (ammoLabel) ammoLabel.text = weapon ? weapon.HudText : ""; }
        private CargoCondition CurrentCondition => player && player.CarriedCargo
            ? player.CarriedCargo.Condition : mission ? mission.PrimaryCondition : null;

        private void UpdateHealth(float current, float maximum)
        {
            if (current < previousHealth) healthFlashUntil = Time.unscaledTime + .16f;
            previousHealth = current;
            targetHealthFraction = maximum > 0 ? current / maximum : 0;
            healthLabel.text = $"HEALTH   {current:0} / {maximum:0} HP";
            DrawHealth();
        }

        private void DrawHealth()
        {
            healthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(displayedHealthFraction), 1);
            healthFill.color = Time.unscaledTime < healthFlashUntil ? DamageRed :
                targetHealthFraction > .6f ? new Color(.36f, .86f, .53f) :
                targetHealthFraction > .3f ? new Color(1f, .73f, .25f) : DamageRed;
        }

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
            return RectTransformUtility.RectangleContainsScreenPoint(panel, screenPoint) ||
                (prompt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(prompt.rectTransform, screenPoint));
        }
        private void Update()
        {
            if (!player || !mission || !objective) return;
            CargoItem carried = player.CarriedCargo;
            objective.text = mission.SurvivorLost ? "MISSION FAILED" :
                mission.IsComplete ? (mission.RecoveryFailed ? "RECOVERY FAILED / ESCAPED" : "MISSION COMPLETE") :
                mission.CargoDestroyed ? "ESCAPE ALIVE" : mission.IsPrimaryCargo(carried) ? "DELIVER CARGO" : "FIND THE CARGO";
            staminaLabel.text = stamina ? $"STAMINA   {staminaFraction * 100f:0}%" : "";
            var condition = CurrentCondition;
            BindCondition(condition);
            float fraction = condition ? condition.Current / condition.Maximum : 0f;
            RefreshCargoLabels();
            conditionLabel.color = Time.unscaledTime < conditionFlashUntil ? DamageRed : Muted;
            gripLabel.color = Time.unscaledTime < gripFlashUntil ? GripOrange : Muted;
            displayedHealthFraction = Mathf.SmoothDamp(displayedHealthFraction, targetHealthFraction,
                ref healthVelocity, .06f, Mathf.Infinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(displayedHealthFraction - targetHealthFraction) < .001f) displayedHealthFraction = targetHealthFraction;
            DrawHealth();
            if (ammoLabel) ammoLabel.text = weapon ? weapon.HudText : "";
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
            healthLabel = Row("Health", "", 20, 42, muted);
            var bar = new GameObject("HealthBar", typeof(RectTransform), typeof(Image)); bar.layer = 5;
            var barRect = bar.GetComponent<RectTransform>(); barRect.SetParent(panel, false);
            barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0, 1);
            barRect.anchoredPosition = new Vector2(16, -76); barRect.sizeDelta = new Vector2(334, 8);
            bar.GetComponent<Image>().color = new Color(.16f, .20f, .22f);
            bar.GetComponent<Image>().raycastTarget = false;
            var fill = new GameObject("HealthFill", typeof(RectTransform), typeof(Image)); fill.layer = 5;
            healthFill = fill.GetComponent<Image>(); healthFill.raycastTarget = false;
            var fillRect = healthFill.rectTransform; fillRect.SetParent(barRect, false);
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            staminaLabel = Row("Stamina", "", 20, 87, mint);
            conditionLabel = Row("Condition", "", 18, 119, muted);
            gripLabel = Row("Grip", "", 18, 149, muted);
            warning = Row("Warning", "", 15, 182, new Color(1f, .76f, .47f));
            // A compact row underneath the existing condition/grip panel preserves its typography.
            panel.sizeDelta = new Vector2(370, 260);
            ammoLabel = Row("Ammo", "", 17, 218, mint);
            var obj = new GameObject("CargoPrompt", typeof(RectTransform)); obj.layer = 5;
            var rect = obj.GetComponent<RectTransform>(); rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 20); rect.sizeDelta = new Vector2(620, 40);
            prompt = UiTypography.Apply(obj, "", 22, Color.white); prompt.gameObject.SetActive(false);
        }
        private void OnDestroy() { if (canvasObject) Destroy(canvasObject); }
    }
}
