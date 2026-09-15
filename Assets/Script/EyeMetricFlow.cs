using TMPro;
using Tobii;
using UnityEngine;
using UnityEngine.UI;

// App-level flow: test mode menu, recalibration, back-to-home, calibration hints, gaze intro and the top-right HUD.
public class EyeMetricFlow : MonoBehaviour
{
    [SerializeField] private QA qa;
    [SerializeField] private MetricTest metricTest;
    [SerializeField] private GazeCalibrationManager gazeCalibrationManager;
    [SerializeField] private DetectDistance detectDistance;
    [SerializeField] private GameObject canvasTrackBox;
    [SerializeField] private GameObject mainCanvas;
    [SerializeField] private GameObject resultPage;
    [SerializeField] private FollowGazePoint2D gazePointer;
    [SerializeField] private Button exitButton;
    [SerializeField] private TMP_FontAsset font;

    // The Chinese SDF atlas has no digits, so counts inside Chinese sentences use Chinese numerals.
    private static readonly string[] ChineseNumerals = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
    private static readonly Color TextDark = new Color32(0x1F, 0x3A, 0x5F, 0xFF);
    private static readonly Color TextMuted = new Color32(0x45, 0x5A, 0x64, 0xFF);
    private static readonly Color Teal = new Color32(0x12, 0xA1, 0x93, 0xFF);
    private static readonly Color Blue = new Color32(0x1E, 0x88, 0xE5, 0xFF);
    private static readonly Color IconBackground = new Color32(0xED, 0xED, 0xED, 0xEB);
    private static readonly Color IconGlyph = new Color32(0x8A, 0x8A, 0x8A, 0xFF);

    private Canvas _flowCanvas;
    private GameObject _modeMenu;
    private GameObject _recalibrateButton;
    private GazeDebugOverlay _debugOverlay;
    private GameObject _banner;
    private TextMeshProUGUI _bannerText;
    private RectTransform _stimulusBadge;
    private TextMeshProUGUI _stimulusBadgeText;
    private GameObject _gazeIntro;

    private void Awake()
    {
        // Flow overlay sits above the main Canvas (0) but below the gaze dot canvas (10); the HUD sits above everything.
        _flowCanvas = UiFactory.CreateOverlayCanvas("FlowOverlay", 5, transform);
        var hudCanvas = UiFactory.CreateOverlayCanvas("HudOverlay", 20, transform);

        BuildCalibrationHints(_flowCanvas.transform);
        HeadDistanceGuide.Create(_flowCanvas.transform, detectDistance, canvasTrackBox, font);
        BuildGazeIntro(_flowCanvas.transform);
        BuildModeMenu(_flowCanvas.transform);
        BuildHud(hudCanvas.transform);
        BuildHomeButton(resultPage.transform);

        GazeVisual.Attach(gazePointer);
        GazeDwellIndicator.Attach(gazePointer.transform);
    }

    private void OnEnable()
    {
        qa.Completed += OnQuestionnaireCompleted;
        gazeCalibrationManager.CalibrationStarted += OnCalibrationStarted;
        gazeCalibrationManager.StimulusShown += OnStimulusShown;
        gazeCalibrationManager.StimulusCleared += OnStimulusCleared;
        gazeCalibrationManager.CalibrationEnded += HideCalibrationHints;
        gazeCalibrationManager.CalibrationFailed += OnCalibrationFailed;
        gazeCalibrationManager.GazeIntroStarted += OnGazeIntroStarted;
        gazeCalibrationManager.GazeIntroEnded += HideGazeIntro;
    }

    private void OnDisable()
    {
        qa.Completed -= OnQuestionnaireCompleted;
        gazeCalibrationManager.CalibrationStarted -= OnCalibrationStarted;
        gazeCalibrationManager.StimulusShown -= OnStimulusShown;
        gazeCalibrationManager.StimulusCleared -= OnStimulusCleared;
        gazeCalibrationManager.CalibrationEnded -= HideCalibrationHints;
        gazeCalibrationManager.CalibrationFailed -= OnCalibrationFailed;
        gazeCalibrationManager.GazeIntroStarted -= OnGazeIntroStarted;
        gazeCalibrationManager.GazeIntroEnded -= HideGazeIntro;
    }

    private void Start()
    {
        PlayTTS("請選擇要進行的測驗");
    }

    // ==================== Test mode menu ====================

    private void BuildModeMenu(Transform parent)
    {
        var background = UiFactory.CreateImage("ModeMenu", parent, qa.GetComponent<Image>().sprite, Color.white, true);
        UiFactory.Stretch(background.rectTransform);
        _modeMenu = background.gameObject;

        var title = UiFactory.CreateText("Title", background.transform, font, "視覺健康量測", 32f, Color.white);
        UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(858f, 141f));

        var prompt = UiFactory.CreateText("Prompt", background.transform, font, "請選擇要進行的測驗", 28f, TextDark);
        UiFactory.Place(prompt.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, 150f), new Vector2(700f, 50f));

        CreateModeCard(background.transform, new Vector2(-200f, -20f), "單眼測驗", "右眼、左眼分別檢測", MetricTest.TestMode.Single);
        CreateModeCard(background.transform, new Vector2(200f, -20f), "雙眼測驗", "兩眼同時檢測", MetricTest.TestMode.Both);
    }

    private void CreateModeCard(Transform parent, Vector2 position, string title, string description, MetricTest.TestMode mode)
    {
        var card = UiFactory.CreateButton(title, parent, UiFactory.RoundedRect, new Color(1f, 1f, 1f, 0.95f), () => SelectMode(mode));
        ((Image)card.targetGraphic).type = Image.Type.Sliced;
        UiFactory.Place((RectTransform)card.transform, UiFactory.Center, UiFactory.Center, position, new Vector2(340f, 230f));

        var titleText = UiFactory.CreateText("Title", card.transform, font, title, 40f, Teal);
        UiFactory.Place(titleText.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, 30f), new Vector2(300f, 60f));
        var descriptionText = UiFactory.CreateText("Description", card.transform, font, description, 22f, TextMuted);
        UiFactory.Place(descriptionText.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, -35f), new Vector2(300f, 40f));
    }

    private void SelectMode(MetricTest.TestMode mode)
    {
        metricTest.Mode = mode;
        _modeMenu.SetActive(false);
        Nuwa.stopTTS();
    }

    // ==================== HUD (top-right) ====================

    private void BuildHud(Transform parent)
    {
        // Move the existing exit button onto the HUD so full-screen overlays never cover it.
        exitButton.transform.SetParent(parent, false);
        exitButton.onClick.AddListener(ExitApp);

        _debugOverlay = GazeDebugOverlay.Create(parent, gazePointer);
        NetworkSignalIcon.Create(parent, new Vector2(-66f, -20f), 36f, IconBackground, ToggleDebugOverlay);

        var recalibrate = UiFactory.CreateButton("RecalibrateButton", parent, UiFactory.Circle, IconBackground, Recalibrate);
        UiFactory.Place((RectTransform)recalibrate.transform, Vector2.one, Vector2.one, new Vector2(-112f, -20f), new Vector2(36f, 36f));
        var glyph = UiFactory.CreateImage("Glyph", recalibrate.transform, UiFactory.RefreshArrow, IconGlyph);
        UiFactory.Place(glyph.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(28f, 28f));
        _recalibrateButton = recalibrate.gameObject;
        _recalibrateButton.SetActive(false);
    }

    private void ToggleDebugOverlay()
    {
        _debugOverlay.gameObject.SetActive(!_debugOverlay.gameObject.activeSelf);
    }

    private void OnQuestionnaireCompleted()
    {
        _recalibrateButton.SetActive(true);
    }

    private void Recalibrate()
    {
        ResetTestSession();
        mainCanvas.SetActive(true);
        canvasTrackBox.SetActive(true);
        // OpenLock also plays the head positioning instructions.
        detectDistance.OpenLock();
    }

    private void ExitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ==================== Back to home (result page) ====================

    private void BuildHomeButton(Transform parent)
    {
        // Touch only: a gaze-dwell button here could be triggered while the user reads the result.
        // Bottom-right corner sits outside the result art's panels.
        var button = UiFactory.CreateButton("HomeButton", parent, UiFactory.RoundedRect, Teal, GoHome);
        ((Image)button.targetGraphic).type = Image.Type.Sliced;
        UiFactory.Place((RectTransform)button.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(150f, 56f));
        var label = UiFactory.CreateText("Label", button.transform, font, "回到首頁", 26f, Color.white);
        UiFactory.Stretch(label.rectTransform);
    }

    private void GoHome()
    {
        ResetTestSession();
        _recalibrateButton.SetActive(false);
        mainCanvas.SetActive(true);
        canvasTrackBox.SetActive(false);
        qa.RestartQuestionnaire();
        _modeMenu.SetActive(true);
        PlayTTS("請選擇要進行的測驗");
    }

    private void ResetTestSession()
    {
        Nuwa.stopTTS();
        gazeCalibrationManager.ResetSession();
        metricTest.ResetSession();
        metricTest.gameObject.SetActive(false);
        resultPage.SetActive(false);
        gazePointer.gameObject.SetActive(false);
        HideCalibrationHints();
        HideGazeIntro();
    }

    // ==================== Calibration hints ====================

    private void BuildCalibrationHints(Transform parent)
    {
        var banner = UiFactory.CreateImage("CalibrationBanner", parent, UiFactory.RoundedRect, new Color(0f, 0f, 0f, 0.62f));
        banner.type = Image.Type.Sliced;
        // 600 wide keeps clear of the corner stimuli at 15% / 85% of the screen width.
        UiFactory.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(600f, 60f));
        _bannerText = UiFactory.CreateText("Text", banner.transform, font, "", 26f, Color.white);
        UiFactory.Stretch(_bannerText.rectTransform);
        _banner = banner.gameObject;

        var badge = UiFactory.CreateImage("StimulusBadge", parent, UiFactory.Circle, Blue);
        UiFactory.Place(badge.rectTransform, Vector2.zero, UiFactory.Center, Vector2.zero, new Vector2(46f, 46f));
        _stimulusBadgeText = UiFactory.CreateText("Number", badge.transform, TMP_Settings.defaultFontAsset, "", 28f, Color.white);
        _stimulusBadgeText.fontStyle = FontStyles.Bold;
        UiFactory.Stretch(_stimulusBadgeText.rectTransform);
        _stimulusBadge = badge.rectTransform;

        HideCalibrationHints();
    }

    private void OnCalibrationStarted()
    {
        _stimulusBadge.gameObject.SetActive(false);
        ShowBanner("請保持頭部不動，眼睛依序注視藍色圓點");
        PlayTTS("眼睛校正開始，請保持頭部不動，依序看著藍色圓點");
    }

    private void OnStimulusShown(int index, int count, Vector2 screenPosition)
    {
        if (count == 1)
        {
            _stimulusBadge.gameObject.SetActive(false);
            ShowBanner("請先注視畫面中央的藍色圓點");
            return;
        }

        ShowBanner($"請注視第{ToChinese(index)}個藍色圓點（共{ToChinese(count)}個）");
        // Put the number on the inner side of the stimulus so it never leaves the screen.
        var towardCenter = new Vector2(Mathf.Sign(Screen.width * 0.5f - screenPosition.x), Mathf.Sign(Screen.height * 0.5f - screenPosition.y));
        _stimulusBadge.anchoredPosition = screenPosition / _flowCanvas.scaleFactor + towardCenter * 64f;
        _stimulusBadgeText.text = index.ToString();
        _stimulusBadge.gameObject.SetActive(true);
    }

    private void OnStimulusCleared()
    {
        _stimulusBadge.gameObject.SetActive(false);
        ShowBanner("很好，請保持頭部不動");
    }

    private void OnCalibrationFailed()
    {
        _stimulusBadge.gameObject.SetActive(false);
        ShowBanner("校正失敗，請按右上角的按鈕重新校準");
        PlayTTS("校正失敗，請按右上角的按鈕重新校準");
    }

    private void ShowBanner(string text)
    {
        _bannerText.text = text;
        _banner.SetActive(true);
    }

    private void HideCalibrationHints()
    {
        _banner.SetActive(false);
        _stimulusBadge.gameObject.SetActive(false);
    }

    // ==================== Gaze intro ====================

    private void BuildGazeIntro(Transform parent)
    {
        var panel = UiFactory.CreateImage("GazeIntro", parent, null, new Color32(0xEA, 0xF4, 0xFA, 0xFF), true);
        UiFactory.Stretch(panel.rectTransform);

        var title = UiFactory.CreateText("Title", panel.transform, font, "這個紅點是您的視線位置", 40f, TextDark);
        UiFactory.Place(title.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, 150f), new Vector2(800f, 60f));
        var subtitle = UiFactory.CreateText("Subtitle", panel.transform, font, "請試著看看四周，準備好後請注視下方的繼續按鈕", 26f, TextMuted);
        UiFactory.Place(subtitle.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, 96f), new Vector2(900f, 44f));

        // Centred on purpose: the test's direction buttons cover the screen edges, so the gaze
        // resting here when the test starts cannot begin dwelling on one of them.
        var button = UiFactory.CreateButton("ContinueButton", panel.transform, UiFactory.RoundedRect, Teal, gazeCalibrationManager.ConfirmGazeIntro);
        ((Image)button.targetGraphic).type = Image.Type.Sliced;
        UiFactory.Place((RectTransform)button.transform, UiFactory.Center, UiFactory.Center, new Vector2(0f, -40f), new Vector2(220f, 90f));
        var label = UiFactory.CreateText("Label", button.transform, font, "繼續", 40f, Color.white);
        UiFactory.Stretch(label.rectTransform);
        button.gameObject.AddComponent<ButtonTrigger>();

        _gazeIntro = panel.gameObject;
        _gazeIntro.SetActive(false);
    }

    private void OnGazeIntroStarted()
    {
        _gazeIntro.SetActive(true);
        PlayTTS("這個紅點是您的視線位置，請試著看看四周，準備好後，請注視下方的繼續按鈕");
    }

    private void HideGazeIntro()
    {
        _gazeIntro.SetActive(false);
    }

    private static string ToChinese(int value)
    {
        return value > 0 && value < ChineseNumerals.Length ? ChineseNumerals[value] : value.ToString();
    }

    private static void PlayTTS(string text)
    {
        Nuwa.stopTTS();
        Nuwa.startTTS(text);
    }
}
