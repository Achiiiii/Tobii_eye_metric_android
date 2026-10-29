using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// V3 overlays: the drift pause card that walks the user back into place (drawn from
// PositionGuide's state), the exit dialog, and the top-right HUD keys restyled as 44 px outlined squares.
public class V3Overlays : MonoBehaviour
{
    private const float ScaleCm = 12f;            // both pause scales run -12..12 cm
    private const float NominalDistanceCm = 45f;  // to show the sideways offset (degrees) in cm
    private const float TrackX = 31f, TrackW = 578f;

    private HeadFollow _head;
    private GameObject _pause;
    private TextMeshProUGUI _pauseText;
    private Image _silhouette;
    private RectTransform[] _knobs = new RectTransform[2];
    private TextMeshProUGUI[] _knobText = new TextMeshProUGUI[2];
    private Image[] _knobFill = new Image[2];
    private RectTransform[] _hints = new RectTransform[2];
    private RectTransform _hold;

    public static V3Overlays Create(Transform parent, HeadFollow head)
    {
        var canvas = UiFactory.CreateOverlayCanvas("V3Overlay", 18, parent);
        var overlays = canvas.gameObject.AddComponent<V3Overlays>();
        overlays._head = head;
        overlays.BuildPause(canvas.transform);
        PositionGuide.V3Drawn = true;
        return overlays;
    }

    // ==================== drift pause ====================

    private void BuildPause(Transform root)
    {
        _pause = V3.Full("Pause", root).gameObject;
        var dim = UiFactory.CreateImage("Dim", _pause.transform, UiFactory.White, new Color32(0xF3, 0xF3, 0xF1, 0xF0), true);
        UiFactory.Stretch(dim.rectTransform);
        var card = V3.Rect("Card", _pause.transform, 192f, 58f, 640f, 484f);
        V3.Box("Fill", card, V3.A(Color.white, 0.84f), 0f, 0f, 640f, 484f);
        V3.Hairline(card, V3.A(V3.Ink, 0x38 / 255f), 0f, 0f, 640f, 484f);
        V3.Brackets(card, 0f, 0f, 640f, 484f, 14f, 1.5f, 1f, true);
        var tag = V3.Box("Tag", card, V3.Violet, 31f, 30f, 95f, 27f);
        V3.Text("Text", tag.transform, V3.Zh, "測驗暫停中", "Test paused", 15f, Color.white, 0f, 0f, 95f, 27f, TextAlignmentOptions.Center);
        V3.Text("Title", card, V3.Zh, "請回到剛才的位置", "Please go back to where you were", 30f, V3.Ink, 140f, 25f, 470f, 36f, TextAlignmentOptions.Left);

        var mirror = V3.Rect("Mirror", card, 31f, 77f, 160f, 120f);
        V3.Box("Bg", mirror, new Color32(0xE2, 0xE2, 0xDF, 0xFF), 0f, 0f, 160f, 120f);
        _silhouette = V3.Sprite("Head", mirror, V3PositionSprites.Head, V3.Ink, 50f, 33f, 60f, 60f);
        _silhouette.rectTransform.pivot = UiFactory.Center;
        _silhouette.rectTransform.anchoredPosition = new Vector2(80f, -63f);
        V3.Sprite("Target", mirror, V3PositionSprites.Target, Color.white, 0f, 0f, 160f, 120f);
        _pauseText = V3.Text("Instruction", card, V3.Zh, "", 30f, V3.Ink, 223f, 99f, 380f, 36f, TextAlignmentOptions.Left);
        _pauseText.enableAutoSizing = true;
        _pauseText.fontSizeMin = 22f;
        _pauseText.fontSizeMax = 30f;
        V3.Text("Sub", card, V3.ZhMedium, "回到綠框裡，保持一下就會繼續", "Get back into the green zone and hold still", 18f, V3.Text2, 223f, 147f, 380f, 23f, TextAlignmentOptions.Left);

        BuildScale(card, 0, 235f, "偏左", "Left", "偏右", "Right");
        BuildScale(card, 1, 349f, "太近", "Near", "太遠", "Far");
        V3.Box("HoldTrack", card, V3.A(V3.Ink, 0.1f), TrackX, 453f, TrackW, 6f);
        _hold = V3.Box("Hold", card, V3.Green, TrackX, 453f, 0f, 6f).rectTransform;
        _pause.SetActive(false);
    }

    private void BuildScale(Transform card, int i, float y, string leftZh, string leftEn, string rightZh, string rightEn)
    {
        V3.Box("Track", card, Color.white, TrackX, y, TrackW, 48f);
        V3.Hairline(card, V3.A(V3.Ink, 0.2f), TrackX, y, TrackW, 48f);
        // in-place zone: +-4 deg sideways (~3 cm at 45 cm), +-3 cm near-far
        float w0 = X(-3f), w1 = X(3f);
        var win = V3.Box("Window", card, V3.MintSoft, TrackX + w0, y, w1 - w0, 48f);
        V3.Hairline(win.transform, V3.Green, 0f, 0f, w1 - w0, 48f);
        V3.Text("L", card, V3.ZhMedium, leftZh, leftEn, 20f, V3.Text2, TrackX + 14f, y + 12f, 80f, 24f);
        V3.Text("R", card, V3.ZhMedium, rightZh, rightEn, 20f, V3.Text2, TrackX + TrackW - 94f, y + 12f, 80f, 24f, TextAlignmentOptions.TopRight);
        for (int cm = -12; cm <= 12; cm++)
        {
            bool major = cm % 6 == 0;
            V3.Box("Tick", card, major ? V3.Ink : V3.A(V3.Ink, 0.3f), TrackX + X(cm) - 1f, y + 56f, 2f, major ? 10f : 6f);
            if (major)
                V3.Text("Num", card, V3.Mono, Mathf.Abs(cm).ToString(), 14f, V3.Text2, TrackX + X(cm) - 20f, y + 69f, 40f, 16f, TextAlignmentOptions.Top);
        }
        var knob = V3.Rect("Knob", card, 0f, y - 4f, 52f, 52f);
        _knobFill[i] = V3.Box("Fill", knob, Color.white, 0f, 0f, 52f, 52f);
        V3.Hairline(knob, V3.Ink, 0f, 0f, 52f, 52f, 1.5f);
        _knobText[i] = V3.Text("Value", knob, V3.Mono, "", 21f, V3.Ink, 0f, 0f, 52f, 52f, TextAlignmentOptions.Center);
        _knobs[i] = knob;
        var hint = V3.Rect("Hint", card, 0f, y + 7f, 34f, 34f);
        V3.Box("Fill", hint, V3.Mint, 0f, 0f, 34f, 34f);
        var arrow = V3.Sprite("Arrow", hint, V3.Icon("arrowR"), V3.Ink, 5f, 5f, 24f, 24f).rectTransform;
        arrow.pivot = UiFactory.Center;
        arrow.anchoredPosition = new Vector2(17f, -17f);
        _hints[i] = hint;
    }

    private static float X(float cm) => Mathf.InverseLerp(-ScaleCm, ScaleCm, Mathf.Clamp(cm, -ScaleCm, ScaleCm)) * TrackW;

    private void Update()
    {
        bool on = PositionGuide.Active;
        if (_pause.activeSelf != on)
        {
            _pause.SetActive(on);
            V3Sounds.Play(on ? V3Sounds.Cue.Pause : V3Sounds.Cue.InRange);
        }
        if (!on)
            return;
        _pauseText.text = V3.Lang == 1 ? English(PositionGuide.Text) : PositionGuide.Text;
        _pauseText.color = PositionGuide.Good ? V3.Green : V3.Ink;
        // Tobii's x grows toward the user's right; shown as in a mirror (their right on the right)
        float lateralCm = NominalDistanceCm * Mathf.Tan(PositionGuide.LateralDeg * Mathf.Deg2Rad);
        SetScale(0, lateralCm, Mathf.Abs(PositionGuide.LateralDeg) <= 4f);
        SetScale(1, PositionGuide.DistanceCm, Mathf.Abs(PositionGuide.DistanceCm) <= 3f);
        _hold.sizeDelta = new Vector2(TrackW * PositionGuide.HoldFraction, 6f);
        _silhouette.color = PositionGuide.Good ? V3.Green : V3.Ink;
        float scale = Mathf.Clamp(1f - PositionGuide.DistanceCm / 20f, 0.6f, 1.5f);
        _silhouette.rectTransform.localScale = Vector3.one * scale;
        _silhouette.rectTransform.anchoredPosition = new Vector2(80f + Mathf.Clamp(lateralCm * 3f, -40f, 40f), -63f);
    }

    private void SetScale(int i, float cm, bool inZone)
    {
        float kx = TrackX + X(cm) - 26f;
        _knobs[i].anchoredPosition = new Vector2(kx, _knobs[i].anchoredPosition.y);
        int shown = Mathf.RoundToInt(cm);
        _knobText[i].text = shown > 0 ? "+" + shown : shown.ToString();
        _knobFill[i].color = inZone ? V3.Mint : Color.white;
        _hints[i].gameObject.SetActive(!inZone);
        // the chip sits on the zone's side of the knob and points toward it
        bool toLeft = cm > 0f;
        _hints[i].anchoredPosition = new Vector2(toLeft ? kx - 43f : kx + 52f + 9f, _hints[i].anchoredPosition.y);
        _hints[i].GetChild(1).localEulerAngles = new Vector3(0f, 0f, toLeft ? 180f : 0f);
    }

    private static string English(string zh)
    {
        switch (zh)
        {
            case "請往左移一點": return "Move a little to the left";
            case "請往右移一點": return "Move a little to the right";
            case "請往前靠近一點": return "Move a little closer";
            case "請往後遠離一點": return "Move back a little";
            case "請回到螢幕正前方": return "Come back in front of the screen";
            case "很好，請保持不動": return "Good, hold still";
            default: return zh;
        }
    }

    // ==================== exit dialog ====================

    // Card on a light veil: the question, what it means, 取消 (secondary) and 確定 (primary).
    public static GameObject ExitDialog(Transform parent, Action cancel, Action confirm)
    {
        var dim = UiFactory.CreateImage("ExitDialog", parent, UiFactory.White, new Color32(0xF3, 0xF3, 0xF1, 0xF0), true);
        UiFactory.Stretch(dim.rectTransform);
        const float w = 520f;
        var card = V3.Rect("Card", dim.transform, (V3.W - w) / 2f, 170f, w, 260f);
        V3.Box("Fill", card, Color.white, 0f, 0f, w, 260f);
        V3.Hairline(card, V3.A(V3.Ink, 0x38 / 255f), 0f, 0f, w, 260f);
        V3.Brackets(card, 0f, 0f, w, 260f, 14f, 1.5f, 1f, true);
        V3.Text("Title", card, V3.Zh, "確定要回到首頁嗎？", "Go back to the home screen?", 30f, V3.Ink, 0f, 36f, w, 40f, TextAlignmentOptions.Top);
        V3.Text("Sub", card, V3.ZhMedium, "測驗進度將不會保留", "The test so far will not be kept", 18f, V3.Text2, 0f, 88f, w, 26f, TextAlignmentOptions.Top);
        V3.Key(card, "取消", "Cancel", "arrowL", new Color32(0xED, 0xED, 0xEA, 0xFF), 40f, 158f, 210f, false, cancel);
        V3.Key(card, "確定", "Go home", "home", V3.Mint, 270f, 158f, 210f, false, confirm);
        dim.gameObject.SetActive(false);
        return dim.gameObject;
    }

    // ==================== HUD ====================

    // The scene's round grey HUD buttons become 44 px white squares with a 1.5 px ink edge:
    // exit at the far right, recalibrate left of it, the violet signal glyph left of that.
    public static void RestyleHud(Button exit, RectTransform network, GameObject recalibrate)
    {
        Square((RectTransform)exit.transform, new Vector2(-40f, -34f), V3.Icon("exit"));
        Square((RectTransform)recalibrate.transform, new Vector2(-40f - 44f - 14f, -34f), UiFactory.RefreshArrow);
        var networkImage = network.GetComponent<Image>();
        if (networkImage != null)
            networkImage.color = new Color(1f, 1f, 1f, 0f);
        network.anchorMin = network.anchorMax = Vector2.one;
        network.pivot = Vector2.one;
        network.anchoredPosition = new Vector2(-40f - 44f - 14f - 44f - 4f, -38f);
    }

    private static void Square(RectTransform rect, Vector2 position, Sprite glyph)
    {
        foreach (var graphic in rect.GetComponentsInChildren<Graphic>(true))
            if (graphic.transform != rect)
                graphic.enabled = false;
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.sizeDelta = new Vector2(44f, 44f);
        rect.anchoredPosition = position;
        var image = rect.GetComponent<Image>();
        image.sprite = UiFactory.White;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        var button = rect.GetComponent<Button>();
        if (button != null)
            button.transition = Selectable.Transition.None;
        var face = V3.Rect("V3Face", rect, 0f, 0f, 44f, 44f);
        V3.Hairline(face, V3.Ink, 0f, 0f, 44f, 44f, 1.5f);
        V3.Sprite("Glyph", face, glyph, V3.Ink, 11f, 11f, 22f, 22f);
        rect.gameObject.AddComponent<V3PressTint>().Target = image;
    }
}

// The head silhouette and face target from the position page, shared with the pause card.
public static class V3PositionSprites
{
    public static Sprite Head => V3Position.HeadSprite();
    public static Sprite Target => V3Position.TargetSprite();
}
