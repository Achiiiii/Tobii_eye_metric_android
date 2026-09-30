using UnityEngine;

// Asks the user to sit back (or forward) during the test when they drift from the distance they
// were calibrated at. Turning the robot's head fixes direction, not distance, and on the device
// users leaned in by up to 9 cm as the symbols shrank - which throws off the gaze model and also
// makes the symbol subtend a larger angle than the test intends.
public class DistanceReminder : MonoBehaviour
{
    private const float TriggerCm = 5f;
    private const float SustainSeconds = 1.5f;
    private const float RepeatSeconds = 8f;

    private HeadFollow _head;
    private MetricTest _metricTest;
    private float _outSince = -1f;
    private float _nextAllowed;

    public static DistanceReminder Create(Transform parent, HeadFollow head, MetricTest metricTest)
    {
        var go = new GameObject("DistanceReminder");
        go.transform.SetParent(parent, false);
        var reminder = go.AddComponent<DistanceReminder>();
        reminder._head = head;
        reminder._metricTest = metricTest;
        return reminder;
    }

    private void Update()
    {
        if (!_metricTest.gameObject.activeInHierarchy || !_head.HasFace || _head.CalibrationDistance <= 0f)
        {
            _outSince = -1f;
            return;
        }

        float offCm = (_head.Distance - _head.CalibrationDistance) * 100f;
        if (Mathf.Abs(offCm) < TriggerCm)
        {
            _outSince = -1f;
            return;
        }

        float now = Time.unscaledTime;
        if (_outSince < 0f)
            _outSince = now;
        // Not while an answer is being selected: the speech would compete with the choice.
        if (now - _outSince < SustainSeconds || now < _nextAllowed || GazeDwellIndicator.IsDwelling)
            return;

        _nextAllowed = now + RepeatSeconds;
        // Same wording as the head distance check before calibration.
        string text = offCm < 0f ? "請往後遠離一點" : "請往前靠近一點";
        Debug.Log($"[DIST] {offCm:+0;-0} cm from the calibrated distance, saying {text}");
        Nuwa.stopTTS();
        Nuwa.startTTS(text);
    }
}
