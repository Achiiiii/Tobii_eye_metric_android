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

using System.Collections;
using UnityEngine;
using Tobii.StreamEngine;
using System.Threading.Tasks;
using System;

namespace Tobii
{
    public class ReferenceBool
    {
        public bool Value { get; set; }
        public ReferenceBool(bool value)
        {
            this.Value = value;
        }

        public static implicit operator ReferenceBool(bool val)
        {
            return new ReferenceBool(val);
        }

        public static bool operator ==(ReferenceBool rb1, ReferenceBool rb2) => rb1.Value == rb2.Value;

        public static bool operator !=(ReferenceBool rb1, ReferenceBool rb2) => rb1.Value != rb2.Value;

        public override bool Equals(object other) => Value == ((ReferenceBool)other).Value;

        public override int GetHashCode() => base.GetHashCode();
    }

    public class StreamEngineCalibration : MonoBehaviour
    {
        /// <summary>
        /// Provides access to gaze data etc from StreamEngineDevice. 
        /// </summary>
        [SerializeField]
        private StreamEngineDevice streamEngineDevice;

        private bool IsCalibrationStarted = false;

        public bool StopCalibration()
        {
            Debug.Log(string.Format("StopCalibration"));
            if (streamEngineDevice.DeviceContext == IntPtr.Zero) return true;

            var error = ConfigInterop.tobii_calibration_stop(streamEngineDevice.DeviceContext);
            if (error != tobii_error_t.TOBII_ERROR_NO_ERROR)
            {
                Debug.Log(string.Format("Error stopping calibration: {0}", error));
                return false;
            }

            IsCalibrationStarted = false;
            return true;
        }

        public IEnumerator StartCalibrationRoutine(ReferenceBool success)
        {
            Debug.Log(string.Format("StartCalibrationRoutine"));

            var task = Task.Run(() => StartCalibrationTask());
            success.Value = false;

            while (task.IsCompleted == false)
            {
                yield return null;
            }

            success.Value = task.Result;
        }

        private Task<bool> StartCalibrationTask()
        {
            Debug.Log(string.Format("StartCalibrationTask"));

            if (streamEngineDevice.DeviceContext == IntPtr.Zero)
                return Task.FromResult(false);

            // Both eyes even in single-eye rounds. The webcam processor refuses to track one eye
            // (tobii_set_enabled_eye: TOBII_ERROR_NOT_SUPPORTED) but accepts a one-eye calibration,
            // and that mismatch made single-eye gaze worse on the robot: raw error 83 -> 99 px with
            // the right eye and 72 -> 105 px with the left.
            var error = ConfigInterop.tobii_calibration_start(streamEngineDevice.DeviceContext, tobii_enabled_eye_t.TOBII_ENABLED_EYE_BOTH);
            if (error != tobii_error_t.TOBII_ERROR_NO_ERROR)
            {
                Debug.Log(string.Format("Error starting calibration: {0}", error));
                return Task.FromResult(false);
            }

            IsCalibrationStarted = true;
            return Task.FromResult(true);
        }

        public IEnumerator ClearCalibrationRoutine(ReferenceBool success)
        {
            Debug.Log(string.Format("ClearCalibrationRoutine"));

            var task = Task.Run(() => ClearCalibrationTask());
            success.Value = false;

            while (task.IsCompleted == false)
            {
                yield return null;
            }

            success.Value = task.Result;
        }
        private Task<bool> ClearCalibrationTask()
        {
            Debug.Log(string.Format("ClearCalibrationTask"));

            if (streamEngineDevice.DeviceContext == IntPtr.Zero) return Task.FromResult(false);

            var error = ConfigInterop.tobii_calibration_clear(streamEngineDevice.DeviceContext);

            if (error != tobii_error_t.TOBII_ERROR_NO_ERROR)
            {
                Debug.Log(string.Format("Error clearing calibration: {0}", error));
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public IEnumerator CollectCalibrationDataRoutine(Vector2 stimuliPositionShown, ReferenceBool success)
        {
            Debug.Log(string.Format("CollectCalibrationDataRoutine" + stimuliPositionShown));

            yield return null;

            var task = Task.Run(() => CollectCalibrationDataTask(stimuliPositionShown));
            success.Value = false;

            while (task.IsCompleted == false)
            {
                yield return null;
            }

            success.Value = task.Result;
        }
        // Experiment (CalibrationExperimentProbe): collect each point this many times, and report the
        // target shifted by this much (viewport units) to test whether the calibration is used at all.
        public static int CollectRepeats = 1;
        public static Vector2 TargetOffset = Vector2.zero;

        private Task<bool> CollectCalibrationDataTask(Vector2 stimuliPositionShown)
        {
            Debug.Log(string.Format("CollectCalibrationDataTask"));
            if (streamEngineDevice.DeviceContext == IntPtr.Zero) return Task.FromResult(false);

            Vector2 target = stimuliPositionShown + TargetOffset;
            for (int i = 0; i < Mathf.Max(1, CollectRepeats); i++)
            {
                var error = ConfigInterop.tobii_calibration_collect_data_2d(streamEngineDevice.DeviceContext, target.x, 1 - target.y);
                if (error != tobii_error_t.TOBII_ERROR_NO_ERROR)
                {
                    Debug.Log(string.Format("Error calibration collect2d: {0}", error));
                    return Task.FromResult(false);
                }
            }

            return Task.FromResult(true);
        }

        public IEnumerator ComputeAndApplyCalibrationRoutine(ReferenceBool success)
        {
            Debug.Log(string.Format("ComputeAndApplyCalibrationRoutine"));
            var task = Task.Run(() => ComputeAndApplyCalibrationTask());
            success.Value = false;

            while (task.IsCompleted == false)
            {
                yield return null;
            }

            success.Value = task.Result;
        }
        private Task<bool> ComputeAndApplyCalibrationTask()
        {
            Debug.Log(string.Format("ComputeAndApplyCalibrationTask"));
            if (streamEngineDevice.DeviceContext == IntPtr.Zero) return Task.FromResult(false);

            // The webcam processor returns TOBII_ERROR_INTERNAL from every compute_and_apply, yet the
            // runtime log (logcat tag TobiiRT) shows a new calibration id each time and the id read
            // back changes: the calibration is applied. So success is judged by the id changing.
            ConfigInterop.tobii_get_calibration_id(streamEngineDevice.DeviceContext, out uint idBefore);
            var error = ConfigInterop.tobii_calibration_compute_and_apply(streamEngineDevice.DeviceContext);
            if (error == tobii_error_t.TOBII_ERROR_NO_ERROR)
                return Task.FromResult(true);

            var idResult = ConfigInterop.tobii_get_calibration_id(streamEngineDevice.DeviceContext, out uint idAfter);
            bool applied = idResult == tobii_error_t.TOBII_ERROR_NO_ERROR && idAfter != 0 && idAfter != idBefore;
            Debug.Log(applied
                ? string.Format("Calibration compute and apply returned {0}, but calibration id {1} -> {2}: applied", error, idBefore, idAfter)
                : string.Format("Error calibration compute and apply: {0} (calibration id {1} -> {2})", error, idBefore, idAfter));
            return Task.FromResult(applied);
        }

        public IEnumerator StopCalibrationRoutine(ReferenceBool success)
        {
            Debug.Log(string.Format("StopCalibrationRoutine"));
            var task = Task.Run(() => StopCalibrationTask());
            success.Value = false;

            while (task.IsCompleted == false)
            {
                yield return null;
            }

            success.Value = task.Result;
        }
        private Task<bool> StopCalibrationTask()
        {
            Debug.Log(string.Format("StopCalibrationTask"));
            if (streamEngineDevice.DeviceContext == IntPtr.Zero) return Task.FromResult(true);

            if (IsCalibrationStarted == false) return Task.FromResult(true);

            var error = ConfigInterop.tobii_calibration_stop(streamEngineDevice.DeviceContext);
            if (error != tobii_error_t.TOBII_ERROR_NO_ERROR)
            {
                Debug.Log(string.Format("Error stopping calibration: {0}", error));
                return Task.FromResult(false);
            }

            IsCalibrationStarted = false;
            return Task.FromResult(true);
        }
    }
}

