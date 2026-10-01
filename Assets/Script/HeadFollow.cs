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
// (in BetweenTrials mode) it holds only while a gaze selection is filling, unless the drift is
// large - reading the symbol survives a small turn of the screen, a half-filled dwell does not.
public class HeadFollow : MonoBehaviour
{
    public enum FollowMode
    {
        Off,
        BetweenTrials,
        Continuous
    }

    private const float CommandHz = 8f;
    // Each move covers most of the error in one go, then waits for the pose to settle (below).
    private const float Gain = 0.8f;
    private const float MaxStepDeg = 10f;
    // Fast enough that a correction started after an answer finishes inside the answer gap.
    private const float MotorSpeedDegPerSec = 60f;
    // Hysteresis: start correcting beyond EnterDeadzone, stop once inside ExitDeadzone. With a
    // 5 degree deadzone the user sat 3-5 degrees off the calibrated pose for 10 s at a time and
    // gaze visibly drifted; the pose is steady to about 0.3 degrees when still, so 3 is safe.
    private const float EnterDeadzoneDeg = 3f;
    private const float ExitDeadzoneDeg = 1.5f;
    // Even mid-selection, drift this large is corrected at once: gaze is already off by then.
    private const float CatchUpErrorDeg = 12f;
    // Hardware travel measured over 615 read-backs on 2026-09-30: neck_z never went past -30.6
    // when told -35, and neck_y (negative looks up) never past -14.8 when told -16 or -20.
    // Commanding beyond these left an axis "moving" forever.
    private const float YawLimitDeg = 30f;
    private const float PitchUpLimitDeg = -14.5f;
    private const float PitchDownLimitDeg = 12f;
    // How far beyond the head's tilt range the user may sit before the setup is flagged.
    private const float EyeLevelMarginDeg = 3f;
    // The accepted range of EyeElevationDeg: +17.5 to -15 degrees.
    public const float EyeLevelHighDeg = -PitchUpLimitDeg + EyeLevelMarginDeg;
    public const float EyeLevelLowDeg = -PitchDownLimitDeg - EyeLevelMarginDeg;
    // The motors ignored moves of 1.4 and 2.5 degrees on the device, so no move is smaller than
    // this (overshooting by at most ExitDeadzone).
    private const float MinMoveDeg = 3f;
    // A move that achieved less than this share of its travel did not happen: a hardware stop,
    // or a move the motor ignored. Either way, stop pushing that way for a while - but only for a
    // while: remembering it as a limit locked the head off to one side on the device.
    private const float StallRatio = 0.3f;
    private const float StallBackoffSeconds = 3f;
    private const float HomeYawDeg = 0f;
    private const float HomePitchDeg = 0f;
    private const float PoseSmoothingSeconds = 0.12f;
    // Head pose trails the motor by the camera latency plus ~80 ms of inference plus smoothing.
    // Stepping again before it catches up overshot 14 degrees of error by 6 on the device, so
    // after each move an axis waits for the motor to arrive and this long more.
    private const float PoseLagSeconds = 0.4f;
    private const float PoseStaleSeconds = 0.5f;
    // After the face has been gone this long, go back to where it was last seen centred.
    private const float LostRecoverSeconds = 1.5f;
    // Shorter than ButtonTrigger's 1 s selection cooldown, so the head is still again before the
    // next answer can start filling.
    private const float PostAnswerWindowSeconds = 0.8f;
    private const float LogIntervalSeconds = 1f;

    public FollowMode Mode { get; set; } = FollowMode.BetweenTrials;
    // True from the head distance check until the result page: a session is under way.
    public bool Engaged => _engaged;
    public string Status { get; private set; } = "idle";
    // How far the user's head is from where it should be, in degrees (yaw, pitch).
    public Vector2 Error => _hasPose ? _poseAngles - _targetAngles : Vector2.zero;
    public bool HasFace => _hasPose && Time.unscaledTime - _lastPoseTime <= PoseStaleSeconds;
    // Head distance from the camera now and during calibration, in metres (0 until calibrated).
    public float Distance => _poseDepth;
    public float CalibrationDistance => _calibrationDepth;
    // True while a move is under way or its effect has not yet reached the head pose.
    public bool IsMoving => Time.unscaledTime < _yaw.SettleUntil || Time.unscaledTime < _pitch.SettleUntil;
    public bool Calibrated => _calibrationDepth > 0f;

    // Whether the user sits outside what the robot's head can tilt to: +1 too high for it (it
    // would have to look up past its stop), -1 too low, 0 fine. It uses the user's elevation from
    // the robot's base - direction in the image minus the head's tilt - so it holds before the
    // head has moved and in any follow mode. On the robot, sitting low gave 12.6 degrees,
    // sitting high 17-19, and the earlier high-seated runs 21-22.
    public int EyeLevelMismatch
    {
        get
        {
            if (!HasFace)
                return 0;
            float elevation = EyeElevationDeg;
            if (elevation > EyeLevelHighDeg)
                return 1;
            if (elevation < EyeLevelLowDeg)
                return -1;
            return 0;
        }
    }

    // Degrees the user's head sits above the robot's level line of sight (neck_y is negative
    // looking up).
    public float EyeElevationDeg => _poseAngles.y - _pitch.Actual;
    // Where the user is compared with calibration, independent of where the head points: the
    // user's direction from the robot's base (motor yaw plus direction within the image), in
    // degrees, positive toward the user's right; and the change in distance, in metres.
    public float LateralOffsetDeg => (_yaw.Actual + _poseAngles.x) - (_calibrationMotor.x + _targetAngles.x);
    public float DistanceOffset => _poseDepth - _calibrationDepth;

    private StreamEngineDevice _device;
    private GazeCalibrationManager _calibration;
    private MetricTest _metricTest;
    private GameObject _resultPage;

    private bool _engaged;
    private bool _calibrating;
    private bool _catchingUp;
    private bool _recovered;
    private bool _robotReady;
    private float _nextReadyCheck;
    private float _nextCommand;
    private float _nextLog;
    private float _answerWindowUntil;

    private bool _hasPose;
    private float _lastPoseTime;
    private Vector2 _poseAngles;  // user's head direction from the camera: (yaw, pitch) degrees
    private float _poseDepth;     // metres
    private float _calibrationDepthSum;
    private float _calibrationDepth;
    private Vector2 _calibrationMotor;
    // Set by PositionGuide while it walks a user back to where they were calibrated.
    private bool _holdAtCalibration;
    private float _nextMotorRead;
    private Vector3 _headRotationDeg;

    // Where the user's head sat during calibration; before that, straight ahead of the camera.
    private Vector2 _targetAngles;
    private Vector2 _calibrationSum;
    private int _calibrationSamples;
    // Motor angles the last time the user was seen near the target; where to look when lost.
    private Vector2 _lastGoodMotor = new Vector2(HomeYawDeg, HomePitchDeg);

    // Directions confirmed on the robot on 2026-09-30: with the user 14.9 degrees to +x, driving
    // neck_z +20 brought them back to 1.5 (and -15 was cleared by driving it negative), so yaw
    // follows +x directly. Pitch brought +18.8 down to 4 with negative neck_y (+ looks down).
    // An automatic sign check used to flip these when the error grew during a correction, but it
    // cannot tell a wrong sign from a user who moves faster than the motor; it flipped a correct
    // yaw on the device and drove the robot away, so the signs are fixed.
    private readonly Axis _yaw = new Axis("yaw", Nuwa.NuwaMotorType.neck_z, HomeYawDeg, -YawLimitDeg, YawLimitDeg, +1f);
    private readonly Axis _pitch = new Axis("pitch", Nuwa.NuwaMotorType.neck_y, HomePitchDeg, PitchUpLimitDeg, PitchDownLimitDeg, -1f);

    private class Axis
    {
        public readonly string Name;
        public readonly Nuwa.NuwaMotorType Motor;
        public readonly float Home;
        public readonly float Min;
        public readonly float Max;
        public readonly float Sign;
        public bool Correcting;
        public float SettleUntil;
        public bool Commanded;
        public float CommandFrom;
        public float CommandTo;
        // After a move that did not happen: which way not to push (+1/-1) and until when.
        public float BlockedDirection;
        public float BlockedUntil;
        public float Actual;

        public Axis(string name, Nuwa.NuwaMotorType motor, float home, float min, float max, float sign)
        {
            Name = name;
            Motor = motor;
            Home = home;
            Min = min;
            Max = max;
            Sign = sign;
            Actual = home;
        }

        public void EndRun()
        {
            Correcting = false;
            Commanded = false;
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
        {
            _device.OnHeadPosePosition.AddListener(OnHeadPosition);
            _device.OnHeadPoseRotation.AddListener(OnHeadRotation);
        }
        _calibration.CalibrationStarted += OnCalibrationStarted;
        _calibration.CalibrationEnded += OnCalibrationEnded;
        _calibration.CalibrationFailed += OnCalibrationFailed;
        _metricTest.Answered += OnAnswered;
    }

    private void OnDisable()
    {
        if (_device != null)
        {
            _device.OnHeadPosePosition.RemoveListener(OnHeadPosition);
            _device.OnHeadPoseRotation.RemoveListener(OnHeadRotation);
        }
        _calibration.CalibrationStarted -= OnCalibrationStarted;
        _calibration.CalibrationEnded -= OnCalibrationEnded;
        _calibration.CalibrationFailed -= OnCalibrationFailed;
        _metricTest.Answered -= OnAnswered;
    }

    // Called once the questionnaire is done (or a recalibration starts) and the head distance
    // check begins.
    public void Engage()
    {
        _engaged = true;
        _calibrationDepth = 0f;
        _holdAtCalibration = false;
        _catchingUp = false;
        _recovered = false;
        _targetAngles = Vector2.zero;
        _lastGoodMotor = new Vector2(HomeYawDeg, HomePitchDeg);
        EndRuns();
        Debug.Log("[HeadFollow] engaged, mode=" + Mode);
    }

    // Called when going back home or restarting a session; turns the head back to its rest pose.
    public void ReturnHome()
    {
        _engaged = false;
        _calibrating = false;
        _calibrationDepth = 0f;
        _holdAtCalibration = false;
        _catchingUp = false;
        _targetAngles = Vector2.zero;
        EndRuns();
        Status = "home";
        DriveTo(HomeYawDeg, HomePitchDeg);
        Debug.Log("[HeadFollow] returning home");
    }

    // For A/B comparisons from the debug overlay. Switching to Off leaves the head where it is:
    // driving it home mid-session would itself move the user away from the calibrated pose.
    // Pick the mode on the home page to compare whole sessions.
    public FollowMode CycleMode()
    {
        Mode = (FollowMode)(((int)Mode + 1) % Enum.GetValues(typeof(FollowMode)).Length);
        _catchingUp = false;
        EndRuns();
        Debug.Log("[HeadFollow] mode=" + Mode);
        return Mode;
    }

    // While true, stop following and face the direction the head had during calibration, so that
    // "straight in front of the screen" is where the user sat then.
    public void SetHoldAtCalibration(bool hold)
    {
        if (_holdAtCalibration == hold)
            return;
        _holdAtCalibration = hold;
        _catchingUp = false;
        EndRuns();
        if (hold)
            DriveTo(_calibrationMotor.x, _calibrationMotor.y);
        Debug.Log(hold
            ? $"[HeadFollow] facing the calibrated position {_calibrationMotor.x:0.0},{_calibrationMotor.y:0.0} while the user is guided back"
            : "[HeadFollow] following again");
    }

    // Logged to check how obliquely the camera sees the face after the head has turned to follow.
    private void OnHeadRotation(Vector3 radians)
    {
        _headRotationDeg = radians * Mathf.Rad2Deg;
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
            _poseDepth = depth;
        }
        else
        {
            float blend = 1f - Mathf.Exp(-(now - _lastPoseTime) / PoseSmoothingSeconds);
            _poseAngles = Vector2.Lerp(_poseAngles, angles, blend);
            _poseDepth = Mathf.Lerp(_poseDepth, depth, blend);
        }
        _hasPose = true;
        _lastPoseTime = now;
        _recovered = false;

        if (_calibrating)
        {
            _calibrationSum += angles;
            _calibrationDepthSum += depth;
            _calibrationSamples++;
        }
    }

    private void OnCalibrationStarted()
    {
        _calibrating = true;
        _calibrationSum = Vector2.zero;
        _calibrationDepthSum = 0f;
        _calibrationSamples = 0;
        EndRuns();
    }

    private void OnCalibrationEnded()
    {
        _calibrating = false;
        if (_calibrationSamples == 0)
            return;
        _targetAngles = _calibrationSum / _calibrationSamples;
        _calibrationDepth = _calibrationDepthSum / _calibrationSamples;
        _lastGoodMotor = new Vector2(ReadMotor(_yaw), ReadMotor(_pitch));
        _calibrationMotor = _lastGoodMotor;
        _yaw.Actual = _calibrationMotor.x;
        _pitch.Actual = _calibrationMotor.y;
        Debug.Log($"[HeadFollow] target = calibration pose yaw {_targetAngles.x:0.0} pitch {_targetAngles.y:0.0} ({_calibrationSamples} samples), motor {_lastGoodMotor.x:0.0},{_lastGoodMotor.y:0.0}");
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

        // Keep the read-back current even while not moving: LateralOffsetDeg depends on it.
        if (_engaged && _robotReady && now >= _nextMotorRead)
        {
            _nextMotorRead = now + 0.25f;
            _yaw.Actual = ReadMotor(_yaw);
            _pitch.Actual = ReadMotor(_pitch);
        }

        bool allowed = MovementAllowed(out string reason);
        Status = reason;
        if (allowed && now >= _nextCommand)
        {
            _nextCommand = now + 1f / CommandHz;
            Step();
        }

        if (_engaged && now >= _nextLog)
        {
            _nextLog = now + LogIntervalSeconds;
            Vector2 error = _poseAngles - _targetAngles;
            Debug.Log($"[HEAD] mode={Mode} state={Status} pose={(_hasPose ? $"{_poseAngles.x:0.0},{_poseAngles.y:0.0}" : "none")} "
                      + $"target={_targetAngles.x:0.0},{_targetAngles.y:0.0} err={error.x:0.0},{error.y:0.0} "
                      + $"motor={_yaw.Actual:0.0},{_pitch.Actual:0.0} "
                      + $"dist={_poseDepth * 100f:0}cm cal={_calibrationDepth * 100f:0}cm "
                      + $"lateral={(Calibrated ? LateralOffsetDeg : 0f):0.0} rot={_headRotationDeg.x:0},{_headRotationDeg.y:0},{_headRotationDeg.z:0}");
        }
    }

    private bool MovementAllowed(out string reason)
    {
        if (Mode == FollowMode.Off) { reason = "off"; return false; }
        if (!_engaged) { reason = "idle"; return false; }
        if (!_robotReady) { reason = "waiting for robot"; return false; }
        if (_holdAtCalibration) { reason = "guiding: facing calibrated position"; return false; }
        if (_calibrating) { reason = "hold: calibrating"; return false; }

        float sinceFace = Time.unscaledTime - _lastPoseTime;
        if (!_hasPose || sinceFace > PoseStaleSeconds)
        {
            if (_hasPose && sinceFace > LostRecoverSeconds && !_recovered)
            {
                // The face left the image; look back to where it was last seen centred.
                _recovered = true;
                EndRuns();
                DriveTo(_lastGoodMotor.x, _lastGoodMotor.y);
                Debug.Log($"[HeadFollow] face lost for {sinceFace:0.0} s, returning to {_lastGoodMotor.x:0.0},{_lastGoodMotor.y:0.0}");
            }
            reason = _recovered ? "lost: back to last good" : "hold: no face";
            return false;
        }

        // Holding for the whole trial made drift under 12 degrees wait for the next answer, 2-4 s
        // away, which read as lag. Only a selection in progress needs the screen to stay put.
        bool inTrial = _metricTest.gameObject.activeInHierarchy;
        if (inTrial && Mode == FollowMode.BetweenTrials && Time.unscaledTime > _answerWindowUntil
            && GazeDwellIndicator.IsDwelling)
        {
            Vector2 error = Error;
            if (!_catchingUp && Mathf.Max(Mathf.Abs(error.x), Mathf.Abs(error.y)) > CatchUpErrorDeg)
                _catchingUp = true;
            if (!_catchingUp)
            {
                reason = "hold: selecting";
                return false;
            }
            reason = "tracking: catch-up";
            return true;
        }
        reason = "tracking";
        return true;
    }

    private void Step()
    {
        _yaw.Actual = ReadMotor(_yaw);
        _pitch.Actual = ReadMotor(_pitch);
        Vector2 error = Error;

        // Both axes before calibration too: a user sitting too high or low should be brought to
        // the middle of the image before the calibration dots appear, so the eyes are well framed
        // for the samples that matter most. (Nothing moves while the dots are up.)
        float yawTarget = StepAxis(_yaw, error.x);
        float pitchTarget = StepAxis(_pitch, error.y);

        if (!float.IsNaN(yawTarget))
            Nuwa.setMotorPositionInDegree(_yaw.Motor, yawTarget, MotorSpeedDegPerSec);
        if (!float.IsNaN(pitchTarget))
            Nuwa.setMotorPositionInDegree(_pitch.Motor, pitchTarget, MotorSpeedDegPerSec);

        bool settled = !_yaw.Correcting && !_pitch.Correcting;
        if (settled)
        {
            _catchingUp = false;
            _lastGoodMotor = new Vector2(_yaw.Actual, _pitch.Actual);
        }
    }

    // Returns the new motor target for this axis, or NaN when it should stay where it is.
    private float StepAxis(Axis axis, float error)
    {
        // Still moving, or the pose has not caught up with the last move yet.
        if (Time.unscaledTime < axis.SettleUntil)
            return float.NaN;

        if (axis.Commanded)
        {
            axis.Commanded = false;
            float wanted = axis.CommandTo - axis.CommandFrom;
            float achieved = axis.Actual - axis.CommandFrom;
            bool stalled = Mathf.Abs(wanted) >= 1f && achieved * Mathf.Sign(wanted) < StallRatio * Mathf.Abs(wanted);
            Debug.Log($"[MOTOR] {axis.Name} {axis.CommandFrom:0.0} -> asked {axis.CommandTo:0.0}, reached {axis.Actual:0.0}{(stalled ? " (did not move)" : "")}");
            if (stalled)
            {
                axis.BlockedDirection = Mathf.Sign(wanted);
                axis.BlockedUntil = Time.unscaledTime + StallBackoffSeconds;
                axis.EndRun();
                return float.NaN;
            }
        }

        float magnitude = Mathf.Abs(error);
        if (!axis.Correcting)
        {
            if (magnitude < EnterDeadzoneDeg)
                return float.NaN;
            axis.Correcting = true;
        }
        else if (magnitude < ExitDeadzoneDeg)
        {
            axis.EndRun();
            return float.NaN;
        }

        float step = Mathf.Clamp(Gain * error, -MaxStepDeg, MaxStepDeg);
        if (Mathf.Abs(step) < MinMoveDeg)
            step = Mathf.Sign(error) * MinMoveDeg;
        float direction = Mathf.Sign(axis.Sign * step);
        if (direction == axis.BlockedDirection && Time.unscaledTime < axis.BlockedUntil)
        {
            axis.EndRun();
            return float.NaN;
        }
        float target = Mathf.Clamp(axis.Actual + axis.Sign * step, axis.Min, axis.Max);
        float travel = Mathf.Abs(target - axis.Actual);
        if (travel < 0.5f)
        {
            // Pinned at a limit: nothing more this axis can do.
            axis.EndRun();
            return float.NaN;
        }
        axis.SettleUntil = Time.unscaledTime + travel / MotorSpeedDegPerSec + PoseLagSeconds;
        axis.Commanded = true;
        axis.CommandFrom = axis.Actual;
        axis.CommandTo = target;
        return target;
    }

    private void EndRuns()
    {
        _yaw.EndRun();
        _pitch.EndRun();
    }

    private void DriveTo(float yaw, float pitch)
    {
        if (!_robotReady)
            return;
        // A long move (going home, looking back for a lost face): hold off corrections until the
        // motors have arrived and the pose reflects it.
        foreach (var axis in new[] { _yaw, _pitch })
        {
            float travel = Mathf.Abs((axis == _yaw ? yaw : pitch) - axis.Actual);
            axis.SettleUntil = Time.unscaledTime + travel / MotorSpeedDegPerSec + PoseLagSeconds;
        }
        Nuwa.setMotorPositionInDegree(_yaw.Motor, Mathf.Clamp(yaw, _yaw.Min, _yaw.Max), MotorSpeedDegPerSec);
        Nuwa.setMotorPositionInDegree(_pitch.Motor, Mathf.Clamp(pitch, _pitch.Min, _pitch.Max), MotorSpeedDegPerSec);
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
            DriveTo(HomeYawDeg, HomePitchDeg);
    }

    private void OnApplicationQuit()
    {
        DriveTo(HomeYawDeg, HomePitchDeg);
    }
}
