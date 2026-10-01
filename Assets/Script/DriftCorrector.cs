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
// bottom 50 px too high - the "everything rides up when I look down" the user reported. So each
// axis gets its own piecewise-linear correction through three points: x through left / centre /
// right, y through bottom / centre / top, each the median of its recent samples.
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
    // A group needs this many samples to count, and an axis needs two groups to be corrected:
    // the centre alone is what pushed the bottom of the screen off.
    private const int MinimumPerGroup = 2;

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
    // Correction model: for each axis, three points ordered low to high on screen (left / centre /
    // right for x, bottom / centre / top for y), with a value where enough samples exist.
    private readonly float[] _xPosition = new float[3];
    private readonly float[] _xValue = new float[3];
    private readonly bool[] _xKnown = new bool[3];
    private readonly float[] _yPosition = new float[3];
    private readonly float[] _yValue = new float[3];
    private readonly bool[] _yKnown = new bool[3];

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
            corrector._residuals[anchor] = new List<Vector2>();
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
        if (!Enabled)
            return raw;
        return raw + new Vector2(
            Interpolate(_xPosition, _xValue, _xKnown, raw.x),
            Interpolate(_yPosition, _yValue, _yKnown, raw.y));
    }

    // Piecewise-linear through the known points, flat beyond the outermost ones; nothing with
    // fewer than two known points.
    private static float Interpolate(float[] position, float[] value, bool[] known, float at)
    {
        int count = 0;
        int first = -1, last = -1;
        for (int i = 0; i < 3; i++)
        {
            if (!known[i])
                continue;
            count++;
            if (first < 0)
                first = i;
            last = i;
        }
        if (count < 2)
            return 0f;
        if (at <= position[first])
            return value[first];
        if (at >= position[last])
            return value[last];

        int lower = first;
        for (int i = first + 1; i <= last; i++)
        {
            if (!known[i])
                continue;
            if (at <= position[i])
            {
                float t = Mathf.InverseLerp(position[lower], position[i], at);
                return Mathf.Lerp(value[lower], value[i], t);
            }
            lower = i;
        }
        return value[last];
    }

    private void Rebuild()
    {
        Vector2 centre = ScreenCentre(_metricTest.blackRT);
        _xPosition[0] = ScreenCentre(OptionFor("left")).x;
        _xPosition[1] = centre.x;
        _xPosition[2] = ScreenCentre(OptionFor("right")).x;
        _yPosition[0] = ScreenCentre(OptionFor("down")).y;
        _yPosition[1] = centre.y;
        _yPosition[2] = ScreenCentre(OptionFor("up")).y;

        // The up and down options sit in the centre column and left and right on the centre row,
        // so their other component counts toward the centre of that axis.
        SetGroup(_xValue, _xKnown, 0, Pool(true, Anchor.Left));
        SetGroup(_xValue, _xKnown, 1, Pool(true, Anchor.Centre, Anchor.Up, Anchor.Down));
        SetGroup(_xValue, _xKnown, 2, Pool(true, Anchor.Right));
        SetGroup(_yValue, _yKnown, 0, Pool(false, Anchor.Down));
        SetGroup(_yValue, _yKnown, 1, Pool(false, Anchor.Centre, Anchor.Left, Anchor.Right));
        SetGroup(_yValue, _yKnown, 2, Pool(false, Anchor.Up));

        Debug.Log("[FIX] " + Describe());
    }

    private List<float> Pool(bool x, params Anchor[] anchors)
    {
        var values = new List<float>();
        foreach (var anchor in anchors)
        {
            foreach (var residual in _residuals[anchor])
                values.Add(x ? residual.x : residual.y);
        }
        return values;
    }

    private static void SetGroup(float[] value, bool[] known, int index, List<float> samples)
    {
        known[index] = samples.Count >= MinimumPerGroup;
        value[index] = known[index] ? Mathf.Clamp(Median(samples), -MaxCorrectionPx, MaxCorrectionPx) : 0f;
    }

    // e.g. "x L+30 C+5 R-25  y D-3 C+55 U+50" (blank where not yet known).
    public string Describe()
    {
        return "x " + Group("L", _xValue, _xKnown, 0) + " " + Group("C", _xValue, _xKnown, 1) + " " + Group("R", _xValue, _xKnown, 2)
               + "  y " + Group("D", _yValue, _yKnown, 0) + " " + Group("C", _yValue, _yKnown, 1) + " " + Group("U", _yValue, _yKnown, 2);
    }

    private static string Group(string name, float[] value, bool[] known, int index)
    {
        return name + (known[index] ? value[index].ToString("+0;-0") : "?");
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
        foreach (var list in _residuals.Values)
            list.Clear();
        for (int i = 0; i < 3; i++)
        {
            _xKnown[i] = false;
            _yKnown[i] = false;
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
