using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AOT;
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

    // ConfigInterop.tobii_calibration_retrieve passes a lambda as the native callback, which IL2CPP
    // cannot marshal (it threw, silently, inside the task). Use a static callback instead.
    [DllImport(Interop.stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_calibration_retrieve")]
    private static extern tobii_error_t RetrieveCalibration(IntPtr device, tobii_data_receiver_t receiver, IntPtr userData);
    private static readonly tobii_data_receiver_t s_receiver = OnCalibrationData;
    private static byte[] s_received;

    [MonoPInvokeCallback(typeof(tobii_data_receiver_t))]
    private static void OnCalibrationData(IntPtr data, IntPtr size, IntPtr userData)
    {
        int length = size.ToInt32();
        s_received = new byte[length];
        if (length > 0)
            Marshal.Copy(data, s_received, 0, length);
    }

    private void Run(Action action)
    {
        _pending = _pending.ContinueWith(_ =>
        {
            try { action(); }
            catch (Exception e) { Debug.LogError("[CALIBAB] " + e); }
        });
    }

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
        Run(() =>
        {
            var idResult = ConfigInterop.tobii_get_calibration_id(device, out uint id);
            s_received = null;
            var result = RetrieveCalibration(device, s_receiver, IntPtr.Zero);
            byte[] blob = s_received;
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
        Run(() =>
        {
            string result;
            if (calibrated)
            {
                // Returns TOBII_ERROR_INTERNAL like compute_and_apply, but the calibration id changes.
                result = ConfigInterop.tobii_calibration_apply(device, blob).ToString();
            }
            else
            {
                // Clearing is refused outside calibration mode (TOBII_ERROR_CALIBRATION_NOT_STARTED).
                var start = ConfigInterop.tobii_calibration_start(device, tobii_enabled_eye_t.TOBII_ENABLED_EYE_BOTH);
                var clear = ConfigInterop.tobii_calibration_clear(device);
                var stop = ConfigInterop.tobii_calibration_stop(device);
                result = $"start {start}, clear {clear}, stop {stop}";
            }
            ConfigInterop.tobii_get_calibration_id(device, out uint id);
            Debug.Log($"[CALIBAB] trial {trial}: {(calibrated ? "calibrated" : "uncalibrated")} ({result}, id {id})");
        });
    }
}
