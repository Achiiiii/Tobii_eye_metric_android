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

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Tobii.StreamEngine
{
    public static class WearableInterop
    {
        public const string stream_engine_dll = "tobii_stream_engine";

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "tobii_wearable_consumer_data_subscribe")]
        public static extern tobii_error_t tobii_wearable_consumer_data_subscribe(IntPtr device, tobii_wearable_consumer_data_callback_t callback, IntPtr user_data = default(IntPtr));

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "tobii_wearable_consumer_data_unsubscribe")]
        public static extern tobii_error_t tobii_wearable_consumer_data_unsubscribe(IntPtr device);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "tobii_wearable_advanced_data_subscribe")]
        public static extern tobii_error_t tobii_wearable_advanced_data_subscribe(IntPtr device, tobii_wearable_advanced_data_callback_t callback, IntPtr user_data = default(IntPtr));

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "tobii_wearable_advanced_data_unsubscribe")]
        public static extern tobii_error_t tobii_wearable_advanced_data_unsubscribe(IntPtr device);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_get_lens_configuration")]
        public static extern tobii_error_t tobii_get_lens_configuration(IntPtr device, out tobii_lens_configuration_t configuration);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_set_lens_configuration")]
        public static extern tobii_error_t tobii_set_lens_configuration(IntPtr device, ref tobii_lens_configuration_t configuration);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_lens_configuration_writable")]
        public static extern tobii_error_t tobii_lens_configuration_writable(IntPtr device, out tobii_lens_configuration_writable_t writable);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_get_lens_configuration_extended")]
        public static extern tobii_error_t tobii_get_lens_configuration_extended(IntPtr device, out tobii_lens_configuration_extended_t configuration);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_set_lens_configuration_extended")]
        public static extern tobii_error_t tobii_set_lens_configuration_extended(IntPtr device, ref tobii_lens_configuration_extended_t configuration);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_lens_configuration_extended_writable")]
        public static extern tobii_error_t tobii_lens_configuration_extended_writable(IntPtr device, out tobii_lens_configuration_extended_writable_t writable);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "tobii_wearable_foveated_gaze_subscribe")]
        public static extern tobii_error_t tobii_wearable_foveated_gaze_subscribe(IntPtr device, tobii_wearable_foveated_gaze_callback_t callback, IntPtr user_data = default(IntPtr));

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "tobii_wearable_foveated_gaze_unsubscribe")]
        public static extern tobii_error_t tobii_wearable_foveated_gaze_unsubscribe(IntPtr device);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tobii_wearable_diagnostics_image_subscribe")]
        public static extern tobii_error_t tobii_wearable_diagnostics_image_subscribe(IntPtr device, tobii_wearable_diagnostics_image_callback_t callback, IntPtr user_data = default(IntPtr));

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "tobii_wearable_diagnostics_image_unsubscribe")]
        public static extern tobii_error_t tobii_wearable_diagnostics_image_unsubscribe(IntPtr device);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "tobii_wearable_limited_image_subscribe")]
        public static extern tobii_error_t tobii_wearable_limited_image_subscribe(IntPtr device, tobii_wearable_limited_image_callback_t callback, IntPtr user_data = default(IntPtr));

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "tobii_wearable_limited_image_unsubscribe")]
        public static extern tobii_error_t tobii_wearable_limited_image_unsubscribe(IntPtr device);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_wearable_image_subscribe")]
        public static extern tobii_error_t tobii_wearable_image_subscribe(IntPtr device, tobii_wearable_image_callback_t callback, IntPtr user_data = default(IntPtr));

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_wearable_image_unsubscribe")]
        public static extern tobii_error_t tobii_wearable_image_unsubscribe(IntPtr device);

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "tobii_wearable_entrance_pupil_position_subscribe")]
        public static extern tobii_error_t tobii_wearable_entrance_pupil_position_subscribe(IntPtr device, tobii_wearable_entrance_pupil_position_callback_t callback, IntPtr user_data = default(IntPtr));

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, EntryPoint = "tobii_wearable_entrance_pupil_position_unsubscribe")]
        public static extern tobii_error_t tobii_wearable_entrance_pupil_position_unsubscribe(IntPtr device);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void tobii_wearable_consumer_data_callback_t(ref tobii_wearable_consumer_data_t data, IntPtr user_data);

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_wearable_consumer_eye_t
    {
        public tobii_validity_t pupil_position_in_sensor_area_validity;
        public TobiiVector2 pupil_position_in_sensor_area_xy;

        public tobii_validity_t position_guide_validity;
        public TobiiVector2 position_guide_xy;

        public tobii_validity_t blink_validity;
        public tobii_state_bool_t blink;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_wearable_consumer_data_t
    {
        public long timestamp_us;
        public tobii_wearable_consumer_eye_t left;
        public tobii_wearable_consumer_eye_t right;

        public tobii_validity_t gaze_origin_combined_validity;
        public TobiiVector3 gaze_origin_combined_mm_xyz;
        public tobii_validity_t gaze_direction_combined_validity;
        public TobiiVector3 gaze_direction_combined_normalized_xyz;
        public tobii_validity_t convergence_distance_validity;
        public float convergence_distance_mm;
        public tobii_state_bool_t improve_user_position_hmd;
        public tobii_state_bool_t increase_eye_relief;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void tobii_wearable_advanced_data_callback_t(ref tobii_wearable_advanced_data_t data, IntPtr user_data);

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_wearable_advanced_eye_t
    {
        public tobii_validity_t gaze_origin_validity;
        public TobiiVector3 gaze_origin_mm_xyz;

        public tobii_validity_t gaze_direction_validity;
        public TobiiVector3 gaze_direction_normalized_xyz;

        public tobii_validity_t pupil_diameter_validity;
        public float pupil_diameter_mm;

        public tobii_validity_t pupil_position_in_sensor_area_validity;
        public TobiiVector2 pupil_position_in_sensor_area_xy;

        public tobii_validity_t position_guide_validity;
        public TobiiVector2 position_guide_xy;

        public tobii_validity_t blink_validity;
        public tobii_state_bool_t blink;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_wearable_advanced_data_t
    {
        public long timestamp_tracker_us;
        public long timestamp_system_us;

        public tobii_wearable_advanced_eye_t left;
        public tobii_wearable_advanced_eye_t right;

        public tobii_validity_t gaze_origin_combined_validity;
        public TobiiVector3 gaze_origin_combined_mm_xyz;
        public tobii_validity_t gaze_direction_combined_validity;
        public TobiiVector3 gaze_direction_combined_normalized_xyz;
        public tobii_validity_t convergence_distance_validity;
        public float convergence_distance_mm;
        public tobii_validity_t interocular_distance_validity;
        public float interocular_distance_mm;
        public tobii_state_bool_t improve_user_position_hmd;
        public tobii_state_bool_t increase_eye_relief;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_lens_configuration_t
    {
        public TobiiVector3 left_xyz;
        public TobiiVector3 right_xyz;
    }

    public enum tobii_lens_configuration_writable_t
    {
        TOBII_LENS_CONFIGURATION_NOT_WRITABLE,
        TOBII_LENS_CONFIGURATION_WRITABLE,
    }  

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_lens_configuration_extended_per_eye_t
    {
        public TobiiVector3 offset_xyz;
        public TobiiVector3 rotation_xyz;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_lens_configuration_extended_t
    {
        public tobii_lens_configuration_extended_per_eye_t left;
        public tobii_lens_configuration_extended_per_eye_t right;
    }  

    public enum tobii_lens_configuration_extended_writable_t
    {
        TOBII_LENS_CONFIGURATION_NOT_WRITABLE,
        TOBII_LENS_CONFIGURATION_WRITABLE,
    }

    public enum tobii_wearable_foveated_tracking_state_t
    {
        TOBII_WEARABLE_FOVEATED_TRACKING_STATE_TRACKING = 0,
        TOBII_WEARABLE_FOVEATED_TRACKING_STATE_EXTRAPOLATED = 1,
        TOBII_WEARABLE_FOVEATED_TRACKING_STATE_LAST_KNOWN = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_wearable_foveated_gaze_t
    {
        public long timestamp_us;
        public tobii_wearable_foveated_tracking_state_t tracking_state;
        public TobiiVector3 gaze_direction_combined_normalized_xyz;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void tobii_wearable_foveated_gaze_callback_t(ref tobii_wearable_foveated_gaze_t data, IntPtr user_data);

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_wearable_entrance_pupil_position_t
    {
        public long timestamp_tracker_us;
        public long timestamp_system_us;
        public tobii_validity_t entrance_pupil_position_left_validity;
        public TobiiVector3 entrance_pupil_position_left_mm_xyz;
        public tobii_validity_t entrance_pupil_position_right_validity;
        public TobiiVector3 entrance_pupil_position_right_mm_xyz;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void tobii_wearable_entrance_pupil_position_callback_t(ref tobii_wearable_entrance_pupil_position_t data, IntPtr user_data);

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_wearable_image_region_t
    {
        public uint camera;
        public int width;
        public int height;
        public int stride;
        public int offset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_wearable_diagnostics_image_t
    {
        public long timestamp_tracker_us;
        public long timestamp_system_us;
        public int bits_per_pixel;
        public int padding_per_pixel;
        public int image_regions_count;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public tobii_wearable_image_region_t[] image_regions;
        public IntPtr data_size;
        public IntPtr data;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void tobii_wearable_diagnostics_image_callback_t(ref tobii_wearable_diagnostics_image_t data, IntPtr user_data);

    [StructLayout(LayoutKind.Sequential)]
    public struct tobii_wearable_image_t
    {
        public long timestamp_tracker_us;
        public long timestamp_system_us;
        public int bits_per_pixel;
        public int padding_per_pixel;
        public int image_regions_count;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public tobii_wearable_image_region_t[] image_regions;
        public IntPtr data_size;
        public IntPtr data;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void tobii_wearable_image_callback_t(ref tobii_wearable_image_t data, IntPtr user_data);

    public struct tobii_wearable_limited_image_eye_t
    {
        public int width;
        public int height;

        public IntPtr data_size;
        public IntPtr data;
    }

    public struct tobii_wearable_limited_image_t
    {
        public long timestamp_us;

        public int bits_per_pixel;
        public tobii_wearable_limited_image_eye_t left;
        public tobii_wearable_limited_image_eye_t right;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void tobii_wearable_limited_image_callback_t(ref tobii_wearable_limited_image_t data, IntPtr user_data);
}
