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
    // About 0.35 s at the ~11 Hz gaze rate measured on the robot.
    private const int FixationSamples = 4;
    private const float FixationDispersionPx = 60f;
    // Beyond this from the symbol the fixation is not trusted as a look at it.
    private const float MaxResidualPx = 150f;
    private const float MaxOffsetPx = 150f;
    private const float Gain = 0.3f;

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
        _calibration.CalibrationStarted += OnCalibrationStarted;
        _calibration.TestCountdownStarted += OnTestCountdownStarted;
    }

    private void OnDisable()
    {
        _pointer.SampleAdded -= OnSample;
        _metricTest.SymbolShown -= OnSymbolShown;
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
        _searching = false;
        Accepted = 0;
        Trials = 0;
        Debug.Log("[DRIFT] offset reset (" + reason + ")");
    }

    private void OnSymbolShown()
    {
        if (_searching)
            Finish("no look at the symbol found (" + (_rejection ?? "no steady fixation") + ")");
        if (!Enabled)
            return;
        _searching = true;
        _shownAt = Time.unscaledTime;
        _rejection = null;
        _recent.Clear();
        Trials++;
    }

    private void OnSample(Vector2 sample)
    {
        if (!_searching)
            return;

        float elapsed = Time.unscaledTime - _shownAt;
        if (elapsed > SearchWindowSeconds)
        {
            Finish("no look at the symbol found (" + (_rejection ?? "no steady fixation") + ")");
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

        Vector2 residual = symbol - centroid;
        if (residual.magnitude > MaxResidualPx)
        {
            _rejection = $"nearest fixation {residual.magnitude:0} px from the symbol";
            return;
        }

        Vector2 offset = Vector2.ClampMagnitude(_pointer.DriftOffset + Gain * residual, MaxOffsetPx);
        _pointer.DriftOffset = offset;
        Accepted++;
        Finish($"residual {residual.x:+0;-0},{residual.y:+0;-0} px at {elapsed:0.00} s -> offset {offset.x:+0;-0},{offset.y:+0;-0} px");
    }

    private void Finish(string result)
    {
        _searching = false;
        Debug.Log($"[DRIFT] trial {Trials}: {result} ({Accepted}/{Trials} used)");
    }

    private static Vector2 ScreenCentre(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
    }
}
