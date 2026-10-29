using System;
using TMPro;
using Tobii;
using UnityEngine;
using UnityEngine.UI;

// V3 pages between the head position check and the test: the cover-eye page (one-eye
// mode), the calibration card with its 3 s start, the calibration dots on a plain page, the gaze practice (three
// E tiles + a gaze "start test" key, replacing the red-dot intro) and the countdown. GazeCalibrationManager still
// runs the sequence; these pages follow its events and call NextButton / ConfirmGazeIntro.
public class V3Setup : MonoBehaviour
{
    private enum View { None, Cover, CalIntro, CalRun, CalFailed, Practice, Countdown }

    private const float CalIntroSeconds = 3f;
    private static readonly Vector2[] TilePos = { new Vector2(418f, 321f), new Vector2(578f, 321f), new Vector2(738f, 321f) };

    private V3Flow _flow;
    private GazeCalibrationManager _cal;
    private MetricTest _test;
    private GameObject _mainCanvas;
    private Action _recalibrate;

    private View _view = View.None;
    private string _side = "both";
    private float _viewSince;
    private bool _nextSent;

    // pages
    private GameObject _coverPage, _calPage, _runPage, _pracPage, _cdPage;
    private V3.SideParts _coverSide, _calSide, _pracSide;
    private V3.PanelParts _coverPanel, _calPanel;
    private V3.LineParts _coverLine, _calLine, _pracLine;
    private TextMeshProUGUI _coverLabelL, _coverLabelR;
    private Image _coverChipL, _coverChipR, _palmL, _palmR, _eyeL, _eyeR;
    private TextMeshProUGUI _calText, _calSub;
    private Image _calDot;
    private RectTransform _calHold;
    private V3.KeyParts _retry;
    private GameObject _calMap;

    private TextMeshProUGUI _runText;
    private RectTransform _target;
    private Image _targetArc, _targetCore;
    private Image _targetHalo;
    private bool _targetOn;
    private float _targetScale;

    private ButtonTrigger[] _tiles = new ButtonTrigger[3];
    private RectTransform[] _tileE = new RectTransform[3];
    private Image[] _tileEImg = new Image[3];
    private GameObject[] _tileCheck = new GameObject[3];
    private bool[] _tileDone = new bool[3];
    private GameObject _lookTag;
    private RectTransform _lookRing;
    private V3.KeyParts _start;
    private ButtonTrigger _startTrigger;
    private TextMeshProUGUI _pracText, _pracSub;
    private Image _pracDot;
    private V3.PanelParts _pracPanel;

    private TextMeshProUGUI _cdTag, _cdNumber, _cdCoverText;
    private Image _cdTagFill, _cdArc;
    private GameObject _cdCover;
    private float _cdSeconds = 3f;

    public static V3Setup Create(V3Flow flow, GazeCalibrationManager cal, MetricTest test, GameObject mainCanvas, Action recalibrate)
    {
        var root = V3.Full("Setup", flow.Pages);
        var page = root.gameObject.AddComponent<V3Setup>();
        page._flow = flow;
        page._cal = cal;
        page._test = test;
        page._mainCanvas = mainCanvas;
        page._recalibrate = recalibrate;
        page.Build(root);
        StimulusPoint.HideVisual = true;
        // the scene's instructions page (title art, eye picture, text, continue button) is replaced; they sit on
        // the scene canvas's page object (眼動測試), whose background art becomes the plain V3 page colour
        foreach (var child in mainCanvas.GetComponentsInChildren<Transform>(true))
        {
            if (child.parent == null || child.parent.parent != mainCanvas.transform)
                continue;
            if (child.name == "Title" || child.name == "SampleImage" || child.name == "Text (TMP) (1)" || child.name == "Next")
                V3Flow.Conceal(child.gameObject);
        }
        if (mainCanvas.transform.childCount > 0 && mainCanvas.transform.GetChild(0).TryGetComponent<Image>(out var pageArt))
        {
            pageArt.sprite = null;
            pageArt.color = V3.Page;
        }
        cal.TrialInstructionsShown += page.OnInstructions;
        cal.CalibrationStarted += page.OnCalibrationStarted;
        cal.StimulusShown += page.OnStimulusShown;
        cal.StimulusCleared += page.OnStimulusCleared;
        cal.CalibrationFailed += () => { V3Sounds.Play(V3Sounds.Cue.Fail); page.Show(View.CalFailed); };
        cal.CalibrationEnded += () => V3Sounds.Play(V3Sounds.Cue.Done);
        cal.GazeIntroStarted += page.OnPracticeStarted;
        cal.GazeIntroEnded += () => { if (page._view == View.Practice) page.Show(View.None); };
        cal.TestCountdownStarted += page.OnCountdownStarted;
        cal.TestCountdownEnded += () => { if (page._view == View.Countdown) page.Show(View.None); };
        return page;
    }

    // Leave whatever page is up (recalibrate, back home).
    public void Clear() => Show(View.None);

    // ==================== build ====================

    private void Build(Transform root)
    {
        BuildCover(root);
        BuildCalCard(root);
        BuildRun(root);
        BuildPractice(root);
        BuildCountdown(root);
        Show(View.None);
    }

    private void BuildCover(Transform root)
    {
        _coverPage = V3.Full("Cover", root).gameObject;
        var t = _coverPage.transform;
        _coverSide = V3.Side(t, "Setup", 4, new[] { "位置", "遮眼", "校正", "練習" }, new[] { "Position", "Cover", "Calibrate", "Practice" });
        V3.SetNumber(_coverSide, 2);
        V3.SetStep(_coverSide, 1);
        V3.Key(t, "繼續", "Continue", "arrowR", V3.Mint, 40f, 488f, 224f, false, () => { if (_view == View.Cover) Show(View.CalIntro); });
        _coverPanel = V3.Panel(t, "eye", "", "", "OCCLUSION");
        var p = _coverPanel.Root;
        // two eyes as in a mirror (the user's left eye on the left), the covered one faded under a violet palm
        _eyeL = V3.Sprite("EyeL", p, V3.Icon("eye"), V3.Ink, 341f - 304f + 4f, 287f - 132f + 4f, 92f, 92f);
        _eyeR = V3.Sprite("EyeR", p, V3.Icon("eye"), V3.Ink, 449f - 304f + 4f, 287f - 132f + 4f, 92f, 92f);
        _palmL = V3.Sprite("PalmL", p, PalmSprite(), V3.Violet, 341f - 304f + 23f, 287f - 132f + 23f, 54f, 54f);
        _palmR = V3.Sprite("PalmR", p, PalmSprite(), V3.Violet, 449f - 304f + 23f, 287f - 132f + 23f, 54f, 54f);
        _coverChipL = V3.Box("ChipL", p, V3.Violet, 365f - 304f, 403f - 132f, 52f, 26f);
        _coverLabelL = V3.Text("Label", _coverChipL.transform, V3.Zh, "", 18f, Color.white, 0f, 0f, 52f, 26f, TextAlignmentOptions.Center);
        _coverChipR = V3.Box("ChipR", p, V3.Mint, 473f - 304f, 403f - 132f, 52f, 26f);
        _coverLabelR = V3.Text("Label", _coverChipR.transform, V3.Zh, "", 18f, V3.Ink, 0f, 0f, 52f, 26f, TextAlignmentOptions.Center);
        _coverLine = V3.KebbiLine(p, 587f - 304f, 258f - 132f, 26f, 260f);
        string[] tipsZh = { "手輕輕遮住，不要壓到眼睛", "兩隻眼睛都保持張開", "測驗中請一直遮著" };
        string[] tipsEn = { "Cover gently, don't press the eye", "Keep both eyes open", "Keep it covered during the test" };
        for (int i = 0; i < 3; i++)
        {
            float y = 372f - 132f + i * 32f;
            V3.Box("Bullet", p, V3.Ink, 669f - 304f, y + 9f, 5f, 5f);
            V3.Text("Tip", p, V3.ZhMedium, tipsZh[i], tipsEn[i], 18f, V3.Text2, 669f - 304f + 14f, y, 290f, 23f);
        }
    }

    private void BuildCalCard(Transform root)
    {
        _calPage = V3.Full("Calibration", root).gameObject;
        var t = _calPage.transform;
        _calSide = V3.Side(t, "Setup", 3, new[] { "位置", "遮眼", "校正", "練習" }, new[] { "Position", "Cover", "Calibrate", "Practice" });
        _retry = V3.Key(t, "重新校正", "Recalibrate", "target", V3.Mint, 40f, 488f, 224f, false, () => _recalibrate?.Invoke());
        _calPanel = V3.Panel(t, "target", "眼睛校正", "Calibration", "CALIBRATION");
        var p = _calPanel.Root;
        _calLine = V3.KebbiLine(p, 357f - 304f, 258f - 132f, 26f, 256f);
        // map of the five dots in their order
        _calMap = V3.Rect("Map", p, 725f - 304f, 257f - 132f, 168f, 100f).gameObject;
        V3.Box("Bg", _calMap.transform, new Color32(0xE2, 0xE2, 0xDF, 0xFF), 0f, 0f, 168f, 100f);
        var dots = new[] { new Vector2(.5f, .5f), new Vector2(.15f, .15f), new Vector2(.85f, .15f), new Vector2(.85f, .85f), new Vector2(.15f, .85f) };
        for (int i = 0; i < dots.Length; i++)
        {
            float x = dots[i].x * 168f, y = dots[i].y * 100f;
            V3.Sprite("Dot", _calMap.transform, UiFactory.Circle, V3.Violet, x - 6f, y - 6f, 12f, 12f);
            V3.Text("N", _calMap.transform, V3.Mono, (i + 1).ToString(), 13f, V3.Text2, x + 8f, y - 11f, 20f, 16f);
        }
        Status(p, out _calDot, out _calText, out _calSub, out _calHold, true);
    }

    private static void Status(Transform p, out Image dot, out TextMeshProUGUI text, out TextMeshProUGUI sub, out RectTransform hold, bool withHold, float top = 446f)
    {
        float y = top - 132f;
        V3.Box("StatRule", p, V3.A(V3.Ink, 0.12f), 29f, y, 622f, 1f);
        dot = V3.Sprite("Dot", p, UiFactory.Circle, V3.Ink, 29f, y + 25f, 10f, 10f);
        text = V3.Text("Status", p, V3.Zh, "", 24f, V3.Ink, 51f, y + 13f, 600f, 32f);
        sub = V3.Text("Sub", p, V3.ZhMedium, "", 17f, V3.Text2, 51f, y + 52f, 600f, 24f);
        hold = null;
        if (withHold)
        {
            V3.Box("HoldTrack", p, V3.A(V3.Ink, 0.1f), 29f, y + 86f, 622f, 3f);
            hold = V3.Box("Hold", p, V3.Green, 29f, y + 86f, 0f, 3f).rectTransform;
        }
    }

    private void BuildRun(Transform root)
    {
        _runPage = V3.Full("CalibrationRun", root).gameObject;
        _runText = V3.Text("Text", _runPage.transform, V3.Zh, "", 24f, V3.Ink, 0f, 34f, V3.W, 44f, TextAlignmentOptions.Center);
        // target (120 x 120): pulsing halo, white disc with a violet track, green progress arc, violet core, white pip
        _target = V3.Rect("Target", _runPage.transform, 0f, 0f, 120f, 120f);
        _target.pivot = UiFactory.Center;
        _targetHalo = V3.Sprite("Halo", _target, RingSprite(40f, 1f), V3.A(V3.Violet, 0.22f), 0f, 0f, 120f, 120f);
        V3.Sprite("Disc", _target, UiFactory.Circle, Color.white, 31f, 31f, 58f, 58f);
        V3.Sprite("Track", _target, RingSprite(26f, 3f), V3.A(V3.Violet, 0.22f), 0f, 0f, 120f, 120f);
        _targetArc = V3.Sprite("Arc", _target, RingSprite(26f, 3f), V3.Mint, 0f, 0f, 120f, 120f);
        _targetArc.type = Image.Type.Filled;
        _targetArc.fillMethod = Image.FillMethod.Radial360;
        _targetArc.fillOrigin = (int)Image.Origin360.Top;
        _targetArc.fillClockwise = true;
        _targetCore = V3.Sprite("Core", _target, UiFactory.Circle, V3.Violet, 49f, 49f, 22f, 22f);
        V3.Sprite("Pip", _target, UiFactory.Circle, Color.white, 56.5f, 56.5f, 7f, 7f);
        _target.gameObject.SetActive(false);
    }

    private void BuildPractice(Transform root)
    {
        _pracPage = V3.Full("Practice", root).gameObject;
        var t = _pracPage.transform;
        _pracSide = V3.Side(t, "Setup", 3, new[] { "位置", "遮眼", "校正", "練習" }, new[] { "Position", "Cover", "Calibrate", "Practice" });
        _pracPanel = V3.Panel(t, "eye", "視線練習", "Gaze practice", "GAZE PRACTICE");
        V3.SetTag(_pracPanel, V3.T("用眼睛操作", "Use your eyes"));
        var p = _pracPanel.Root;
        _pracLine = V3.KebbiLine(p, 341f - 304f, 193f - 132f, 26f, 480f);

        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float x = TilePos[i].x - 304f, y = TilePos[i].y - 132f;
            var button = UiFactory.CreateButton("Tile", p, UiFactory.White, new Color(1f, 1f, 1f, 0f), () => OnTileDone(index));
            button.transition = Selectable.Transition.None;
            V3.At((RectTransform)button.transform, x, y, 132f, 112f);
            _tiles[i] = button.gameObject.AddComponent<ButtonTrigger>();
            var e = V3.Sprite("E", button.transform, ESprite(), V3.Ink, 44f, 34f, 44f, 44f);
            e.rectTransform.pivot = UiFactory.Center;
            e.rectTransform.anchoredPosition = new Vector2(66f, -56f);
            _tileE[i] = e.rectTransform;
            _tileEImg[i] = e;
            _tileCheck[i] = V3.Sprite("Check", button.transform, V3.Icon("check"), V3.Ink, 90f, 10f, 22f, 22f).gameObject;
        }
        _lookRing = V3.Rect("LookRing", p, 0f, 0f, 88f, 88f);
        _lookRing.pivot = UiFactory.Center;
        V3.Sprite("Ring", _lookRing, RingSprite(43f, 1f), V3.Violet, 0f, 0f, 88f, 88f);
        _lookTag = V3.Box("LookTag", p, V3.Violet, 0f, 0f, 64f, 26f).gameObject;
        V3.Text("Text", _lookTag.transform, V3.Zh, "看這裡", "Look", 15f, Color.white, 0f, 0f, 64f, 26f, TextAlignmentOptions.Center);

        Status(p, out _pracDot, out _pracText, out _pracSub, out _, false, 459f);

        _start = V3.Key(t, "開始測驗", "Start test", "arrowR", V3.Mint, 40f, 488f);
        _start.Button.onClick.AddListener(() =>
        {
            if (_view != View.Practice)
                return;
            V3Sounds.Play(V3Sounds.Cue.Select);
            _cal.ConfirmGazeIntro();
        });
        _startTrigger = _start.Button.gameObject.AddComponent<ButtonTrigger>();
    }

    private void BuildCountdown(Transform root)
    {
        _cdPage = V3.Full("Countdown", root).gameObject;
        var t = _cdPage.transform;
        _cdTagFill = V3.Box("Tag", t, V3.Violet, 464f, 120f, 96f, 30f);
        _cdTag = V3.Text("Text", _cdTagFill.transform, V3.Zh, "", 18f, Color.white, 0f, 0f, 96f, 30f, TextAlignmentOptions.Center);
        V3.Text("Title", t, V3.Zh, "即將開始，請注視畫面中央", "Starting soon - look at the centre", 30f, V3.Ink, 0f, 160f, V3.W, 40f, TextAlignmentOptions.Top);
        V3.Sprite("Track", t, RingSprite(54f, 3f), V3.A(V3.Ink, 0.12f), 452f, 240f, 120f, 120f);
        _cdArc = V3.Sprite("Arc", t, RingSprite(54f, 3f), V3.Violet, 452f, 240f, 120f, 120f);
        _cdArc.type = Image.Type.Filled;
        _cdArc.fillMethod = Image.FillMethod.Radial360;
        _cdArc.fillOrigin = (int)Image.Origin360.Top;
        _cdNumber = V3.Text("Number", t, V3.Head, "3", 56f, V3.Ink, 452f, 240f, 120f, 120f, TextAlignmentOptions.Center);
        _cdCover = V3.Rect("Cover", t, 0f, 400f, V3.W, 40f).gameObject;
        _cdCoverText = V3.Text("Text", _cdCover.transform, V3.Zh, "", 22f, V3.Violet, 0f, 0f, V3.W, 40f, TextAlignmentOptions.Center);
    }

    // ==================== events ====================

    private void OnInstructions(string side)
    {
        _side = side;
        _nextSent = false;
        Show(side == "both" ? View.CalIntro : View.Cover);
    }

    private void OnCalibrationStarted()
    {
        _runText.text = V3.T("請看著畫面中央的圓點", "Look at the dot in the centre");
        Show(View.CalRun);
    }

    private void OnStimulusShown(int index, int count, Vector2 screen)
    {
        _runText.text = count == 1 ? V3.T("請看著畫面中央的圓點", "Look at the dot in the centre")
            : V3.T($"請看第 {index} 個圓點（共 {count} 個）", $"Look at dot {index} of {count}");
        var canvas = _flow.Canvas;
        _target.anchoredPosition = new Vector2(screen.x / canvas.scaleFactor, screen.y / canvas.scaleFactor - V3.H);
        _target.gameObject.SetActive(true);
        _targetOn = true;
        _targetScale = 0.6f;
        _targetCore.color = V3.Violet;
        V3Sounds.Play(V3Sounds.Cue.DotShow);
    }

    private void OnStimulusCleared()
    {
        _targetOn = false;
        V3Sounds.Play(V3Sounds.Cue.DotDone);
        _runText.text = V3.T("很好，請保持頭部不動", "Good, keep your head still");
    }

    private void OnPracticeStarted()
    {
        for (int i = 0; i < 3; i++)
            _tileDone[i] = false;
        Show(View.Practice);
        V3.Speak("這個圓圈會跟著您的眼睛。請看著紫色的E不動，就能選取。");
    }

    private void OnTileDone(int index)
    {
        if (_view != View.Practice || _tileDone[index])
            return;
        _tileDone[index] = true;
        V3Sounds.Play(V3Sounds.Cue.Select);
        if (Array.TrueForAll(_tileDone, d => d))
            V3.Speak("做得很好！請看著開始測驗的按鈕");
    }

    private void OnCountdownStarted(string side, float seconds)
    {
        _side = side;
        _cdSeconds = seconds;
        string tag = side == "left" ? V3.T("左眼測驗", "Left eye") : side == "both" ? V3.T("雙眼測驗", "Both eyes") : V3.T("右眼測驗", "Right eye");
        _cdTag.text = tag;
        _cdCover.SetActive(side != "both");
        _cdCoverText.text = side == "left" ? V3.T("記得遮住右眼", "Keep your right eye covered") : V3.T("記得遮住左眼", "Keep your left eye covered");
        Show(View.Countdown);
    }

    // ==================== state ====================

    private void Show(View view)
    {
        _view = view;
        _viewSince = Time.unscaledTime;
        bool single = _test.Mode == MetricTest.TestMode.Single;
        _coverPage.SetActive(view == View.Cover);
        _calPage.SetActive(view == View.CalIntro || view == View.CalFailed);
        _runPage.SetActive(view == View.CalRun);
        _pracPage.SetActive(view == View.Practice);
        _cdPage.SetActive(view == View.Countdown);
        if (view != View.CalRun)
        {
            _target.gameObject.SetActive(false);
            _targetOn = false;
        }

        if (view == View.Cover)
        {
            bool right = _side != "left";
            string cover = right ? V3.T("左眼", "left eye") : V3.T("右眼", "right eye");
            string see = right ? V3.T("右眼", "right eye") : V3.T("左眼", "left eye");
            _coverPanel.Title.text = right ? V3.T("右眼檢測", "Right eye") : V3.T("左眼檢測", "Left eye");
            V3.SetTag(_coverPanel, right ? V3.T("第 1 眼，共 2 眼", "Eye 1 of 2") : V3.T("第 2 眼，共 2 眼", "Eye 2 of 2"));
            V3.SetLine(_coverLine, V3.T($"請用手遮住<color=#6E56CF>{cover}</color>，\n用{see}看畫面。", $"Cover your <color=#6E56CF>{cover}</color>,\nlook with your {see}."));
            // as in a mirror: the user's left eye drawn on the left; the covered one fades under the palm
            _palmL.enabled = right;
            _palmR.enabled = !right;
            _eyeL.color = V3.A(V3.Ink, right ? 0.22f : 1f);
            _eyeR.color = V3.A(V3.Ink, right ? 1f : 0.22f);
            _coverLabelL.text = V3.T("左眼", "Left");
            _coverLabelR.text = V3.T("右眼", "Right");
            _coverChipL.color = right ? V3.Violet : V3.Mint;
            _coverLabelL.color = right ? Color.white : V3.Ink;
            _coverChipR.color = right ? V3.Mint : V3.Violet;
            _coverLabelR.color = right ? V3.Ink : Color.white;
        }
        if (view == View.CalIntro || view == View.CalFailed)
        {
            V3.SetNumber(_calSide, single ? 3 : 2);
            _calSide.Of.text = single ? "/ 04" : "/ 03";
            SetSteps(_calSide, single, 2, 1);
            bool failed = view == View.CalFailed;
            _retry.Root.gameObject.SetActive(failed);
            _calMap.SetActive(!failed);
            V3.SetTag(_calPanel, failed ? V3.T("需要重來", "Try again") : V3.T("自動開始", "Starts by itself"));
            V3.SetLine(_calLine, failed ? V3.T("這次沒有成功，\n沒關係，再試一次。", "Not this time.\nLet's try again.")
                : V3.T("接下來頭不要動，\n用眼睛依序看圓點。", "Keep your head still,\nlook at each dot."));
            _calText.text = failed ? V3.T("校正沒有成功", "Calibration did not succeed") : V3.T("準備開始", "Getting ready");
            _calSub.text = failed ? V3.T("請按「重新校正」，再看一次圓點", "Press Recalibrate and look at the dots again")
                : V3.T("圓點出現時，看著它直到變綠", "Look at each dot until it turns green");
            _calDot.color = failed ? V3.Red : V3.Ink;
            _calHold.sizeDelta = new Vector2(0f, 3f);
        }
        if (view == View.Practice)
        {
            V3.SetTag(_pracPanel, V3.T("用眼睛操作", "Use your eyes"));
            _pracLine.Text.text = "";
            SetSteps(_pracSide, single, 3, 2);
            V3.SetNumber(_pracSide, single ? 4 : 3);
            _pracSide.Of.text = single ? "/ 04" : "/ 03";
        }
    }

    // The step column: one-eye mode adds 遮眼 (cover) as step 2.
    private static void SetSteps(V3.SideParts side, bool single, int singleCurrent, int bothCurrent)
    {
        string[] zh = single ? new[] { "位置", "遮眼", "校正", "練習" } : new[] { "位置", "校正", "練習" };
        string[] en = single ? new[] { "Position", "Cover", "Calibrate", "Practice" } : new[] { "Position", "Calibrate", "Practice" };
        for (int i = 0; i < side.Steps.Length; i++)
        {
            bool used = i < zh.Length;
            side.Steps[i].gameObject.SetActive(used);
            side.StepDots[i].gameObject.SetActive(used);
            if (used)
                side.Steps[i].text = V3.T(zh[i], en[i]);
        }
        V3.SetStep(side, single ? singleCurrent : bothCurrent);
    }

    private void Update()
    {
        if (_view == View.None)
            return;
        // something else took over (recalibrate, home): drop the page
        if (!_flow.FlowVisible)
        {
            Show(View.None);
            return;
        }
        if (_view == View.CalRun || _view == View.Countdown)
            _flow.Plain = true;
        float t = Time.unscaledTime - _viewSince;
        switch (_view)
        {
            case View.CalIntro:
                _calHold.sizeDelta = new Vector2(622f * Mathf.Clamp01(t / CalIntroSeconds), 3f);
                if (t >= CalIntroSeconds && !_nextSent)
                {
                    _nextSent = true;
                    _cal.NextButton();
                }
                break;
            case View.CalRun:
                UpdateTarget();
                break;
            case View.Practice:
                UpdatePractice();
                break;
            case View.Countdown:
                float left = Mathf.Max(0f, _cdSeconds - t);
                _cdNumber.text = Mathf.Max(1, Mathf.CeilToInt(left)).ToString();
                _cdArc.fillAmount = 1f - left / _cdSeconds;
                break;
        }
    }

    private void UpdateTarget()
    {
        if (!_target.gameObject.activeSelf)
            return;
        float dt = Time.unscaledDeltaTime;
        // in: pop from .6 with a little overshoot; out: shrink to .4 and fade
        float goal = _targetOn ? 1f : 0.4f;
        _targetScale = Mathf.MoveTowards(_targetScale, goal, dt * (_targetOn ? 2.2f : 2f));
        _target.localScale = Vector3.one * _targetScale;
        var point = StimulusPoint.Current;
        float progress = point != null ? point.Progress : 1f;
        _targetArc.fillAmount = _targetOn ? progress : 1f;
        _targetCore.color = progress >= 1f || !_targetOn ? V3.Mint : V3.Violet;
        float halo = Mathf.Repeat(Time.unscaledTime / 1.4f, 1f);
        _targetHalo.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.05f, 1f - Mathf.Pow(1f - halo, 3f));
        _targetHalo.color = V3.A(V3.Violet, 0.22f * (1f - halo));
        if (!_targetOn && _targetScale <= 0.41f)
            _target.gameObject.SetActive(false);
    }

    private void UpdatePractice()
    {
        int done = 0;
        int next = -1;
        for (int i = 0; i < 3; i++)
        {
            if (_tileDone[i]) done++;
            else if (next < 0) next = i;
        }
        for (int i = 0; i < 3; i++)
        {
            var button = _tiles[i].GetComponent<Button>();
            button.interactable = i == next;
            bool isNext = i == next;
            _tileEImg[i].color = _tileDone[i] ? V3.A(V3.Ink, 0.45f) : isNext ? V3.Violet : V3.A(V3.Ink, 0.4f);
            _tileCheck[i].SetActive(_tileDone[i]);
            // the E is a key: looked at, it sinks 10 px and shrinks; done, it sits 16 px down
            float press = _tileDone[i] ? 1f : _tiles[i].Gazed && isNext ? 0.62f : 0f;
            V3KeyPress.Ease(_tileE[i], new Vector2(66f, -56f), press, isNext && !_tiles[i].Gazed);
        }
        bool tilesDone = next < 0;
        _lookTag.SetActive(!tilesDone);
        _lookRing.gameObject.SetActive(!tilesDone && !_tiles[Mathf.Max(0, next)].Gazed);
        if (!tilesDone)
        {
            Vector2 c = new Vector2(TilePos[next].x - 304f + 66f, -(TilePos[next].y - 132f + 56f));
            ((RectTransform)_lookTag.transform).anchoredPosition = new Vector2(c.x - 32f, c.y + 56f + 13f);
            _lookRing.anchoredPosition = c;
            float k = Mathf.Repeat(Time.unscaledTime / 1.4f, 1f);
            _lookRing.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.1f, 1f - Mathf.Pow(1f - k, 3f));
            _lookRing.GetComponentInChildren<Image>().color = V3.A(V3.Violet, 1f - k);
        }

        V3.SetKeyEnabled(_start, tilesDone);
        _start.Fill.rectTransform.sizeDelta = new Vector2(224f, 72f * Mathf.Clamp01(_startTrigger.Progress));
        _pracSub.text = tilesDone ? V3.T("看著按鈕不動，就會開始", "Hold your gaze on the button to start") : $"{done} / 3";
        _pracText.text = tilesDone ? V3.T("很好！現在看著「開始測驗」", "Well done! Now look at \"Start test\"") : V3.T("看著紫色的 E，直到它被按下去", "Look at the violet E until it presses down");
        _pracText.color = tilesDone ? V3.Green : V3.Ink;
        _pracDot.color = tilesDone ? V3.Green : V3.Ink;
        string words = tilesDone ? V3.T("做得很好！\n準備開始測驗。", "Well done!\nReady for the test.") : V3.T("這個圓圈會跟著您的眼睛，\n看著不動就能選取。", "This ring follows your eyes.\nHold still on a thing to pick it.");
        if (_pracLine.Text.text != words)
            V3.SetLine(_pracLine, words);
    }

    // ==================== sprites ====================

    private static readonly System.Collections.Generic.Dictionary<string, Sprite> s_rings = new System.Collections.Generic.Dictionary<string, Sprite>();
    private static Sprite s_palm, s_e;

    // A ring of the given radius and half-width inside a 120-unit square (Design's 120 x 120 SVGs).
    public static Sprite RingSprite(float radius, float halfWidth)
    {
        string key = radius + "/" + halfWidth;
        if (s_rings.TryGetValue(key, out var cached))
            return cached;
        const int size = 240;
        var sprite = V3.Raster(size, p =>
        {
            float d = Mathf.Abs((p / 2f - new Vector2(60f, 60f)).magnitude - radius) - halfWidth;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d * 2f));
        });
        s_rings[key] = sprite;
        return sprite;
    }

    // The optotype E (5 x 5 grid, opening to the right), white to tint.
    public static Sprite ESprite()
    {
        return s_e != null ? s_e : (s_e = V3.Raster(100, p =>
        {
            float x = p.x / 20f, y = 5f - p.y / 20f;   // grid units, y down
            bool ink = x < 1f || (y < 1f) || (y >= 2f && y < 3f) || (y >= 4f);
            return new Color(1f, 1f, 1f, ink && x >= 0f && x <= 5f && y >= 0f && y <= 5f ? 1f : 0f);
        }));
    }

    // Open palm (lucide "hand"), violet stroke.
    private static Sprite PalmSprite()
    {
        if (s_palm != null)
            return s_palm;
        float[][] lines =
        {
            new float[] { 18, 11, 18, 6 }, new float[] { 14, 10, 14, 4 }, new float[] { 10, 10.5f, 10, 6 },
            new float[] { 22, 8, 22, 14 }, new float[] { 6, 14, 6, 10 },
        };
        return s_palm = V3.Raster(96, p =>
        {
            Vector2 u = new Vector2(p.x / 4f, 24f - p.y / 4f);
            float d = float.MaxValue;
            foreach (var l in lines)
                d = Mathf.Min(d, Seg(u, new Vector2(l[0], l[1]), new Vector2(l[2], l[3])) - 1f);
            // finger tips
            foreach (var c in new[] { new Vector2(16, 6), new Vector2(12, 4), new Vector2(8, 6), new Vector2(20, 8) })
                d = Mathf.Min(d, Mathf.Abs((u - c).magnitude - 2f) - 1f + (u.y > c.y ? 10f : 0f));
            // palm: the arc from the little finger down round the heel of the hand to the thumb
            float palm = Mathf.Abs((u - new Vector2(14f, 14f)).magnitude - 8f) - 1f;
            if (u.y > 14f)
                d = Mathf.Min(d, palm);
            d = Mathf.Min(d, Seg(u, new Vector2(2.4f, 16.2f), new Vector2(7f, 20f)) - 1f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d * 4f));
        });
    }

    private static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }
}

// The "pressed key" look for gaze-selected E's (practice tiles, test options): looked at, the E sinks and
// shrinks; no colour, no shadow. press 0 = up, 1 = all the way (16 px down, 76 %). Eased over ~.45 s.
public static class V3KeyPress
{
    private static readonly System.Collections.Generic.Dictionary<RectTransform, float> s_state = new System.Collections.Generic.Dictionary<RectTransform, float>();

    // Back up at once (e.g. while the E is hidden or popping in); the position only, the scale is left alone.
    public static void Reset(RectTransform e, Vector2 rest)
    {
        s_state[e] = 0f;
        e.anchoredPosition = rest;
    }

    public static void Ease(RectTransform e, Vector2 rest, float press, bool breathe = false)
    {
        s_state.TryGetValue(e, out float shown);
        shown = Mathf.Lerp(shown, press, 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.12f));
        s_state[e] = shown;
        e.anchoredPosition = rest + new Vector2(0f, -16f * shown);
        float scale = 1f - 0.24f * shown;
        if (breathe)
            scale *= 1f + 0.06f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.4f);
        e.localScale = Vector3.one * scale;
    }
}
