using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// V3 questionnaire page (Q1-Q7). QA keeps the answers, the gates and the saved file; this
// page draws the question on screen and calls QA. The scene's own question pages stay in place but invisible.
public class V3Questionnaire : MonoBehaviour
{
    public struct Question
    {
        public string Cat, CatEn, Words, WordsEn;
        public string[] OptsEn;   // English labels for QA's options (yes/no: [yes, no])
    }

    // Kebbi's wording for the screen and speech; QA's own texts are still what the answer file stores.
    public static readonly Question[] Questions =
    {
        new Question { Cat = "健康狀況", CatEn = "Health", Words = "請問您是否為\n糖尿病患者呢？", WordsEn = "Do you have\ndiabetes?", OptsEn = new[] { "Yes", "No" } },
        new Question { Cat = "眼睛檢查", CatEn = "Eye exam", Words = "過去一年\n是否做過眼睛檢查？", WordsEn = "Have you had an eye exam\nin the past year?", OptsEn = new[] { "Yes", "No" } },
        new Question { Cat = "視力狀況", CatEn = "Eyesight", Words = "是否有視力問題，例如\n看遠看近或閱讀有困難？", WordsEn = "Any trouble seeing far,\nnear or reading?", OptsEn = new[] { "Yes", "No" } },
        new Question { Cat = "眼科病史", CatEn = "Eye history", Words = "您以前有沒有\n眼睛方面的疾病？", WordsEn = "Have you had any\neye conditions?",
            OptsEn = new[] { "None", "Short / long sight, astigmatism", "Lazy eye (amblyopia)", "Glaucoma", "Cataract", "Macular / retinal disease", "Other" } },
        new Question { Cat = "眼科手術", CatEn = "Eye surgery", Words = "過去是否有做過\n眼科疾病的手術？", WordsEn = "Have you ever had\neye surgery?", OptsEn = new[] { "Yes", "No" } },
        new Question { Cat = "手術史", CatEn = "Surgery", Words = "做過哪一種\n眼科手術呢？", WordsEn = "Which eye surgery\ndid you have?",
            OptsEn = new[] { "Cataract surgery", "Refractive (LASIK etc.)", "Glaucoma surgery", "Macular / retinal surgery", "Other", "None" } },
        new Question { Cat = "矯正器具", CatEn = "Correction", Words = "您現在是否戴著\n眼鏡或隱形眼鏡？", WordsEn = "Are you wearing glasses\nor contact lenses now?",
            OptsEn = new[] { "Yes, glasses", "Yes, contact lenses", "No", "Ortho-K lens (last night)" } },
    };

    private const float BodyW = 624f, BodyH = 355f;

    private QA _qa;
    private V3Flow _flow;
    private Action _goHome;
    private GameObject _page;
    private V3.SideParts _side;
    private V3.PanelParts _panel;
    private V3.LineParts _line;
    private V3.KeyParts _back, _next;
    private RectTransform _content;
    private CanvasGroup _contentGroup;
    private RectTransform _answers;
    private readonly List<Cell> _cells = new List<Cell>();
    private bool _busy;

    private class Cell
    {
        public Button Button;
        public Image Bg, Box, Mark;
        public Image[] Lines;
        public bool Radio, YesNo;
        public int Option;
        public TextMeshProUGUI Label;
        public Image Inset;
        public Vector2 Rest;
    }

    public static V3Questionnaire Create(V3Flow flow, QA qa, Action goHome)
    {
        var root = V3.Full("Questionnaire", flow.Pages);
        var page = root.gameObject.AddComponent<V3Questionnaire>();
        page._qa = qa;
        page._flow = flow;
        page._goHome = goHome;
        page._page = V3.Full("View", root).gameObject;
        page.Build();
        // The scene's question pages keep their buttons wired to QA but are no longer drawn or touched.
        V3Flow.Conceal(qa.questionPage);
        qa.SpeechText = i => i >= 0 && i < Questions.Length ? Questions[i].Words : null;
        qa.QuestionShown += page.OnQuestionShown;
        return page;
    }

    private void Build()
    {
        var t = _page.transform;
        _side = V3.Side(t, "Question", 7);
        _back = V3.Key(t, "上一題", "Back", "arrowL", new Color32(0xED, 0xED, 0xEA, 0xFF), 40f, 0f, 224f, true, OnBack);
        _next = V3.Key(t, "繼續", "Continue", "arrowR", V3.Mint, 40f, 488f, 224f, false, OnNext);
        _panel = V3.Panel(t, "list", "", "", "QUESTIONNAIRE");
        _content = V3.Rect("Content", _panel.Body, 0f, 0f, BodyW, BodyH);
        _contentGroup = _content.gameObject.AddComponent<CanvasGroup>();
        _line = V3.KebbiLine(_content, 8f, 0f, 30f, 480f);
        _answers = V3.Rect("Answers", _content, 0f, 0f, BodyW, BodyH);
    }

    private void Update()
    {
        bool show = _flow.FlowVisible && _qa.questionPage.activeInHierarchy;
        if (_page.activeSelf != show)
        {
            _page.SetActive(show);
            if (show)
                Draw(_qa.Current, false);
        }
        if (show)
            _flow.SetProgress((_qa.Current + 1) / 7f);
    }

    private void OnQuestionShown(int index)
    {
        _busy = false;
        if (_page.activeSelf)
            Draw(index, true);
    }

    private void Draw(int index, bool animate)
    {
        var q = Questions[index];
        bool yesNo = !(index == 3 || index == 5 || index == 6);
        _panel.Title.text = V3.T(q.Cat, q.CatEn);
        V3.SetTag(_panel, yesNo ? V3.T("點一下作答", "Tap to answer") : index == 6 ? V3.T("單選", "Choose one") : V3.T("可複選", "Choose all that apply"));
        V3.SetNumber(_side, index + 1);
        _back.Label.text = index == 0 ? V3.T("回首頁", "Home") : V3.T("上一題", "Back");
        _next.Label.text = index == 6 ? V3.T("完成", "Done") : V3.T("繼續", "Continue");
        _next.Root.gameObject.SetActive(!yesNo);
        // back sits on the bottom line when it is alone, above continue otherwise
        _back.Root.anchoredPosition = new Vector2(40f, -(yesNo ? 504f : 420f));

        foreach (Transform child in _answers)
            Destroy(child.gameObject);
        _cells.Clear();
        float top;
        if (yesNo)
        {
            const float h = 124f, gap = 16f, w = (BodyW - gap) / 2f;
            top = BodyH - h;
            AddCell(0, V3.T(_qa.displayYesLabels[index], q.OptsEn[0]), 0f, top, w, h, false, true);
            AddCell(1, V3.T(_qa.displayNoLabels[index], q.OptsEn[1]), w + gap, top, w, h, false, true);
        }
        else
        {
            string[] options = index == 3 ? _qa.displayQ4Options : index == 5 ? _qa.displayQ6Options : _qa.displayQ7Options;
            int rows = (options.Length + 1) / 2;
            float h = options.Length <= 4 ? 66f : 48f, gap = 10f, w = (BodyW - gap) / 2f;
            top = BodyH - (rows * h + (rows - 1) * gap);
            for (int o = 0; o < options.Length; o++)
                AddCell(o, V3.T(options[o], o < q.OptsEn.Length ? q.OptsEn[o] : options[o]), (o % 2) * (w + gap), top + (o / 2) * (h + gap), w, h, index == 6, false);
        }

        FitOptionLabels();
        V3.SetLine(_line, V3.T(q.Words, q.WordsEn));
        _line.Root.anchoredPosition = new Vector2(8f, -Mathf.Max(0f, (top - _line.Root.sizeDelta.y) / 2f));
        Paint(index);

        if (animate)
        {
            StopAllCoroutines();
            StartCoroutine(SlideIn());
        }
    }

    private void AddCell(int option, string label, float x, float y, float w, float h, bool radio, bool yesNo)
    {
        var c = new Cell { Option = option, Radio = radio, YesNo = yesNo };
        V3.KeyShadow(_answers, x, y, w, h);
        c.Button = UiFactory.CreateButton("Answer", _answers, UiFactory.White, Color.white, () => OnCell(c));
        c.Button.transition = Selectable.Transition.None;
        var rect = V3.At((RectTransform)c.Button.transform, x, y, w, h);
        c.Bg = (Image)c.Button.targetGraphic;
        c.Rest = rect.anchoredPosition;
        // pressed in: a dark inner edge along the top
        c.Inset = V3.Box("Inset", rect, new Color(10 / 255f, 60 / 255f, 30 / 255f, 0.3f), 1f, 1f, w - 2f, 3f);
        c.Inset.enabled = false;
        var lineColor = yesNo ? V3.Ink : V3.A(V3.Ink, 0x47 / 255f);
        c.Lines = new[]
        {
            V3.Box("LineT", rect, lineColor, 0f, 0f, w, 1f), V3.Box("LineB", rect, lineColor, 0f, h - 1f, w, 1f),
            V3.Box("LineL", rect, lineColor, 0f, 0f, 1f, h), V3.Box("LineR", rect, lineColor, w - 1f, 0f, 1f, h)
        };
        if (yesNo)
        {
            V3.Brackets(rect, 0f, 0f, w, h, 7f, 1.5f, 4f, false);
            V3.Text("Label", rect, V3.Zh, label, 46f, V3.Ink, 0f, 0f, w, h, TextAlignmentOptions.Center);
        }
        else
        {
            float by = (h - 20f) / 2f;
            if (radio)
            {
                V3.Sprite("Ring", rect, V3.RingSprite, V3.Ink, 16f, by, 20f, 20f);
                c.Mark = V3.Sprite("Dot", rect, UiFactory.Circle, V3.Ink, 21f, by + 5f, 10f, 10f);
            }
            else
            {
                V3.Hairline(rect, V3.Ink, 16f, by, 20f, 20f, 1.5f);
                c.Box = V3.Box("Fill", rect, V3.Ink, 16f, by, 20f, 20f);
                c.Mark = V3.Sprite("Check", rect, V3.Icon("check"), V3.Mint, 16f, by, 20f, 20f);
            }
            c.Label = V3.Text("Label", rect, V3.Zh, label, 21f, V3.Ink, 50f, 0f, w - 60f, h, TextAlignmentOptions.Left);
        }
        _cells.Add(c);
    }

    private void FitOptionLabels()
    {
        float size = 21f;
        foreach (var c in _cells)
        {
            if (c.Label == null)
                continue;
            c.Label.ForceMeshUpdate();
            float room = c.Label.rectTransform.sizeDelta.x;
            if (c.Label.preferredWidth > room)
                size = Mathf.Min(size, Mathf.Max(15f, 21f * room / c.Label.preferredWidth));
        }
        foreach (var c in _cells)
            if (c.Label != null)
                c.Label.fontSize = Mathf.Floor(size);
    }

    private void Paint(int index)
    {
        foreach (var c in _cells)
        {
            bool on = c.YesNo
                ? _qa.BoolAnswer(index) == (c.Option == 0)
                : _qa.IsOptionSelected(index, c.Option);
            c.Bg.color = on ? V3.Mint : Color.white;
            c.Inset.enabled = on;
            ((RectTransform)c.Button.transform).anchoredPosition = c.Rest + new Vector2(0f, on ? -1f : 0f);
            if (!c.YesNo)
                foreach (var line in c.Lines)
                    line.color = on ? V3.Ink : V3.A(V3.Ink, 0x47 / 255f);
            if (c.Box != null)
                c.Box.enabled = on;
            if (c.Mark != null)
                c.Mark.enabled = on;
        }
        bool yesNo = !(index == 3 || index == 5 || index == 6);
        if (!yesNo)
            V3.SetKeyEnabled(_next, _qa.HasSelection(index));
    }

    private void OnCell(Cell c)
    {
        if (_busy)
            return;
        int index = _qa.Current;
        if (c.YesNo)
        {
            // show the choice in mint for a moment, then answer (QA moves to the next question)
            _busy = true;
            V3Sounds.Play(V3Sounds.Cue.Select);
            foreach (var other in _cells)
            {
                other.Bg.color = other == c ? V3.Mint : Color.white;
                other.Inset.enabled = other == c;
            }
            StartCoroutine(AnswerLater(index, c.Option == 0));
            return;
        }
        if (index == 3) _qa.ToggleQ4Option(c.Option);
        else if (index == 5) _qa.ToggleQ6Option(c.Option);
        else _qa.SelectQ7Option(c.Option);
        V3Sounds.Play(_qa.IsOptionSelected(index, c.Option) ? V3Sounds.Cue.Select : V3Sounds.Cue.Unselect);
        Paint(index);
    }

    private IEnumerator AnswerLater(int index, bool yes)
    {
        yield return new WaitForSecondsRealtime(0.38f);
        if (yes) _qa.ButtonYes(index);
        else _qa.ButtonNo(index);
        _busy = false;
    }

    private void OnNext()
    {
        int index = _qa.Current;
        if (_busy || !_qa.HasSelection(index))
            return;
        if (index == 3) _qa.ConfirmQ4();
        else if (index == 5) _qa.ConfirmQ6();
        else if (index == 6) _qa.ConfirmQ7();
    }

    private void OnBack()
    {
        if (_busy)
            return;
        if (!_qa.PreviousQuestion())
            _goHome?.Invoke();
    }

    // the next card comes in from the right (.35 s, ease out)
    private IEnumerator SlideIn()
    {
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / 0.35f, 3f);
            _contentGroup.alpha = k;
            _content.anchoredPosition = new Vector2(60f * (1f - k), 0f);
            yield return null;
        }
        _contentGroup.alpha = 1f;
        _content.anchoredPosition = Vector2.zero;
    }
}
