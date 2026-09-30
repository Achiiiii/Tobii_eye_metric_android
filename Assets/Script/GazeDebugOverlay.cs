using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Developer overlay (tap the network icon 5 times) for measuring gaze latency/noise
// and tuning the fixation smoothing on the device.
public class GazeDebugOverlay : MonoBehaviour
{
    private const float RadiusFactor = 1.25f;
    private const float WindowStepSeconds = 0.1f;

    private FollowGazePoint2D _pointer;
    private HeadFollow _headFollow;
    private TextMeshProUGUI _headModeLabel;
    private TextMeshProUGUI _text;
    private GazeLatencyStats.Snapshot _previous;
    private float _previousTime;

    public static GazeDebugOverlay Create(Transform parent, FollowGazePoint2D pointer, HeadFollow headFollow)
    {
        var panel = UiFactory.CreateImage("GazeDebugOverlay", parent, UiFactory.RoundedRect, new Color(0f, 0f, 0f, 0.72f), true);
        panel.type = Image.Type.Sliced;
        UiFactory.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(340f, 300f));

        var overlay = panel.gameObject.AddComponent<GazeDebugOverlay>();
        overlay._pointer = pointer;
        overlay._headFollow = headFollow;

        // The Chinese SDF atlas has no Latin glyphs, so use TMP's default font here.
        var font = TMP_Settings.defaultFontAsset;
        overlay._text = UiFactory.CreateText("Stats", panel.transform, font, "measuring...", 15f, Color.white, TextAlignmentOptions.TopLeft);
        UiFactory.Place(overlay._text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(316f, 206f));

        string[] labels = { "radius -", "radius +", "window -", "window +" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            var button = UiFactory.CreateButton(labels[i], panel.transform, UiFactory.RoundedRect, new Color(1f, 1f, 1f, 0.2f), () => overlay.Adjust(index));
            ((Image)button.targetGraphic).type = Image.Type.Sliced;
            UiFactory.Place((RectTransform)button.transform, Vector2.zero, Vector2.zero, new Vector2(12f + i * 80f, 10f), new Vector2(74f, 32f));
            var label = UiFactory.CreateText("Label", button.transform, font, labels[i], 14f, Color.white);
            UiFactory.Stretch(label.rectTransform);
        }

        // Cycles Off / BetweenTrials / Continuous, for comparing accuracy with and without it.
        var headButton = UiFactory.CreateButton("HeadMode", panel.transform, UiFactory.RoundedRect, new Color(1f, 0.65f, 0.14f, 0.35f), overlay.CycleHeadMode);
        ((Image)headButton.targetGraphic).type = Image.Type.Sliced;
        UiFactory.Place((RectTransform)headButton.transform, Vector2.zero, Vector2.zero, new Vector2(12f, 48f), new Vector2(314f, 32f));
        overlay._headModeLabel = UiFactory.CreateText("Label", headButton.transform, font, "", 14f, Color.white);
        UiFactory.Stretch(overlay._headModeLabel.rectTransform);
        overlay.RefreshHeadModeLabel();

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

        _text.text = "<mspace=0.58em>"
            + $"camera fps       {frames / elapsed,7:0.0}\n"
            + $"process frame    {processMs,7:0.0} ms\n"
            + $"gaze rate        {gazeSamples / elapsed,7:0.0} Hz\n"
            + $"dispatch delay   {dispatchMs,7:0.0} ms\n"
            + $"raw vs dot       {lagPx,7:0} px\n"
            + $"fixation spread  {spreadPx,7:0} px\n"
            + $"radius {radiusPx,5:0} px  window {_pointer.FixationWindowSeconds:0.00} s\n"
            + $"head    {_headFollow.Status}\n"
            + $"head err {_headFollow.Error.x,6:0.0} {_headFollow.Error.y,6:0.0} deg"
            + "</mspace>";

        _previous = current;
        _previousTime = Time.unscaledTime;
    }

    private void CycleHeadMode()
    {
        _headFollow.CycleMode();
        RefreshHeadModeLabel();
    }

    private void RefreshHeadModeLabel()
    {
        _headModeLabel.text = "head follow: " + _headFollow.Mode;
    }

    private void Adjust(int index)
    {
        switch (index)
        {
            case 0: _pointer.FixationRadiusScreenFraction /= RadiusFactor; break;
            case 1: _pointer.FixationRadiusScreenFraction *= RadiusFactor; break;
            case 2: _pointer.FixationWindowSeconds -= WindowStepSeconds; break;
            case 3: _pointer.FixationWindowSeconds += WindowStepSeconds; break;
        }
        Debug.Log($"[GazeDebugOverlay] fixationRadius={_pointer.FixationRadiusScreenFraction:0.000} (x screen width) window={_pointer.FixationWindowSeconds:0.00}s");
    }
}
