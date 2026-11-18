using CargoSquad.Cargo;
using TMPro;
using CargoSquad.Combat;
using CargoSquad.Player;
using CargoSquad.Visuals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CargoSquad.UI
{
    /// <summary>Owns the deployment pause and selects visuals/stats on the existing player.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CharacterSelection : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private SurvivorVisual heavy;
        [SerializeField] private SurvivorVisual medic;
        [SerializeField] private CharacterDefinition heavyDefinition;
        [SerializeField] private CharacterDefinition medicDefinition;
        [SerializeField] private CargoMissionOverlay gameplayOverlay;
        private GameObject selectionPanel;
        private GameObject canvasObject;
        private GameObject ownedEventSystem;
        private Image[] cardImages;
        private Outline[] outlines;
        private TMP_Text[] selectedLabels;
        private TMP_Text abilityStatus;
        private TMP_Text abilityTitle;
        private float previousTimeScale;
        private bool ownsPause;
        private static readonly Color Ink = new Color(0.035f, 0.055f, 0.075f);
        private static readonly Color Muted = new Color(0.60f, 0.68f, 0.72f);
        private static readonly Color Mint = new Color(0.52f, 0.94f, 0.72f);
        public int SelectedIndex { get; private set; }
        public bool HasStarted { get; private set; }
        public Button StartButton { get; private set; }
        public Button[] Cards { get; private set; }
        public SurvivorVisual ActiveVisual => SelectedIndex == 0 ? heavy : medic;

        private void Awake()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f; ownsPause = true;
            player.enabled = false;
            player.GetComponent<CargoInteractor>().enabled = false;
            heavy.gameObject.SetActive(false); medic.gameObject.SetActive(false);
            if (gameplayOverlay) gameplayOverlay.enabled = false;
            BuildInterface(); SelectCharacter(0);
        }

        public void SelectCharacter(int index)
        {
            if (HasStarted || index < 0 || index > 1) return;
            SelectedIndex = index;
            for (int i = 0; i < 2; i++)
            {
                cardImages[i].color = i == index ? new Color(0.09f, 0.17f, 0.18f) : new Color(0.075f, 0.095f, 0.12f);
                outlines[i].enabled = i == index;
                selectedLabels[i].text = i == index ? "SELECTED" : "SELECT SPECIALIST";
                selectedLabels[i].color = i == index ? Mint : Muted;
            }
            StartButton.GetComponentInChildren<TMP_Text>().text = "START AS " + (index == 0 ? "HEAVY" : "MEDIC");
        }

        public void StartSelectedCharacter()
        {
            if (HasStarted) return;
            var definition = SelectedIndex == 0 ? heavyDefinition : medicDefinition;
            heavy.gameObject.SetActive(SelectedIndex == 0); medic.gameObject.SetActive(SelectedIndex == 1);
            var stamina = player.GetComponent<PlayerStamina>(); stamina.SetDefinition(definition);
            player.GetComponent<Health>().ResetMaximum(definition.MaxHealth);
            player.GetComponent<CargoInteractor>().SetCarrySocket(ActiveVisual.GetComponentInChildren<CargoCarrySocket>());
            player.GetComponent<CharacterAbility>().Configure((SurvivorKind)SelectedIndex, ActiveVisual);
            player.GetComponent<PlayerWeapon>()?.Equip((SurvivorKind)SelectedIndex, ActiveVisual);
            HasStarted = true;
            selectionPanel.SetActive(false);
            abilityStatus.transform.parent.gameObject.SetActive(true);
            player.enabled = true; player.GetComponent<CargoInteractor>().enabled = true;
            if (gameplayOverlay) gameplayOverlay.enabled = true;
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f; ownsPause = false;
            EventSystem.current?.SetSelectedGameObject(null);
        }

        private void Update()
        {
            if (!HasStarted) return;
            var ability = player.GetComponent<CharacterAbility>();
            string name = SelectedIndex == 0 ? "HEAVY  /  SHOULDER BASH" : "MEDIC  /  SECOND WIND";
            string state = player.GetComponent<Health>().IsDead ? "Unavailable / downed" :
                player.GetComponent<CargoInteractor>().CarriedCargo ? "Unavailable / carrying cargo" :
                ability.CooldownRemaining > 0f ? $"Ready in {ability.CooldownRemaining:0.0}s" : "Q  /  Ready";
            abilityTitle.text = name;
            abilityStatus.text = state;
        }

        private RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.gameObject.layer = 5; r.SetParent(parent, false); r.sizeDelta = size; r.anchoredPosition = position; return r;
        }
        private Image Panel(string name, Transform parent, Vector2 size, Vector2 position, Color color, bool blocks = false)
        {
            var r = Rect(name, parent, size, position); var image = r.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = blocks; return image;
        }
        private TMP_Text Label(string name, Transform parent, string text, Vector2 size, Vector2 position, int fontSize, Color color)
        {
            return UiTypography.Apply(Rect(name, parent, size, position).gameObject, text, fontSize, color);
        }
        private void BuildInterface()
        {
            canvasObject = new GameObject("DeploymentCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.layer = 5; canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<Canvas>().sortingOrder = 100;
            canvasObject.GetComponent<Canvas>().pixelPerfect = true;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = Panel("CharacterSelection", canvasObject.transform, Vector2.zero, Vector2.zero, Ink, true);
            selectionPanel = background.gameObject;
            background.rectTransform.anchorMin = Vector2.zero; background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            var content = Rect("Content", background.transform, new Vector2(1000, 740), Vector2.zero);
            Panel("TopRule", content, new Vector2(74, 3), new Vector2(0, 327), Mint);
            Label("Eyebrow", content, "C A R G O  S Q U A D   /   D E P L O Y M E N T", new Vector2(850, 26), new Vector2(0, 298), 13, Mint);
            Label("Title", content, "Choose your specialist", new Vector2(900, 65), new Vector2(0, 244), 42, Color.white);
            Label("Subtitle", content, "Choose your role. Press Start to begin the mission.", new Vector2(850, 35), new Vector2(0, 194), 17, Muted);
            Cards = new Button[2]; cardImages = new Image[2]; outlines = new Outline[2]; selectedLabels = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                var card = Panel(i == 0 ? "HeavyCard" : "MedicCard", content, new Vector2(350, 385), new Vector2(i == 0 ? -190 : 190, -27), Color.white, true);
                cardImages[i] = card; Cards[i] = card.gameObject.AddComponent<Button>();
                Cards[i].targetGraphic = card; var colors = Cards[i].colors; colors.highlightedColor = new Color(.85f, 1f, .95f); Cards[i].colors = colors;
                Cards[i].onClick.AddListener(() => SelectCharacter(index));
                outlines[i] = card.gameObject.AddComponent<Outline>(); outlines[i].effectColor = Mint; outlines[i].effectDistance = new Vector2(2, -2);
                Label("Number", card.transform, "0" + (i + 1), new Vector2(50, 25), new Vector2(-125, 155), 14, Muted);
                var portrait = Rect("HeadPortrait", card.transform, new Vector2(205, 205), new Vector2(0, 64)).gameObject.AddComponent<RawImage>();
                portrait.raycastTarget = false;
                var orbit = Rect("HologramOrbit", card.transform, new Vector2(205, 24), new Vector2(0, -30)).gameObject.AddComponent<HologramOrbit>();
                orbit.color = new Color(.35f, .85f, 1f, .55f); orbit.raycastTarget = false;
                var scan = Panel("HologramScan", card.transform, new Vector2(182, 1), new Vector2(0, 60), new Color(.4f, 1f, 1f, .18f));
                var live = portrait.gameObject.AddComponent<HologramHeadPreview>();
                live.Initialize(i == 0 ? heavy : medic, i == 0 ? "HV4_J_HEAD" : "MR1_J_HEAD", portrait, i, scan.rectTransform);
                Label("Name", card.transform, i == 0 ? "Heavy" : "Medic", new Vector2(290, 42), new Vector2(0, -58), 32, Color.white);
                Label("Role", card.transform, i == 0 ? "BREAK THE LINE" : "KEEP MOVING", new Vector2(290, 24), new Vector2(0, -96), 16, Mint);
                Label("Description", card.transform, i == 0 ? "Armored shoulder bash\nSlower pace. Greater carrying strength." : "Restore stamina to 100\nFaster pace. A second wind on demand.",
                    new Vector2(335, 58), new Vector2(0, -137), 16, Muted);
                selectedLabels[i] = Label("Selection", card.transform, "", new Vector2(290, 25), new Vector2(0, -176), 15, Mint);
            }
            var start = Panel("StartButton", content, new Vector2(330, 58), new Vector2(0, -285), Mint, true);
            StartButton = start.gameObject.AddComponent<Button>(); StartButton.targetGraphic = start;
            Label("StartLabel", start.transform, "", new Vector2(320, 55), Vector2.zero, 21, Ink);
            StartButton.onClick.AddListener(StartSelectedCharacter);
            Label("Controls", content, "WASD Move / SHIFT Sprint / E Cargo / Q Ability / HOLD LMB Aim / RELEASE Fire / R Reload", new Vector2(1000, 30), new Vector2(0, -342), 16, Muted);
            var hud = Panel("AbilityStatus", canvasObject.transform, new Vector2(370, 88), Vector2.zero, new Color(.035f,.055f,.075f,.9f), true);
            hud.rectTransform.anchorMin = hud.rectTransform.anchorMax = Vector2.one;
            hud.rectTransform.pivot = Vector2.one; hud.rectTransform.anchoredPosition = new Vector2(-14, -14);
            abilityTitle = Label("AbilityTitle", hud.transform, "", new Vector2(344, 26), new Vector2(0, 23), 16, Muted);
            abilityStatus = Label("AbilityText", hud.transform, "", new Vector2(344, 43), new Vector2(0, -13), 24, Mint);
            hud.gameObject.SetActive(false);
            if (!EventSystem.current)
            {
                ownedEventSystem = new GameObject("DeploymentEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ownedEventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }
        private void OnDestroy()
        {
            if (ownsPause) Time.timeScale = previousTimeScale;
            if (ownedEventSystem) Destroy(ownedEventSystem);
        }
    }
}
