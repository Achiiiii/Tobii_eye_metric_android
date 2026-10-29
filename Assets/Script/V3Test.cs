using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// V3 test and result pages. The test stays MetricTest's own: same symbol, sizes, rules
// and gaze-dwell buttons. Only the look changes: a plain page, a status bar (eye, level, 11-step bar), the four
// answer E's bare (same size as the symbol, the ophthalmologist's design) placed 80 px in from the edges, and
// pressed like keys while looked at.
// The result is redrawn from MetricTest's ResultShown (values, message) on a V3 panel.
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
    private Image _tagFill;
    private TextMeshProUGUI _tag, _level;
    private Image[] _bar = new Image[11];
    private RectTransform[] _options;
    private ButtonTrigger[] _optionTriggers;
    private Vector2[] _optionRest;
    private bool _laidOut;

    private GameObject _resultView;
    private V3.PanelParts _panel;
    private V3.LineParts _line;
    private RectTransform _scores;
    private TextMeshProUGUI _date;
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

    // The scene's test objects: hit areas drawn clear, the symbols in ink.
    private void RestyleScene()
    {
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
        _tagFill = V3.Box("Tag", t, V3.Violet, 40f, 41f, 96f, 30f);
        _tag = V3.Text("Text", _tagFill.transform, V3.Zh, "", 18f, Color.white, 0f, 0f, 96f, 30f, TextAlignmentOptions.Center);
        _level = V3.Text("Level", t, V3.Zh, "", 18f, V3.Ink, 148f, 34f, 84f, 44f, TextAlignmentOptions.Left);
        for (int i = 0; i < 11; i++)
            _bar[i] = V3.Box("Step", t, V3.A(V3.Ink, 0.12f), 232f + i * 11f, 49f, 8f, 14f);
        _testView.SetActive(false);
    }

    private void Update()
    {
        bool flow = _flow.FlowVisible;
        bool testing = flow && _test.gameObject.activeInHierarchy;
        if (_testView.activeSelf != testing)
        {
            _testView.SetActive(testing);
            if (testing)
                _laidOut = false;
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
        if (!_resultPage.activeInHierarchy)
            _hasResult = false;
    }

    private void UpdateTest()
    {
        string side = _test.CurrentSide;
        _tag.text = side == "left" ? V3.T("左眼測驗", "Left eye") : side == "both" ? V3.T("雙眼測驗", "Both eyes") : V3.T("右眼測驗", "Right eye");
        int level = _test.Level;
        _level.text = V3.T($"第 <size=20>{level}</size> 級", $"Level <size=20>{level}</size>");
        for (int i = 0; i < 11; i++)
            _bar[i].color = i + 1 < level ? V3.Mint : i + 1 == level ? V3.Ink : V3.A(V3.Ink, 0.12f);

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
        var t = _resultView.transform;
        var side = V3.Rect("Side", t, 40f, 132f, 224f, 60f);
        V3.Text("Label", side, V3.Mono, "COMPLETED", 11f, V3.A(V3.Ink, 0.6f), 0f, 0f, 224f, 14f, TextAlignmentOptions.TopLeft, 0.14f);
        _date = V3.Text("Date", side, V3.Mono, "", 22f, V3.Text2, 0f, 19f, 224f, 26f);
        V3.Key(t, "回到首頁", "Home", "home", V3.Mint, 40f, 488f, 224f, false, () => _goHome?.Invoke());
        _panel = V3.Panel(t, "eye", "視力結果", "Your result", "RESULT");
        _scores = V3.Rect("Scores", _panel.Root, 29f, 61f, 622f, 170f);
        _line = V3.KebbiLine(_panel.Root, 37f, 255f, 24f, 500f);
        _resultView.SetActive(false);
    }

    private void OnResult(MetricTest.TestMode mode, List<int> levels, string message)
    {
        _hasResult = true;
        V3Sounds.Play(V3Sounds.Cue.Result);
        _date.text = DateTime.Now.ToString("yyyy.MM.dd");
        V3.SetTag(_panel, mode == MetricTest.TestMode.Both ? V3.T("雙眼", "Both eyes") : V3.T("單眼", "One eye"));
        foreach (Transform child in _scores)
            Destroy(child.gameObject);
        if (mode == MetricTest.TestMode.Both)
            ScoreBox(V3.T("雙眼視力", "Both eyes"), levels[0], 0f, 622f);
        else
        {
            ScoreBox(V3.T("右眼", "Right eye"), levels[0], 0f, 302f);
            ScoreBox(V3.T("左眼", "Left eye"), levels.Count > 1 ? levels[1] : levels[0], 320f, 302f);
        }
        V3.SetLine(_line, V3.Lang == 1 && ResultEn.TryGetValue(message, out var en) ? en : message);
        // the line sits centred in the space under the scores (y 255-404 in the panel)
        _line.Root.anchoredPosition = new Vector2(37f, -(255f + Mathf.Max(0f, (149f - _line.Root.sizeDelta.y) / 2f)));
    }

    // One score box: eye label, the value in Unbounded 64, the 11-step scale with the level marked.
    private void ScoreBox(string label, int level, float x, float w)
    {
        var box = V3.Rect("Score", _scores, x, 0f, w, 170f);
        V3.Box("Fill", box, new Color32(0xFB, 0xFB, 0xFA, 0xFF), 0f, 0f, w, 170f);
        V3.Hairline(box, V3.Ink, 0f, 0f, w, 170f);
        V3.Sprite("Icon", box, V3.Icon("eye"), V3.Ink, 21f, 17f, 24f, 24f);
        V3.Text("Label", box, V3.Zh, label, 20f, V3.Ink, 53f, 17f, w - 70f, 24f, TextAlignmentOptions.Left);
        float value = MetricTest.GetEyeMetric(level);
        V3.Text("Value", box, V3.Head, value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), 64f, V3.Ink, 21f, 48f, w - 42f, 70f, TextAlignmentOptions.TopLeft);
        float span = w - 42f - 10f, step = span / 10f;
        int[] labelled = { 0, 4, 7, 10 };
        string[] names = { "0.1", "0.5", "1.0", "2.0" };
        for (int i = 0; i < 11; i++)
        {
            bool current = i + 1 == level;
            V3.Box("Step", box, i + 1 < level ? V3.Violet : current ? V3.Ink : V3.A(V3.Ink, 0.15f), 21f + i * step, current ? 130f : 133f, 10f, current ? 14f : 8f);
        }
        for (int j = 0; j < labelled.Length; j++)
            V3.Text("Tick", box, V3.Mono, names[j], 11f, V3.Text2, 21f + labelled[j] * step - 5f, 147f, 20f, 12f, TextAlignmentOptions.Top);
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
