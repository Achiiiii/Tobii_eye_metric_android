using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AOT;
using Tobii.StreamEngine;
using UnityEngine;

namespace Tobii
{
    // Logs what the processor made of each calibration point ([CALREPORT]): where each eye's gaze
    // mapped against the point shown, and whether the point was used. Diagnostic only - the
    // calibration barely changes the gaze on this processor - but it shows a failed point or a bad
    // eye, and gives the vendor concrete numbers.
    // ConfigInterop's retrieve/parse wrappers pass lambdas as native callbacks, which IL2CPP cannot
    // marshal, so this uses its own imports with static callbacks.
    public static class CalibrationReport
    {
        [DllImport(Interop.stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_calibration_retrieve")]
        private static extern tobii_error_t Retrieve(IntPtr device, tobii_data_receiver_t receiver, IntPtr userData);

        [DllImport(Interop.stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_calibration_parse")]
        private static extern tobii_error_t Parse(IntPtr api, IntPtr data, IntPtr size, ConfigInterop.tobii_calibration_point_data_receiver_t receiver, IntPtr userData);

        private static readonly tobii_data_receiver_t s_dataReceiver = OnData;
        private static readonly ConfigInterop.tobii_calibration_point_data_receiver_t s_pointReceiver = OnPoint;
        // Only touched inside Log, which runs one at a time on the task below.
        private static byte[] s_blob;
        private static readonly List<tobii_calibration_point_data_t> s_points = new List<tobii_calibration_point_data_t>();
        private static readonly object s_lock = new object();

        [MonoPInvokeCallback(typeof(tobii_data_receiver_t))]
        private static void OnData(IntPtr data, IntPtr size, IntPtr userData)
        {
            int length = size.ToInt32();
            s_blob = new byte[length];
            if (length > 0)
                Marshal.Copy(data, s_blob, 0, length);
        }

        [MonoPInvokeCallback(typeof(ConfigInterop.tobii_calibration_point_data_receiver_t))]
        private static void OnPoint(ref tobii_calibration_point_data_t point, IntPtr userData)
        {
            s_points.Add(point);
        }

        // screenPx: the display size in pixels, to express the errors in pixels.
        public static void LogAsync(StreamEngineDevice device, Vector2 screenPx)
        {
            if (device == null)
                return;
            IntPtr deviceContext = device.DeviceContext;
            IntPtr apiContext = device.ApiContext;
            if (deviceContext == IntPtr.Zero || apiContext == IntPtr.Zero)
                return;
            Task.Run(() =>
            {
                try
                {
                    lock (s_lock)
                        Log(deviceContext, apiContext, screenPx);
                }
                catch (Exception e)
                {
                    Debug.LogError("[CALREPORT] " + e);
                }
            });
        }

        private static void Log(IntPtr deviceContext, IntPtr apiContext, Vector2 screenPx)
        {
            s_blob = null;
            var retrieved = Retrieve(deviceContext, s_dataReceiver, IntPtr.Zero);
            if (retrieved != tobii_error_t.TOBII_ERROR_NO_ERROR || s_blob == null || s_blob.Length == 0)
            {
                Debug.Log($"[CALREPORT] retrieve: {retrieved}, {(s_blob != null ? s_blob.Length : 0)} bytes");
                return;
            }

            s_points.Clear();
            IntPtr buffer = Marshal.AllocHGlobal(s_blob.Length);
            tobii_error_t parsed;
            try
            {
                Marshal.Copy(s_blob, 0, buffer, s_blob.Length);
                parsed = Parse(apiContext, buffer, new IntPtr(s_blob.Length), s_pointReceiver, IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            Debug.Log($"[CALREPORT] {s_blob.Length} bytes, parse: {parsed}, {s_points.Count} point(s)");

            float leftSum = 0f, rightSum = 0f;
            int leftCount = 0, rightCount = 0;
            foreach (var p in s_points)
            {
                var target = new Vector2(p.point_xy.x, p.point_xy.y);
                string left = Describe(p.left_status, p.left_mapping_xy, target, screenPx, ref leftSum, ref leftCount);
                string right = Describe(p.right_status, p.right_mapping_xy, target, screenPx, ref rightSum, ref rightCount);
                Debug.Log($"[CALREPORT] point ({target.x:0.00},{target.y:0.00}) left {left} | right {right}");
            }
            Debug.Log($"[CALREPORT] mean mapping error: left {(leftCount > 0 ? (leftSum / leftCount).ToString("0") + " px" : "-")}, right {(rightCount > 0 ? (rightSum / rightCount).ToString("0") + " px" : "-")}");
        }

        private static string Describe(tobii_calibration_point_status_t status, TobiiVector2 mapping, Vector2 target, Vector2 screenPx, ref float sum, ref int count)
        {
            string state = status == tobii_calibration_point_status_t.TOBII_CALIBRATION_POINT_STATUS_VALID_AND_USED_IN_CALIBRATION ? "used"
                : status == tobii_calibration_point_status_t.TOBII_CALIBRATION_POINT_STATUS_VALID_BUT_NOT_USED_IN_CALIBRATION ? "not used"
                : "failed";
            if (status == tobii_calibration_point_status_t.TOBII_CALIBRATION_POINT_STATUS_FAILED_OR_INVALID)
                return state;
            var offset = Vector2.Scale(new Vector2(mapping.x, mapping.y) - target, screenPx);
            sum += offset.magnitude;
            count++;
            return $"{state} {offset.x:+0;-0},{offset.y:+0;-0} px";
        }
    }
}
