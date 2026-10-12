using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Draws the questionnaire's questions, options and buttons as text from QA's display strings,
// replacing the images that had the wording baked in. Saved answers are unaffected.
public static class QuestionnaireText
{
    private static readonly Color PanelFill = new Color32(0x17, 0xAC, 0x50, 0xD9);
    private static readonly Color ButtonFill = new Color32(0xFF, 0xFF, 0xFF, 0xD9);
    private static readonly Color Border = new Color32(0x08, 0x5C, 0x91, 0xFF);
    private static readonly Color TextColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    private static readonly Color HintFill = new Color(0f, 0f, 0f, 0.62f);
    // The rounded sprites have 24 px slice borders; a multiplier of 2 gives ~10 px corners and a 4 px stroke.
    private const float CornerMultiplier = 2f;
    // The multi-option question art had 14 px of empty space above its panel.
    private const float ListPanelTopInset = 14f;
    private const float OptionLabelOffset = 45f;

    public static void Apply(QA qa, TMP_FontAsset font)
    {
        for (int i = 0; i < qa.questions.Length; i++)
        {
            Transform page = qa.questions[i].transform;
            Image[] toggles = TogglesFor(qa, i);
            string[] options = OptionsFor(qa, i);
            var panel = (RectTransform)page.Find("Q");

            if (toggles == null)
            {
                DrawFrame(panel, PanelFill, 0f);
                AddCenteredQuestion(panel, font, qa.displayQuestionTexts[i]);
            }
            else
            {
                if (i == 5 && options.Length > toggles.Length)
                {
                    toggles = AddTogglesAbove(toggles, options.Length, qa.ToggleQ6Option);
                    qa.Q6Toggles = toggles;
                }

                DrawFrame(panel, PanelFill, ListPanelTopInset);
                AddListQuestion(panel, font, qa.displayQuestionTexts[i]);
                for (int o = 0; o < toggles.Length && o < options.Length; o++)
                    AddOption((RectTransform)toggles[o].transform, font, options[o]);
            }

            StyleButton(page.Find("Yes"), font, qa.displayYesLabels[i]);
            StyleButton(page.Find("No"), font, qa.displayNoLabels[i]);
            Transform next = page.Find("Next");
            StyleButton(next, font, qa.displayNextLabel);
            if (toggles != null)
                AddSelectionHint(qa, i, next, font);
        }
    }

    private static Image[] TogglesFor(QA qa, int index)
    {
        switch (index)
        {
            case 3: return qa.Q4Toggles;
            case 5: return qa.Q6Toggles;
            case 6: return qa.Q7Toggles;
            default: return null;
        }
    }

    private static string[] OptionsFor(QA qa, int index)
    {
        switch (index)
        {
            case 3: return qa.displayQ4Options;
            case 5: return qa.displayQ6Options;
            case 6: return qa.displayQ7Options;
            default: return null;
        }
    }

    // Clones the first option row for options that have no toggle in the scene, stacking the new rows above it.
    private static Image[] AddTogglesAbove(Image[] toggles, int count, UnityAction<int> onToggle)
    {
        var first = (RectTransform)toggles[0].transform;
        float spacing = toggles.Length > 1
            ? first.anchoredPosition.y - ((RectTransform)toggles[1].transform).anchoredPosition.y
            : 49f;

        var result = new Image[count];
        System.Array.Copy(toggles, result, toggles.Length);
        for (int index = toggles.Length; index < count; index++)
        {
            var clone = Object.Instantiate(toggles[0].gameObject, first.parent);
            clone.name = first.name + " (" + index + ")";
            var rect = (RectTransform)clone.transform;
            rect.anchoredPosition = first.anchoredPosition + new Vector2(0f, spacing * (count - index));

            // The clone carries the first row's scene bindings (option 0); point it at its own option.
            int optionIndex = index;
            foreach (var button in clone.GetComponentsInChildren<Button>(true))
            {
                for (int p = 0; p < button.onClick.GetPersistentEventCount(); p++)
                    button.onClick.SetPersistentListenerState(p, UnityEventCallState.Off);
                button.onClick.AddListener(() => onToggle(optionIndex));
            }
            result[index] = clone.GetComponent<Image>();
        }
        return result;
    }

    private static Image DrawFrame(RectTransform target, Color fillColor, float topInset)
    {
        // Keep the original Image (it may be a button's hit area) but stop drawing the baked artwork.
        var original = target.GetComponent<Image>();
        original.sprite = null;
        original.color = Color.clear;

        var fill = UiFactory.CreateImage("Fill", target, UiFactory.RoundedRect, fillColor);
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = CornerMultiplier;
        UiFactory.Stretch(fill.rectTransform);
        fill.rectTransform.offsetMax = new Vector2(0f, -topInset);

        var frame = UiFactory.CreateImage("Frame", target, UiFactory.RoundedFrame, Border);
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = CornerMultiplier;
        UiFactory.Stretch(frame.rectTransform);
        frame.rectTransform.offsetMax = new Vector2(0f, -topInset);
        return fill;
    }

    private static void AddCenteredQuestion(RectTransform panel, TMP_FontAsset font, string text)
    {
        var label = UiFactory.CreateText("QuestionText", panel, font, UiFactory.WithLatinFallback(text, font), 32f, Color.white);
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 32f;
        UiFactory.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(24f, 6f);
        label.rectTransform.offsetMax = new Vector2(-24f, -6f);
    }

    private static void AddListQuestion(RectTransform panel, TMP_FontAsset font, string text)
    {
        var label = UiFactory.CreateText("QuestionText", panel, font, UiFactory.WithLatinFallback(text, font), 32f, Color.white, TextAlignmentOptions.MidlineLeft);
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 32f;
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(30f, -(ListPanelTopInset + 22f));
        rect.sizeDelta = new Vector2(-60f, 44f);
    }

    private static void AddOption(RectTransform toggle, TMP_FontAsset font, string text)
    {
        // The toggle is the 26 px square that fills yellow when selected; draw the checkbox outline around it.
        var box = UiFactory.CreateImage("CheckboxFrame", toggle, UiFactory.RoundedFrame, Color.white);
        box.type = Image.Type.Sliced;
        box.pixelsPerUnitMultiplier = CornerMultiplier;
        UiFactory.Place(box.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(40f, 40f));

        var label = UiFactory.CreateText("OptionText", toggle, font, UiFactory.WithLatinFallback(text, font), 30f, TextColor, TextAlignmentOptions.MidlineLeft);
        UiFactory.UsePlainMaterial(label);
        label.fontStyle = FontStyles.Bold;
        label.enableWordWrapping = false;
        UiFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(OptionLabelOffset, 0f), new Vector2(640f, 40f));

        // Stretch the existing click area so it covers the checkbox and the whole label.
        float width = label.GetPreferredValues(label.text, 2000f, 40f).x;
        if (width <= 1f)
            width = text.Length * 30f;
        foreach (var button in toggle.GetComponentsInChildren<Button>(true))
        {
            if (button.transform == toggle)
                continue;
            var clickArea = (RectTransform)button.transform;
            float left = -20f;
            float right = OptionLabelOffset - toggle.rect.width * 0.5f + width;
            UiFactory.Place(clickArea, UiFactory.Center, UiFactory.Center, new Vector2((left + right) * 0.5f, 0f), new Vector2(right - left, 40f));
        }
    }

    // The option questions cannot be left blank, so their Next button starts disabled.  This
    // sits under the button and says why, and QA shows or hides it as options are picked.
    private static void AddSelectionHint(QA qa, int index, Transform next, TMP_FontAsset font)
    {
        if (next == null)
            return;

        var pill = UiFactory.CreateImage("SelectionHint", next, UiFactory.RoundedRect, HintFill);
        pill.type = Image.Type.Sliced;
        pill.pixelsPerUnitMultiplier = CornerMultiplier;
        UiFactory.Place(pill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(300f, 38f));
        var label = UiFactory.CreateText("Text", pill.transform, font, UiFactory.WithLatinFallback("請至少勾選一項", font), 22f, Color.white);
        UiFactory.Stretch(label.rectTransform);

        qa.RegisterNextGate(index, next.GetComponent<Button>(), pill.gameObject);
    }

    private static void StyleButton(Transform target, TMP_FontAsset font, string text)
    {
        if (target == null)
            return;

        var fill = DrawFrame((RectTransform)target, ButtonFill, 0f);
        var label = UiFactory.CreateText("Label", target, font, UiFactory.WithLatinFallback(text, font), 36f, TextColor);
        UiFactory.UsePlainMaterial(label);
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 38f;
        UiFactory.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(10f, 6f);
        label.rectTransform.offsetMax = new Vector2(-10f, -6f);

        var button = target.GetComponent<Button>();
        if (button != null)
            button.targetGraphic = fill;
    }
}
