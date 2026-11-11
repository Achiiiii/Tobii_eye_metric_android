using System;
using TMPro;
using Tobii;
using UnityEngine;
using UnityEngine.UI;

// V3 pages between the head position check and the test: the cover-eye page (one-eye
// mode), the calibration page with its 3 s start, the calibration dots on a plain page, the gaze practice (one E on
// a square of glass + a gaze "start test" key, replacing the red-dot intro) and the countdown. GazeCalibrationManager
// still runs the sequence; these pages follow its events and call NextButton / ConfirmGazeIntro.
public class V3Setup : MonoBehaviour
{
    private enum View { None, Cover, CalIntro, CalRun, CalFailed, Practice, Countdown }

    private const float CalIntroSeconds = 3f;
    private static readonly float[] TileX = { 512f };   // the practice tile's centre (y TileY + 90): a single E
    private const float TileY = 196f;

    private V3Flow _flow;
    private GazeCalibrationManager _cal;
    private MetricTest _test;
    private GameObject _mainCanvas;

    private View _view = View.None;
    private string _side = "both";
    private float _viewSince;
    private bool _nextSent;

    // pages
    private GameObject _coverPage, _calPage, _runPage, _pracPage, _cdPage;
    private TextMeshProUGUI _coverNote, _coverTitle, _calNote, _calTitle, _calAuto;
    private GameObject _calAutoKey;
    private V3.LineParts _coverLine, _calLine, _pracLine;
    private static readonly float[] LensX = { 692f, 868f };   // the trial frame's lenses, as in a mirror: the user's left eye on the left
    private readonly V3Instruments.Lens[] _lens = new V3Instruments.Lens[2];
    private readonly Image[] _lensLamp = new Image[2], _lensHalo = new Image[2];
    private readonly TextMeshProUGUI[] _lensEye = new TextMeshProUGUI[2], _lensHow = new TextMeshProUGUI[2];
    private readonly Image[] _calCore = new Image[5], _calHalo = new Image[5];
    private readonly float[] _calLitAt = new float[5];
    private int _calLit;
    private V3.GuideParts _calHold;
    private V3.KeyParts _retry;
    private GameObject _calMap;

    private TextMeshProUGUI _runText;
    // a dot not taken for this long: the head has probably moved away - offer to line up again or go home
    private const float StuckSeconds = 20f;
    private GameObject _stuck;
    private float _dotSince;
    private Action _recalibrate, _goHome;
    private RectTransform _target;
    private Image _targetCore;
    private V3LiquidRing _targetRing;
    private FollowGazePoint2D _gaze;
    private readonly V3GazeSteadiness _targetSteadiness = new V3GazeSteadiness();
    private float _targetProgress, _targetPopT = 1f;
    private Image _targetHalo;
    private bool _targetOn;
    private float _targetScale;

    private ButtonTrigger[] _tiles = new ButtonTrigger[TileX.Length];
    private RectTransform[] _tileE = new RectTransform[TileX.Length];
    private Image[] _tileEImg = new Image[TileX.Length];
    private V3GlassFace[] _tileGlass = new V3GlassFace[TileX.Length];
    private Image[][] _tileRim = new Image[TileX.Length][];
    private GameObject[] _tileCheck = new GameObject[TileX.Length];
    private bool[] _tileDone = new bool[TileX.Length];
    private GameObject _lookTag;
    private RectTransform _lookRing;
    private V3.KeyParts _start;
    private ButtonTrigger _startTrigger;
    private TextMeshProUGUI _pracText, _pracSub;

    private TextMeshProUGUI _cdTag, _cdNumber, _cdCoverText;
    private V3.GuideParts _cdGuide;
    private GameObject _cdCover;
    private float _cdSeconds = 3f;

    public static V3Setup Create(V3Flow flow, GazeCalibrationManager cal, MetricTest test, GameObject mainCanvas, Action recalibrate, Action goHome)
    {
        var root = V3.Full("Setup", flow.Pages);
        var page = root.gameObject.AddComponent<V3Setup>();
        page._flow = flow;
        page._cal = cal;
        page._test = test;
        page._mainCanvas = mainCanvas;
        page._recalibrate = recalibrate;
        page._goHome = goHome;
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
            pageArt.color = V3.LightFlat;
        }
        cal.TrialInstructionsShown += page.OnInstructions;
        cal.CalibrationStarted += page.OnCalibrationStarted;
        cal.StimulusShown += page.OnStimulusShown;
        cal.StimulusCleared += page.OnStimulusCleared;
        cal.CalibrationFailed += () => { V3Sounds.Play(V3Sounds.Cue.Fail); KebbiLight.Flash(KebbiLight.SoftRed); page.Show(View.CalFailed); };
        cal.CalibrationEnded += () => { V3Sounds.Play(V3Sounds.Cue.Done); KebbiLight.Flash(KebbiLight.Green); };
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
        BuildStuck();
        foreach (var page in new[] { _coverPage, _calPage, _pracPage, _cdPage })
            page.AddComponent<V3PageIn>();
        Show(View.None);
    }

    // One-eye mode: which eye to cover - the note, the title, Kebbi's line and tips on the left; on the right a trial
    // frame, as in a mirror: the covered eye's lens frosted with a violet palm on it, the other clear with the eye behind
    // it, a lamp and the words under each.
    private void BuildCover(Transform root)
    {
        _coverPage = V3.Full("Cover", root).gameObject;
        var t = _coverPage.transform;
        _coverNote = V3.Note(t, 40f, 70f);
        _coverTitle = V3.Title(t, 40f, 106f, 560f, 60f, V3Type.Display);
        _coverLine = V3.KebbiLine(t, 40f, 190f, V3Type.Key, 460f);
        V3.Text("Tip", t, V3.ZhMedium, "測驗中請一直遮著", "Keep it covered during the test", V3Type.Body, V3.Text2, 142f, 280f, 460f, 28f);
        const float lensY = 232f;
        for (int i = 0; i < 2; i++)
        {
            _lens[i] = V3Instruments.CreateLens(t, LensX[i], lensY, 75f, 8f, PalmSprite());
            _lensHalo[i] = V3.Sprite("Halo", t, UiFactory.Circle, Color.clear, 0f, 323f, 24f, 24f);
            _lensLamp[i] = V3.Sprite("Lamp", _lensHalo[i].transform, UiFactory.Circle, Color.clear, 6f, 6f, 12f, 12f);
            V3LampGlow.Attach(_lensLamp[i], 12f);
            _lensEye[i] = V3.Text("Eye", t, V3.Zh, "", V3Type.Key, V3.Ink, 0f, 320f, 120f, 32f, TextAlignmentOptions.Left);
            _lensHow[i] = V3.Text("How", t, V3.ZhMedium, "", V3Type.BodyS, V3.Text2, LensX[i] - 70f, 354f, 140f, 26f, TextAlignmentOptions.Top);
        }
        V3.Key(t, "繼續", "Continue", "arrowR", V3.Green, 772f, 470f, 210f, false, () => { if (_view == View.Cover) Show(View.CalIntro); });
    }

    // Calibration (and its failure): note, title, Kebbi's line on the left; on the right a glass plate shaped like the
    // screen with a graticule cut in it and five lights where the dots will appear, coming on in their order (all red
    // when it failed); "starts by itself" with its guide, or 重新校正.
    private void BuildCalCard(Transform root)
    {
        _calPage = V3.Full("Calibration", root).gameObject;
        var t = _calPage.transform;
        _calNote = V3.Note(t, 40f, 70f);
        _calTitle = V3.Title(t, 40f, 106f, 560f, 110f, V3Type.Display);
        _calLine = V3.KebbiLine(t, 40f, 240f, V3Type.Key, 460f);
        // the screen in miniature: 272 x 160 (1024 x 600)
        const float sx = 644f, sy = 170f, sw = 272f, sh = 160f;
        V3.PatchShadow(t, sx, sy, sw, sh, false);
        V3GlassFace.Attach(V3.Box("Glass", t, Color.white, sx, sy, sw, sh), sw, sh, 10f, 5f, 0.3f);
        V3.Sprite("Reticle", t, V3Instruments.Reticle((int)sw, (int)sh), Color.white, sx, sy, sw, sh);
        // the five dots in their order (centre, top left, top right, bottom right, bottom left)
        _calMap = V3.Rect("Map", t, 0f, 0f, V3.W, V3.H).gameObject;
        var dots = new[] { new Vector2(.5f, .5f), new Vector2(.15f, .2f), new Vector2(.85f, .2f), new Vector2(.85f, .8f), new Vector2(.15f, .8f) };
        for (int i = 0; i < dots.Length; i++)
            dots[i] = new Vector2(sx + dots[i].x * sw, sy + dots[i].y * sh);
        for (int i = 0; i < dots.Length; i++)
        {
            V3.Sprite("Edge", _calMap.transform, UiFactory.Circle, V3.A(V3.Ink, 0.35f), dots[i].x - 8f, dots[i].y - 8f, 16f, 16f);
            V3.Sprite("Dot", _calMap.transform, UiFactory.Circle, Color.white, dots[i].x - 6f, dots[i].y - 6f, 12f, 12f);
            _calHalo[i] = V3.Sprite("Halo", _calMap.transform, UiFactory.Circle, Color.clear, dots[i].x - 15f, dots[i].y - 15f, 30f, 30f);
            _calCore[i] = V3.Sprite("Core", _calMap.transform, UiFactory.Circle, Color.clear, dots[i].x - 6f, dots[i].y - 6f, 12f, 12f);
            V3.Text("N", _calMap.transform, V3.Mono, (i + 1).ToString(), V3Type.Data, V3.Text2, dots[i].x + 11f, dots[i].y - 16f, 20f, 14f);
        }
        // starting by itself: a glass piece in the key's place, the key's size - the words, the seconds left, a thick
        // liquid guide
        _calAutoKey = V3.Rect("AutoStart", t, 772f, 470f, 210f, 72f).gameObject;
        var auto = _calAutoKey.transform;
        V3.PatchShadow(auto, 0f, 0f, 210f, 72f, false);
        V3GlassFace.Attach(V3.Box("Glass", auto, Color.white, 0f, 0f, 210f, 72f), 210f, 72f);
        V3.Text("Label", auto, V3.Zh, "自動開始", "Auto start", V3Type.Key, V3.Ink, 20f, 0f, 130f, 58f, TextAlignmentOptions.Left);
        _calAuto = V3.Text("Seconds", auto, V3.Num, "3", V3Type.Display, V3.Ink, 128f, 4f, 64f, 54f, TextAlignmentOptions.Right);
        _calHold = V3.Guide(auto, 16f, 51f, 178f);
        // only the dots again: the head position was already confirmed
        _retry = V3.Key(t, "重新校正", "Recalibrate", "target", V3.Green, 772f, 470f, 210f, false, () => { if (_view == View.CalFailed) _cal.RetryCalibration(); });
    }

    private void BuildRun(Transform root)
    {
        _runPage = V3.Full("CalibrationRun", root).gameObject;
        _runText = V3.Text("Text", _runPage.transform, V3.Zh, "", V3Type.Key, V3.Ink, 0f, 34f, V3.W, 44f, TextAlignmentOptions.Center);
        // target (120 x 120): pulsing halo, white disc, the liquid ring filling as it is looked at (as the selection
        // ring: calmer the stiller the eyes, catching the light toward the gaze, locking when the dot is taken), violet
        // core, white pip; it sinks a little as it fills and springs back
        _target = V3.Rect("Target", _runPage.transform, 0f, 0f, 120f, 120f);
        _target.pivot = UiFactory.Center;
        _targetHalo = V3.Sprite("Halo", _target, RingSprite(40f, 1f), V3.A(V3.Violet, 0.22f), 0f, 0f, 120f, 120f);
        V3.Sprite("Disc", _target, UiFactory.Circle, Color.white, 31f, 31f, 58f, 58f);
        _targetRing = V3LiquidRing.Create(_target, 120f, 26f, 4.2f, V3.A(V3.Violet, 0.22f));
        _gaze = FindObjectOfType<FollowGazePoint2D>(true);
        _targetCore = V3.Sprite("Core", _target, UiFactory.Circle, V3.Violet, 49f, 49f, 22f, 22f);
        V3.Sprite("Pip", _target, UiFactory.Circle, Color.white, 56.5f, 56.5f, 7f, 7f);
        _target.gameObject.SetActive(false);
    }

    // Gaze practice: note and title, the test's black E on a square of the keys' glass (marked while not yet done, with
    // a "看這裡" tag and a pulsing ring; chosen, the glass lights green inside its green rim, as an answer does),
    // Kebbi's line, the gaze "開始測驗" key.
    private void BuildPractice(Transform root)
    {
        _pracPage = V3.Full("Practice", root).gameObject;
        var t = _pracPage.transform;
        _pracSub = V3.Note(t, 40f, 70f);
        _pracText = V3.Title(t, 40f, 106f, 940f, 44f, V3Type.Title);
        for (int i = 0; i < TileX.Length; i++)
        {
            int index = i;
            float x = TileX[i] - 90f;
            V3.PatchShadow(t, x, TileY, 180f, 180f, false);
            var button = UiFactory.CreateButton("Tile", t, UiFactory.White, Color.white, () => OnTileDone(index));
            button.transition = Selectable.Transition.None;
            var tile = V3.At((RectTransform)button.transform, x, TileY, 180f, 180f);
            _tileGlass[i] = V3GlassFace.Attach((Image)button.targetGraphic, 180f, 180f, 10f, 5f);
            _tiles[i] = button.gameObject.AddComponent<ButtonTrigger>();
            // chosen: the green rim and its light, as round a chosen answer
            var glow = V3.Sprite("RimGlow", tile, V3.RimGlowSprite, V3.Green, -4f - V3.GlowReach, -4f - V3.GlowReach, 188f + 2f * V3.GlowReach, 188f + 2f * V3.GlowReach);
            glow.type = Image.Type.Sliced;
            var rim = V3.Sprite("Rim", tile, V3.RimSprite, V3.Green, -4f, -4f, 188f, 188f);
            rim.type = Image.Type.Sliced;
            _tileRim[i] = new[] { glow, rim };
            var e = V3.Sprite("E", tile, ESprite(), V3.Ink, 38f, 38f, 104f, 104f);
            e.rectTransform.pivot = UiFactory.Center;
            e.rectTransform.anchoredPosition = new Vector2(90f, -90f);
            _tileE[i] = e.rectTransform;
            _tileEImg[i] = e;
            _tileCheck[i] = V3.Sprite("Badge", tile, UiFactory.Circle, V3.Green, 162f, -12f, 30f, 30f).gameObject;
            V3.Sprite("Check", _tileCheck[i].transform, V3.Icon("check"), V3.Ink, 6f, 6f, 18f, 18f);
        }
        _lookRing = V3.Rect("LookRing", t, 0f, 0f, 150f, 150f);
        _lookRing.pivot = UiFactory.Center;
        V3.Sprite("Ring", _lookRing, RingSprite(43f, 1f), V3.Violet, 0f, 0f, 150f, 150f);
        _lookTag = V3.Box("LookTag", t, V3.Violet, 0f, 0f, 64f, 26f).gameObject;
        V3.Text("Text", _lookTag.transform, V3.Zh, "看這裡", "Look", V3Type.Label, Color.white, 0f, 0f, 64f, 26f, TextAlignmentOptions.Center);
        _pracLine = V3.KebbiLine(t, 40f, 414f, V3Type.Key, 600f);

        _start = V3.Key(t, "開始測驗", "Start test", "arrowR", V3.Green, 772f, 470f, 210f);
        _start.Button.onClick.AddListener(() =>
        {
            if (_view != View.Practice)
                return;
            V3Sounds.Play(V3Sounds.Cue.Select);
            _cal.ConfirmGazeIntro();
        });
        _startTrigger = _start.Button.gameObject.AddComponent<ButtonTrigger>();
    }

    // The countdown: the number alone, large, with a glass tube under it filling over the seconds.
    // Over the calibration dots: the calibration seems stuck (a dot not taken for a while, usually because the head has
    // moved out of the camera's view) - line up again (back to the head position check) or go home. It closes by itself
    // if the dot is taken after all.
    private void BuildStuck()
    {
        var dim = UiFactory.CreateImage("Stuck", _runPage.transform, UiFactory.White, V3.Veil, true);
        UiFactory.Stretch(dim.rectTransform);
        const float w = 520f, h = 290f;
        var card = V3.Rect("Card", dim.transform, (V3.W - w) / 2f, 150f, w, h);
        V3.GlassPanel(card, 0f, 0f, w, h);
        V3.Text("Title", card, V3.Zh, "校正好像卡住了", "Calibration seems stuck", V3Type.Title, V3.Ink, 0f, 36f, w, 40f, TextAlignmentOptions.Top);
        var sub = V3.Text("Sub", card, V3.Zh, "頭可能移開了，可以重新對準位置，\n或回到首頁", "Your head may have moved.\nLine up again, or go home.", V3Type.BodyS, V3.Text2, 0f, 86f, w, 56f, TextAlignmentOptions.Top);
        sub.lineSpacing = -6f;
        V3.Key(card, "回首頁", "Home", "home", Color.white, 40f, 180f, 210f, false, () => { _stuck.SetActive(false); _goHome?.Invoke(); });
        V3.Key(card, "重新對準", "Line up", "target", V3.Green, 270f, 180f, 210f, false, () => { _stuck.SetActive(false); _recalibrate?.Invoke(); });
        _stuck = dim.gameObject;
        V3CardIn.Attach(_stuck, dim, card, true);
        _stuck.SetActive(false);
    }

    private void BuildCountdown(Transform root)
    {
        _cdPage = V3.Full("Countdown", root).gameObject;
        var t = _cdPage.transform;
        _cdTag = V3.Note(t, 40f, 40f);
        _cdNumber = V3.Text("Number", t, V3.Zh, "3", V3Type.Huge, V3.Ink, 362f, 110f, 300f, 260f, TextAlignmentOptions.Center);
        // a glass tube under it, filling over the three seconds
        _cdGuide = V3.Guide(t, 512f - 130f, 390f, 260f);
        V3.Text("Title", t, V3.Zh, "即將開始，請注視畫面中央", "Starting soon - look at the centre", V3Type.Key, V3.Ink, 0f, 466f, V3.W, 34f, TextAlignmentOptions.Top);
        _cdCover = V3.Rect("Cover", t, 0f, 508f, V3.W, 34f).gameObject;
        _cdCoverText = V3.Text("Text", _cdCover.transform, V3.Zh, "", V3Type.Body, V3.Violet, 0f, 0f, V3.W, 34f, TextAlignmentOptions.Top);
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
        _dotSince = Time.unscaledTime;
        _stuck.SetActive(false);
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
        _stuck.SetActive(false);
        _targetOn = false;
        V3Sounds.Play(V3Sounds.Cue.DotDone);
        _runText.text = V3.T("很好，請保持頭部不動", "Good, keep your head still");
    }

    private void OnPracticeStarted()
    {
        for (int i = 0; i < TileX.Length; i++)
            _tileDone[i] = false;
        Show(View.Practice);
        V3.Speak("這個圓圈會跟著您的眼睛。請看著標示的E不動，就能選取。");
    }

    private void OnTileDone(int index)
    {
        if (_view != View.Practice || _tileDone[index])
            return;
        _tileDone[index] = true;
        V3Sounds.Play(V3Sounds.Cue.Select);
        if (Array.TrueForAll(_tileDone, d => d))
        {
            V3.Speak("做得很好！請看著開始測驗的按鈕");
            V3Sounds.Play(V3Sounds.Cue.Happy);
            KebbiLight.Flash(KebbiLight.Green);
        }
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
        // the cover page (before each eye's calibration): a rough demonstration of covering an eye
        if (view == View.Cover && _view != View.Cover)
        {
            KebbiMotion.Allowed = true;
            KebbiMotion.Play("cover_eye");
        }
        _view = view;
        _viewSince = Time.unscaledTime;
        V3Flow.SheenOffSetup = view == View.CalRun || view == View.Countdown;
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
            _coverTitle.text = right ? V3.T("右眼檢測", "Right eye") : V3.T("左眼檢測", "Left eye");
            _coverNote.text = V3.T("遮眼", "Cover") + "  ·  " + (right ? V3.T("第 1 眼，共 2 眼", "eye 1 of 2") : V3.T("第 2 眼，共 2 眼", "eye 2 of 2"));
            V3.SetLine(_coverLine, V3.T($"請用手遮住<u>{cover}</u>，\n用{see}看畫面。", $"Cover your <u>{cover}</u>,\nlook with your {see}."));
            for (int i = 0; i < 2; i++)
            {
                bool covered = (i == 0) == right;
                _lens[i].Covered = covered;
                Color lamp = covered ? V3.Violet : V3.GreenLit;
                _lensLamp[i].color = lamp;
                _lensHalo[i].color = V3.A(lamp, 0.22f);
                _lensEye[i].text = i == 0 ? V3.T("左眼", "Left") : V3.T("右眼", "Right");
                _lensHow[i].text = covered ? V3.T("遮住", "Cover") : V3.T("看畫面", "Look");
                // the lamp and the eye's name centred together under the lens
                float left = LensX[i] - (30f + _lensEye[i].preferredWidth) / 2f;
                _lensHalo[i].rectTransform.anchoredPosition = new Vector2(left, -323f);
                _lensEye[i].rectTransform.anchoredPosition = new Vector2(left + 30f, -320f);
            }
        }
        if (view == View.CalIntro || view == View.CalFailed)
        {
            bool failed = view == View.CalFailed;
            _retry.Root.gameObject.SetActive(failed);
            _calLit = 0;
            SetCalLights(failed ? 5 : 0, failed ? V3.Red : V3.Violet);
            _calAutoKey.SetActive(!failed);
            V3.SetGuide(_calHold, 0f);
            string which = !single ? V3.T("雙眼", "both eyes") : _side == "left" ? V3.T("第 2 眼，共 2 眼", "eye 2 of 2") : V3.T("第 1 眼，共 2 眼", "eye 1 of 2");
            _calNote.text = V3.T("眼睛校正", "Calibration") + "  ·  " + (failed ? V3.T("需要重來", "try again") : which);
            _calTitle.fontSizeMax = V3Type.Display;
            _calTitle.text = failed ? V3.T("校正沒有成功", "Calibration did not succeed") : V3.T("接下來頭不要動，\n用眼睛依序看圓點。", "Keep your head still,\nlook at each dot.");
            _calLine.Root.gameObject.SetActive(!failed);
            if (failed)
                V3.Speak(V3.T("這次校正沒有成功，沒關係，請按重新校正，再看一次圓點。", "Calibration did not succeed. Press Recalibrate and look at the dots again."));
            else
                V3.SetLine(_calLine, V3.T("圓點出現時，看著它直到變綠", "Look at each dot until it turns green"));
        }
        if (view == View.Practice)
            _pracLine.Text.text = "";
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
        _flow.NoRuler = true;
        float t = Time.unscaledTime - _viewSince;
        switch (_view)
        {
            case View.Cover:
                // the lenses frost over or clear toward their states
                foreach (var lens in _lens)
                    lens.Ease();
                break;
            case View.CalIntro:
                V3.SetGuide(_calHold, t / CalIntroSeconds);
                _calAuto.text = Mathf.Max(1, Mathf.CeilToInt(CalIntroSeconds - t)).ToString();
                SetCalLights(Mathf.Min(5, 1 + Mathf.FloorToInt(t / CalIntroSeconds * 5f)), V3.Violet);
                if (t >= CalIntroSeconds && !_nextSent)
                {
                    _nextSent = true;
                    _cal.NextButton();
                }
                break;
            case View.CalFailed:
                SetCalLights(5, V3.Red);
                break;
            case View.CalRun:
                if (_targetOn && !_stuck.activeSelf && Time.unscaledTime - _dotSince > StuckSeconds)
                {
                    _stuck.SetActive(true);
                    V3Sounds.Play(V3Sounds.Cue.Pause);
                    V3.Speak(V3.T("校正好像卡住了，可以重新對準，或回到首頁。", "Calibration seems stuck. You can line up again or go back home."));
                }
                UpdateTarget();
                break;
            case View.Practice:
                UpdatePractice();
                break;
            case View.Countdown:
                float left = Mathf.Max(0f, _cdSeconds - t);
                _cdNumber.text = Mathf.Max(1, Mathf.CeilToInt(left)).ToString();
                V3.SetGuide(_cdGuide, 1f - left / _cdSeconds);
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
        var point = StimulusPoint.Current;
        float progress = point != null ? point.Progress : 1f;
        if (_targetOn && progress >= 1f && _targetProgress < 1f)
        {
            _targetPopT = 0f;
            _targetRing.Lock();
        }
        if (_targetOn && progress <= 0f)
        {
            _targetRing.Unlock();
            _targetSteadiness.Clear();
        }
        _targetProgress = _targetOn ? progress : 1f;
        _targetRing.Fill = _targetProgress;
        // the eyes' steadiness and where they drift from the dot, while there is gaze
        if (_gaze != null && _gaze.isActiveAndEnabled && _targetOn)
        {
            _targetSteadiness.Add(_gaze.DisplayedScreenPosition);
            _targetRing.Steady = _targetSteadiness.Steady;
            _targetRing.Lean = _targetSteadiness.Lean;
        }
        _targetPopT = Mathf.Min(1f, _targetPopT + dt / V3Motion.Quick);
        float press = _targetOn && _targetProgress < 1f ? 1f - 0.06f * _targetProgress : 1f + 0.1f * Mathf.Sin(_targetPopT * Mathf.PI);
        _target.localScale = Vector3.one * (_targetScale * press);
        _targetCore.color = progress >= 1f || !_targetOn ? V3.Green : V3.Violet;
        float halo = Mathf.Repeat(Time.unscaledTime / 1.4f, 1f);
        _targetHalo.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.05f, 1f - Mathf.Pow(1f - halo, 3f));
        _targetHalo.color = V3.A(V3.Violet, 0.22f * (1f - halo));
        if (!_targetOn && _targetScale <= 0.41f)
            _target.gameObject.SetActive(false);
    }

    // The glass plate's lights: the first `lit` of them on, in that colour.
    // Each comes on with a small burst: its halo starts wide and bright and settles.
    private void SetCalLights(int lit, Color colour)
    {
        float now = Time.unscaledTime;
        for (int i = _calLit; i < lit; i++)
            _calLitAt[i] = now;
        _calLit = lit;
        for (int i = 0; i < _calCore.Length; i++)
        {
            float k = V3Motion.EaseOut((now - _calLitAt[i]) / V3Motion.Slow);
            _calCore[i].color = i < lit ? colour : Color.clear;
            _calHalo[i].color = i < lit ? V3.A(colour, Mathf.Lerp(0.6f, 0.24f, k)) : Color.clear;
            _calHalo[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.9f, 1f, k);
        }
    }

    private void UpdatePractice()
    {
        int done = 0;
        int next = -1;
        for (int i = 0; i < TileX.Length; i++)
        {
            if (_tileDone[i]) done++;
            else if (next < 0) next = i;
        }
        for (int i = 0; i < TileX.Length; i++)
        {
            var button = _tiles[i].GetComponent<Button>();
            button.interactable = i == next;
            bool isNext = i == next;
            _tileEImg[i].color = _tileDone[i] ? V3.A(V3.Ink, 0.45f) : isNext ? V3.Ink : V3.A(V3.Ink, 0.3f);
            _tileCheck[i].SetActive(_tileDone[i]);
            _tileGlass[i].Lit = _tileDone[i];
            foreach (var line in _tileRim[i])
                line.enabled = _tileDone[i];
            // the E is a key: looked at, it sinks 10 px and shrinks (the glass shading as it is pressed); chosen, it comes
            // back up
            float press = !_tileDone[i] && _tiles[i].Gazed && isNext ? 0.62f : 0f;
            _tileGlass[i].Press = press;
            V3KeyPress.Ease(_tileE[i], new Vector2(90f, -90f), press, isNext && !_tiles[i].Gazed);
        }
        bool tilesDone = next < 0;
        _lookTag.SetActive(!tilesDone);
        _lookRing.gameObject.SetActive(!tilesDone && !_tiles[Mathf.Max(0, next)].Gazed);
        if (!tilesDone)
        {
            Vector2 c = new Vector2(TileX[next], -(TileY + 90f));
            ((RectTransform)_lookTag.transform).anchoredPosition = new Vector2(c.x - 32f, -(TileY - 34f));
            _lookRing.anchoredPosition = c;
            float k = Mathf.Repeat(Time.unscaledTime / 1.4f, 1f);
            _lookRing.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.1f, 1f - Mathf.Pow(1f - k, 3f));
            _lookRing.GetComponentInChildren<Image>().color = V3.A(V3.Violet, 1f - k);
        }

        V3.SetKeyEnabled(_start, tilesDone);
        _pracSub.text = V3.T("視線練習  ·  用眼睛操作", "Gaze practice  ·  use your eyes") + (tilesDone || TileX.Length == 1 ? "" : $"  ·  {done} / {TileX.Length}");
        _pracText.text = tilesDone ? V3.T("很好！現在看著「開始測驗」", "Well done! Now look at \"Start test\"") : V3.T("看著標示的 E，直到它被按下去", "Look at the marked E until it presses down");
        // the title says what to do; Kebbi's line comes only once it is done
        _pracLine.Root.gameObject.SetActive(tilesDone);
        string words = V3.T("做得很好！\n準備開始測驗。", "Well done!\nReady for the test.");
        if (tilesDone && _pracLine.Text.text != words)
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
