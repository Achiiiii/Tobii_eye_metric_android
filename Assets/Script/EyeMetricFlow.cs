using System.Collections;
using TMPro;
using Tobii;
using UnityEngine;
using UnityEngine.UI;

// App-level flow: test mode menu, recalibration, back-to-home, stage transitions, calibration hints,
// gaze intro, the top-right HUD and the robot's system alert bar.
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

    private const float HeadConfirmedSeconds = 1.8f;
    private const float CountdownFadeSeconds = 0.25f;
    private const float PerfLogSeconds = 5f;

    // The Chinese SDF atlas has no digits, so counts inside Chinese sentences use Chinese numerals.
    private static readonly string[] ChineseNumerals = { "", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
    private static readonly Color TextDark = new Color32(0x1F, 0x3A, 0x5F, 0xFF);
    // Descriptions used to be dark grey on the outlined font material and were hard to read.
    private static readonly Color DescriptionText = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    private static readonly Color Teal = new Color32(0x12, 0xA1, 0x93, 0xFF);
    // The numbered tag beside each calibration dot: amber so it cannot be mistaken for the
    // blue stimulus it labels, nor for the red gaze dot.
    private static readonly Color BadgeFill = new Color32(0xF5, 0xA6, 0x23, 0xFF);
    private static readonly Color BadgeEdge = new Color32(0x8A, 0x55, 0x00, 0xFF);
    private static readonly Color BadgeText = new Color32(0x2A, 0x1A, 0x00, 0xFF);
    private static readonly Color SuccessGreen = new Color32(0x2E, 0x9E, 0x5A, 0xFF);
    private static readonly Color CancelGrey = new Color32(0x90, 0xA4, 0xAE, 0xFF);
    private static readonly Color IconBackground = new Color32(0xED, 0xED, 0xED, 0xEB);
    private static readonly Color IconGlyph = new Color32(0x8A, 0x8A, 0x8A, 0xFF);

    private Canvas _flowCanvas;
    private GameObject _modeMenu;
    private GameObject _recalibrateButton;
    private GameObject _exitDialog;
    private GazeDebugOverlay _debugOverlay;
    private HeadFollow _headFollow;
    private DriftCorrector _driftCorrector;
    private GameObject _banner;
    private TextMeshProUGUI _bannerText;
    private RectTransform _stimulusBadge;
    private TextMeshProUGUI _stimulusBadgeText;
    private GameObject _gazeIntro;
    private GameObject _headConfirmed;
    private RectTransform _headConfirmedBadge;
    private CanvasGroup _testCountdown;
    private TextMeshProUGUI _testCountdownTitle;
    private TextMeshProUGUI _testCountdownNumber;
    private AudioSource _sfx;
    private AudioClip _successClip;
    private Coroutine _headConfirmedRoutine;
    private Coroutine _testCountdownRoutine;
    private volatile bool _robotServiceStarted;
    private GazeLatencyStats.Snapshot _perfPrevious;
    private float _perfPreviousTime;
    private int _perfPreviousFrame;
    private long _perfPreviousDropped;

    private void Awake()
    {
        // Logs and warnings without stack traces: on the device each one cost a managed stack walk
        // and made ~3 of every 4 logcat lines. Errors and exceptions keep theirs.
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        // MetricTest swaps its selection sound for a neutral tick; keep the original chime for successes.
        _successClip = metricTest.audioSource.clip;

        // Flow overlay sits above the main Canvas (0) but below the gaze dot canvas (10); the HUD sits above everything.
        _flowCanvas = UiFactory.CreateOverlayCanvas("FlowOverlay", 5, transform);
        var hudCanvas = UiFactory.CreateOverlayCanvas("HudOverlay", 20, transform);

        QuestionnaireText.Apply(qa, font);
        BuildCalibrationHints(_flowCanvas.transform);
        // Created before the head distance guide, which shows its eye-level notice.
        _headFollow = HeadFollow.Create(transform, gazeCalibrationManager, metricTest, resultPage);
        detectDistance.HeadFollow = _headFollow;
        HeadDistanceGuide.Create(_flowCanvas.transform, detectDistance, canvasTrackBox, font, _headFollow);
        BuildHeadConfirmed(_flowCanvas.transform);
        BuildGazeIntro(_flowCanvas.transform);
        BuildTestCountdown(_flowCanvas.transform);
        BuildModeMenu(_flowCanvas.transform);
        // Above the gaze dot (10), below the HUD (20), so exit and recalibrate stay usable.
        var guideCanvas = UiFactory.CreateOverlayCanvas("GuideOverlay", 15, transform);
        PositionGuide.Create(guideCanvas.transform, font, _headFollow, metricTest);
        _driftCorrector = DriftCorrector.Create(transform, gazePointer, metricTest, _headFollow, gazeCalibrationManager);
        OptionReveal.Create(transform, metricTest, _driftCorrector, _headFollow, gazePointer);
        // Focuses the camera on the user's face at each head distance check.
        FaceFocus.Create(transform, detectDistance, _headFollow, FindObjectOfType<AndroidWebcamCaptureClient>());
        BuildHud(hudCanvas.transform);
        BuildHomeButton(resultPage.transform);

        GazeVisual.Attach(gazePointer);
        GazeDwellIndicator.Attach(gazePointer.transform);
    }

    private void OnEnable()
    {
        qa.Completed += OnQuestionnaireCompleted;
        detectDistance.HeadPositionConfirmed += OnHeadPositionConfirmed;
        gazeCalibrationManager.CalibrationStarted += OnCalibrationStarted;
        gazeCalibrationManager.StimulusShown += OnStimulusShown;
        gazeCalibrationManager.StimulusCleared += OnStimulusCleared;
        gazeCalibrationManager.CalibrationEnded += HideCalibrationHints;
        gazeCalibrationManager.CalibrationFailed += OnCalibrationFailed;
        gazeCalibrationManager.GazeIntroStarted += OnGazeIntroStarted;
        gazeCalibrationManager.GazeIntroEnded += HideGazeIntro;
        gazeCalibrationManager.TestCountdownStarted += OnTestCountdownStarted;
        gazeCalibrationManager.TestCountdownEnded += OnTestCountdownEnded;
        Nuwa.onWikiServiceStart += OnRobotServiceStart;
    }

    private void OnDisable()
    {
        qa.Completed -= OnQuestionnaireCompleted;
        detectDistance.HeadPositionConfirmed -= OnHeadPositionConfirmed;
        gazeCalibrationManager.CalibrationStarted -= OnCalibrationStarted;
        gazeCalibrationManager.StimulusShown -= OnStimulusShown;
        gazeCalibrationManager.StimulusCleared -= OnStimulusCleared;
        gazeCalibrationManager.CalibrationEnded -= HideCalibrationHints;
        gazeCalibrationManager.CalibrationFailed -= OnCalibrationFailed;
        gazeCalibrationManager.GazeIntroStarted -= OnGazeIntroStarted;
        gazeCalibrationManager.GazeIntroEnded -= HideGazeIntro;
        gazeCalibrationManager.TestCountdownStarted -= OnTestCountdownStarted;
        gazeCalibrationManager.TestCountdownEnded -= OnTestCountdownEnded;
        Nuwa.onWikiServiceStart -= OnRobotServiceStart;
    }

    private void Start()
    {
        PlayTTS("請選擇要進行的測驗");
        // Speech is voice-only in this app; the robot's own alert bar would cover the test UI.
        NuwaSystemUi.SetSystemAlertsEnabled(false);
    }

    private void Update()
    {
        // The robot service callback arrives on a Java thread; apply the setting on the main thread.
        if (_robotServiceStarted)
        {
            _robotServiceStarted = false;
            NuwaSystemUi.SetSystemAlertsEnabled(false);
        }
        LogPerformance();
    }

    // Every few seconds: how long tobii_process_frame takes and how many gaze samples arrive.
    // That time sits on the main thread today, so it bounds both the frame rate and gaze rate.
    // Mean time for the dot to reach a large gaze jump in this interval, and how many jumps.
    private string CatchUpText(GazeLatencyStats.Snapshot current)
    {
        long count = current.CatchUpCount - _perfPrevious.CatchUpCount;
        return count > 0 ? $"{(current.CatchUpSum - _perfPrevious.CatchUpSum) / count * 1000.0:0} ms x{count}" : "-";
    }

    // Mean RMS scatter of gaze samples within the current fixation: the noise the dot smooths.
    private string SpreadText(GazeLatencyStats.Snapshot current)
    {
        long count = current.SpreadCount - _perfPrevious.SpreadCount;
        return count > 0 ? $"{(current.SpreadSum - _perfPrevious.SpreadSum) / count:0} px" : "-";
    }

    private void LogPerformance()
    {
        float elapsed = Time.unscaledTime - _perfPreviousTime;
        if (elapsed < PerfLogSeconds)
            return;

        var current = GazeLatencyStats.Take();
        long frames = current.Frames - _perfPrevious.Frames;
        long samples = current.GazeSamples - _perfPrevious.GazeSamples;
        if (frames > 0)
        {
            double processMs = GazeLatencyStats.TicksToMilliseconds(current.FrameTicks - _perfPrevious.FrameTicks) / frames;
            long dropped = System.Threading.Interlocked.Read(ref GazeFrameWorker.Dropped);
            Debug.Log($"[PERF] process frame {processMs:0.0} ms  frames {frames / elapsed:0.0}/s  gaze {samples / elapsed:0.0} Hz  "
                      + $"render {(Time.frameCount - _perfPreviousFrame) / elapsed:0.0} fps  skipped {(dropped - _perfPreviousDropped) / elapsed:0.0}/s  "
                      + $"catch-up {CatchUpText(current)}  spread {SpreadText(current)}");
            _perfPreviousDropped = dropped;
        }
        _perfPrevious = current;
        _perfPreviousTime = Time.unscaledTime;
        _perfPreviousFrame = Time.frameCount;
    }

    private void OnRobotServiceStart()
    {
        _robotServiceStarted = true;
    }

    private void OnApplicationPause(bool paused)
    {
        // Give the alert bar back to other robot apps while this one is in the background.
        NuwaSystemUi.SetSystemAlertsEnabled(paused);
    }

    private void OnApplicationQuit()
    {
        NuwaSystemUi.SetSystemAlertsEnabled(true);
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
        var descriptionText = UiFactory.CreateText("Description", card.transform, font, description, 22f, DescriptionText);
        UiFactory.UsePlainMaterial(descriptionText);
        UiFactory.Place(descriptionText.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, -35f), new Vector2(300f, 40f));
    }

    private void SelectMode(MetricTest.TestMode mode)
    {
        metricTest.Mode = mode;
        _modeMenu.SetActive(false);
        qa.BeginQuestionnaire();
    }

    // ==================== HUD (top-right) ====================

    private void BuildHud(Transform parent)
    {
        // Move the existing exit button onto the HUD so full-screen overlays never cover it.
        exitButton.transform.SetParent(parent, false);
        exitButton.onClick.AddListener(OnExitPressed);

        _debugOverlay = GazeDebugOverlay.Create(parent, gazePointer, _headFollow, _driftCorrector,
            FindObjectOfType<AndroidWebcamCaptureClient>());
        NetworkSignalIcon.Create(parent, new Vector2(-66f, -20f), 36f, IconBackground, ToggleDebugOverlay);

        var recalibrate = UiFactory.CreateButton("RecalibrateButton", parent, UiFactory.Circle, IconBackground, Recalibrate);
        UiFactory.Place((RectTransform)recalibrate.transform, Vector2.one, Vector2.one, new Vector2(-112f, -20f), new Vector2(36f, 36f));
        var glyph = UiFactory.CreateImage("Glyph", recalibrate.transform, UiFactory.RefreshArrow, IconGlyph);
        UiFactory.Place(glyph.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(28f, 28f));
        _recalibrateButton = recalibrate.gameObject;
        _recalibrateButton.SetActive(false);

        BuildExitDialog(parent);
    }

    private void ToggleDebugOverlay()
    {
        _debugOverlay.gameObject.SetActive(!_debugOverlay.gameObject.activeSelf);
    }

    private void OnQuestionnaireCompleted()
    {
        _recalibrateButton.SetActive(true);
        // From the head distance check on, keep the user centred for the camera.
        _headFollow.Engage();
    }

    private void Recalibrate()
    {
        ResetTestSession();
        // A fresh calibration starts from the head distance check, so start following again.
        _headFollow.Engage();
        mainCanvas.SetActive(true);
        canvasTrackBox.SetActive(true);
        // OpenLock also plays the head positioning instructions.
        detectDistance.OpenLock();
    }

    private void OnExitPressed()
    {
        if (_modeMenu.activeSelf)
            ExitApp();
        else
            _exitDialog.SetActive(true);
    }

    private void BuildExitDialog(Transform parent)
    {
        var dim = UiFactory.CreateImage("ExitDialog", parent, null, new Color(0f, 0f, 0f, 0.55f), true);
        UiFactory.Stretch(dim.rectTransform);

        var card = UiFactory.CreateImage("Card", dim.transform, UiFactory.RoundedRect, Color.white);
        card.type = Image.Type.Sliced;
        UiFactory.Place(card.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(480f, 250f));

        var title = UiFactory.CreateText("Title", card.transform, font, "確定要回到首頁嗎？", 32f, TextDark);
        UiFactory.Place(title.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, 55f), new Vector2(440f, 50f));
        var subtitle = UiFactory.CreateText("Subtitle", card.transform, font, "測驗進度將不會保留", 22f, DescriptionText);
        UiFactory.UsePlainMaterial(subtitle);
        UiFactory.Place(subtitle.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, 10f), new Vector2(440f, 36f));

        CreateDialogButton(card.transform, new Vector2(-95f, -62f), "取消", CancelGrey, () => _exitDialog.SetActive(false));
        CreateDialogButton(card.transform, new Vector2(95f, -62f), "確定", Teal, GoHome);

        _exitDialog = dim.gameObject;
        _exitDialog.SetActive(false);
    }

    private void CreateDialogButton(Transform parent, Vector2 position, string text, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var button = UiFactory.CreateButton(text, parent, UiFactory.RoundedRect, color, onClick);
        ((Image)button.targetGraphic).type = Image.Type.Sliced;
        UiFactory.Place((RectTransform)button.transform, UiFactory.Center, UiFactory.Center, position, new Vector2(170f, 60f));
        var label = UiFactory.CreateText("Label", button.transform, font, text, 28f, Color.white);
        UiFactory.Stretch(label.rectTransform);
    }

    private void ExitApp()
    {
        NuwaSystemUi.SetSystemAlertsEnabled(true);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ==================== Back to home ====================

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
        _exitDialog.SetActive(false);
        _recalibrateButton.SetActive(false);
        mainCanvas.SetActive(true);
        qa.RestartQuestionnaire();
        _modeMenu.SetActive(true);
        PlayTTS("請選擇要進行的測驗");
    }

    private void ResetTestSession()
    {
        Nuwa.stopTTS();
        // A session cut short mid-question must not leave gaze selection held for the next one.
        GazeSelectionGate.Open();
        _headFollow.ReturnHome();
        StopHeadConfirmed();
        StopTestCountdown();
        detectDistance.CloseLock();
        canvasTrackBox.SetActive(false);
        gazeCalibrationManager.ResetSession();
        metricTest.ResetSession();
        metricTest.gameObject.SetActive(false);
        resultPage.SetActive(false);
        gazePointer.gameObject.SetActive(false);
        HideCalibrationHints();
        HideGazeIntro();
    }

    // ==================== Head position confirmed ====================

    private void BuildHeadConfirmed(Transform parent)
    {
        var panel = UiFactory.CreateImage("HeadConfirmed", parent, null, new Color32(0xEC, 0xF8, 0xF0, 0xFF), true);
        UiFactory.Stretch(panel.rectTransform);

        var badge = UiFactory.CreateImage("Badge", panel.transform, UiFactory.Circle, SuccessGreen);
        UiFactory.Place(badge.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, 70f), new Vector2(150f, 150f));
        var check = UiFactory.CreateImage("Check", badge.transform, UiFactory.Check, Color.white);
        UiFactory.Place(check.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(110f, 110f));
        _headConfirmedBadge = badge.rectTransform;

        var title = UiFactory.CreateText("Title", panel.transform, font, "頭部位置確認完成", 40f, SuccessGreen);
        UiFactory.Place(title.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, -40f), new Vector2(800f, 60f));
        var subtitle = UiFactory.CreateText("Subtitle", panel.transform, font, "接下來請保持頭部不動", 28f, DescriptionText);
        UiFactory.UsePlainMaterial(subtitle);
        UiFactory.Place(subtitle.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, -95f), new Vector2(800f, 44f));

        _headConfirmed = panel.gameObject;
        _headConfirmed.SetActive(false);
    }

    private void OnHeadPositionConfirmed()
    {
        StopHeadConfirmed();
        _headConfirmedRoutine = StartCoroutine(HeadConfirmedRoutine());
    }

    private IEnumerator HeadConfirmedRoutine()
    {
        _headConfirmed.SetActive(true);
        if (_successClip != null)
            _sfx.PlayOneShot(_successClip);
        PlayTTS("頭部位置確認完成");

        for (float t = 0f; t < HeadConfirmedSeconds; t += Time.deltaTime)
        {
            float scale = t < 0.18f
                ? Mathf.Lerp(0.5f, 1.12f, t / 0.18f)
                : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((t - 0.18f) / 0.12f));
            _headConfirmedBadge.localScale = Vector3.one * scale;
            yield return null;
        }

        _headConfirmed.SetActive(false);
        _headConfirmedRoutine = null;
        gazeCalibrationManager.BeginFirstTrial();
    }

    private void StopHeadConfirmed()
    {
        if (_headConfirmedRoutine != null)
        {
            StopCoroutine(_headConfirmedRoutine);
            _headConfirmedRoutine = null;
        }
        _headConfirmed.SetActive(false);
    }

    // ==================== Test start countdown ====================

    private void BuildTestCountdown(Transform parent)
    {
        var panel = UiFactory.CreateImage("TestCountdown", parent, null, new Color(0.12f, 0.23f, 0.37f, 0.94f), true);
        UiFactory.Stretch(panel.rectTransform);
        _testCountdown = panel.gameObject.AddComponent<CanvasGroup>();

        _testCountdownTitle = UiFactory.CreateText("Title", panel.transform, font, "", 48f, Color.white);
        UiFactory.Place(_testCountdownTitle.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, 115f), new Vector2(800f, 70f));
        var subtitle = UiFactory.CreateText("Subtitle", panel.transform, font, "即將開始，請注視畫面中央", 28f, new Color(1f, 1f, 1f, 0.85f));
        UiFactory.Place(subtitle.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, 58f), new Vector2(800f, 44f));
        // The Chinese SDF atlas has no digits, so the countdown uses TMP's default font.
        _testCountdownNumber = UiFactory.CreateText("Number", panel.transform, TMP_Settings.defaultFontAsset, "", 120f, Color.white);
        UiFactory.Place(_testCountdownNumber.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, -70f), new Vector2(300f, 160f));

        panel.gameObject.SetActive(false);
    }

    private void OnTestCountdownStarted(string side, float seconds)
    {
        StopTestCountdown();
        _testCountdownRoutine = StartCoroutine(TestCountdownRoutine(side, seconds));
    }

    private IEnumerator TestCountdownRoutine(string side, float seconds)
    {
        string title = side == "left" ? "左眼測驗" : side == "both" ? "雙眼測驗" : "右眼測驗";
        _testCountdownTitle.text = title;
        _testCountdown.alpha = 0f;
        _testCountdown.gameObject.SetActive(true);
        PlayTTS(title + "，即將開始");

        int shownNumber = -1;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float remaining = seconds - t;
            int number = Mathf.CeilToInt(remaining);
            if (number != shownNumber)
            {
                shownNumber = number;
                _testCountdownNumber.text = number.ToString();
                _sfx.PlayOneShot(UiSounds.Tick);
            }

            float intoSecond = number - remaining;
            _testCountdownNumber.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(intoSecond / 0.25f));
            _testCountdown.alpha = Mathf.Clamp01(t / CountdownFadeSeconds);
            yield return null;
        }
        _testCountdownRoutine = null;
    }

    private void OnTestCountdownEnded()
    {
        StopTestCountdown();
        _testCountdown.gameObject.SetActive(true);
        _testCountdownRoutine = StartCoroutine(FadeOutTestCountdown());
    }

    private IEnumerator FadeOutTestCountdown()
    {
        float start = _testCountdown.alpha;
        for (float t = 0f; t < CountdownFadeSeconds; t += Time.deltaTime)
        {
            _testCountdown.alpha = Mathf.Lerp(start, 0f, t / CountdownFadeSeconds);
            yield return null;
        }
        _testCountdown.gameObject.SetActive(false);
        _testCountdownRoutine = null;
    }

    private void StopTestCountdown()
    {
        if (_testCountdownRoutine != null)
        {
            StopCoroutine(_testCountdownRoutine);
            _testCountdownRoutine = null;
        }
        _testCountdown.gameObject.SetActive(false);
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

        // A blue circle read as one more calibration dot; a rounded amber tag differs from the
        // stimuli in both shape and colour.
        var badge = UiFactory.CreateImage("StimulusBadge", parent, UiFactory.RoundedRect, BadgeFill);
        badge.type = Image.Type.Sliced;
        badge.pixelsPerUnitMultiplier = 2f;
        UiFactory.Place(badge.rectTransform, Vector2.zero, UiFactory.Center, Vector2.zero, new Vector2(54f, 42f));
        var badgeEdge = UiFactory.CreateImage("Frame", badge.transform, UiFactory.RoundedFrame, BadgeEdge);
        badgeEdge.type = Image.Type.Sliced;
        badgeEdge.pixelsPerUnitMultiplier = 2f;
        UiFactory.Stretch(badgeEdge.rectTransform);
        _stimulusBadgeText = UiFactory.CreateText("Number", badge.transform, TMP_Settings.defaultFontAsset, "", 28f, BadgeText);
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
        var subtitle = UiFactory.CreateText("Subtitle", panel.transform, font, "請試著看看四周，準備好後請注視下方的繼續按鈕", 26f, DescriptionText);
        UiFactory.UsePlainMaterial(subtitle);
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
