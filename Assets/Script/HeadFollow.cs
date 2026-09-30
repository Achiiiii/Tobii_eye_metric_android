using System;
using Tobii;
using UnityEngine;

// Turns the robot's head (neck_z / neck_y) so the user's head stays where it was during gaze
// calibration. Elderly users drift over a test of several minutes, and the webcam gaze model is
// most accurate near the head pose it was calibrated at.
//
// The input is Tobii's own head pose (metres, camera space), so this needs no second camera user
// and no extra inference. The camera and the screen both sit on the robot's head, so turning it
// keeps the camera-to-screen geometry the calibration relies on - but it also moves what the user
// is looking at. The head therefore never moves while calibration dots are up, and during the test
// (in BetweenTrials mode) only in the short gap after each answer.
public class HeadFollow : MonoBehaviour
{
    public enum FollowMode
    {
        Off,
        BetweenTrials,
        Continuous
    }

    private const float CommandHz = 4f;
    private const float Gain = 0.5f;
    private const float MaxStepDeg = 4f;
    private const float MotorSpeedDegPerSec = 20f;
    // Hysteresis: start correcting beyond EnterDeadzone, stop once inside ExitDeadzone.
    private const float EnterDeadzoneDeg = 5f;
    private const float ExitDeadzoneDeg = 2.5f;
    private const float YawRangeDeg = 35f;
    private const float PitchRangeDeg = 12f;
    private const float HomeYawDeg = 0f;
    private const float HomePitchDeg = 0f;
    private const float PoseSmoothingSeconds = 0.2f;
    private const float PoseStaleSeconds = 0.5f;
    // Shorter than ButtonTrigger's 1 s selection cooldown, so the head is still again before the
    // next answer can start filling.
    private const float PostAnswerWindowSeconds = 0.8f;
    // Sign check: after this much commanded correction, the error should have shrunk, not grown.
    private const float SignCheckMinCommandDeg = 8f;
    private const float SignCheckGrowthRatio = 0.6f;
    private const float LogIntervalSeconds = 1f;

    public FollowMode Mode { get; set; } = FollowMode.BetweenTrials;
    public string Status { get; private set; } = "idle";
    // How far the user's head is from where it should be, in degrees (yaw, pitch).
    public Vector2 Error => _hasPose ? _poseAngles - _targetAngles : Vector2.zero;

    private StreamEngineDevice _device;
    private GazeCalibrationManager _calibration;
    private MetricTest _metricTest;
    private GameObject _resultPage;

    private bool _engaged;
    private bool _calibrating;
    private bool _robotReady;
    private float _nextReadyCheck;
    private float _nextCommand;
    private float _nextLog;
    private float _answerWindowUntil;

    private bool _hasPose;
    private float _lastPoseTime;
    private Vector2 _poseAngles;  // user's head direction from the camera: (yaw, pitch) degrees

    // Where the user's head sat during calibration; before that, straight ahead of the camera.
    private Vector2 _targetAngles;
    private Vector2 _calibrationSum;
    private int _calibrationSamples;

    private readonly Axis _yaw = new Axis("yaw", Nuwa.NuwaMotorType.neck_z, HomeYawDeg, YawRangeDeg, -1f);
    private readonly Axis _pitch = new Axis("pitch", Nuwa.NuwaMotorType.neck_y, HomePitchDeg, PitchRangeDeg, -1f);

    // neck_z +: the robot turns to its own right, which is the user's left. Tobii's x grows toward
    // the user's right, so the yaw correction is negated. neck_y +: the robot looks down, and y
    // grows upward, so pitch is negated too. Both signs are re-checked on the device (see Axis).
    private class Axis
    {
        public readonly string Name;
        public readonly Nuwa.NuwaMotorType Motor;
        public readonly float Home;
        public readonly float Range;
        public float Sign;
        public bool Correcting;
        public bool Disabled;
        public int Flips;
        public float RunStartError;
        public float RunCommanded;
        public float Actual;

        public Axis(string name, Nuwa.NuwaMotorType motor, float home, float range, float sign)
        {
            Name = name;
            Motor = motor;
            Home = home;
            Range = range;
            Sign = sign;
            Actual = home;
        }
    }

    public static HeadFollow Create(Transform parent, GazeCalibrationManager calibration, MetricTest metricTest, GameObject resultPage)
    {
        var go = new GameObject("HeadFollow");
        go.transform.SetParent(parent, false);
        // Inactive until wired: AddComponent would otherwise run OnEnable with null references.
        go.SetActive(false);
        var follow = go.AddComponent<HeadFollow>();
        follow._calibration = calibration;
        follow._metricTest = metricTest;
        follow._resultPage = resultPage;
        follow._device = FindObjectOfType<StreamEngineDevice>();
        if (follow._device == null)
            Debug.LogWarning("[HeadFollow] StreamEngineDevice not found; head following is unavailable.");
        go.SetActive(true);
        return follow;
    }

    private void OnEnable()
    {
        if (_device != null)
            _device.OnHeadPosePosition.AddListener(OnHeadPosition);
        _calibration.CalibrationStarted += OnCalibrationStarted;
        _calibration.CalibrationEnded += OnCalibrationEnded;
        _calibration.CalibrationFailed += OnCalibrationFailed;
        _metricTest.Answered += OnAnswered;
    }

    private void OnDisable()
    {
        if (_device != null)
            _device.OnHeadPosePosition.RemoveListener(OnHeadPosition);
        _calibration.CalibrationStarted -= OnCalibrationStarted;
        _calibration.CalibrationEnded -= OnCalibrationEnded;
        _calibration.CalibrationFailed -= OnCalibrationFailed;
        _metricTest.Answered -= OnAnswered;
    }

    // Called once the questionnaire is done and the head distance check begins.
    public void Engage()
    {
        _engaged = true;
        _targetAngles = Vector2.zero;
        ResetAxes();
        Debug.Log("[HeadFollow] engaged, mode=" + Mode);
    }

    // Called when going back home or restarting a session; turns the head back to its rest pose.
    public void ReturnHome()
    {
        _engaged = false;
        _calibrating = false;
        _targetAngles = Vector2.zero;
        Status = "home";
        DriveHome();
        Debug.Log("[HeadFollow] returning home");
    }

    // For A/B comparisons from the debug overlay. Switching to Off leaves the head where it is:
    // driving it home mid-session would itself move the user away from the calibrated pose.
    // Pick the mode on the home page to compare whole sessions.
    public FollowMode CycleMode()
    {
        Mode = (FollowMode)(((int)Mode + 1) % Enum.GetValues(typeof(FollowMode)).Length);
        ResetAxes();
        Debug.Log("[HeadFollow] mode=" + Mode);
        return Mode;
    }

    private void OnHeadPosition(Vector3 metres)
    {
        // Tobii: x toward the user's right, y up, z from the camera to the user. z's sign is not
        // relied on anywhere else (DetectDistance takes its absolute value), so neither is it here.
        float depth = Mathf.Abs(metres.z);
        if (depth < 0.05f)
            return;
        var angles = new Vector2(
            Mathf.Atan2(metres.x, depth) * Mathf.Rad2Deg,
            Mathf.Atan2(metres.y, depth) * Mathf.Rad2Deg);

        float now = Time.unscaledTime;
        if (!_hasPose || now - _lastPoseTime > PoseStaleSeconds)
        {
            _poseAngles = angles;
        }
        else
        {
            float blend = 1f - Mathf.Exp(-(now - _lastPoseTime) / PoseSmoothingSeconds);
            _poseAngles = Vector2.Lerp(_poseAngles, angles, blend);
        }
        _hasPose = true;
        _lastPoseTime = now;

        if (_calibrating)
        {
            _calibrationSum += angles;
            _calibrationSamples++;
        }
    }

    private void OnCalibrationStarted()
    {
        _calibrating = true;
        _calibrationSum = Vector2.zero;
        _calibrationSamples = 0;
    }

    private void OnCalibrationEnded()
    {
        _calibrating = false;
        if (_calibrationSamples > 0)
        {
            _targetAngles = _calibrationSum / _calibrationSamples;
            Debug.Log($"[HeadFollow] target = calibration pose yaw {_targetAngles.x:0.0} pitch {_targetAngles.y:0.0} ({_calibrationSamples} samples)");
        }
    }

    private void OnCalibrationFailed()
    {
        _calibrating = false;
    }

    private void OnAnswered()
    {
        _answerWindowUntil = Time.unscaledTime + PostAnswerWindowSeconds;
    }

    private void Update()
    {
        // Stay put while the result is read; ReturnHome() runs when the user leaves the page.
        if (_engaged && _resultPage.activeInHierarchy)
        {
            _engaged = false;
            Debug.Log("[HeadFollow] result shown, holding");
        }

        UpdateRobotReady();
        float now = Time.unscaledTime;

        bool allowed = MovementAllowed(out string reason);
        Status = reason;

        if (allowed && now >= _nextCommand)
        {
            _nextCommand = now + 1f / CommandHz;
            Step();
        }
        else if (!allowed)
        {
            // Whatever run was in progress is over; the next one starts from fresh measurements.
            _yaw.Correcting = false;
            _pitch.Correcting = false;
        }

        if (_engaged && now >= _nextLog)
        {
            _nextLog = now + LogIntervalSeconds;
            Vector2 error = _poseAngles - _targetAngles;
            Debug.Log($"[HEAD] mode={Mode} state={Status} pose={(_hasPose ? $"{_poseAngles.x:0.0},{_poseAngles.y:0.0}" : "none")} "
                      + $"target={_targetAngles.x:0.0},{_targetAngles.y:0.0} err={error.x:0.0},{error.y:0.0} "
                      + $"motor={_yaw.Actual:0.0},{_pitch.Actual:0.0} sign={_yaw.Sign:+0;-0},{_pitch.Sign:+0;-0}");
        }
    }

    private bool MovementAllowed(out string reason)
    {
        if (Mode == FollowMode.Off) { reason = "off"; return false; }
        if (!_engaged) { reason = "idle"; return false; }
        if (!_robotReady) { reason = "waiting for robot"; return false; }
        if (_calibrating) { reason = "hold: calibrating"; return false; }
        if (!_hasPose || Time.unscaledTime - _lastPoseTime > PoseStaleSeconds) { reason = "hold: no face"; return false; }

        bool inTrial = _metricTest.gameObject.activeInHierarchy;
        if (inTrial && Mode == FollowMode.BetweenTrials && Time.unscaledTime > _answerWindowUntil)
        {
            reason = "hold: trial";
            return false;
        }
        reason = "tracking";
        return true;
    }

    private void Step()
    {
        _yaw.Actual = ReadMotor(_yaw);
        _pitch.Actual = ReadMotor(_pitch);
        Vector2 error = _poseAngles - _targetAngles;

        float yawTarget = StepAxis(_yaw, error.x);
        float pitchTarget = StepAxis(_pitch, error.y);
        if (!float.IsNaN(yawTarget))
            Nuwa.setMotorPositionInDegree(_yaw.Motor, yawTarget, MotorSpeedDegPerSec);
        if (!float.IsNaN(pitchTarget))
            Nuwa.setMotorPositionInDegree(_pitch.Motor, pitchTarget, MotorSpeedDegPerSec);
    }

    // Returns the new motor target for this axis, or NaN when it should stay where it is.
    private float StepAxis(Axis axis, float error)
    {
        if (axis.Disabled)
            return float.NaN;

        float magnitude = Mathf.Abs(error);
        if (!axis.Correcting)
        {
            if (magnitude < EnterDeadzoneDeg)
                return float.NaN;
            axis.Correcting = true;
            axis.RunStartError = magnitude;
            axis.RunCommanded = 0f;
        }
        else if (magnitude < ExitDeadzoneDeg)
        {
            axis.Correcting = false;
            return float.NaN;
        }

        CheckSign(axis, magnitude);
        if (axis.Disabled)
            return float.NaN;

        // Step from where the motor actually is: the pose lags the motor, so accumulating on the
        // previous command would overshoot. The gain below 1 absorbs the rest of that lag.
        float step = Mathf.Clamp(Gain * error, -MaxStepDeg, MaxStepDeg);
        float target = Mathf.Clamp(axis.Actual + axis.Sign * step, axis.Home - axis.Range, axis.Home + axis.Range);
        axis.RunCommanded += Mathf.Abs(target - axis.Actual);
        return target;
    }

    // With the wrong sign every correction pushes the user further off-centre. Once enough
    // correction has been commanded, an error that grew instead of shrinking means the sign is
    // wrong: flip it once. A second flip means something else is off, so stop moving that axis.
    private void CheckSign(Axis axis, float magnitude)
    {
        if (axis.RunCommanded < SignCheckMinCommandDeg)
            return;
        if (magnitude - axis.RunStartError < SignCheckGrowthRatio * axis.RunCommanded)
            return;

        if (axis.Flips == 0)
        {
            axis.Sign = -axis.Sign;
            axis.Flips++;
            Debug.LogWarning($"[HeadFollow] {axis.Name} error grew from {axis.RunStartError:0.0} to {magnitude:0.0} after {axis.RunCommanded:0.0} deg of correction; flipping its sign to {axis.Sign:+0;-0}");
        }
        else
        {
            axis.Disabled = true;
            Debug.LogWarning($"[HeadFollow] {axis.Name} still diverging after a sign flip; this axis is disabled until the next session");
        }
        axis.RunStartError = magnitude;
        axis.RunCommanded = 0f;
    }

    private void ResetAxes()
    {
        foreach (var axis in new[] { _yaw, _pitch })
        {
            axis.Correcting = false;
            axis.RunCommanded = 0f;
        }
    }

    private void DriveHome()
    {
        if (!_robotReady)
            return;
        Nuwa.setMotorPositionInDegree(_yaw.Motor, _yaw.Home, MotorSpeedDegPerSec);
        Nuwa.setMotorPositionInDegree(_pitch.Motor, _pitch.Home, MotorSpeedDegPerSec);
    }

    private static float ReadMotor(Axis axis)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return Nuwa.getMotorPresentPossitionInDegree(axis.Motor);
#else
        return axis.Actual;
#endif
    }

    // Motor calls go through the robot service, which connects asynchronously after launch.
    private void UpdateRobotReady()
    {
        if (_robotReady || Time.unscaledTime < _nextReadyCheck)
            return;
        _nextReadyCheck = Time.unscaledTime + 1f;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var plugin = new AndroidJavaClass("com.u2a.sdk.NuwaPlugin"))
            using (var robot = plugin.GetStatic<AndroidJavaObject>("mRobot"))
                _robotReady = robot != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[HeadFollow] robot readiness check failed: " + e.Message);
        }
#else
        _robotReady = true;
#endif
        if (_robotReady)
            Debug.Log("[HeadFollow] robot service ready");
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && _engaged)
            DriveHome();
    }

    private void OnApplicationQuit()
    {
        DriveHome();
    }
}
