using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Walks the user back to where they sat during calibration when they move well away from it in
// the test. Turning the robot's head keeps the face in the image, but after a user moved about
// 16 cm sideways the camera saw the face 20 degrees off and their looks at the symbol landed on the
// options. Users cannot be expected to remember where they sat, so this guides them step by step:
// the robot faces the calibrated position (so "straight in front of the screen" is right again),
// answering pauses, an arrow shows which way, two scales (left-right, near-far) show how far with
// a marker and a green "in place" zone, and the voice repeats the direction until they are back.
public class PositionGuide : MonoBehaviour
{
    private const float EnterLateralDeg = 10f;
    private const float EnterDistanceCm = 5f;
    private const float ExitLateralDeg = 4f;
    private const float ExitDistanceCm = 3f;
    private const float EnterSustainSeconds = 1.5f;
    private const float HoldSeconds = 1f;
    private const float SuccessShowSeconds = 0.8f;
    // A new direction must hold this long before it is spoken, so a user crossing over does not
    // get "left, right, left".
    private const float InstructionSettleSeconds = 0.6f;
    private const float RepeatSeconds = 5f;
    private const float FaceLostSeconds = 1.5f;
    // Scale ends; beyond these the marker waits at the edge.
    private const float LateralScaleDeg = 20f;
    private const float DistanceScaleCm = 12f;
    private const float ScaleWidth = 420f;

    private static readonly Color TitleColor = new Color32(0x1F, 0x3A, 0x5F, 0xFF);
    private static readonly Color TextColor = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    private static readonly Color MoveColor = new Color32(0xE0, 0x6A, 0x3B, 0xFF);
    private static readonly Color GoodColor = new Color32(0x2E, 0x9E, 0x5A, 0xFF);
    // Same zone tints as the head distance check before calibration.
    private static readonly Color InZoneTint = new Color32(0xC8, 0xE6, 0xC9, 0xFF);
    private static readonly Color OutZoneTint = new Color32(0xFF, 0xD6, 0xC4, 0xFF);

    private const string TitleText = "請回到剛才的位置";
    private const string MoveLeftText = "請往左移一點";
    private const string MoveRightText = "請往右移一點";
    private const string MoveCloserText = "請往前靠近一點";
    private const string MoveBackText = "請往後遠離一點";
    private const string FaceScreenText = "請回到螢幕正前方";
    private const string GoodText = "很好，請保持不動";

    // True while guiding; gaze selection and drift sampling pause meanwhile.
    public static bool Active { get; private set; }

    private enum Direction { None, Left, Right, Closer, Back }

    private HeadFollow _head;
    private MetricTest _metricTest;
    private TMP_FontAsset _font;

    private GameObject _visual;
    private RectTransform _arrow;
    private readonly List<Image> _arrowParts = new List<Image>();
    private TextMeshProUGUI _instruction;
    private Scale _lateralScale;
    private Scale _distanceScale;

    // A horizontal scale with a green zone in the middle and a marker for the current value.
    private class Scale
    {
        public RectTransform Marker;
        public float Range;

        public void Set(float value)
        {
            Marker.anchoredPosition = new Vector2(Mathf.Clamp(value / Range, -1f, 1f) * ScaleWidth * 0.5f, 0f);
        }
    }

    private bool _guiding;
    private float _startedAt;
    private float _outSince = -1f;
    private float _holdTimer;
    private bool _saidGood;
    private float _successUntil = -1f;
    private float _faceLostSince = -1f;
    private Direction _pending = Direction.None;
    private float _pendingSince;
    private string _spoken;
    private float _spokenAt;

    public static PositionGuide Create(Transform parent, TMP_FontAsset font, HeadFollow head, MetricTest metricTest)
    {
        var root = UiFactory.Stretch(UiFactory.CreateRect("PositionGuide", parent));
        var guide = root.gameObject.AddComponent<PositionGuide>();
        guide._head = head;
        guide._metricTest = metricTest;
        guide._font = font;

        var visual = UiFactory.Stretch(UiFactory.CreateRect("Visual", root));
        guide._visual = visual.gameObject;

        // Dims the test and swallows touches, so an option cannot be tapped while answering is paused.
        var dim = UiFactory.CreateImage("Dim", visual, UiFactory.White, new Color(0f, 0f, 0f, 0.55f), true);
        UiFactory.Stretch(dim.rectTransform);

        var panel = UiFactory.CreateImage("Panel", visual, UiFactory.RoundedRect, Color.white);
        panel.type = Image.Type.Sliced;
        UiFactory.Place(panel.rectTransform, UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(600f, 460f));

        var title = UiFactory.CreateText("Title", panel.transform, font, UiFactory.WithLatinFallback(TitleText, font), 34f, TitleColor);
        UiFactory.UsePlainMaterial(title);
        title.fontStyle = FontStyles.Bold;
        UiFactory.Place(title.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, 185f), new Vector2(560f, 50f));

        guide._arrow = UiFactory.Place(UiFactory.CreateRect("Arrow", panel.transform), UiFactory.Center, UiFactory.Center, new Vector2(0f, 110f), new Vector2(200f, 90f));
        guide.AddChevron(-26f);
        guide.AddChevron(26f);

        guide._instruction = UiFactory.CreateText("Instruction", panel.transform, font, "", 30f, TextColor);
        UiFactory.UsePlainMaterial(guide._instruction);
        UiFactory.Place(guide._instruction.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(0f, 38f), new Vector2(560f, 44f));

        // Mirror-like: a user off to their right sees the marker right of the green zone, and moving
        // left brings it in. Near-far reads like the distance check: 太近 left, 太遠 right.
        guide._lateralScale = CreateScale(panel.transform, font, -50f, "左右", LateralScaleDeg, ExitLateralDeg, "偏左", "偏右");
        guide._distanceScale = CreateScale(panel.transform, font, -145f, "前後", DistanceScaleCm, ExitDistanceCm, "太近", "太遠");

        guide._visual.SetActive(false);
        return guide;
    }

    private static Scale CreateScale(Transform parent, TMP_FontAsset font, float y, string name, float range, float tolerance, string lowLabel, string highLabel)
    {
        var caption = UiFactory.CreateText(name, parent, font, name, 24f, TextColor);
        UiFactory.UsePlainMaterial(caption);
        caption.fontStyle = FontStyles.Bold;
        UiFactory.Place(caption.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(-250f, y), new Vector2(70f, 36f));

        var bar = UiFactory.Place(UiFactory.CreateRect(name + "Scale", parent), UiFactory.Center, UiFactory.Center, new Vector2(30f, y), new Vector2(ScaleWidth, 34f));
        float half = ScaleWidth * 0.5f;
        float zoneHalf = half * tolerance / range;
        AddZone(bar, font, -half, -zoneHalf, OutZoneTint, lowLabel);
        AddZone(bar, font, -zoneHalf, zoneHalf, InZoneTint, "剛好");
        AddZone(bar, font, zoneHalf, half, OutZoneTint, highLabel);

        var marker = UiFactory.Place(UiFactory.CreateRect("Marker", bar), UiFactory.Center, UiFactory.Center, Vector2.zero, new Vector2(6f, 48f));
        var line = UiFactory.CreateImage("Line", marker, UiFactory.White, TextColor);
        UiFactory.Stretch(line.rectTransform);
        return new Scale { Marker = marker, Range = range };
    }

    private static void AddZone(RectTransform bar, TMP_FontAsset font, float from, float to, Color tint, string label)
    {
        var zone = UiFactory.CreateImage(label, bar, UiFactory.White, tint);
        UiFactory.Place(zone.rectTransform, UiFactory.Center, new Vector2(0f, 0.5f), new Vector2(from, 0f), new Vector2(to - from, 34f));
        var text = UiFactory.CreateText("Label", zone.transform, font, label, 18f, TextColor);
        UiFactory.UsePlainMaterial(text);
        UiFactory.Stretch(text.rectTransform);
    }

    // One ">" made of two rounded bars meeting at the tip; the arrow container is rotated to point.
    private void AddChevron(float x)
    {
        foreach (float side in new[] { 1f, -1f })
        {
            var bar = UiFactory.CreateImage("Bar", _arrow, UiFactory.RoundedRect, MoveColor);
            bar.type = Image.Type.Sliced;
            bar.pixelsPerUnitMultiplier = 3f;
            UiFactory.Place(bar.rectTransform, UiFactory.Center, UiFactory.Center, new Vector2(x - 10f, 18f * side), new Vector2(56f, 16f));
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f * side);
            _arrowParts.Add(bar);
        }
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        bool inTest = _metricTest.gameObject.activeInHierarchy && _head.Calibrated;

        if (!_guiding)
        {
            if (!inTest || !_head.HasFace || !OutsideEnterZone())
            {
                _outSince = -1f;
                return;
            }
            if (_outSince < 0f)
                _outSince = now;
            // Not in the middle of a selection: let that finish first.
            if (now - _outSince >= EnterSustainSeconds && !GazeDwellIndicator.IsDwelling)
                Begin(now);
            return;
        }

        if (!inTest)
        {
            End(false);
            return;
        }
        if (_successUntil >= 0f)
        {
            if (now >= _successUntil)
                End(true);
            return;
        }

        if (!_head.HasFace)
        {
            _holdTimer = 0f;
            _saidGood = false;
            if (_faceLostSince < 0f)
                _faceLostSince = now;
            if (now - _faceLostSince >= FaceLostSeconds)
            {
                Show(Direction.None, FaceScreenText, MoveColor);
                Speak(FaceScreenText, now, true);
            }
            return;
        }
        _faceLostSince = -1f;

        float lateral = _head.LateralOffsetDeg;
        float distanceCm = _head.DistanceOffset * 100f;
        _lateralScale.Set(lateral);
        _distanceScale.Set(distanceCm);
        float lateralRatio = Mathf.Abs(lateral) / ExitLateralDeg;
        float distanceRatio = Mathf.Abs(distanceCm) / ExitDistanceCm;

        if (Mathf.Max(lateralRatio, distanceRatio) <= 1f)
        {
            Show(Direction.None, GoodText, GoodColor);
            if (!_saidGood)
            {
                _saidGood = true;
                Speak(GoodText, now, false);
            }
            _holdTimer += Time.unscaledDeltaTime;
            if (_holdTimer >= HoldSeconds)
            {
                _successUntil = now + SuccessShowSeconds;
                Debug.Log($"[GUIDE] back in place after {now - _startedAt:0.0} s (lateral {lateral:+0.0;-0.0} deg, distance {distanceCm:+0;-0} cm)");
            }
            return;
        }
        _holdTimer = 0f;
        _saidGood = false;

        // Tobii's x, and so the lateral offset, grows toward the user's right: they move left.
        Direction direction = lateralRatio >= distanceRatio
            ? (lateral > 0f ? Direction.Left : Direction.Right)
            : (distanceCm > 0f ? Direction.Closer : Direction.Back);
        string text = TextFor(direction);
        Show(direction, text, MoveColor);

        if (direction != _pending)
        {
            _pending = direction;
            _pendingSince = now;
        }
        if (now - _pendingSince >= InstructionSettleSeconds)
            Speak(text, now, true);
    }

    private bool OutsideEnterZone()
    {
        return Mathf.Abs(_head.LateralOffsetDeg) > EnterLateralDeg
               || Mathf.Abs(_head.DistanceOffset * 100f) > EnterDistanceCm;
    }

    private void Begin(float now)
    {
        _guiding = true;
        Active = true;
        ButtonTrigger.Paused = true;
        _head.SetHoldAtCalibration(true);
        _visual.SetActive(true);
        _startedAt = now;
        _holdTimer = 0f;
        _saidGood = false;
        _successUntil = -1f;
        _faceLostSince = -1f;

        float lateral = _head.LateralOffsetDeg;
        float distanceCm = _head.DistanceOffset * 100f;
        Direction direction = Mathf.Abs(lateral) / ExitLateralDeg >= Mathf.Abs(distanceCm) / ExitDistanceCm
            ? (lateral > 0f ? Direction.Left : Direction.Right)
            : (distanceCm > 0f ? Direction.Closer : Direction.Back);
        _pending = direction;
        _pendingSince = now;
        _lateralScale.Set(lateral);
        _distanceScale.Set(distanceCm);
        Show(direction, TextFor(direction), MoveColor);
        Nuwa.stopTTS();
        Nuwa.startTTS(TitleText + "，" + TextFor(direction));
        _spoken = TextFor(direction);
        _spokenAt = now;
        Debug.Log($"[GUIDE] start: lateral {lateral:+0.0;-0.0} deg, distance {distanceCm:+0;-0} cm; answering paused");
    }

    private void End(bool backInPlace)
    {
        _guiding = false;
        Active = false;
        ButtonTrigger.Paused = false;
        _head.SetHoldAtCalibration(false);
        _visual.SetActive(false);
        _outSince = -1f;
        _successUntil = -1f;
        if (!backInPlace)
            Debug.Log($"[GUIDE] cancelled after {Time.unscaledTime - _startedAt:0.0} s");
    }

    private void OnDisable()
    {
        if (_guiding)
            End(false);
    }

    // Says the text when it changes, and repeats it every few seconds while it stays the same.
    private void Speak(string text, float now, bool repeat)
    {
        if (text == _spoken && (!repeat || now - _spokenAt < RepeatSeconds))
            return;
        _spoken = text;
        _spokenAt = now;
        Nuwa.stopTTS();
        Nuwa.startTTS(text);
    }

    private void Show(Direction direction, string text, Color color)
    {
        _instruction.text = UiFactory.WithLatinFallback(text, _font);
        _instruction.color = direction == Direction.None && color == GoodColor ? GoodColor : TextColor;

        _arrow.gameObject.SetActive(direction != Direction.None);
        switch (direction)
        {
            case Direction.Right: _arrow.localRotation = Quaternion.identity; break;
            case Direction.Left: _arrow.localRotation = Quaternion.Euler(0f, 0f, 180f); break;
            // Up for "closer": forward, as in a navigation arrow.
            case Direction.Closer: _arrow.localRotation = Quaternion.Euler(0f, 0f, 90f); break;
            case Direction.Back: _arrow.localRotation = Quaternion.Euler(0f, 0f, 270f); break;
        }
        foreach (var part in _arrowParts)
            part.color = color;
    }

    private static string TextFor(Direction direction)
    {
        switch (direction)
        {
            case Direction.Left: return MoveLeftText;
            case Direction.Right: return MoveRightText;
            case Direction.Closer: return MoveCloserText;
            case Direction.Back: return MoveBackText;
            default: return FaceScreenText;
        }
    }
}
