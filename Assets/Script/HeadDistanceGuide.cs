using TMPro;
using UnityEngine;
using UnityEngine.UI;

// On-screen guidance for the head distance check driven by DetectDistance.
public class HeadDistanceGuide : MonoBehaviour
{
    private const float ScaleNearMeters = 0.15f;
    private const float ScaleFarMeters = 0.65f;
    private const float ScaleWidth = 600f;
    private const float HoldBarWidth = 420f;
    private static readonly Color InRangeColor = new Color32(0x2E, 0x9E, 0x5A, 0xFF);
    private static readonly Color OutOfRangeColor = new Color32(0xE0, 0x6A, 0x3B, 0xFF);
    private static readonly Color InRangeZoneTint = new Color32(0xC8, 0xE6, 0xC9, 0xFF);
    private static readonly Color OutOfRangeZoneTint = new Color32(0xFF, 0xD6, 0xC4, 0xFF);
    private static readonly Color TextDark = new Color32(0x1A, 0x1A, 0x1A, 0xFF);

    private DetectDistance _detectDistance;
    private GameObject _canvasTrackBox;
    private GameObject _content;
    private Image _statusPill;
    private TextMeshProUGUI _statusText;
    private RectTransform _holdFill;
    private RectTransform _marker;
    private TextMeshProUGUI _markerLabel;

    public static HeadDistanceGuide Create(Transform parent, DetectDistance detectDistance, GameObject canvasTrackBox, TMP_FontAsset font)
    {
        var root = UiFactory.Stretch(UiFactory.CreateRect("HeadDistanceGuide", parent));
        var guide = root.gameObject.AddComponent<HeadDistanceGuide>();
        guide._detectDistance = detectDistance;
        guide._canvasTrackBox = canvasTrackBox;

        var content = UiFactory.Stretch(UiFactory.CreateRect("Content", root));
        guide._content = content.gameObject;

        // 700 wide keeps clear of the top-right HUD icons.
        var banner = UiFactory.CreateImage("Instructions", content, UiFactory.RoundedRect, new Color(0f, 0f, 0f, 0.62f));
        banner.type = Image.Type.Sliced;
        UiFactory.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(700f, 84f));
        var bannerText = UiFactory.CreateText("Text", banner.transform, font, "請將頭部距離機器人約四十公分，臉部與螢幕保持平行\n調整到畫面變成綠色後，請保持不動", 24f, Color.white);
        UiFactory.Stretch(bannerText.rectTransform);

        guide._statusPill = UiFactory.CreateImage("Status", content, UiFactory.RoundedRect, OutOfRangeColor);
        guide._statusPill.type = Image.Type.Sliced;
        UiFactory.Place(guide._statusPill.rectTransform, new Vector2(0.5f, 0f), UiFactory.Center, new Vector2(0f, 152f), new Vector2(HoldBarWidth, 64f));
        guide._statusText = UiFactory.CreateText("Text", guide._statusPill.transform, font, "", 32f, Color.white);
        UiFactory.Stretch(guide._statusText.rectTransform);

        var holdTrack = UiFactory.CreateImage("HoldTrack", content, UiFactory.White, new Color(0f, 0f, 0f, 0.25f));
        UiFactory.Place(holdTrack.rectTransform, new Vector2(0.5f, 0f), UiFactory.Center, new Vector2(0f, 108f), new Vector2(HoldBarWidth, 12f));
        var holdFill = UiFactory.CreateImage("HoldFill", holdTrack.transform, UiFactory.White, InRangeColor);
        UiFactory.Place(holdFill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 12f));
        guide._holdFill = holdFill.rectTransform;

        var scale = UiFactory.Place(UiFactory.CreateRect("DistanceScale", content), new Vector2(0.5f, 0f), UiFactory.Center, new Vector2(0f, 44f), new Vector2(ScaleWidth, 32f));
        float near = ScalePosition(DetectDistance.MinDistanceMeters);
        float far = ScalePosition(DetectDistance.MaxDistanceMeters);
        AddZone(scale, font, 0f, near, OutOfRangeZoneTint, "太近");
        AddZone(scale, font, near, far, InRangeZoneTint, "剛好");
        AddZone(scale, font, far, ScaleWidth, OutOfRangeZoneTint, "太遠");

        guide._marker = UiFactory.Place(UiFactory.CreateRect("Marker", scale), new Vector2(0f, 0.5f), UiFactory.Center, Vector2.zero, new Vector2(6f, 46f));
        var markerLine = UiFactory.CreateImage("Line", guide._marker, UiFactory.White, TextDark);
        UiFactory.Stretch(markerLine.rectTransform);
        // The Chinese SDF atlas has no digits, so the distance readout uses TMP's default font.
        guide._markerLabel = UiFactory.CreateText("Label", guide._marker, TMP_Settings.defaultFontAsset, "", 20f, TextDark);
        UiFactory.Place(guide._markerLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(90f, 26f));

        guide._content.SetActive(false);
        return guide;
    }

    private void Update()
    {
        bool visible = _detectDistance.IsChecking && _canvasTrackBox.activeInHierarchy;
        if (_content.activeSelf != visible)
            _content.SetActive(visible);
        if (!visible)
            return;

        var zone = _detectDistance.Zone;
        _statusPill.color = zone == DetectDistance.DistanceZone.InRange ? InRangeColor : OutOfRangeColor;
        _statusText.text = zone switch
        {
            DetectDistance.DistanceZone.TooFar => "請往前靠近一點",
            DetectDistance.DistanceZone.TooClose => "請往後遠離一點",
            _ => "很好，請保持不動"
        };
        _holdFill.sizeDelta = new Vector2(HoldBarWidth * _detectDistance.HoldProgress, _holdFill.sizeDelta.y);
        _marker.anchoredPosition = new Vector2(ScalePosition(_detectDistance.DistanceMeters), 0f);
        _markerLabel.text = Mathf.RoundToInt(_detectDistance.DistanceMeters * 100f) + " cm";
    }

    private static float ScalePosition(float meters)
    {
        return Mathf.InverseLerp(ScaleNearMeters, ScaleFarMeters, meters) * ScaleWidth;
    }

    private static void AddZone(Transform scale, TMP_FontAsset font, float from, float to, Color color, string label)
    {
        var zone = UiFactory.CreateImage(label, scale, UiFactory.White, color);
        UiFactory.Place(zone.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(from, 0f), new Vector2(to - from, 32f));
        var text = UiFactory.CreateText("Label", zone.transform, font, label, 18f, TextDark);
        UiFactory.Stretch(text.rectTransform);
    }
}
