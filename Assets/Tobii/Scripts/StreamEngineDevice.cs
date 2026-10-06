/*
  COPYRIGHT 2025 - PROPERTY OF TOBII AB
  -------------------------------------
  2025 TOBII AB - KARLSROVAGEN 2D, DANDERYD 182 53, SWEDEN - All Rights Reserved.

  NOTICE:  All information contained herein is, and remains, the property of Tobii AB and its suppliers, if any.
  The intellectual and technical concepts contained herein are proprietary to Tobii AB and its suppliers and may be
  covered by U.S.and Foreign Patents, patent applications, and are protected by trade secret or copyright law.
  Dissemination of this information or reproduction of this material is strictly forbidden unless prior written
  permission is obtained from Tobii AB.
*/

using AOT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Tobii.StreamEngine;
using UnityEngine;
using UnityEngine.Events;
using static TobiiProcessor.Interop;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem; // Needed for Keyboard.current
#endif

public struct Tobii_HeadPose
{
    public long Timestamp;
    public bool IsValid;
    public Vector3 Position;
    public Vector3 Rotation;
}

public class StreamEngineDevice : MonoBehaviour
{
    // Time sync interval in seconds
    public const int TimeSyncInterval = 30;

    public bool IsConnected => _streamEngineContext != null;

    // Display area of the current display relative to camera
    [Tooltip("Display corner positions (in meters) relative to camera.")]
    public Vector3 displayCornerTopLeftPosInMeters = new Vector3(-0.168f, -0.05f, 0.0f);
    public Vector3 displayCornerTopRightPosInMeters = new Vector3(0.168f, -0.05f, 0.0f);
    public Vector3 displayCornerBottomLeftPosInMeters = new Vector3(-0.168f, -0.205f, 0.0f);

    [Tooltip("Default camera diagnal FOV, overwriten when running on Android as it can be retrieved from the camera parameters.")]
    public float CameraFov = 78;

    // Aspect ratio of the camera image as determined by arriving image data
    private Vector2 lastAspectRatio = Vector2.zero;

    // Webcam related elements to be hidden/disabled when not in use
    [Tooltip("Nexus Capture Clients, will be disabled when hardware tracker in use.")]
    public GameObject nexusCaptureClients;

    // Webcam related UI elements to be hidden/disabled when not in use
    [Tooltip("Webcam related UI elements, will be disabled when hardware tracker in use.")]
    public GameObject webcamUI;

    // Head pose visualisation elements
    [Tooltip("Head pose visualisation elements, will be disabled when head pose is not available.")]
    [SerializeField]
    private GameObject headPoseVisualisation;

    // Tobii Stream Engine context
    public IntPtr DeviceContext => _streamEngineContext.DeviceContext;
    private static IntPtr deviceContext;
    private StreamEngineContext _streamEngineContext;
    private IntPtr processorContext = IntPtr.Zero;
    private IntPtr apiContext = IntPtr.Zero;
    private string license;

    // Events
    public UnityEvent<Vector2> OnGazePoint;
    public UnityEvent<Vector3> OnHeadPoseRotation;
    public UnityEvent<Vector3> OnHeadPosePosition;

    // Callbacks
    private tobii_gaze_callback_t _gazeCallback;
    private tobii_gaze_point_callback_t _gazePointCallback;
    private tobii_head_pose_callback_t _headPoseCallback;
    private Tobii_HeadPose _tobiiHeadPose;

    private string err = ""; // Quick and dirty way to display errors
    private static tobii_processor_log_func_t _logCallback;
    private bool eyetracker5L = false;

    private Task<bool> setDisplaySettingTask;

    // Storage for delegate passed to native code
    [MonoPInvokeCallback(typeof(tobii_processor_log_func_t))]
    private static void ProcessorLogCallback(IntPtr context, tobii_processor_log_level_t level, string text)
    {
        Debug.Log($"[Processor] {level.ToString().Split('_').Last()} {text}");
    }

    IEnumerator Start()
    {
        // Assign the callbacks
        _gazeCallback = OnGaze;
        _headPoseCallback = OnHeadPose;
        _gazePointCallback = On5LGazePoint;

        // Load license file string
        TextAsset seTextAsset = Resources.Load<TextAsset>("se_license_key");
        if (seTextAsset == null)
        {
            err += "Missing license file\n";
            Debug.LogError("Failed to load license file");
            yield break;
        }
        license = System.Text.Encoding.Unicode.GetString(seTextAsset.bytes);

        // Create Tobii API context
        tobii_error_t result = Interop.tobii_api_create(out apiContext, null);
        if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            Debug.Log($"Failed to create API context {result}");
            yield break;
        }

#if PLATFORM_ANDROID
        // Bind to Android hardware tracker via TobiiAndroidBridge if available
        yield return BindToAndroidHWTracker();
#endif

        // Test for Tobii Eyetracker 5L
        tobii_eyetracker_t[] deviceList;
        result = ConnectionManagerInterop.tobii_find_all_eyetrackers(out deviceList);
        if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            Debug.Log($"Failed to find_all_eyetrackers {result}");
            yield break;
        }

        Debug.Log("Tobii Eye Tracker device enumeration complete. " + deviceList.Length + " devices found.");

        // If we have a Tobii Eye Tracker device then we should use it.
        if (deviceList.Length > 0)
        {
            eyetracker5L = true;

            // Hide/disable the webcam related objects
            if (nexusCaptureClients != null)
                nexusCaptureClients.SetActive(false);
            if (webcamUI != null)
                webcamUI.SetActive(false);

            // Disable the AndroidWebcamCaptureClient if present
            var androidWebcamCaptureClient = GetComponent<AndroidWebcamCaptureClient>();
            if (androidWebcamCaptureClient != null)
                androidWebcamCaptureClient.enabled = false;

            Debug.Log($"Found {deviceList.Length} Tobii Eye Tracker devices");
            for (int i = 0; i < deviceList.Length; i++)
            {
                Debug.Log($"Device {i} model: {deviceList[i].url}");
            }

            // Connect to the first device in the list
            result = ConnectionManagerInterop.tobii_connect_eyetracker(deviceList[0], license, out deviceContext);
            if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
            {
                Debug.Log($"Failed to connect to eye tracker with license. License error {result}");
                yield break;
            }
            Debug.Log("Connected to eye tracker!");

            // Create StreamEngineContext
            _streamEngineContext = new StreamEngineContext(apiContext, deviceContext);
            if (_streamEngineContext == null)
            {
                err += "Failed to create StreamEngineContext\n";
                Debug.LogError("Failed to create StreamEngineContext");
                yield break;
            }
            Debug.Log("Tobii Device context created!");

            // Set up display area asynchronously
            if (setDisplaySettingTask == null || setDisplaySettingTask.IsCompleted)
            {
                Debug.Log("Starting ApplyDisplaySettings thread");
                Debug.Log($"TopLeft: {displayCornerTopLeftPosInMeters}, TopRight: {displayCornerTopRightPosInMeters}, BottomLeft: {displayCornerBottomLeftPosInMeters}");
                setDisplaySettingTask = Task.Run(() => SetTrackerDisplaySettings());
            }

            GCHandle handle = GCHandle.Alloc(this);

            // Subscribe to gaze point (5L specific path)
            result = ScreenbasedInterop.tobii_gaze_point_subscribe(deviceContext, _gazePointCallback, GCHandle.ToIntPtr(handle));
            if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
                Debug.Log($"Failed to subscribe to gaze {result}");
            else
                Debug.Log("Subscribed to gaze!");

            // Subscribe to head pose
            result = ScreenbasedInterop.tobii_head_pose_subscribe(deviceContext, _headPoseCallback, GCHandle.ToIntPtr(handle));
            if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
            {
                Debug.Log($"Failed to subscribe to head pose {result}. Not necessarily critical if license does not support head pose.");
                // Hide the head visualisation if head pose is not available
                if (headPoseVisualisation != null)
                    headPoseVisualisation.SetActive(false);
            }
            else
                Debug.Log("Subscribed to head pose!");
        }
        else
        {
            Debug.Log("No Tobii Eye Tracker devices found, falling back to webcam processor path.");
        }
    }

    private IEnumerator BindToAndroidHWTracker()
    {
        // Ensure binder bridge is initialized
        Debug.Log("[StreamEngineDevice] Calling TobiiAndroidBridge.Initialize()");
        try { TobiiAndroidBridge.Initialize(); }
        catch (Exception ex)
        {
            Debug.LogError("[StreamEngineDevice] Initialize() exception: " + ex.Message);
            yield break;
        }

        // Wait briefly for binder readiness (up to 2s)
        float deadline = Time.time + 2f;
        int loopCount = 0;
        while (!TobiiAndroidBridge.IsReady() && Time.time < deadline)
        {
            if ((++loopCount & 0x3F) == 0) // every 64 iterations
                Debug.Log("[StreamEngineDevice] Waiting for binder... t=" + Time.time.ToString("F2"));
            yield return null;
        }

        if (!TobiiAndroidBridge.IsReady())
        {
            Debug.LogWarning("[StreamEngineDevice] Binder not ready -> will rely on processor path.");
            yield break;
        }
        Debug.Log("[StreamEngineDevice] Binder ready.");
    }

    private Task<bool> SetTrackerDisplaySettings()
    {
        if (deviceContext == IntPtr.Zero) return Task.FromResult(true);

        tobii_geometry_mounting_t geometry_mounting;
        tobii_error_t error = ConfigInterop.tobii_get_geometry_mounting(deviceContext, out geometry_mounting);
        if (error != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            Debug.Log(string.Format("Error tobii_get_geometry_mounting: {0}", error));
            return Task.FromResult(false);
        }

        tobii_display_area_t display_area;
        Vector2 sizeInMM = GetDisplayAreaInMM();
        error = ConfigInterop.tobii_calculate_display_area_basic(apiContext, sizeInMM.x, sizeInMM.y, 0, ref geometry_mounting, out display_area);
        if (error != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            Debug.Log(string.Format("Error tobii_calculate_display_area_basic: {0}", error));
            return Task.FromResult(false);
        }

        error = ConfigInterop.tobii_set_display_area(deviceContext, ref display_area);
        if (error != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            Debug.Log(string.Format("Error tobii_set_display_area: {0}", error));
            return Task.FromResult(false);
        }
        Debug.Log("Display area successfully configured asynchronously!");
        return Task.FromResult(true);
    }

    public Vector2 GetDisplayAreaInMM()
    {
        var mm = 1000f;
        return new Vector2(
            (float)Math.Sqrt(Math.Pow((displayCornerTopRightPosInMeters.x - displayCornerTopLeftPosInMeters.x) * mm, 2) +
                             Math.Pow((displayCornerTopRightPosInMeters.y - displayCornerTopLeftPosInMeters.y) * mm, 2) +
                             Math.Pow((displayCornerTopRightPosInMeters.z - displayCornerTopLeftPosInMeters.z) * mm, 2)),
            (float)Math.Sqrt(Math.Pow((displayCornerTopLeftPosInMeters.x - displayCornerBottomLeftPosInMeters.x) * mm, 2) +
                             Math.Pow((displayCornerTopLeftPosInMeters.y - displayCornerBottomLeftPosInMeters.y) * mm, 2) +
                             Math.Pow((displayCornerTopLeftPosInMeters.z - displayCornerBottomLeftPosInMeters.z) * mm, 2))
        );
    }

    // Held for each frame and taken by OnDestroy, so the device is never torn down under a frame
    // that GazeFrameWorker is still processing. Calibration calls deliberately do not take it.
    private readonly object _frameLock = new object();
    private bool _closing;

    // Image data arrives here from the AndroidWebcamCaptureClient (or other media capture clients).
    // On Android this runs on GazeFrameWorker's thread: no Unity API calls beyond Debug.Log.
    public void ProcessMediaCaptureFrame(tobii_image_frame_t frame, Tobii.MediaCaptureClientLib.mcclient_frame_format_type formatType)
    {
        lock (_frameLock)
        {
            if (_closing)
                return;
            ProcessFrameLocked(frame);
        }
    }

    // Experiment (FovABProbe): a camera FOV to switch to; the processor is recreated with it on the
    // next frame, which drops any calibration, so it is only requested before a calibration.
    private volatile float _requestedFov = -1f;
    // The FOV reported by the camera plugin (the horizontal one since V2.3.5).
    public float CameraReportedFov { get; private set; }
    public Vector2 FrameSize => lastAspectRatio;

    public void RequestFov(float fov)
    {
        _requestedFov = fov;
    }

    private void ProcessFrameLocked(tobii_image_frame_t frame)
    {
        try
        {
            float requestedFov = _requestedFov;
            if (requestedFov > 0f && !Mathf.Approximately(requestedFov, CameraFov) && lastAspectRatio != Vector2.zero
                && lastAspectRatio == new Vector2(frame.width, frame.height))
            {
                Debug.Log($"[FOVAB] recreating the processor with FOV {requestedFov:0.0} (was {CameraFov:0.0})");
                CameraFov = requestedFov;
                CreateWebcamProcessor();
            }

            if (lastAspectRatio == Vector2.zero || lastAspectRatio != new Vector2(frame.width, frame.height))
            {
                Debug.Log($"Creating new processor for resolution: {frame.width}x{frame.height}");
                lastAspectRatio = new Vector2(frame.width, frame.height);
                CreateWebcamProcessor();
            }

            long processStart = GazeLatencyStats.Now();
            tobii_error_t result = Interop.tobii_process_frame(deviceContext, frame);
            GazeLatencyStats.RecordFrameProcessed(processStart);
            if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
                Debug.LogError($"Error processing frame: {result}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in ProcessMediaCaptureFrame: {ex.Message}\nStack: {ex.StackTrace}");
        }
    }

    private void CreateWebcamProcessor()
    {
        if (processorContext != IntPtr.Zero)
        {
            Debug.Log("Cleaning up existing processor");
            if (_streamEngineContext != null && deviceContext != IntPtr.Zero)
            {
                ScreenbasedInterop.tobii_gaze_unsubscribe(deviceContext);
                ScreenbasedInterop.tobii_head_pose_unsubscribe(deviceContext);
                ConnectionManagerInterop.tobii_disconnect(deviceContext);
            }
            tobii_processor_destroy(processorContext);
            processorContext = IntPtr.Zero;
        }

        tobii_camera_parameters_t tcpt = new tobii_camera_parameters_t();
        tcpt.fov = CameraFov;
        tcpt.w_aspect_ratio = lastAspectRatio.x;
        tcpt.h_aspect_ratio = lastAspectRatio.y;

        _logCallback = ProcessorLogCallback;
        var log = new tobii_processor_log_t { log_func = _logCallback };

        Debug.Log("Creating processor with tcpt.w_aspect_ratio: " + tcpt.w_aspect_ratio + ", tcpt.h_aspect_ratio: " + tcpt.h_aspect_ratio + ", FOV: " + tcpt.fov);
        processorContext = tobii_processor_create(log, tcpt);

        if (processorContext == IntPtr.Zero)
        {
            err += "Failed to create processor\n";
            Debug.LogError("Failed to create processor");
            return;
        }

        tobii_error_t result = ConnectionManagerInterop.tobii_connect_processor(processorContext, license, out deviceContext);
        if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            err += result + "\n";
            Debug.Log($"Failed to connect to processor with license. License error {result}");
            return;
        }

        Debug.Log("Tobii Device context created!");

        _streamEngineContext = new StreamEngineContext(apiContext, deviceContext);
        if (_streamEngineContext == null)
        {
            err += "Failed to create StreamEngineContext\n";
            Debug.LogError("Failed to create StreamEngineContext");
            return;
        }

        tobii_display_area_t displayArea = new tobii_display_area_t
        {
            top_left_mm_xyz = new TobiiVector3 { x = displayCornerTopLeftPosInMeters.x * 1000f, y = displayCornerTopLeftPosInMeters.y * 1000f, z = displayCornerTopLeftPosInMeters.z * 1000f },
            top_right_mm_xyz = new TobiiVector3 { x = displayCornerTopRightPosInMeters.x * 1000f, y = displayCornerTopRightPosInMeters.y * 1000f, z = displayCornerTopRightPosInMeters.z * 1000f },
            bottom_left_mm_xyz = new TobiiVector3 { x = displayCornerBottomLeftPosInMeters.x * 1000f, y = displayCornerBottomLeftPosInMeters.y * 1000f, z = displayCornerBottomLeftPosInMeters.z * 1000f }
        };

        result = ConfigInterop.tobii_set_display_area(deviceContext, ref displayArea);
        if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
        {
            err += result + "\n";
            Debug.Log($"Failed to set display area {result}");
            return;
        }
        else
        {
            Debug.Log("Tobii Display area set configured!");
        }

        GCHandle handle = GCHandle.Alloc(this);

        result = ScreenbasedInterop.tobii_gaze_subscribe(deviceContext, _gazeCallback, GCHandle.ToIntPtr(handle));
        if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
            Debug.Log($"Failed to subscribe to gaze {result}");
        else
            Debug.Log("Subscribed to gaze!");

        result = ScreenbasedInterop.tobii_head_pose_subscribe(deviceContext, _headPoseCallback, GCHandle.ToIntPtr(handle));
        if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
            Debug.Log($"Failed to subscribe to head pose {result}. Not necessarily critical if license does not support head pose.");
        else
            Debug.Log("Subscribed to head pose!");
    }

    // Called from AndroidWebcamCaptureClient when camera is initialized
    public void OnCameraInitialized(float fov, float aspectRatioWidth, float aspectRationHeight)
    {
        Debug.Log("Camera FOV set from Android: " + fov);
        CameraFov = fov;
        CameraReportedFov = fov;
    }

    private Queue<(Vector2 point, long enqueuedAt)> gazePointQueue = new Queue<(Vector2 point, long enqueuedAt)>();
    private readonly object gazeQueueLock = new object();

    [MonoPInvokeCallback(typeof(tobii_gaze_callback_t))]
    private static void OnGaze(ref tobii_gaze_point_t gazePoint, IntPtr userData)
    {
        var instance = (StreamEngineDevice)GCHandle.FromIntPtr(userData).Target;
        if (instance == null)
        {
            Debug.LogError("Failed to retrieve the instance from userData in OnGaze callback.");
            return;
        }

        if (gazePoint.validity == tobii_validity_t.TOBII_VALIDITY_VALID)
        {
            Vector2 point = new Vector2(gazePoint.position.x, gazePoint.position.y);
            lock (instance.gazeQueueLock)
            {
                if (instance.gazePointQueue.Count < 10)
                    instance.gazePointQueue.Enqueue((point, GazeLatencyStats.Now()));
            }
        }
    }

    [MonoPInvokeCallback(typeof(tobii_gaze_point_callback_t))]
    private static void On5LGazePoint(ref tobii_gaze_point_t gazePoint, IntPtr userData)
    {
        var instance = (StreamEngineDevice)GCHandle.FromIntPtr(userData).Target;
        if (instance == null)
        {
            Debug.LogError("Failed to retrieve the instance from userData in OnGaze callback.");
            return;
        }

        if (gazePoint.validity == tobii_validity_t.TOBII_VALIDITY_VALID)
        {
            Vector2 point = new Vector2(gazePoint.position.x, gazePoint.position.y);
            lock (instance.gazeQueueLock)
            {
                if (instance.gazePointQueue.Count < 10)
                    instance.gazePointQueue.Enqueue((point, GazeLatencyStats.Now()));
            }
        }
    }

    private Queue<Tobii_HeadPose> headPoseQueue = new Queue<Tobii_HeadPose>();
    private readonly object queueLock = new object();

    [MonoPInvokeCallback(typeof(tobii_head_pose_callback_t))]
    private static void OnHeadPose(ref tobii_head_pose_t head_pose, IntPtr user_data)
    {
        var instance = (StreamEngineDevice)GCHandle.FromIntPtr(user_data).Target;
        if (instance == null)
        {
            Debug.LogError("Failed to retrieve the instance from userData in OnHeadPose callback.");
            return;
        }

        instance._tobiiHeadPose = new Tobii_HeadPose
        {
            Timestamp = head_pose.timestamp_us,
            IsValid = head_pose.position_validity == tobii_validity_t.TOBII_VALIDITY_VALID &&
                      head_pose.rotation_x_validity == tobii_validity_t.TOBII_VALIDITY_VALID &&
                      head_pose.rotation_y_validity == tobii_validity_t.TOBII_VALIDITY_VALID &&
                      head_pose.rotation_z_validity == tobii_validity_t.TOBII_VALIDITY_VALID,
            Position = new Vector3(head_pose.position_xyz.x, head_pose.position_xyz.y, head_pose.position_xyz.z),
            Rotation = new Vector3(head_pose.rotation_xyz.x, head_pose.rotation_xyz.y, head_pose.rotation_xyz.z),
        };

        if (instance._tobiiHeadPose.IsValid)
        {
            lock (instance.queueLock)
            {
                if (instance.headPoseQueue.Count < 10)
                    instance.headPoseQueue.Enqueue(instance._tobiiHeadPose);
            }
        }
    }

    void Update()
    {
        // Quit app when Escape is pressed (supports both input systems)
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var keyboard = Keyboard.current; // Fully resolved via using above
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            Application.Quit();
#else
        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();
#endif

        lock (queueLock)
        {
            while (headPoseQueue.Count > 0)
            {
                var headPose = headPoseQueue.Dequeue();
                if (headPose.IsValid)
                {
                    OnHeadPoseRotation.Invoke(headPose.Rotation);
                    var pos = headPose.Position / 1000f; // Convert mm to meters
                    OnHeadPosePosition.Invoke(pos);
                }
            }
        }

        lock (gazeQueueLock)
        {
            while (gazePointQueue.Count > 0)
            {
                var (point, enqueuedAt) = gazePointQueue.Dequeue();
                GazeLatencyStats.RecordGazeDispatched(enqueuedAt);
                OnGazePoint.Invoke(point);
            }
        }
    }

    private void OnGUI()
    {
        // Display the err to screen
        GUI.Label(new Rect(10, Screen.height - 60, 300, 50), err);
    }

    private void OnDestroy()
    {
        // Waits for a frame in flight on the worker thread, then refuses new ones.
        lock (_frameLock)
            _closing = true;

        if (deviceContext == IntPtr.Zero) return;

        try
        {
            Debug.Log("Starting cleanup sequence");

            if (deviceContext != IntPtr.Zero)
            {
                Debug.Log("Unsubscribing from gaze");
                var result = ScreenbasedInterop.tobii_gaze_unsubscribe(deviceContext);
                if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
                    Debug.LogWarning($"Failed to unsubscribe from gaze {result}");

                if (eyetracker5L)
                {
                    Debug.Log("Unsubscribing from gaze point");
                    result = ScreenbasedInterop.tobii_gaze_point_unsubscribe(deviceContext);
                    if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
                        Debug.LogWarning($"Failed to unsubscribe from gaze point {result}");
                }
                else
                {
                    Debug.Log("Unsubscribing from headpose");
                    result = ScreenbasedInterop.tobii_head_pose_unsubscribe(deviceContext);
                    if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
                        Debug.LogWarning($"Failed to unsubscribe from headpose {result}");
                }
            }

            if (processorContext != IntPtr.Zero)
            {
                Debug.Log("Destroying processor");
                tobii_processor_destroy(processorContext);
                processorContext = IntPtr.Zero;
            }

            if (deviceContext != IntPtr.Zero)
            {
                Debug.Log("Disconnecting device");
                ConnectionManagerInterop.tobii_disconnect(deviceContext);
                deviceContext = IntPtr.Zero;
            }

            if (apiContext != IntPtr.Zero)
            {
                Debug.Log("Destroying API context");
                var result = Interop.tobii_api_destroy(apiContext);
                if (result != tobii_error_t.TOBII_ERROR_NO_ERROR)
                    Debug.LogWarning($"Failed to destroy api {result}");
                apiContext = IntPtr.Zero;
            }

            _streamEngineContext = null;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during cleanup: {e.Message}\nStack: {e.StackTrace}");
        }
    }
}

public class StreamEngineContext
{
    public IntPtr DeviceContext { get; private set; }
    public IntPtr ApiContext { get; private set; }
    public string Url { get; private set; }

    public StreamEngineContext(IntPtr apiContext, IntPtr deviceContext)
    {
        ApiContext = apiContext;
        DeviceContext = deviceContext;
    }
}
