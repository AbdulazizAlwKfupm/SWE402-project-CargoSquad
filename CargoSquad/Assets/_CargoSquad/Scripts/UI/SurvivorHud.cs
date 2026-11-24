using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.Visuals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CargoSquad.UI
{
    /// <summary>Read-only presentation of the selected survivor. Never owns gameplay state or timers.</summary>
    [DisallowMultipleComponent]
    public sealed class SurvivorHud : MonoBehaviour
    {
        [SerializeField] private Color panelColor = new Color(.025f,.045f,.06f,.96f);
        [SerializeField] private Color healthColor = new Color(.36f,.86f,.53f);
        [SerializeField] private Color staminaColor = new Color(.29f,.78f,.90f);
        private Health health;
        private PlayerStamina stamina;
        private CharacterAbility ability;
        private CargoInteractor carrier;
        private CharacterSelection selection;
        private PlayerWeapon weapon;
        private RectTransform panel;
        private TMP_Text healthText, staminaText, characterName, ammoText, abilityName, cooldownText;
        private Image healthFill, staminaFill;
        private AbilityHudIcon abilityIcon;
        private RawImage portraitImage;
        private HologramHeadPreview portrait;
        private SurvivorVisual portraitVisual;
        private float targetHealth, shownHealth, healthVelocity, targetStamina, shownStamina, staminaVelocity;
        private float previousHealth, flashUntil;
        private bool built;
        public Image HealthFill => healthFill;
        public Image StaminaFill => staminaFill;
        public AbilityHudIcon AbilityIcon => abilityIcon;
        public TMP_Text CooldownText => cooldownText;
        public HologramHeadPreview Portrait => portrait;
        public float HealthFraction => targetHealth;
        public float StaminaFraction => targetStamina;

        public void Bind(Health hp, PlayerStamina energy, CharacterAbility power, CargoInteractor cargo, CharacterSelection choice)
        {
            Unsubscribe(); Build(); health = hp; stamina = energy; ability = power; carrier = cargo; selection = choice;
            weapon = cargo ? cargo.GetComponent<PlayerWeapon>() : null;
            if (weapon) weapon.Changed += RefreshAmmo;
            RefreshAmmo();
            if (health)
            {
                previousHealth = health.Current; health.Changed += OnHealth;
                OnHealth(health.Current, health.Maximum); shownHealth = targetHealth;
            }
            if (stamina) { stamina.Changed += OnStamina; OnStamina(stamina.Current, stamina.Maximum); shownStamina = targetStamina; }
            RefreshPortrait(); Draw(); RefreshAbility();
        }
        private void OnHealth(float current, float maximum)
        {
            if (current < previousHealth) flashUntil = Time.unscaledTime + .16f;
            previousHealth = current;
            targetHealth = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            healthText.text = $"HEALTH   {current:0} / {maximum:0} HP";
            Draw();
        }
        private void OnStamina(float current, float maximum)
        {
            targetStamina = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            staminaText.text = $"STAMINA   {current:0} / {maximum:0}";
        }
        private void Update()
        {
            if (!built) return;
            shownHealth = Smooth(shownHealth, targetHealth, ref healthVelocity);
            shownStamina = Smooth(shownStamina, targetStamina, ref staminaVelocity);
            RefreshPortrait(); RefreshAbility(); RefreshAmmo(); Draw();
        }
        static float Smooth(float value, float target, ref float velocity)
        {
            value = Mathf.SmoothDamp(value, target, ref velocity, .06f, Mathf.Infinity, Time.unscaledDeltaTime);
            return Mathf.Abs(value - target) < .001f ? target : value;
        }
        private void Draw()
        {
            if (!built) return;
            healthFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(shownHealth), 1);
            staminaFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(shownStamina), 1);
            healthFill.color = Time.unscaledTime < flashUntil || targetHealth <= .3f ? new Color(1,.28f,.28f) :
                targetHealth <= .6f ? new Color(1,.73f,.25f) : healthColor;
            staminaFill.color = targetStamina <= .2f ? new Color(1,.73f,.25f) : staminaColor;
        }
        private void RefreshAbility()
        {
            if (!ability) { abilityName.text = "ABILITY"; cooldownText.text = "UNAVAILABLE"; return; }
            bool down = health && health.IsDead;
            bool carrying = carrier && carrier.CarriedCargo;
            bool blocked = down || carrying || ability.IsCharging;
            float remaining = ability.CooldownRemaining;
            float duration = ability.CooldownDuration;
            bool ready = remaining <= 0f && !blocked;
            abilityIcon.SetState(ability.Kind, duration > 0 ? remaining / duration : 0, ready);
            abilityName.text = ability.Kind == SurvivorKind.Heavy ? "SHOULDER BASH" : "SECOND WIND";
            cooldownText.text = down ? "DOWNED" : carrying ? "CARRYING" : remaining > 0f ? $"{remaining:0.0}s" : ability.IsCharging ? "ACTIVE" : "READY";
            cooldownText.color = ready ? new Color(.52f,.94f,.72f) : new Color(.77f,.85f,.89f);

        }
        private void RefreshAmmo()
        {
            string value = weapon ? weapon.HudText : "";
            if (ammoText.text != value) ammoText.text = value;
        }
        private void RefreshPortrait()
        {
            if (!selection || !selection.HasStarted || !selection.ActiveVisual) return;
            var visual = selection.ActiveVisual;
            if (portraitVisual == visual && portrait && portrait.Texture) return;
            if (portrait) Destroy(portrait);
            portraitVisual = visual;
            characterName.text = selection.SelectedIndex == 0 ? "HEAVY" : "MEDIC";
            portrait = portraitImage.gameObject.AddComponent<HologramHeadPreview>();
            portrait.Initialize(visual, selection.SelectedIndex == 0 ? "HV4_J_HEAD" : "MR1_J_HEAD", portraitImage, 2, null);
        }
        public bool ContainsScreenPoint(Vector2 position) => isActiveAndEnabled && panel &&
            RectTransformUtility.RectangleContainsScreenPoint(panel, position);
        private RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.layer = 5;
            var rect = obj.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }
        private Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool blocks = false)
        {
            var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = blocks; return image;
        }
        private TMP_Text Text(string name, Transform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color)
            => UiTypography.Apply(Rect(name, parent, position, size).gameObject, value, fontSize, color, TextAlignmentOptions.MidlineLeft);
        private Image Bar(string name, Vector2 position, Color color)
        {
            var bar = Box(name + "Track", panel, position, new Vector2(244, 9), new Color(.12f,.18f,.21f));
            var fill = Box(name + "Fill", bar.transform, Vector2.zero, Vector2.zero, color);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            // Small tick marks distinguish the meters without decorative clutter.
            for (int i = 1; i < 4; i++) Box(name + "Tick" + i, bar.transform, new Vector2(244 * i / 4f, 0), new Vector2(1, 9), new Color(.025f,.045f,.06f,.65f));
            return fill;
        }
        private void Build()
        {
            if (built) return; built = true;
            gameObject.layer = 5;
            panel = GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(16,16); panel.sizeDelta = new Vector2(484,132);
            var background = gameObject.GetComponent<Image>();
            if (!background) background = gameObject.AddComponent<Image>();
            background.color = panelColor; background.raycastTarget = true;
            Box("Accent", panel, new Vector2(0,130), new Vector2(484,2), new Color(.30f,.72f,.83f,.8f));
            var portraitFrame = Box("PortraitFrame", panel, new Vector2(8,10), new Vector2(86,112), new Color(.025f,.07f,.085f));
            portraitImage = Rect("SelectedHead", portraitFrame.transform, new Vector2(2,25), new Vector2(82,82)).gameObject.AddComponent<RawImage>();
            portraitImage.raycastTarget = false;
            Text("PortraitCaption", portraitFrame.transform, new Vector2(6,4), new Vector2(78,17), "LIVE / LINK", 10, new Color(.43f,.82f,.86f));
            var orbit = Rect("HeadOrbit", portraitFrame.transform, new Vector2(5,19), new Vector2(80,8)).gameObject.AddComponent<HologramOrbit>();
            orbit.rectTransform.pivot = new Vector2(.5f,.5f);
            orbit.rectTransform.anchoredPosition = new Vector2(43,27);
            orbit.gameObject.AddComponent<CanvasRenderer>();
            orbit.SetAllDirty();
            orbit.color = new Color(.35f,.85f,1f,.5f); orbit.raycastTarget = false;
            characterName = Text("CharacterName", panel, new Vector2(104,104), new Vector2(244,22), "SURVIVOR", 19, Color.white);
            healthText = Text("Health", panel, new Vector2(104,82), new Vector2(244,18), "HEALTH", 16, new Color(.87f,.93f,.92f));
            healthFill = Bar("Health", new Vector2(104,70), healthColor);
            staminaText = Text("Stamina", panel, new Vector2(104,46), new Vector2(244,18), "STAMINA", 16, new Color(.67f,.86f,.90f));
            staminaFill = Bar("Stamina", new Vector2(104,34), staminaColor);
            ammoText = Text("Ammo", panel, new Vector2(104,8), new Vector2(244,19), "", 14, new Color(.77f,.90f,.84f));
            ammoText.enableAutoSizing = true; ammoText.fontSizeMin = 11; ammoText.fontSizeMax = 14;
            ammoText.textWrappingMode = TextWrappingModes.NoWrap;
            var tile = Box("AbilityStatus", panel, new Vector2(362,10), new Vector2(112,112), new Color(.055f,.105f,.13f));
            abilityName = Text("AbilityTitle", tile.transform, new Vector2(6,93), new Vector2(100,16), "ABILITY", 10, new Color(.75f,.85f,.88f));
            var iconRect = Rect("AbilityIcon", tile.transform, new Vector2(34,37), new Vector2(54,54));
            iconRect.pivot = new Vector2(.5f,.5f); iconRect.anchoredPosition = new Vector2(56,63);
            abilityIcon = iconRect.gameObject.AddComponent<AbilityHudIcon>(); abilityIcon.raycastTarget = false;
            var key = Box("AbilityKey", tile.transform, new Vector2(87,74), new Vector2(18,18), new Color(.16f,.26f,.29f));
            UiTypography.Apply(Rect("KeyText", key.transform, Vector2.zero, new Vector2(18,18)).gameObject, "Q", 12, Color.white);
            cooldownText = Text("AbilityText", tile.transform, new Vector2(6,6), new Vector2(100,24), "READY", 24, new Color(.52f,.94f,.72f));
            cooldownText.alignment = TextAlignmentOptions.Center;
        }
        private void Unsubscribe()
        {
            if (weapon) weapon.Changed -= RefreshAmmo;
            if (health) health.Changed -= OnHealth;
            if (stamina) stamina.Changed -= OnStamina;
        }
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();
    }
}
