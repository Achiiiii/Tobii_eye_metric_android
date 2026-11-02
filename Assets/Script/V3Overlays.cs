using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// V3 overlays: the drift pause card that walks the user back into place (drawn from
// PositionGuide's state: two metal scales with violet markers, the status and the hold guide), the exit dialog,
// and the top-right HUD keys restyled as small white discs.
public class V3Overlays : MonoBehaviour
{
    private const float ScaleCm = 12f;            // both pause scales run -12..12 cm
    private const float NominalDistanceCm = 45f;  // to show the sideways offset (degrees) in cm
    private const float TrackX = 32f, TrackW = 496f;

    private HeadFollow _head;
    private GameObject _pause;
    private TextMeshProUGUI _pauseText;
    private Image _pauseDot;
    private readonly RectTransform[] _marks = new RectTransform[2];
    private V3.GuideParts _hold;

    public static V3Overlays Create(Transform parent, HeadFollow head)
    {
        var canvas = UiFactory.CreateOverlayCanvas("V3Overlay", 18, parent);
        var overlays = canvas.gameObject.AddComponent<V3Overlays>();
        overlays._head = head;
        overlays.BuildPause(canvas.transform);
        return overlays;
    }

    // ==================== drift pause ====================

    private void BuildPause(Transform root)
    {
        _pause = V3.Full("Pause", root).gameObject;
        var dim = UiFactory.CreateImage("Dim", _pause.transform, UiFactory.White, new Color32(0xF2, 0xF5, 0xFA, 0xE0), true);
        UiFactory.Stretch(dim.rectTransform);
        var card = V3.Rect("Card", _pause.transform, 232f, 110f, 560f, 380f);
        V3.Patch(card, 0f, 0f, 560f, 380f);
        V3.Note(card, TrackX, 30f, 400f).gameObject.AddComponent<V3Loc>().Set("測驗暫停中", "Test paused", 4f, 4f);
        var title = V3.Text("Title", card, V3.Zh, "請回到剛才的位置", "Please go back to where you were", 34f, V3.Ink, TrackX, 56f, 500f, 44f, TextAlignmentOptions.TopLeft);
        title.enableAutoSizing = true;
        title.fontSizeMin = 24f;
        title.fontSizeMax = 34f;
        V3.Text("Sub", card, V3.Zh, "回到綠框裡，保持一下就會繼續", "Get back into the green zone and hold still", 19f, V3.Text2, TrackX, 112f, 500f, 26f, TextAlignmentOptions.TopLeft);
        // in-place zone: +-4 deg sideways (~3 cm at 45 cm), +-3 cm near-far
        float z0 = X(-3f) / TrackW, z1 = X(3f) / TrackW;
        _marks[0] = V3.Scale(card, TrackX, 172f, TrackW, z0, z1, new[] { "LEFT", "", "", "", "RIGHT" });
        _marks[1] = V3.Scale(card, TrackX, 240f, TrackW, z0, z1, new[] { "NEAR", "", "", "", "FAR" });
        _pauseDot = V3.Sprite("Dot", card, UiFactory.Circle, V3.Violet, TrackX, 300f, 12f, 12f);
        _pauseText = V3.Text("Instruction", card, V3.Zh, "", 21f, V3.Violet, TrackX + 22f, 290f, 470f, 30f, TextAlignmentOptions.TopLeft);
        _hold = V3.Guide(card, TrackX, 338f, TrackW);
        _pause.SetActive(false);
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
        _pauseText.color = PositionGuide.Good ? V3.Ink : V3.Violet;
        _pauseDot.color = PositionGuide.Good ? V3.Green : V3.Violet;
        // Tobii's x grows toward the user's right; shown as in a mirror (their right on the right)
        float lateralCm = NominalDistanceCm * Mathf.Tan(PositionGuide.LateralDeg * Mathf.Deg2Rad);
        V3.SetMark(_marks[0], TrackW, X(lateralCm) / TrackW);
        V3.SetMark(_marks[1], TrackW, X(PositionGuide.DistanceCm) / TrackW);
        V3.SetGuide(_hold, PositionGuide.HoldFraction);
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
        var dim = UiFactory.CreateImage("ExitDialog", parent, UiFactory.White, new Color32(0xF2, 0xF5, 0xFA, 0xE0), true);
        UiFactory.Stretch(dim.rectTransform);
        const float w = 520f;
        var card = V3.Rect("Card", dim.transform, (V3.W - w) / 2f, 170f, w, 260f);
        V3.Patch(card, 0f, 0f, w, 260f);
        V3.Text("Title", card, V3.Zh, "確定要回到首頁嗎？", "Go back to the home screen?", 30f, V3.Ink, 0f, 36f, w, 40f, TextAlignmentOptions.Top);
        V3.Text("Sub", card, V3.ZhMedium, "測驗進度將不會保留", "The test so far will not be kept", 18f, V3.Text2, 0f, 88f, w, 26f, TextAlignmentOptions.Top);
        V3.Key(card, "取消", "Cancel", "arrowL", new Color32(0xED, 0xED, 0xEA, 0xFF), 40f, 158f, 210f, false, cancel);
        V3.Key(card, "確定", "Go home", "home", V3.Green, 270f, 158f, 210f, false, confirm);
        dim.gameObject.SetActive(false);
        return dim.gameObject;
    }

    // ==================== HUD ====================

    // The scene's round grey HUD buttons become 30 px white discs with an ink glyph (the thin V3.1 top row):
    // exit at the far right, recalibrate left of it, the violet signal bars left of that.
    public static void RestyleHud(Button exit, RectTransform network, GameObject recalibrate)
    {
        Disc((RectTransform)exit.transform, new Vector2(-40f, -26f), V3.Icon("exit"));
        Disc((RectTransform)recalibrate.transform, new Vector2(-40f - 36f - 10f, -26f), UiFactory.RefreshArrow);
        var networkImage = network.GetComponent<Image>();
        if (networkImage != null)
            networkImage.color = new Color(1f, 1f, 1f, 0f);
        network.anchorMin = network.anchorMax = Vector2.one;
        network.pivot = Vector2.one;
        network.anchoredPosition = new Vector2(-40f - 36f - 10f - 36f - 12f, -26f);
    }

    private static void Disc(RectTransform rect, Vector2 position, Sprite glyph)
    {
        foreach (var graphic in rect.GetComponentsInChildren<Graphic>(true))
            if (graphic.transform != rect)
                graphic.enabled = false;
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.sizeDelta = new Vector2(36f, 36f);
        rect.anchoredPosition = position;
        var image = rect.GetComponent<Image>();
        image.sprite = UiFactory.Circle;
        image.type = Image.Type.Simple;
        image.color = Color.white;
        var button = rect.GetComponent<Button>();
        if (button != null)
            button.transition = Selectable.Transition.None;
        V3.Sprite("Glyph", rect, glyph, V3.Ink, 8f, 8f, 20f, 20f);
        rect.gameObject.AddComponent<V3PressTint>().Target = image;
    }
}

