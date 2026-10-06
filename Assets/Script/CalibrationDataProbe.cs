using Tobii;
using UnityEngine;

// Temporary experiment: does more calibration data per point help? With the camera focused on the
// face the calibration takes the error from about 90 to 54 px, and each point is collected only
// once (~0.6 s, ~8 frames at ~13 fps). Odd sessions keep that; even sessions collect each point
// 3 times with 0.5 s pauses so new frames come in. Compare with the accuracy check after each
// calibration. The setting changes when a head distance check starts, before the calibration.
public class CalibrationDataProbe : MonoBehaviour
{
    private const int MoreDataRepeats = 3;

    private DetectDistance _detectDistance;
    private bool _wasChecking;
    private int _session;

    public static CalibrationDataProbe Create(Transform parent, DetectDistance detectDistance)
    {
        var go = new GameObject("CalibrationDataProbe");
        go.transform.SetParent(parent, false);
        var probe = go.AddComponent<CalibrationDataProbe>();
        probe._detectDistance = detectDistance;
        return probe;
    }

    private void Update()
    {
        bool checking = _detectDistance.IsChecking;
        if (checking && !_wasChecking)
        {
            _session++;
            bool more = _session % 2 == 0;
            StreamEngineCalibration.CollectRepeats = more ? MoreDataRepeats : 1;
            Debug.Log($"[CALEXP] session {_session}: each calibration point collected {(more ? MoreDataRepeats + " times, 0.5 s apart" : "once")}");
        }
        _wasChecking = checking;
    }
}
