using System.Collections;
using TMPro;
using Tobii;
using UnityEngine;
using UnityEngine.UI;

// App-level flow: test mode menu, recalibration, back-to-home, stage transitions (with their speech and sounds),
// the top-right HUD and the robot's system alert bar. The V3 pages draw every screen.
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
    private const float PerfLogSeconds = 5f;

    private static readonly Color IconBackground = new Color32(0xED, 0xED, 0xED, 0xEB);
    private static readonly Color IconGlyph = new Color32(0x8A, 0x8A, 0x8A, 0xFF);

    private GameObject _modeMenu;
    private V3Home _home;
    private V3Flow _v3;
    private V3Setup _v3Setup;
    private RectTransform _networkIcon;
    private HudSlot _exitSlot, _networkSlot;
    private GameObject _recalibrateButton;
    private GameObject _exitDialog;
    private GazeDebugOverlay _debugOverlay;
    private HeadFollow _headFollow;
    private DriftCorrector _driftCorrector;
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

        // The HUD sits above everything.
        var hudCanvas = UiFactory.CreateOverlayCanvas("HudOverlay", V3Layer.Hud, transform);

        QuestionnaireText.Apply(qa, font);
        _headFollow = HeadFollow.Create(transform, gazeCalibrationManager, metricTest, resultPage);
        detectDistance.HeadFollow = _headFollow;
        BuildHome();
        _v3 = V3Flow.Create(transform, _modeMenu);
        V3Sounds.Install(transform);
        KebbiMotion.Install(transform);
        KebbiLight.Install(transform);
        V3Speech.Install();
        V3Questionnaire.Create(_v3, qa, GoHome);
        V3Position.Create(_v3, detectDistance, _headFollow, canvasTrackBox, metricTest, BackToQuestionnaire);
        _v3Setup = V3Setup.Create(_v3, gazeCalibrationManager, metricTest, mainCanvas, Recalibrate, GoHome);
        V3Presence.Create(transform, _headFollow, _modeMenu, GoHome);
        V3Test.Create(_v3, metricTest, resultPage, GoHome);
        PositionGuide.Create(transform, _headFollow, metricTest);
        _driftCorrector = DriftCorrector.Create(transform, gazePointer, metricTest, _headFollow, gazeCalibrationManager);
        OptionReveal.Create(transform, metricTest, _driftCorrector, _headFollow, gazePointer);
        // Focuses the camera on the user's face at each head distance check.
        FaceFocus.Create(transform, detectDistance, _headFollow, FindObjectOfType<AndroidWebcamCaptureClient>());
        BuildHud(hudCanvas.transform);
        V3Overlays.RestyleHud(exitButton, _networkIcon, _recalibrateButton);
        V3Overlays.Create(transform, _headFollow);

        GazeVisual.Attach(gazePointer);
        GazeDwellIndicator.Attach(gazePointer.transform);

        _home.ShownChanged += ArrangeHudForHome;
        _modeMenu.SetActive(true);
    }

    private void OnEnable()
    {
        qa.Completed += OnQuestionnaireCompleted;
        detectDistance.HeadPositionConfirmed += OnHeadPositionConfirmed;
        gazeCalibrationManager.CalibrationStarted += OnCalibrationStarted;
        gazeCalibrationManager.CalibrationEnded += OnCalibrationEnded;
        gazeCalibrationManager.CalibrationFailed += OnCalibrationFailed;
        gazeCalibrationManager.TestCountdownStarted += OnTestCountdownStarted;
        gazeCalibrationManager.TestCountdownEnded += OnTestCountdownEnded;
        Nuwa.onWikiServiceStart += OnRobotServiceStart;
    }

    private void OnDisable()
    {
        qa.Completed -= OnQuestionnaireCompleted;
        detectDistance.HeadPositionConfirmed -= OnHeadPositionConfirmed;
        gazeCalibrationManager.CalibrationStarted -= OnCalibrationStarted;
        gazeCalibrationManager.CalibrationEnded -= OnCalibrationEnded;
        gazeCalibrationManager.CalibrationFailed -= OnCalibrationFailed;
        gazeCalibrationManager.TestCountdownStarted -= OnTestCountdownStarted;
        gazeCalibrationManager.TestCountdownEnded -= OnTestCountdownEnded;
        Nuwa.onWikiServiceStart -= OnRobotServiceStart;
    }

    private void Start()
    {
        PlayTTS("請選擇要進行的測驗");
        // Speech is voice-only in this app; the robot's own alert bar would cover the test UI.
        NuwaSystemUi.SetSystemAlertsEnabled(false);
        // the chest and hand lights are the app's while it is in front: a slow violet breath on the home
        KebbiLight.Take();
        KebbiLight.Breathe(KebbiLight.Violet);
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

    // The V3 home (3D E field, headline, two mode buttons) replaces the old mode menu; _modeMenu is its root.
    private void BuildHome()
    {
        _home = V3Home.Create(transform, gazePointer, SelectMode);
        _modeMenu = _home.gameObject;
    }

    private void SelectMode(MetricTest.TestMode mode)
    {
        metricTest.Mode = mode;
        // Kebbi points to the chosen side (the both-eyes key is on the person's left, the robot's right)
        KebbiMotion.Play(mode == MetricTest.TestMode.Both ? "point_right" : "point_left");
        KebbiLight.Flash(KebbiLight.Green);
        _modeMenu.SetActive(false);
        qa.BeginQuestionnaire();
    }

    // ==================== HUD (top-right) ====================

    private void BuildHud(Transform parent)
    {
        // Move the existing exit button onto the HUD so full-screen overlays never cover it.
        exitButton.transform.SetParent(parent, false);
        exitButton.onClick.AddListener(OnExitPressed);

        var validation = AccuracyValidation.Create(transform, gazePointer, _headFollow, font);
        _debugOverlay = GazeDebugOverlay.Create(parent, gazePointer, _headFollow, _driftCorrector,
            FindObjectOfType<AndroidWebcamCaptureClient>(), () =>
            {
                _debugOverlay.gameObject.SetActive(false);
                validation.Run();
            });
        _networkIcon = (RectTransform)NetworkSignalIcon.Create(parent, new Vector2(-66f, -20f), 36f, IconBackground, ToggleDebugOverlay).transform;

        var recalibrate = UiFactory.CreateButton("RecalibrateButton", parent, UiFactory.Circle, IconBackground, Recalibrate);
        UiFactory.Place((RectTransform)recalibrate.transform, Vector2.one, Vector2.one, new Vector2(-112f, -20f), new Vector2(36f, 36f));
        var glyph = UiFactory.CreateImage("Glyph", recalibrate.transform, UiFactory.RefreshArrow, IconGlyph);
        UiFactory.Place(glyph.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(28f, 28f));
        _recalibrateButton = recalibrate.gameObject;
        _recalibrateButton.SetActive(false);

        BuildExitDialog(parent);
    }

    // On the V3 home exit keeps its corner and the zh/EN toggle sits left of it (x 800-926), so the
    // network glyph moves left of the toggle, centred on the chrome row (y 56); elsewhere it goes back.
    private struct HudSlot
    {
        public bool Saved;
        public Vector2 AnchorMin, AnchorMax, Pivot, Position, Size;
    }

    private void ArrangeHudForHome(bool home)
    {
        MoveHudIcon(_networkIcon, ref _networkSlot, home, 832f - 12f);
    }

    private static void MoveHudIcon(RectTransform rect, ref HudSlot slot, bool home, float rightEdge)
    {
        if (!slot.Saved)
        {
            slot = new HudSlot { Saved = true, AnchorMin = rect.anchorMin, AnchorMax = rect.anchorMax, Pivot = rect.pivot, Position = rect.anchoredPosition, Size = rect.sizeDelta };
        }
        if (home)
        {
            Vector2 size = rect.rect.size;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(rightEdge - 1024f, -44f);
        }
        else
        {
            rect.anchorMin = slot.AnchorMin;
            rect.anchorMax = slot.AnchorMax;
            rect.pivot = slot.Pivot;
            rect.sizeDelta = slot.Size;
            rect.anchoredPosition = slot.Position;
        }
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
        // the questionnaire done: a cheer before the head position check
        KebbiMotion.Play("cheer");
        V3Sounds.Play(V3Sounds.Cue.Done);
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
        {
            _exitDialog.SetActive(true);
            KebbiMotion.Play("think");
        }
    }

    private void BuildExitDialog(Transform parent)
    {
        _exitDialog = V3Overlays.ExitDialog(parent, () => { _exitDialog.SetActive(false); KebbiMotion.Play("ok"); }, GoHome);
    }

    private void ExitApp()
    {
        NuwaSystemUi.SetSystemAlertsEnabled(true);
        _headFollow.ReturnHome();
        V3Sounds.Play(V3Sounds.Cue.Bye);
        KebbiMotion.Stop();
        KebbiLight.Release();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ==================== Back to home ====================

    // "上一步" on the head position page: back to the last question, answers kept.
    private void BackToQuestionnaire()
    {
        ResetTestSession();
        _recalibrateButton.SetActive(false);
        mainCanvas.SetActive(true);
        qa.ReopenLastQuestion();
    }

    private void GoHome()
    {
        ResetTestSession();
        _exitDialog.SetActive(false);
        _recalibrateButton.SetActive(false);
        mainCanvas.SetActive(true);
        qa.RestartQuestionnaire();
        _modeMenu.SetActive(true);
        KebbiMotion.Allowed = true;
        KebbiLight.Breathe(KebbiLight.Violet);
        PlayTTS("請選擇要進行的測驗");
    }

    private void ResetTestSession()
    {
        Nuwa.stopTTS();
        _v3Setup.Clear();
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
        if (_recalibrateButton != null)
            SetHudHidden(false);
    }

    // ==================== Head position confirmed ====================

    private void OnHeadPositionConfirmed()
    {
        KebbiMotion.Play("clap");
        KebbiLight.Flash(KebbiLight.Green);
        StopHeadConfirmed();
        _headConfirmedRoutine = StartCoroutine(HeadConfirmedRoutine());
    }

    // The V3 position page shows the confirmation in its status line; after a short pause the first trial begins.
    private IEnumerator HeadConfirmedRoutine()
    {
        V3Sounds.Play(V3Sounds.Cue.Done);
        PlayTTS("頭部位置確認完成");
        yield return new WaitForSeconds(HeadConfirmedSeconds);
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
    }

    // ==================== Test start countdown ====================

    // V3Setup draws the countdown; this speaks it and ticks each second.
    private void OnTestCountdownStarted(string side, float seconds)
    {
        StopTestCountdown();
        _testCountdownRoutine = StartCoroutine(TestCountdownRoutine(side, seconds));
    }

    private IEnumerator TestCountdownRoutine(string side, float seconds)
    {
        string title = side == "left" ? "左眼測驗" : side == "both" ? "雙眼測驗" : "右眼測驗";
        PlayTTS(title + "，即將開始");

        int shownNumber = -1;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            int number = Mathf.CeilToInt(seconds - t);
            if (number != shownNumber)
            {
                shownNumber = number;
                V3Sounds.Play(number <= 1 ? V3Sounds.Cue.CountLast : V3Sounds.Cue.Count);
            }
            yield return null;
        }
        _testCountdownRoutine = null;
    }

    private void OnTestCountdownEnded()
    {
        StopTestCountdown();
    }

    private void StopTestCountdown()
    {
        if (_testCountdownRoutine != null)
        {
            StopCoroutine(_testCountdownRoutine);
            _testCountdownRoutine = null;
        }
    }

    // ==================== Calibration ====================

    // While the calibration dots run, the top-right keys (signal, recalibrate, exit) are hidden: nothing to look
    // at but the dot. They come back when it ends, fails or the session is reset.
    private bool _hudHidden, _recalibrateWasOn;

    private void SetHudHidden(bool hidden)
    {
        if (hidden == _hudHidden)
            return;
        _hudHidden = hidden;
        if (hidden)
            _recalibrateWasOn = _recalibrateButton.activeSelf;
        exitButton.gameObject.SetActive(!hidden);
        _networkIcon.gameObject.SetActive(!hidden);
        _recalibrateButton.SetActive(!hidden && _recalibrateWasOn);
    }

    private void OnCalibrationStarted()
    {
        // from the calibration to the result the arms stay still and the lights steady: nothing on the robot may move
        // or change while the eyes are measured
        KebbiMotion.Allowed = false;
        KebbiLight.Steady(KebbiLight.Violet, 0.5f);
        SetHudHidden(true);
        PlayTTS("眼睛校正開始，請保持頭部不動，依序看著紫色圓點");
    }

    private void OnCalibrationEnded()
    {
        SetHudHidden(false);
    }

    private void OnCalibrationFailed()
    {
        SetHudHidden(false);
        PlayTTS("校正沒有成功，請按重新校正，再看一次圓點");
    }

    private static void PlayTTS(string text)
    {
        Nuwa.stopTTS();
        Nuwa.startTTS(text);
    }
}
