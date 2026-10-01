using System.Collections.Generic;
using Tobii;
using UnityEngine;

// Corrects slow gaze drift during the test. Every trial puts a new symbol at the screen centre, and
// people look at it before choosing a direction, so the first steady fixation near it after it
// appears samples where the tracker places "the centre". The difference is folded into
// FollowGazePoint2D.DriftOffset a fraction at a time, so one bad sample moves it little and only a
// consistent offset over several trials is corrected.
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
    // Beyond this from the symbol the fixation is not trusted as a look at it.
    private const float MaxResidualPx = 150f;
    private const float MaxOffsetPx = 150f;
    // Until a few trials are in, step toward each sample; after that, follow the median of the
    // recent ones. On the device single residuals scattered by up to 100 px, and the median
    // ignores the odd fixation that was not really on the symbol.
    private const float Gain = 0.3f;
    private const int MedianWindow = 5;
    private const int MedianMinimum = 3;
    private const float MedianBlend = 0.5f;

    public bool Enabled { get; set; } = true;
    public Vector2 Offset => _pointer.DriftOffset;
    public int Accepted { get; private set; }
    public int Trials { get; private set; }

    private FollowGazePoint2D _pointer;
    private MetricTest _metricTest;
    private HeadFollow _head;
    private GazeCalibrationManager _calibration;

    private bool _searching;
    private float _shownAt;
    private string _rejection;
    private readonly List<Vector2> _recent = new List<Vector2>();
    // Every steady fixation near the symbol this trial. The first one is often an undershoot -
    // coming up from the lower option the eye lands short, below the symbol, then corrects - and
    // taking only the first one pushed the whole dot 76 px upward in five trials on the device.
    private readonly List<Vector2> _candidates = new List<Vector2>();
    // For each used trial, the offset that would have put that fixation exactly on the symbol.
    private readonly List<Vector2> _implied = new List<Vector2>();
    // Recent samples, to measure where the gaze sat on an option while it was being chosen.
    private readonly List<(float time, Vector2 position)> _history = new List<(float, Vector2)>();
    private const float OptionSampleSeconds = 1f;

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
        go.SetActive(true);
        return corrector;
    }

    private void OnEnable()
    {
        _pointer.SampleAdded += OnSample;
        _metricTest.SymbolShown += OnSymbolShown;
        _metricTest.SideChosen += OnSideChosen;
        _calibration.CalibrationStarted += OnCalibrationStarted;
        _calibration.TestCountdownStarted += OnTestCountdownStarted;
    }

    private void OnDisable()
    {
        _pointer.SampleAdded -= OnSample;
        _metricTest.SymbolShown -= OnSymbolShown;
        _metricTest.SideChosen -= OnSideChosen;
        _calibration.CalibrationStarted -= OnCalibrationStarted;
        _calibration.TestCountdownStarted -= OnTestCountdownStarted;
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (!enabled)
            ResetOffset("disabled");
    }

    private void OnCalibrationStarted()
    {
        ResetOffset("new calibration");
    }

    // Each round of the test starts with a countdown; in single-eye mode the second round covers
    // the other eye, which changes the gaze estimate, so the offset is learnt again.
    private void OnTestCountdownStarted(string side, float seconds)
    {
        ResetOffset("round " + side);
    }

    private void ResetOffset(string reason)
    {
        _pointer.DriftOffset = Vector2.zero;
        _implied.Clear();
        _searching = false;
        Accepted = 0;
        Trials = 0;
        Debug.Log("[DRIFT] offset reset (" + reason + ")");
    }

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

    private void OnSample(Vector2 sample)
    {
        float now = Time.unscaledTime;
        _history.Add((now, sample));
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

        float elapsed = Time.unscaledTime - _shownAt;
        if (elapsed > SearchWindowSeconds || (elapsed > SelectionStartSeconds && GazeDwellIndicator.IsDwelling))
        {
            Conclude();
            return;
        }
        if (elapsed < SearchDelaySeconds)
            return;
        // The screen moves with the robot's head; gaze while it turns says nothing about drift.
        if (_head.IsMoving)
        {
            _recent.Clear();
            _rejection = _rejection ?? "robot head was moving";
            return;
        }

        _recent.Add(sample);
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

        Vector2 symbol = ScreenCentre(_metricTest.blackRT);
        float toSymbol = Vector2.Distance(centroid, symbol);
        foreach (var option in _metricTest.sidesRT)
        {
            // Looking at a direction (often still the one just chosen); keep searching.
            if (Vector2.Distance(centroid, ScreenCentre(option)) < toSymbol)
            {
                _rejection = "fixations were on the options";
                return;
            }
        }

        if (Vector2.Distance(symbol, centroid) > MaxResidualPx)
        {
            _rejection = $"nearest fixation {Vector2.Distance(symbol, centroid):0} px from the symbol";
            return;
        }

        // Keep looking: the trial's estimate is the median of all its steady fixations on the symbol.
        _candidates.Add(centroid);
        _recent.Clear();
    }

    // Ends this trial's search: the median of the fixations found becomes one drift sample.
    private void Conclude()
    {
        if (_candidates.Count == 0)
        {
            Finish("no look at the symbol found (" + (_rejection ?? "no steady fixation") + ")");
            return;
        }

        Vector2 residual = ScreenCentre(_metricTest.blackRT) - Median(_candidates);
        _implied.Add(_pointer.DriftOffset + residual);
        if (_implied.Count > MedianWindow)
            _implied.RemoveAt(0);
        Vector2 offset = _implied.Count >= MedianMinimum
            ? Vector2.Lerp(_pointer.DriftOffset, Median(_implied), MedianBlend)
            : _pointer.DriftOffset + Gain * residual;
        offset = Vector2.ClampMagnitude(offset, MaxOffsetPx);
        _pointer.DriftOffset = offset;
        Accepted++;
        Finish($"residual {residual.x:+0;-0},{residual.y:+0;-0} px from {_candidates.Count} fixation(s) -> offset {offset.x:+0;-0},{offset.y:+0;-0} px");
    }

    // Measurement only, for now: how far the gaze sat from an option while it was chosen by
    // dwell. Users reported the dot riding high when they look down, which the centre symbol
    // cannot show; per-direction residuals can, and are what a multi-point correction needs.
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
        Vector2 residual = ScreenCentre(option) - centroid;
        Debug.Log($"[OPTION] {side}: residual {residual.x:+0;-0},{residual.y:+0;-0} px ({_history.Count} samples, offset {_pointer.DriftOffset.x:+0;-0},{_pointer.DriftOffset.y:+0;-0})");
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

    private void Finish(string result)
    {
        _searching = false;
        Debug.Log($"[DRIFT] trial {Trials}: {result} ({Accepted}/{Trials} used)");
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
        xs.Sort();
        ys.Sort();
        int middle = points.Count / 2;
        return points.Count % 2 == 1
            ? new Vector2(xs[middle], ys[middle])
            : new Vector2((xs[middle - 1] + xs[middle]) * 0.5f, (ys[middle - 1] + ys[middle]) * 0.5f);
    }

    private static Vector2 ScreenCentre(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }
}
