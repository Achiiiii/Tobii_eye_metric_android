using Tobii;
using UnityEngine;

// Temporary diagnostic: asks FrameCapture for a camera frame at the moments that matter - 2 s into
// the head distance check, at each calibration point once the user has had time to look at it, and
// every 10 s during the test - up to FrameCapture's limit.
public class FrameCaptureProbe : MonoBehaviour
{
    private const float HeadCheckDelaySeconds = 2f;
    private const float CalibrationPointDelaySeconds = 0.8f;
    private const float TestIntervalSeconds = 10f;

    private DetectDistance _detectDistance;
    private GazeCalibrationManager _calibration;
    private MetricTest _metricTest;
    private bool _wasChecking;
    private float _headCheckAt = -1f;
    private float _calibrationAt = -1f;
    private string _calibrationTag;
    private float _nextTestFrame;

    public static FrameCaptureProbe Create(Transform parent, DetectDistance detectDistance, GazeCalibrationManager calibration, MetricTest metricTest)
    {
        FrameCapture.Init();
        var go = new GameObject("FrameCaptureProbe");
        go.transform.SetParent(parent, false);
        go.SetActive(false);
        var probe = go.AddComponent<FrameCaptureProbe>();
        probe._detectDistance = detectDistance;
        probe._calibration = calibration;
        probe._metricTest = metricTest;
        go.SetActive(true);
        return probe;
    }

    private void OnEnable()
    {
        _calibration.StimulusShown += OnStimulusShown;
    }

    private void OnDisable()
    {
        _calibration.StimulusShown -= OnStimulusShown;
    }

    private void OnStimulusShown(int index, int count, Vector2 screenPosition)
    {
        _calibrationTag = $"calib{index}of{count}";
        _calibrationAt = Time.unscaledTime + CalibrationPointDelaySeconds;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        bool checking = _detectDistance.IsChecking;
        if (checking && !_wasChecking)
            _headCheckAt = now + HeadCheckDelaySeconds;
        _wasChecking = checking;

        if (_headCheckAt > 0f && now >= _headCheckAt)
        {
            _headCheckAt = -1f;
            FrameCapture.Request("headcheck");
        }
        if (_calibrationAt > 0f && now >= _calibrationAt)
        {
            _calibrationAt = -1f;
            FrameCapture.Request(_calibrationTag);
        }
        if (_metricTest.gameObject.activeInHierarchy)
        {
            if (now >= _nextTestFrame)
            {
                if (_nextTestFrame > 0f)
                    FrameCapture.Request("test");
                _nextTestFrame = now + TestIntervalSeconds;
            }
        }
        else
        {
            _nextTestFrame = 0f;
        }
    }
}
