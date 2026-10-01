using System.Collections.Generic;
using Tobii;
using UnityEngine;

// Corrects the systematic part of the gaze error during the test, as a function of where on the
// screen the gaze is. The test itself provides reference points: the centre symbol, which people
// look at before choosing, and each option chosen by dwell, which the gaze sat on.
//
// On the robot the raw error was not one shift. With the user sitting high, gaze read ~50 px low
// at the top and the centre but about right at the bottom, and the left and right options read
// 30-40 px toward the middle. A single offset learnt at the centre fixed the top and pushed the
// bottom 50 px too high - the "everything rides up when I look down" the user reported.
//
// Nor does the error split by axis: in the next run the up and down options, in the same column,
// were off horizontally by -32 and +16 px, and left and right by -4 and -37 px vertically, so a
// per-axis correction pooling them made the down option worse. Each of the five reference points
// - centre, up, down, left, right - therefore keeps its own 2D correction (the median of its last
// samples), applied exactly at that point and blended linearly between the centre and the two
// nearest options elsewhere. A point without data counts as no correction, so the correction
// fades toward sides not yet measured instead of spreading the centre's everywhere.
//
// Everything is learnt and applied in raw screen space: FollowGazePoint2D hands raw samples here
// and asks Correct() for the position to show, so a learnt correction never feeds back into what
// is learnt next.
public class DriftCorrector : MonoBehaviour
{
    // Before this the saccade to the centre is usually still under way.
    private const float SearchDelaySeconds = 0.25f;
    private const float SearchWindowSeconds = 2.5f;
    // After this, a dwell in progress means the user has moved on to choosing an option. (Before
    // it, the indicator may still be finishing the previous answer.)
    private const float SelectionStartSeconds = 1f;
    // About 0.35 s at the ~11 Hz gaze rate measured on the robot.
    private const int FixationSamples = 4;
    private const float FixationDispersionPx = 60f;
    // Beyond this from its reference point a sample is not trusted as a look at it.
    private const float MaxResidualPx = 150f;
    private const float MaxCorrectionPx = 150f;
    // Raw samples over the last second of a dwell describe where the gaze sat on the option.
    private const float OptionSampleSeconds = 1f;
    // Per reference point, the most recent residuals kept; a group's value is their median.
    private const int HistoryPerAnchor = 5;
    // A reference point needs this many samples before its correction is used.
    private const int MinimumPerAnchor = 2;

    private enum Anchor { Centre, Up, Down, Left, Right }

    public bool Enabled { get; private set; } = true;
    public int Accepted { get; private set; }
    public int Trials { get; private set; }
    public int OptionSamples { get; private set; }

    private FollowGazePoint2D _pointer;
    private MetricTest _metricTest;
    private HeadFollow _head;
    private GazeCalibrationManager _calibration;

    private bool _searching;
    private float _shownAt;
    private string _rejection;
    private readonly List<Vector2> _recent = new List<Vector2>();
    // Raw centroids of every steady fixation near the symbol this trial. The first one is often an
    // undershoot - coming up from the lower option the eye lands short, below the symbol - so the
    // trial's sample is their median rather than the first.
    private readonly List<Vector2> _candidates = new List<Vector2>();
    private readonly List<(float time, Vector2 position)> _history = new List<(float, Vector2)>();

    // Raw-space residuals per reference point: where it is, minus where the raw gaze sat.
    private readonly Dictionary<Anchor, List<Vector2>> _residuals = new Dictionary<Anchor, List<Vector2>>();
    // Correction model: per reference point, where it is on screen and its correction, if known.
    private readonly Dictionary<Anchor, Vector2> _position = new Dictionary<Anchor, Vector2>();
    private readonly Dictionary<Anchor, Vector2> _value = new Dictionary<Anchor, Vector2>();
    private readonly Dictionary<Anchor, bool> _known = new Dictionary<Anchor, bool>();

    public static DriftCorrector Create(Transform parent, FollowGazePoint2D pointer, MetricTest metricTest, HeadFollow head, GazeCalibrationManager calibration)
    {
        var go = new GameObject("DriftCorrector");
        go.transform.SetParent(parent, false);
        // Inactive until wired: AddComponent would otherwise run OnEnable with null references.
        go.SetActive(false);
        var corrector = go.AddComponent<DriftCorrector>();
        corrector._pointer = pointer;
        corrector._metricTest = metricTest;
        corrector._head = head;
        corrector._calibration = calibration;
        foreach (Anchor anchor in System.Enum.GetValues(typeof(Anchor)))
        {
            corrector._residuals[anchor] = new List<Vector2>();
            corrector._value[anchor] = Vector2.zero;
            corrector._known[anchor] = false;
        }
        pointer.Correction = corrector.Correct;
        go.SetActive(true);
        return corrector;
    }

    private void OnEnable()
    {
        _pointer.RawSampleAdded += OnSample;
        _metricTest.SymbolShown += OnSymbolShown;
        _metricTest.SideChosen += OnSideChosen;
        _calibration.CalibrationStarted += OnCalibrationStarted;
        _calibration.TestCountdownStarted += OnTestCountdownStarted;
    }

    private void OnDisable()
    {
        _pointer.RawSampleAdded -= OnSample;
        _metricTest.SymbolShown -= OnSymbolShown;
        _metricTest.SideChosen -= OnSideChosen;
        _calibration.CalibrationStarted -= OnCalibrationStarted;
        _calibration.TestCountdownStarted -= OnTestCountdownStarted;
    }

    // ==================== Correction ====================

    // Raw screen position -> position to show.
    public Vector2 Correct(Vector2 raw)
    {
        if (!Enabled || !AnyKnown())
            return raw;

        // The options sit around the centre like a plus sign. Express the point in the triangle of
        // the centre and the nearest horizontal and vertical options, and blend their corrections.
        Vector2 centre = _position[Anchor.Centre];
        Vector2 d = raw - centre;
        Anchor horizontal = d.x >= 0f ? Anchor.Right : Anchor.Left;
        Anchor vertical = d.y >= 0f ? Anchor.Up : Anchor.Down;
        Vector2 h = _position[horizontal] - centre;
        Vector2 v = _position[vertical] - centre;

        float determinant = h.x * v.y - h.y * v.x;
        if (Mathf.Abs(determinant) < 1f)
            return raw + ValueAt(Anchor.Centre);
        float a = (d.x * v.y - d.y * v.x) / determinant;
        float b = (h.x * d.y - h.y * d.x) / determinant;
        a = Mathf.Max(0f, a);
        b = Mathf.Max(0f, b);
        // Beyond the options, hold the value on the outer edge.
        if (a + b > 1f)
        {
            float sum = a + b;
            a /= sum;
            b /= sum;
        }
        return raw + (1f - a - b) * ValueAt(Anchor.Centre) + a * ValueAt(horizontal) + b * ValueAt(vertical);
    }

    private bool AnyKnown()
    {
        foreach (var known in _known.Values)
        {
            if (known)
                return true;
        }
        return false;
    }

    private Vector2 ValueAt(Anchor anchor)
    {
        return _known[anchor] ? _value[anchor] : Vector2.zero;
    }

    private void Rebuild()
    {
        _position[Anchor.Centre] = ScreenCentre(_metricTest.blackRT);
        _position[Anchor.Up] = ScreenCentre(OptionFor("up"));
        _position[Anchor.Down] = ScreenCentre(OptionFor("down"));
        _position[Anchor.Left] = ScreenCentre(OptionFor("left"));
        _position[Anchor.Right] = ScreenCentre(OptionFor("right"));

        foreach (Anchor anchor in System.Enum.GetValues(typeof(Anchor)))
        {
            var samples = _residuals[anchor];
            _known[anchor] = samples.Count >= MinimumPerAnchor;
            _value[anchor] = _known[anchor] ? Vector2.ClampMagnitude(Median(samples), MaxCorrectionPx) : Vector2.zero;
        }
        Debug.Log("[FIX] " + Describe());
    }

    // e.g. "C-20,-7 U-32,-3 D+16,-32 L-91,-4 R+38,-37" with "?" where not yet known.
    public string Describe()
    {
        return Point("C", Anchor.Centre) + " " + Point("U", Anchor.Up) + " " + Point("D", Anchor.Down)
               + " " + Point("L", Anchor.Left) + " " + Point("R", Anchor.Right);
    }

    private string Point(string name, Anchor anchor)
    {
        return _known[anchor] ? $"{name}{_value[anchor].x:+0;-0},{_value[anchor].y:+0;-0}" : name + "?";
    }

    private void AddResidual(Anchor anchor, Vector2 residual)
    {
        var list = _residuals[anchor];
        list.Add(residual);
        if (list.Count > HistoryPerAnchor)
            list.RemoveAt(0);
        Rebuild();
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (!enabled)
            ResetModel("disabled");
    }

    private void OnCalibrationStarted()
    {
        ResetModel("new calibration");
    }

    // Each round of the test starts with a countdown; in single-eye mode the second round covers
    // the other eye, which changes the gaze estimate, so the correction is learnt again.
    private void OnTestCountdownStarted(string side, float seconds)
    {
        ResetModel("round " + side);
    }

    private void ResetModel(string reason)
    {
        foreach (Anchor anchor in System.Enum.GetValues(typeof(Anchor)))
        {
            _residuals[anchor].Clear();
            _known[anchor] = false;
            _value[anchor] = Vector2.zero;
        }
        _searching = false;
        Accepted = 0;
        Trials = 0;
        OptionSamples = 0;
        Debug.Log("[DRIFT] correction reset (" + reason + ")");
    }

    // ==================== Centre samples ====================

    private void OnSymbolShown()
    {
        if (_searching)
            Conclude();
        if (!Enabled)
            return;
        _searching = true;
        _shownAt = Time.unscaledTime;
        _rejection = null;
        _recent.Clear();
        _candidates.Clear();
        Trials++;
    }

    private void OnSample(Vector2 raw)
    {
        float now = Time.unscaledTime;
        _history.Add((now, raw));
        while (_history.Count > 0 && _history[0].time < now - OptionSampleSeconds)
            _history.RemoveAt(0);

        if (!_searching)
            return;
        // The user is being walked back into place, not looking at the symbol.
        if (PositionGuide.Active)
        {
            Finish("paused for position guidance");
            return;
        }

        float elapsed = now - _shownAt;
        if (elapsed > SearchWindowSeconds || (elapsed > SelectionStartSeconds && GazeDwellIndicator.IsDwelling))
        {
            Conclude();
            return;
        }
        if (elapsed < SearchDelaySeconds)
            return;
        // The screen moves with the robot's head; gaze while it turns says nothing about the error.
        if (_head.IsMoving)
        {
            _recent.Clear();
            _rejection = _rejection ?? "robot head was moving";
            return;
        }

        _recent.Add(raw);
        if (_recent.Count > FixationSamples)
            _recent.RemoveAt(0);
        if (_recent.Count < FixationSamples)
            return;

        Vector2 centroid = Vector2.zero;
        foreach (var point in _recent)
            centroid += point;
        centroid /= _recent.Count;
        foreach (var point in _recent)
        {
            if (Vector2.Distance(point, centroid) > FixationDispersionPx)
                return;
        }

        // Decide what is being looked at from the corrected position, the best estimate there is.
        Vector2 shown = Correct(centroid);
        Vector2 symbol = ScreenCentre(_metricTest.blackRT);
        float toSymbol = Vector2.Distance(shown, symbol);
        foreach (var option in _metricTest.sidesRT)
        {
            // Looking at a direction (often still the one just chosen); keep searching.
            if (Vector2.Distance(shown, ScreenCentre(option)) < toSymbol)
            {
                _rejection = "fixations were on the options";
                return;
            }
        }
        if (toSymbol > MaxResidualPx)
        {
            _rejection = $"nearest fixation {toSymbol:0} px from the symbol";
            return;
        }

        _candidates.Add(centroid);
        _recent.Clear();
    }

    // Ends this trial's search: the median of the fixations found becomes one centre sample.
    private void Conclude()
    {
        if (_candidates.Count == 0)
        {
            Finish("no look at the symbol found (" + (_rejection ?? "no steady fixation") + ")");
            return;
        }

        Vector2 residual = ScreenCentre(_metricTest.blackRT) - Median(_candidates);
        Accepted++;
        _searching = false;
        Debug.Log($"[DRIFT] trial {Trials}: centre raw residual {residual.x:+0;-0},{residual.y:+0;-0} px from {_candidates.Count} fixation(s) ({Accepted}/{Trials} used)");
        AddResidual(Anchor.Centre, residual);
    }

    private void Finish(string result)
    {
        _searching = false;
        Debug.Log($"[DRIFT] trial {Trials}: {result} ({Accepted}/{Trials} used)");
    }

    // ==================== Option samples ====================

    // When an option is chosen by dwell, the gaze sat on it for the last second: a reference
    // point at that option. (It slightly understates the error there - the dot had to be on the
    // option for the choice to complete - so corrections err on the small side.)
    private void OnSideChosen(string side)
    {
        // A dwell completes with the indicator playing its finish; a tap does not.
        if (!(GazeDwellIndicator.IsDwelling || GazeDwellIndicator.IsCompleting) || _history.Count < 3)
            return;
        RectTransform option = OptionFor(side);
        if (option == null)
            return;

        Vector2 centroid = Vector2.zero;
        foreach (var entry in _history)
            centroid += entry.position;
        centroid /= _history.Count;
        Vector2 target = ScreenCentre(option);
        Vector2 residual = target - centroid;
        Vector2 shownResidual = target - Correct(centroid);
        Debug.Log($"[OPTION] {side}: raw residual {residual.x:+0;-0},{residual.y:+0;-0} px, as shown {shownResidual.x:+0;-0},{shownResidual.y:+0;-0} px ({_history.Count} samples)");

        if (!Enabled || PositionGuide.Active || residual.magnitude > MaxResidualPx)
            return;
        OptionSamples++;
        AddResidual(AnchorFor(side), residual);
    }

    private static Anchor AnchorFor(string side)
    {
        switch (side)
        {
            case "up": return Anchor.Up;
            case "down": return Anchor.Down;
            case "left": return Anchor.Left;
            default: return Anchor.Right;
        }
    }

    // The options sit above, below, left and right of the symbol; find the one for this side.
    private RectTransform OptionFor(string side)
    {
        Vector2 symbol = ScreenCentre(_metricTest.blackRT);
        RectTransform best = null;
        float bestScore = float.MinValue;
        foreach (var option in _metricTest.sidesRT)
        {
            Vector2 d = ScreenCentre(option) - symbol;
            float score = side == "up" ? d.y : side == "down" ? -d.y : side == "right" ? d.x : side == "left" ? -d.x : float.MinValue;
            if (score > bestScore)
            {
                bestScore = score;
                best = option;
            }
        }
        return best;
    }

    // ==================== Helpers ====================

    private static float Median(List<float> values)
    {
        var sorted = new List<float>(values);
        sorted.Sort();
        int middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) * 0.5f;
    }

    private static Vector2 Median(List<Vector2> points)
    {
        var xs = new List<float>(points.Count);
        var ys = new List<float>(points.Count);
        foreach (var point in points)
        {
            xs.Add(point.x);
            ys.Add(point.y);
        }
        return new Vector2(Median(xs), Median(ys));
    }

    private static Vector2 ScreenCentre(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }
}
