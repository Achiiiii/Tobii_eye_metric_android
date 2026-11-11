using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// V3 test and result pages. The test stays MetricTest's own: same symbol, sizes, rules, timing
// and gaze-dwell buttons. Only the look changes: the light box (a flat light-box white behind the symbol, the frame
// round the edge), a status row (eye, level, 11 small lights, the head lamp), the four answer E's bare (same size as
// the symbol, the ophthalmologist's design) placed 80 px in from the edges and pressed like keys while looked at, a
// faint framing of the symbol (as the home E's), a neutral ring of light where an answer is taken, and the status row
// lighting up when the level changes. Nothing is drawn over the symbol: there is no gap between trials to show a
// level change in the middle, so it plays in the status row.
// The result is redrawn from MetricTest's ResultShown (values, message) as readouts (V3Readout), one per value.
public class V3Test : MonoBehaviour
{
    // answer E centres on the 1024 x 600 screen (y down)
    private static readonly Dictionary<string, Vector2> OptionAt = new Dictionary<string, Vector2>
    {
        { "up", new Vector2(512f, 138f) }, { "down", new Vector2(512f, 490f) }, { "left", new Vector2(146f, 300f) }, { "right", new Vector2(878f, 300f) },
    };

    private V3Flow _flow;
    private MetricTest _test;
    private GameObject _resultPage;
    private Action _goHome;

    private GameObject _testView;
    private TextMeshProUGUI _tag, _level;
    private Image[] _bar = new Image[11], _barRing = new Image[11], _barGlow = new Image[11];
    // the symbol's framing marks (an L at each corner), snapping in a little as each trial begins
    private readonly RectTransform[] _frame = new RectTransform[8];
    private float _frameSnap = 1f;
    // where an answer is taken: a violet ring spreading and fading (the same for every answer, right or wrong)
    private RectTransform _taken;
    private Image _takenImage;
    private float _takenT = 1f;
    // a level change: the level pops and flashes, a light runs along the small lights to the new one
    private int _shownLevel = -1;
    private float _levelT = 1f;
    private Image _levelSweep;
    // the head lamp: green while in place, violet (as on the pause card) when drifting toward the pause, grey without a
    // face
    private Image _headLamp, _headHalo;
    private HeadFollow _head;
    private RectTransform[] _options;
    private ButtonTrigger[] _optionTriggers;
    private Vector2[] _optionRest;
    private bool _laidOut;

    private GameObject _resultView;
    private TextMeshProUGUI _resNote;
    private V3.LineParts _line;
    private RectTransform _scores;
    private bool _hasResult;

    public static V3Test Create(V3Flow flow, MetricTest test, GameObject resultPage, Action goHome)
    {
        var root = V3.Full("Test", flow.Pages);
        var page = root.gameObject.AddComponent<V3Test>();
        page._flow = flow;
        page._test = test;
        page._resultPage = resultPage;
        page._goHome = goHome;
        page.BuildTest(root);
        page.BuildResult(root);
        test.ResultShown += page.OnResult;
        V3Flow.Conceal(resultPage);
        page.RestyleScene();
        return page;
    }

    // The scene's test objects: its page in the light box's flat white, hit areas drawn clear, the symbols in ink.
    private void RestyleScene()
    {
        var page = _test.GetComponent<Image>();
        if (page != null)
            page.color = V3.LightFlat;
        var black = _test.blackRT.GetComponent<Image>();
        if (black != null)
            black.color = V3.Ink;
        _options = _test.sidesRT;
        _optionTriggers = new ButtonTrigger[_options.Length];
        _optionRest = new Vector2[_options.Length];
        for (int i = 0; i < _options.Length; i++)
        {
            var image = _options[i].GetComponent<Image>();
            if (image != null)
                image.color = V3.Ink;
            var zone = _options[i].parent.GetComponent<Image>();
            if (zone != null)
                zone.color = new Color(1f, 1f, 1f, 0f);
            _optionTriggers[i] = _options[i].parent.GetComponent<ButtonTrigger>();
        }
    }

    // ==================== test ====================

    private void BuildTest(Transform root)
    {
        _testView = V3.Full("TestView", root).gameObject;
        var t = _testView.transform;
        // the eye, the level, then eleven small lights (done green, the current one ringed, the rest dark)
        _tag = V3.Note(t, 40f, 34f, 120f);
        _level = V3.Text("Level", t, V3.Mono, "", V3Type.Data, V3.Ink, 128f, 26f, 110f, 30f, TextAlignmentOptions.Left, 0.12f);
        for (int i = 0; i < 11; i++)
        {
            _barGlow[i] = V3.Shadow(t, 236f + i * 26f, 38f, 22f, 6f, 6f, 1f, 0f, Color.clear);
            _barRing[i] = Rounded(V3.Sprite("Ring", t, UiFactory.RoundedRect, V3.Green, 236f + i * 26f - 1.5f, 36.5f, 25f, 9f), 4.5f);
            _bar[i] = Rounded(V3.Sprite("Light", t, UiFactory.RoundedRect, V3.A(V3.Ink, 0.3f), 236f + i * 26f, 38f, 22f, 6f), 3f);
        }
        // the level pops about its left middle
        _level.rectTransform.pivot = new Vector2(0f, 0.5f);
        _level.rectTransform.anchoredPosition += new Vector2(0f, -15f);
        _levelSweep = Rounded(V3.Sprite("Sweep", t, UiFactory.RoundedRect, V3.A(V3.GreenLit, 0f), 236f, 37f, 22f, 8f), 4f);
        // the head lamp after the lights
        _headHalo = V3.Sprite("HeadHalo", t, UiFactory.Circle, V3.A(V3.GreenLit, 0.25f), 540f, 32f, 18f, 18f);
        _headLamp = V3.Sprite("HeadLamp", t, UiFactory.Circle, V3.GreenLit, 544f, 36f, 10f, 10f);
        V3LampGlow.Attach(_headLamp, 10f);
        V3.Text("HeadLabel", t, V3.Mono, "位置", "POSITION", V3Type.Micro, V3.A(V3.Ink, 0.55f), 564f, 34f, 90f, 14f, TextAlignmentOptions.Left, 0.12f);
        // the light box's aluminium frame round the edge (the symbol and answers sit well inside it)
        V3.LightBoxFrame(t);
        for (int i = 0; i < _frame.Length; i++)
        {
            var arm = V3.Box("Frame", t, V3.A(V3.Ink, 0.4f), 0f, 0f, 1f, 1.6f).rectTransform;
            arm.pivot = new Vector2(0f, 0.5f);
            _frame[i] = arm;
        }
        _takenImage = V3.Sprite("Taken", t, V3Setup.RingSprite(50f, 2.2f), V3.A(V3.Violet, 0f), 0f, 0f, 10f, 10f);
        _taken = _takenImage.rectTransform;
        _taken.pivot = UiFactory.Center;
        _head = FindObjectOfType<HeadFollow>();
        _test.SymbolShown += () => _frameSnap = 0f;
        _test.SideChosen += side =>
        {
            if (!OptionAt.TryGetValue(side, out var at))
                return;
            _taken.anchoredPosition = new Vector2(at.x, -at.y);
            _takenT = 0f;
        };
        _testView.SetActive(false);
    }

    private void Update()
    {
        bool flow = _flow.FlowVisible;
        bool testing = flow && _test.gameObject.activeInHierarchy;
        V3Flow.SheenOffTest = testing;
        if (_testView.activeSelf != testing)
        {
            _testView.SetActive(testing);
            if (testing)
            {
                _laidOut = false;
                _shownLevel = -1;
            }
        }
        if (testing)
        {
            _flow.Plain = true;
            _flow.Transparent = true;
            UpdateTest();
        }

        bool result = flow && _hasResult && _resultPage.activeInHierarchy;
        if (_resultView.activeSelf != result)
            _resultView.SetActive(result);
        // the questions' progress has nothing more to say here
        if (result)
            _flow.NoRuler = true;
        if (!_resultPage.activeInHierarchy)
            _hasResult = false;
    }

    private void UpdateTest()
    {
        string side = _test.CurrentSide;
        _tag.text = side == "left" ? V3.T("左眼測驗", "Left eye") : side == "both" ? V3.T("雙眼測驗", "Both eyes") : V3.T("右眼測驗", "Right eye");
        int level = _test.Level;
        _level.text = V3.T($"第 <size=22>{level}</size> 級", $"LEVEL <size=22>{level}</size>");
        for (int i = 0; i < 11; i++)
        {
            _bar[i].color = i + 1 < level ? V3.GreenLit : i + 1 == level ? Color.white : V3.A(V3.Groove, 0.3f);
            _barGlow[i].color = V3.A(V3.Green, i + 1 < level ? 0.4f : 0f);
            _barRing[i].enabled = i + 1 == level;
        }
        float dt = V3Motion.Step;
        UpdateLevelChange(level, dt);
        UpdateFrame(dt);
        UpdateTaken(dt);
        UpdateHeadLamp();

        if (!_laidOut)
            LayOutOptions();
        // each answer E is a key: looked at, it sinks; the pop-in tween owns the scale while it runs
        for (int i = 0; i < _options.Length; i++)
        {
            if (DOTween.IsTweening(_options[i]) || !_test.OptionsVisible)
            {
                V3KeyPress.Reset(_options[i], _optionRest[i]);
                continue;
            }
            bool gazed = _optionTriggers[i] != null && _optionTriggers[i].Gazed && _optionTriggers[i].GetComponent<Button>().IsInteractable();
            V3KeyPress.Ease(_options[i], _optionRest[i], gazed ? 0.62f : 0f);
        }
    }

    // The level pops (a little larger, green) and a light runs along the small lights from the old level to the new.
    private void UpdateLevelChange(int level, float dt)
    {
        if (_shownLevel < 0)
            _shownLevel = level;
        if (level != _shownLevel)
        {
            _levelT = 0f;
            _sweepFrom = _shownLevel;
            _shownLevel = level;
        }
        _levelT = Mathf.Min(1f, _levelT + dt / V3Motion.Slow);
        float pop = Mathf.Sin(_levelT * Mathf.PI);
        _level.rectTransform.localScale = Vector3.one * (1f + 0.25f * pop);
        _level.color = Color.Lerp(V3.Ink, V3.GreenLit, pop);
        float run = 1f - Mathf.Pow(1f - Mathf.Clamp01(_levelT / 0.7f), 3f);
        float x = 236f + (Mathf.Lerp(_sweepFrom, level, run) - 1f) * 26f;
        _levelSweep.rectTransform.anchoredPosition = new Vector2(x, -37f);
        _levelSweep.color = V3.A(V3.GreenLit, 0.9f * (1f - _levelT));
    }

    private int _sweepFrom = 1;
    private float _symbolPx = 80f;

    // The framing marks round the symbol (on the scene canvas, measured on the screen): an L at each corner, a margin
    // out from it that never reaches the answer E's; a small snap inward as each trial begins.
    private void UpdateFrame(float dt)
    {
        _frameSnap = Mathf.Min(1f, _frameSnap + dt / V3Motion.Quick);
        var corners = new Vector3[4];
        _test.blackRT.GetWorldCorners(corners);
        float k = V3.W / Screen.width;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var c in corners)
        {
            var at = new Vector2(c.x * k, (Screen.height - c.y) * k);
            min = Vector2.Min(min, at);
            max = Vector2.Max(max, at);
        }
        float size = _symbolPx = max.x - min.x;
        bool shown = _test.blackRT.gameObject.activeInHierarchy && size > 1f;
        // the room between the symbol and the answer E's above and below it (they are the symbol's size)
        float room = (OptionAt["down"].y - OptionAt["up"].y) / 2f - size;
        float margin = Mathf.Min(0.3f * size + 12f, room * 0.45f);
        float arm = Mathf.Clamp(size * 0.22f, 10f, 22f);
        bool on = shown && margin >= 6f;
        float grow = 1f + 0.08f * (1f - (1f - Mathf.Pow(1f - _frameSnap, 3f)));
        Vector2 mid = (min + max) / 2f, half = (max - min) / 2f + Vector2.one * margin;
        half *= grow;
        var pts = new[] { mid + new Vector2(-half.x, -half.y), mid + new Vector2(half.x, -half.y), mid + new Vector2(half.x, half.y), mid + new Vector2(-half.x, half.y) };
        for (int i = 0; i < 4; i++)
        {
            Vector2 next = pts[(i + 1) % 4] - pts[i], prev = pts[(i + 3) % 4] - pts[i];
            Arm(_frame[2 * i], pts[i], next.normalized, arm, on);
            Arm(_frame[2 * i + 1], pts[i], prev.normalized, arm, on);
        }
    }

    private static void Arm(RectTransform arm, Vector2 from, Vector2 dir, float length, bool on)
    {
        arm.gameObject.SetActive(on);
        if (!on)
            return;
        arm.anchoredPosition = new Vector2(from.x - dir.x * 0.8f, -(from.y - dir.y * 0.8f));
        arm.sizeDelta = new Vector2(length, 1.6f);
        arm.localEulerAngles = new Vector3(0f, 0f, -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    // the taken ring: from the E's size outward, fading
    private void UpdateTaken(float dt)
    {
        _takenT = Mathf.Min(1f, _takenT + dt / V3Motion.Slow);
        float k = 1f - Mathf.Pow(1f - _takenT, 3f);
        _taken.sizeDelta = Vector2.one * Mathf.Lerp(_symbolPx * 1.1f, _symbolPx * 1.9f, k);
        _takenImage.color = V3.A(V3.Violet, 0.55f * (1f - _takenT));
    }

    // In place: lateral within half the pause's limit (10 deg) and distance within half of its 5 cm; violet beyond, grey
    // without a face.
    private void UpdateHeadLamp()
    {
        Color c;
        if (_head == null || !_head.HasFace)
            c = V3.A(V3.Ink, 0.3f);
        else
        {
            float off = Mathf.Max(Mathf.Abs(_head.LateralOffsetDeg) / 10f, Mathf.Abs(_head.DistanceOffset * 100f) / 5f);
            c = off < 0.5f ? V3.GreenLit : V3.Violet;
        }
        _headLamp.color = c;
        _headHalo.color = V3.A(c, 0.25f + 0.1f * Mathf.Sin(Time.unscaledTime * 3f));
    }

    // Puts each answer E at its V3 position (screen space), whatever the scene's hit areas are.
    private void LayOutOptions()
    {
        var canvas = _test.blackRT.GetComponentInParent<Canvas>().rootCanvas;
        float k = Screen.width / V3.W;
        for (int i = 0; i < _options.Length; i++)
        {
            string dir = _options[i].parent.name;
            if (!OptionAt.TryGetValue(dir, out var at))
                continue;
            var screen = new Vector2(at.x * k, (V3.H - at.y) * k);
            _options[i].position = new Vector3(screen.x, screen.y, _options[i].position.z);
            _optionRest[i] = _options[i].anchoredPosition;
        }
        _laidOut = canvas != null;
    }

    // ==================== result ====================

    private void BuildResult(Transform root)
    {
        _resultView = V3.Full("Result", root).gameObject;
        _resultView.AddComponent<V3PageIn>();
        var t = _resultView.transform;
        _resNote = V3.Note(t, 40f, 70f);
        _scores = V3.Rect("Scores", t, 0f, 0f, V3.W, V3.H);
        _line = V3.KebbiLine(t, 40f, 450f, V3Type.Key, 600f);
        V3.Key(t, "回到首頁", "Home", "home", V3.Green, 772f, 470f, 210f, false, () => _goHome?.Invoke());
        _resultView.SetActive(false);
    }

    private void OnResult(MetricTest.TestMode mode, List<int> levels, string message)
    {
        _hasResult = true;
        V3Sounds.Play(V3Sounds.Cue.Result);
        // the same small celebration for every result
        KebbiMotion.Allowed = true;
        KebbiMotion.Play("clap");
        KebbiLight.Breathe(KebbiLight.Green);
        bool both = mode == MetricTest.TestMode.Both;
        _resNote.text = V3.T("視力結果", "Your result") + "  ·  " + (both ? V3.T("雙眼測驗", "both eyes") : V3.T("單眼測驗", "one eye at a time"));
        foreach (Transform child in _scores)
            Destroy(child.gameObject);
        // one readout in the middle, or one for each eye side by side
        const float y = 264f;
        if (both)
            V3Readout.Create(_scores, V3.W / 2f, y, V3.T("雙眼視力", "Both eyes"), levels[0]);
        else
        {
            V3Readout.Create(_scores, 300f, y, V3.T("右眼", "Right eye"), levels[0]);
            V3Readout.Create(_scores, 724f, y, V3.T("左眼", "Left eye"), levels.Count > 1 ? levels[1] : levels[0]);
        }
        V3.SetLine(_line, V3.Lang == 1 && ResultEn.TryGetValue(message, out var en) ? en : message);
    }

    // RoundedRect sliced so its corners come out at the given radius
    private static Image Rounded(Image image, float radius)
    {
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 24f / radius;
        return image;
    }

    // English for MetricTest's result messages (shown when the page is in English; speech stays Chinese).
    public static readonly Dictionary<string, string> ResultEn = new Dictionary<string, string>
    {
        { "兩眼視力差距較大，可能會影響日常看東西。建議安排眼科或驗光檢查，確認度數是否需要調整。", "Your two eyes differ quite a lot, which can affect everyday seeing. Please have an eye or optometry check to see whether your prescription needs adjusting." },
        { "雙眼有明顯視力差異，建議安排驗光或眼科檢查。", "Your eyes see noticeably differently. An optometry or eye check is recommended." },
        { "雙眼差距較大，建議驗光或眼科檢查。", "Large difference between the eyes: an optometry or eye check is recommended." },
        { "視力已影響日常活動，建議盡快檢查。", "Your eyesight already affects daily life. Please get a check soon." },
        { "看東西會模糊，需要靠近才清楚，可能與度數或眼睛健康有關。", "Things look blurry unless close. It may be your prescription or eye health." },
        { "建議盡速就醫，越早檢查越能保護視力。", "Please see a doctor soon; the earlier, the better for your sight." },
        { "視力偏低，看遠的清晰度會明顯下降，建議安排眼科檢查。", "Your eyesight is low and distance vision is clearly reduced. An eye check is recommended." },
        { "這個視力等級，看電視、開車、走路可能會不太穩。", "At this level, watching TV, driving or walking may feel unsteady." },
        { "如果你有眼鏡，可能度數需要調整；若沒有，建議眼科看看。", "If you wear glasses, they may need a new prescription; if not, see an eye doctor." },
        { "視力比標準稍弱一點，可能是疲勞、近視或需要更新眼鏡度數。", "Slightly below standard: maybe tiredness, short sight or glasses that need updating." },
        { "看東西可能偶爾會覺得不夠清楚，建議近期驗光或眼科檢查。", "Things may sometimes look unclear. An optometry or eye check soon is recommended." },
        { "視力稍低，若看遠會吃力，可考慮配眼鏡或檢查度數。", "Slightly low. If distance is hard, consider glasses or a prescription check." },
        { "很棒！你的視力在正常範圍，看遠看近都沒問題。", "Great! Your eyesight is in the normal range, near and far." },
        { "看起來眼睛狀況很好，日常生活完全足夠。", "Your eyes look good, more than enough for daily life." },
        { "目前視力正常，請保持充足休息與用眼習慣。", "Your eyesight is normal. Keep resting your eyes well." },
        { "哇！你看得超清楚！你的眼睛超厲害的！", "Wow! You see very clearly. Excellent eyes!" },
        { "你的視力比一般標準還要好，看遠的清晰度非常優秀。", "Better than standard: your distance vision is excellent." },
        { "超級厲害！你有像老鷹一樣的視力喔！", "Amazing! You have eagle eyes!" },
    };
}
