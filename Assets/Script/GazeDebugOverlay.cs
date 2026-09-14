using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Developer overlay (tap the network icon 5 times) for measuring where gaze latency comes from
// and tuning the gaze filter on the device.
public class GazeDebugOverlay : MonoBehaviour
{
    private const float AdjustFactor = 1.5f;

    private FollowGazePoint2D _pointer;
    private TextMeshProUGUI _text;
    private GazeLatencyStats.Snapshot _previous;
    private float _previousTime;

    public static GazeDebugOverlay Create(Transform parent, FollowGazePoint2D pointer)
    {
        var panel = UiFactory.CreateImage("GazeDebugOverlay", parent, UiFactory.RoundedRect, new Color(0f, 0f, 0f, 0.72f), true);
        panel.type = Image.Type.Sliced;
        UiFactory.Place(panel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(340f, 196f));

        var overlay = panel.gameObject.AddComponent<GazeDebugOverlay>();
        overlay._pointer = pointer;

        // The Chinese SDF atlas has no Latin glyphs, so use TMP's default font here.
        var font = TMP_Settings.defaultFontAsset;
        overlay._text = UiFactory.CreateText("Stats", panel.transform, font, "measuring...", 15f, Color.white, TextAlignmentOptions.TopLeft);
        UiFactory.Place(overlay._text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -10f), new Vector2(316f, 140f));

        string[] labels = { "cut -", "cut +", "beta -", "beta +" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            var button = UiFactory.CreateButton(labels[i], panel.transform, UiFactory.RoundedRect, new Color(1f, 1f, 1f, 0.2f), () => overlay.Adjust(index));
            ((Image)button.targetGraphic).type = Image.Type.Sliced;
            UiFactory.Place((RectTransform)button.transform, Vector2.zero, Vector2.zero, new Vector2(12f + i * 80f, 10f), new Vector2(74f, 32f));
            var label = UiFactory.CreateText("Label", button.transform, font, labels[i], 15f, Color.white);
            UiFactory.Stretch(label.rectTransform);
        }

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
        double processMs = frames > 0 ? GazeLatencyStats.TicksToMilliseconds(current.FrameTicks - _previous.FrameTicks) / frames : 0;
        double dispatchMs = gazeSamples > 0 ? GazeLatencyStats.TicksToMilliseconds(current.DispatchTicks - _previous.DispatchTicks) / gazeSamples : 0;
        double lagPx = lagSamples > 0 ? (current.FilterLagSum - _previous.FilterLagSum) / lagSamples : 0;

        _text.text = "<mspace=0.58em>"
            + $"camera fps     {frames / elapsed,7:0.0}\n"
            + $"process frame  {processMs,7:0.0} ms\n"
            + $"gaze rate      {gazeSamples / elapsed,7:0.0} Hz\n"
            + $"dispatch delay {dispatchMs,7:0.0} ms\n"
            + $"filter lag     {lagPx,7:0} px\n"
            + $"minCutoff {_pointer.MinCutoff,6:0.000}  beta {_pointer.Beta,7:0.0000}"
            + "</mspace>";

        _previous = current;
        _previousTime = Time.unscaledTime;
    }

    private void Adjust(int index)
    {
        switch (index)
        {
            case 0: _pointer.MinCutoff /= AdjustFactor; break;
            case 1: _pointer.MinCutoff *= AdjustFactor; break;
            case 2: _pointer.Beta /= AdjustFactor; break;
            case 3: _pointer.Beta *= AdjustFactor; break;
        }
        Debug.Log($"[GazeDebugOverlay] minCutoff={_pointer.MinCutoff:0.000} beta={_pointer.Beta:0.0000}");
    }
}
