using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Read-only diagnostics (tap the network icon 5 times): gaze rate and latency, head follow and
// drift correction state. The tuning switches used during development were removed for release.
public class GazeDebugOverlay : MonoBehaviour
{
    private FollowGazePoint2D _pointer;
    private HeadFollow _headFollow;
    private DriftCorrector _drift;
    private AndroidWebcamCaptureClient _capture;
    private TextMeshProUGUI _text;
    private GazeLatencyStats.Snapshot _previous;
    private float _previousTime;

    public static GazeDebugOverlay Create(Transform parent, FollowGazePoint2D pointer, HeadFollow headFollow, DriftCorrector drift, AndroidWebcamCaptureClient capture)
    {
        var panel = UiFactory.CreateImage("GazeDebugOverlay", parent, UiFactory.RoundedRect, new Color(0f, 0f, 0f, 0.72f), true);
        panel.type = Image.Type.Sliced;
        UiFactory.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(340f, 330f));

        var overlay = panel.gameObject.AddComponent<GazeDebugOverlay>();
        overlay._pointer = pointer;
        overlay._headFollow = headFollow;
        overlay._drift = drift;
        overlay._capture = capture;

        // The Chinese SDF atlas has no Latin glyphs, so use TMP's default font here.
        var font = TMP_Settings.defaultFontAsset;
        overlay._text = UiFactory.CreateText("Stats", panel.transform, font, "measuring...", 15f, Color.white, TextAlignmentOptions.TopLeft);
        UiFactory.Place(overlay._text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(316f, 310f));

        panel.gameObject.SetActive(false);
        return overlay;
    }

    private void OnEnable()
    {
        _previous = GazeLatencyStats.Take();
        _previousTime = Time.unscaledTime;
    }

    private void Update()
    {
        float elapsed = Time.unscaledTime - _previousTime;
        if (elapsed < 1f)
            return;

        var current = GazeLatencyStats.Take();
        long frames = current.Frames - _previous.Frames;
        long gazeSamples = current.GazeSamples - _previous.GazeSamples;
        long lagSamples = current.FilterLagCount - _previous.FilterLagCount;
        long spreadSamples = current.SpreadCount - _previous.SpreadCount;
        double processMs = frames > 0 ? GazeLatencyStats.TicksToMilliseconds(current.FrameTicks - _previous.FrameTicks) / frames : 0;
        double dispatchMs = gazeSamples > 0 ? GazeLatencyStats.TicksToMilliseconds(current.DispatchTicks - _previous.DispatchTicks) / gazeSamples : 0;
        double lagPx = lagSamples > 0 ? (current.FilterLagSum - _previous.FilterLagSum) / lagSamples : 0;
        double spreadPx = spreadSamples > 0 ? (current.SpreadSum - _previous.SpreadSum) / spreadSamples : 0;
        float radiusPx = _pointer.FixationRadiusScreenFraction * Screen.width;
        int factor = _capture != null ? _capture.DownsampleFactor : 0;
        string resolution = factor == 2 ? "1050x780" : factor == 3 ? "700x520" : factor > 0 ? "x" + factor : "n/a";

        _text.text = "<mspace=0.58em>"
            + $"camera fps       {frames / elapsed,7:0.0}\n"
            + $"process frame    {processMs,7:0.0} ms\n"
            + $"gaze rate        {gazeSamples / elapsed,7:0.0} Hz\n"
            + $"dispatch delay   {dispatchMs,7:0.0} ms\n"
            + $"raw vs dot       {lagPx,7:0} px\n"
            + $"fixation spread  {spreadPx,7:0} px\n"
            + $"radius {radiusPx,5:0} px  window {_pointer.FixationWindowSeconds:0.00} s\n"
            + $"resolution {resolution}  drift fix {(_drift.Enabled ? "on" : "off")}\n"
            + $"head    {_headFollow.Mode}: {_headFollow.Status}\n"
            + $"head err {_headFollow.Error.x,6:0.0} {_headFollow.Error.y,6:0.0} deg\n"
            + $"distance {_headFollow.Distance * 100f,4:0} cm  cal {_headFollow.CalibrationDistance * 100f,3:0} cm\n"
            + $"lateral  {(_headFollow.Calibrated ? _headFollow.LateralOffsetDeg : 0f),6:0.0} deg\n"
            + $"fix {_drift.Describe()}\n"
            + $"fix samples centre {_drift.Accepted}/{_drift.Trials}  options {_drift.OptionSamples}"
            + "</mspace>";

        _previous = current;
        _previousTime = Time.unscaledTime;
    }
}
