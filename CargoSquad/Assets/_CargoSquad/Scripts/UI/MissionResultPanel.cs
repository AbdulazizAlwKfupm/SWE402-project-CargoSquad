using CargoSquad.AI;
using CargoSquad.Cargo;
using CargoSquad.Combat;
using CargoSquad.Missions;
using CargoSquad.Player;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CargoSquad.UI
{
    /// <summary>Presentation and modal pause only. CargoExtraction owns the immutable outcome.</summary>
    public sealed class MissionResultPanel : MonoBehaviour
    {
        [SerializeField] private CargoExtraction mission;
        [SerializeField] private CargoInteractor survivor;
        [SerializeField] private Color successColor = new Color(.25f, .94f, .85f);
        [SerializeField] private Color escapeColor = new Color(1, .75f, .35f);
        [SerializeField] private Color failureColor = new Color(1, .35f, .32f);
        [SerializeField, Min(.01f)] private float fadeDuration = .25f;
        [SerializeField] private string successHeading = "MISSION COMPLETE";
        [SerializeField] private string escapeHeading = "ESCAPED — CARGO LOST";
        [SerializeField] private string failureHeading = "MISSION FAILED";
        private CanvasGroup group;
        private Canvas canvas;
        private TMP_Text heading, subtitle, details, footer;
        private Image accent, buttonImage;
        private TMP_Text buttonText;
        private float shownAt, previousScale;
        private bool ownsPause, restarting;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;
        public bool Visible { get; private set; }
        public Button RestartButton { get; private set; }
        public string Heading => heading ? heading.text : "";
        public string Details => details ? details.text : "";
        public float Opacity => group ? group.alpha : 0;

        public void Bind(CargoExtraction existingMission, CargoInteractor player) { mission = existingMission; survivor = player; }
        private void Awake() => BuildInterface();
        private void OnEnable()
        {
            if (!mission) mission = FindFirstObjectByType<CargoExtraction>();
            if (!survivor) survivor = FindFirstObjectByType<CargoInteractor>();
            if (mission) { mission.ResultFinalized += Show; if (mission.Result.Outcome != MissionOutcome.None) Show(mission.Result); }
        }
        private void OnDisable()
        {
            if (mission) mission.ResultFinalized -= Show;
            if (!Visible && canvas) canvas.gameObject.SetActive(false);
        }
        private void Show(MissionResult result)
        {
            if (Visible) return;
            Visible = true; shownAt = Time.unscaledTime;
            Color color = result.Outcome == MissionOutcome.CargoRecovered ? successColor :
                result.Outcome == MissionOutcome.EscapedCargoLost ? escapeColor : failureColor;
            heading.text = result.Outcome == MissionOutcome.CargoRecovered ? successHeading :
                result.Outcome == MissionOutcome.EscapedCargoLost ? escapeHeading : failureHeading;
            subtitle.text = result.Outcome == MissionOutcome.CargoRecovered ? "Cargo Successfully Extracted" :
                result.Outcome == MissionOutcome.EscapedCargoLost ? "Survivor Evacuated" : "Survivor Eliminated";
            details.text = $"SURVIVOR    {result.Survivor.ToUpperInvariant()}\n" +
                $"CARGO    {result.CargoOutcome}\n" +
                $"CARGO CONDITION    {result.CargoCondition:0} / {result.CargoMaximum:0}\n" +
                $"SURVIVOR HEALTH    {result.Health:0} / {result.MaximumHealth:0}" +
                (result.Outcome == MissionOutcome.SurvivorKilled ? "\nREASON    Survivor killed" : "");
            heading.color = accent.color = buttonImage.color = color;
            buttonText.text = result.Outcome == MissionOutcome.SurvivorKilled ? "RETRY MISSION" : "RESTART MISSION";
            footer.text = "C A R G O   S Q U A D   /   M I S S I O N   R E P O R T";
            foreach (var other in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (other != canvas && other.isRootCanvas) other.enabled = false;
            var body = survivor ? survivor.GetComponent<Rigidbody>() : null;
            if (body) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            if (survivor)
            {
                survivor.GetComponent<CharacterAbility>().enabled = false;
                survivor.GetComponent<PlayerWeapon>().enabled = false;
                survivor.GetComponent<PlayerController>().enabled = false;
                survivor.enabled = false;
            }
            foreach (var wolf in FindObjectsByType<MutatedWolfAI>(FindObjectsSortMode.None)) wolf.enabled = false;
            previousScale = Time.timeScale; Time.timeScale = 0; ownsPause = true;
            previousCursorVisible = Cursor.visible; previousCursorLock = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            canvas.gameObject.SetActive(true); group.alpha = 0; group.interactable = group.blocksRaycasts = true;
        }
        private void Update()
        {
            if (Visible) group.alpha = Mathf.SmoothStep(0, 1, (Time.unscaledTime - shownAt) / fadeDuration);
        }
        public void RestartMission()
        {
            if (!Visible || restarting) return;
            restarting = true; RestartButton.interactable = false;
            // A fresh scene reconstructs selection, ammo, health, cargo, navigation and UI once.
            Time.timeScale = 1; ownsPause = false;
            SceneManager.LoadScene(gameObject.scene.path, LoadSceneMode.Single);
        }
        private void OnDestroy()
        {
            if (!ownsPause) return;
            Time.timeScale = previousScale > 0 ? previousScale : 1;
            Cursor.visible = previousCursorVisible; Cursor.lockState = previousCursorLock;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5; rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static TMP_Text Text(string name, Transform parent, string value, Vector2 size, Vector2 position, int fontSize, Color color)
            => UiTypography.Apply(Rect(name, parent, size, position).gameObject, value, fontSize, color);
        private void BuildInterface()
        {
            var host = new GameObject("MissionResultCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            host.layer = 5; host.transform.SetParent(transform, false); canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
            group = host.GetComponent<CanvasGroup>();
            var scaler = host.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = Rect("ResultBackground", host.transform, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            background.rectTransform.anchorMin = Vector2.zero; background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            background.color = new Color(.018f, .032f, .045f, .96f); background.raycastTarget = true;
            var content = Rect("Report", background.transform, new Vector2(1000, 620), Vector2.zero);
            accent = Rect("Accent", content, new Vector2(90, 4), new Vector2(0, 266)).gameObject.AddComponent<Image>(); accent.raycastTarget = false;
            footer = Text("ReportLabel", content, "", new Vector2(900, 26), new Vector2(0, 230), 13, new Color(.6f, .7f, .73f));
            heading = Text("Heading", content, "", new Vector2(1000, 75), new Vector2(0, 157), 46, Color.white);
            subtitle = Text("Subtitle", content, "", new Vector2(900, 40), new Vector2(0, 100), 24, Color.white);
            details = Text("Details", content, "", new Vector2(850, 200), new Vector2(0, -35), 21, new Color(.78f, .84f, .87f));
            details.lineSpacing = 14;
            buttonImage = Rect("RestartButton", content, new Vector2(360, 58), new Vector2(0, -218)).gameObject.AddComponent<Image>();
            RestartButton = buttonImage.gameObject.AddComponent<Button>(); RestartButton.targetGraphic = buttonImage;
            buttonText = Text("RestartLabel", buttonImage.transform, "", new Vector2(350, 55), Vector2.zero, 21, new Color(.02f, .04f, .05f));
            RestartButton.onClick.AddListener(RestartMission);
            host.SetActive(false);
        }
    }
}
