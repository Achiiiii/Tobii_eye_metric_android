using System;
using System.Threading.Tasks;
using Tobii;
using Tobii.StreamEngine;
using UnityEngine;

// Temporary probe: does the Tobii calibration actually change the gaze? compute_and_apply always
// returns TOBII_ERROR_INTERNAL on the robot, yet the runtime logs a new calibration id. After each
// calibration this keeps the calibration blob, then alternates trials between calibrated (odd) and
// cleared (even), logging [CALIBAB] so [OPTION]/[DRIFT] raw residuals can be split by state.
public class CalibrationABProbe : MonoBehaviour
{
    private StreamEngineDevice _device;
    private GazeCalibrationManager _calibration;
    private MetricTest _metricTest;
    private byte[] _blob;
    private int _trial;
    private Task _pending = Task.CompletedTask;

    public static CalibrationABProbe Create(Transform parent, GazeCalibrationManager calibration, MetricTest metricTest)
    {
        var go = new GameObject("CalibrationABProbe");
        go.transform.SetParent(parent, false);
        go.SetActive(false);
        var probe = go.AddComponent<CalibrationABProbe>();
        probe._device = FindObjectOfType<StreamEngineDevice>();
        probe._calibration = calibration;
        probe._metricTest = metricTest;
        go.SetActive(true);
        return probe;
    }

    private void OnEnable()
    {
        _calibration.CalibrationStarted += OnCalibrationStarted;
        _calibration.CalibrationEnded += OnCalibrationEnded;
        _metricTest.SymbolShown += OnSymbolShown;
    }

    private void OnDisable()
    {
        _calibration.CalibrationStarted -= OnCalibrationStarted;
        _calibration.CalibrationEnded -= OnCalibrationEnded;
        _metricTest.SymbolShown -= OnSymbolShown;
    }

    private void OnCalibrationStarted()
    {
        _blob = null;
        _trial = 0;
    }

    private void OnCalibrationEnded()
    {
        IntPtr device = _device.DeviceContext;
        _pending = _pending.ContinueWith(_ =>
        {
            var idResult = ConfigInterop.tobii_get_calibration_id(device, out uint id);
            var result = ConfigInterop.tobii_calibration_retrieve(device, out byte[] blob);
            _blob = result == tobii_error_t.TOBII_ERROR_NO_ERROR && blob != null && blob.Length > 0 ? blob : null;
            Debug.Log($"[CALIBAB] calibration id {id} ({idResult}); retrieve {result}, {(blob != null ? blob.Length : 0)} bytes");
        });
    }

    private void OnSymbolShown()
    {
        _trial++;
        if (_blob == null)
        {
            Debug.Log($"[CALIBAB] trial {_trial}: no calibration blob, left as is");
            return;
        }
        bool calibrated = _trial % 2 == 1;
        IntPtr device = _device.DeviceContext;
        byte[] blob = _blob;
        int trial = _trial;
        _pending = _pending.ContinueWith(_ =>
        {
            var result = calibrated
                ? ConfigInterop.tobii_calibration_apply(device, blob)
                : ConfigInterop.tobii_calibration_clear(device);
            ConfigInterop.tobii_get_calibration_id(device, out uint id);
            Debug.Log($"[CALIBAB] trial {trial}: {(calibrated ? "calibrated" : "uncalibrated")} ({result}, id {id})");
        });
    }
}
