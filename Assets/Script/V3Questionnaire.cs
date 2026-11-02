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


    private QA _qa;
    private V3Flow _flow;
    private Action _goHome;
    private GameObject _page;
    private TextMeshProUGUI _note, _title;
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
        page._page.AddComponent<V3PageIn>();
        page.Build();
        // The scene's question pages keep their buttons wired to QA but are no longer drawn or touched.
        V3Flow.Conceal(qa.questionPage);
        qa.SpeechText = i => i >= 0 && i < Questions.Length ? Questions[i].Words : null;
        qa.QuestionShown += page.OnQuestionShown;
        return page;
    }

    // V3.1 layout: a mono note (question number and topic) and the question as a big title on the left, the answers
    // as backlit windows on the right, Kebbi's line under the title, back and continue along the bottom.
    private void Build()
    {
        var t = _page.transform;
        _content = V3.Full("Content", t);
        _contentGroup = _content.gameObject.AddComponent<CanvasGroup>();
        _note = V3.Note(_content, 40f, 100f);
        _title = V3.Title(_content, 40f, 132f, 480f, 120f, 42f);
        _line = V3.KebbiLine(_content, 40f, 300f, 19f, 360f);
        _answers = V3.Rect("Answers", _content, 0f, 0f, V3.W, V3.H);
        _back = V3.Key(t, "上一題", "Back", "arrowL", Color.white, 40f, 470f, 210f, false, OnBack);
        _next = V3.Key(t, "繼續", "Continue", "arrowR", V3.Green, 772f, 470f, 210f, false, OnNext);
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
        string how = yesNo ? V3.T("點一下作答", "Tap to answer") : index == 6 ? V3.T("單選", "Choose one") : V3.T("可複選", "Choose all that apply");
        _note.text = $"Q{index + 1} / 7  ·  {V3.T(q.Cat, q.CatEn)}" + (yesNo ? "" : "  ·  " + how);
        // English wraps by itself; Chinese keeps its set line break
        _title.text = V3.Lang == 1 ? q.WordsEn.Replace('\n', ' ') : q.Words;
        _title.rectTransform.sizeDelta = new Vector2(yesNo ? 500f : 370f, 120f);
        // English wraps on its own at a comfortable line height; Chinese keeps its two set lines, closer together
        _title.lineSpacing = V3.Lang == 1 ? 0f : -14f;
        _title.fontSizeMax = V3.Lang == 1 ? 36f : 42f;
        _back.Label.text = index == 0 ? V3.T("回首頁", "Home") : V3.T("上一題", "Back");
        _next.Label.text = index == 6 ? V3.T("完成", "Done") : V3.T("繼續", "Continue");
        _next.Root.gameObject.SetActive(!yesNo);

        foreach (Transform child in _answers)
            Destroy(child.gameObject);
        _cells.Clear();
        if (yesNo)
        {
            AddCell(0, V3.T(_qa.displayYesLabels[index], q.OptsEn[0]), 560f, 132f, 196f, 170f, false, true);
            AddCell(1, V3.T(_qa.displayNoLabels[index], q.OptsEn[1]), 776f, 132f, 196f, 170f, false, true);
        }
        else
        {
            // two columns from x 430 between y 120 and 446
            string[] options = index == 3 ? _qa.displayQ4Options : index == 5 ? _qa.displayQ6Options : _qa.displayQ7Options;
            int rows = (options.Length + 1) / 2;
            const float gap = 14f, w = 262f;
            float h = Mathf.Min(88f, (326f - (rows - 1) * gap) / rows);
            for (int o = 0; o < options.Length; o++)
                AddCell(o, V3.T(options[o], o < q.OptsEn.Length ? q.OptsEn[o] : options[o]), 430f + (o % 2) * (w + 18f), 120f + (o / 2) * (h + gap), w, h, index == 6, false);
        }

        FitOptionLabels();
        V3.SetLine(_line, how);
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
        V3.PatchShadow(_answers, x, y, w, h);
        c.Button = UiFactory.CreateButton("Answer", _answers, V3.PatchSprite, Color.white, () => OnCell(c));
        c.Button.transition = Selectable.Transition.None;
        var rect = V3.At((RectTransform)c.Button.transform, x, y, w, h);
        c.Bg = (Image)c.Button.targetGraphic;
        c.Bg.type = Image.Type.Sliced;
        c.Rest = rect.anchoredPosition;
        // pressed in: a dark inner edge along the top
        c.Inset = V3.Box("Inset", rect, new Color(10 / 255f, 60 / 255f, 30 / 255f, 0.3f), 1f, 1f, w - 2f, 3f);
        c.Inset.enabled = false;
        // chosen: a green rim just outside the window, following its rounded corners
        var rim = V3.Sprite("Rim", rect, V3.RimSprite, V3.Green, -4f, -4f, w + 8f, h + 8f);
        rim.type = Image.Type.Sliced;
        c.Lines = new[] { rim };
        if (yesNo)
        {
            V3.Text("Label", rect, V3.Zh, label, 48f, V3.Ink, 0f, 0f, w, h, TextAlignmentOptions.Center);
            c.Mark = V3.Sprite("Badge", rect, UiFactory.Circle, V3.Green, w - 44f, 14f, 30f, 30f);
            V3.Sprite("Check", c.Mark.transform, V3.Icon("check"), V3.Ink, 6f, 6f, 18f, 18f);
        }
        else
        {
            float by = (h - 26f) / 2f;
            if (radio)
            {
                V3.Sprite("Ring", rect, V3.RingSprite, V3.A(V3.Ink, 0.4f), 20f, by, 26f, 26f);
                c.Box = V3.Sprite("Fill", rect, UiFactory.Circle, V3.Green, 20f, by, 26f, 26f);
                c.Mark = V3.Sprite("Check", rect, V3.Icon("check"), V3.Ink, 24f, by + 4f, 18f, 18f);
            }
            else
            {
                V3.Hairline(rect, V3.A(V3.Ink, 0.4f), 20f, by, 26f, 26f, 1.5f);
                c.Box = V3.Box("Fill", rect, V3.Green, 20f, by, 26f, 26f);
                c.Mark = V3.Sprite("Check", rect, V3.Icon("check"), V3.Ink, 24f, by + 4f, 18f, 18f);
            }
            c.Label = V3.Text("Label", rect, V3.Zh, label, 20f, V3.Ink, 58f, 0f, w - 68f, h, TextAlignmentOptions.Left);
            c.Label.enableWordWrapping = true;
            c.Label.lineSpacing = -12f;
        }
        _cells.Add(c);
    }

    // One size for every option: as large as lets the longest wrap into two lines (at least 15).
    private void FitOptionLabels()
    {
        float size = 20f;
        foreach (var c in _cells)
        {
            if (c.Label == null)
                continue;
            c.Label.fontSize = 20f;
            c.Label.ForceMeshUpdate();
            float room = c.Label.rectTransform.sizeDelta.y;
            if (c.Label.preferredHeight > room)
                size = Mathf.Min(size, Mathf.Max(15f, 20f * room / c.Label.preferredHeight));
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
            c.Inset.enabled = false;
            ((RectTransform)c.Button.transform).anchoredPosition = c.Rest;
            foreach (var line in c.Lines)
                line.enabled = on;
            if (c.Box != null)
                c.Box.enabled = on;
            if (c.Mark != null)
                c.Mark.gameObject.SetActive(on);
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
            KebbiMotion.Play("ok");
            KebbiLight.Flash(KebbiLight.Green);
            foreach (var other in _cells)
            {
                foreach (var line in other.Lines)
                    line.enabled = other == c;
                if (other.Mark != null)
                    other.Mark.gameObject.SetActive(other == c);
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
        KebbiMotion.Play("ok");
        KebbiLight.Flash(KebbiLight.Green);
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

    // the next question rises in (.45 s, ease out)
    private IEnumerator SlideIn()
    {
        for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / 0.45f, 3f);
            _contentGroup.alpha = k;
            _content.anchoredPosition = new Vector2(0f, -22f * (1f - k));
            yield return null;
        }
        _contentGroup.alpha = 1f;
        _content.anchoredPosition = Vector2.zero;
    }
}
