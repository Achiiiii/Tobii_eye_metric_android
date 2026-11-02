using UnityEngine;

// Walks the user back to where they sat during calibration when they move well away from it in
// the test. Turning the robot's head keeps the face in the image, but after a user moved about
// 16 cm sideways the camera saw the face 20 degrees off and their looks at the symbol landed on the
// options. Users cannot be expected to remember where they sat, so this guides them step by step:
// the robot faces the calibrated position (so "straight in front of the screen" is right again),
// answering pauses, and the voice repeats the direction until they are back. The V3 pause card
// (V3Overlays) draws it from the state below: the instruction, two scales and the hold.
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

    private const string TitleText = "請回到剛才的位置";
    private const string MoveLeftText = "請往左移一點";
    private const string MoveRightText = "請往右移一點";
    private const string MoveCloserText = "請往前靠近一點";
    private const string MoveBackText = "請往後遠離一點";
    private const string FaceScreenText = "請回到螢幕正前方";
    private const string GoodText = "很好，請保持不動";

    // True while guiding; gaze selection and drift sampling pause meanwhile.
    public static bool Active { get; private set; }

    // For the V3 pause card: the instruction, the arrow (0 none, 1 left, 2 right, 3 closer, 4 back), whether
    // the user is back in place, the offsets and how far the hold has run (0-1).
    public static string Text { get; private set; } = "";
    public static int Arrow { get; private set; }
    public static bool Good { get; private set; }
    public static float LateralDeg { get; private set; }
    public static float DistanceCm { get; private set; }
    public static float HoldFraction { get; private set; }

    private enum Direction { None, Left, Right, Closer, Back }

    private HeadFollow _head;
    private MetricTest _metricTest;

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

    public static PositionGuide Create(Transform parent, HeadFollow head, MetricTest metricTest)
    {
        var go = new GameObject("PositionGuide");
        go.transform.SetParent(parent, false);
        var guide = go.AddComponent<PositionGuide>();
        guide._head = head;
        guide._metricTest = metricTest;
        return guide;
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
                Show(Direction.None, FaceScreenText, false);
                Speak(FaceScreenText, now, true);
            }
            return;
        }
        _faceLostSince = -1f;

        float lateral = _head.LateralOffsetDeg;
        float distanceCm = _head.DistanceOffset * 100f;
        LateralDeg = lateral;
        DistanceCm = distanceCm;
        HoldFraction = Mathf.Clamp01(_holdTimer / HoldSeconds);
        float lateralRatio = Mathf.Abs(lateral) / ExitLateralDeg;
        float distanceRatio = Mathf.Abs(distanceCm) / ExitDistanceCm;

        if (Mathf.Max(lateralRatio, distanceRatio) <= 1f)
        {
            Show(Direction.None, GoodText, true);
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
        Show(direction, text, false);

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
        HoldFraction = 0f;
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
        LateralDeg = lateral;
        DistanceCm = distanceCm;
        Show(direction, TextFor(direction), false);
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

    private static void Show(Direction direction, string text, bool good)
    {
        Text = text;
        Arrow = (int)direction;
        Good = good;
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
