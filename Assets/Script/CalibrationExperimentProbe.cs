using Tobii;
using UnityEngine;

// Temporary experiment: is the Tobii calibration used at all? The parsed calibration reports every
// point as "valid but not used", and calibrated vs cleared trials gave the same error.
// - Odd sessions collect each calibration point 3 times: if the points were dropped for too little
//   data (~0.6 s per point at ~13 fps), they should now read "used".
// - Even sessions tell Tobii every target sits 100 px to the right of where it is drawn: if the
//   calibration is applied, the gaze afterwards should shift about 100 px right; if not, nothing moves.
// The settings change when a head distance check starts, before that session's calibration.
public class CalibrationExperimentProbe : MonoBehaviour
{
    private const int MoreDataRepeats = 3;
    private const float TargetShiftPx = 100f;

    private DetectDistance _detectDistance;
    private bool _wasChecking;
    private int _session;

    public static CalibrationExperimentProbe Create(Transform parent, DetectDistance detectDistance)
    {
        var go = new GameObject("CalibrationExperimentProbe");
        go.transform.SetParent(parent, false);
        var probe = go.AddComponent<CalibrationExperimentProbe>();
        probe._detectDistance = detectDistance;
        return probe;
    }

    private void Update()
    {
        bool checking = _detectDistance.IsChecking;
        if (checking && !_wasChecking)
            StartSession();
        _wasChecking = checking;
    }

    private void StartSession()
    {
        _session++;
        if (_session % 2 == 1)
        {
            StreamEngineCalibration.CollectRepeats = MoreDataRepeats;
            StreamEngineCalibration.TargetOffset = Vector2.zero;
            Debug.Log($"[CALEXP] session {_session}: each calibration point collected {MoreDataRepeats} times");
        }
        else
        {
            StreamEngineCalibration.CollectRepeats = 1;
            StreamEngineCalibration.TargetOffset = new Vector2(TargetShiftPx / Screen.width, 0f);
            Debug.Log($"[CALEXP] session {_session}: calibration targets reported {TargetShiftPx:0} px right of where they are drawn");
        }
    }
}
