/*
COPYRIGHT 2017 - PROPERTY OF TOBII AB
-------------------------------------
2017 TOBII AB - KARLSROVAGEN 2D, DANDERYD 182 53, SWEDEN - All Rights Reserved.

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

namespace Tobii.MediaCaptureClientLib
{
    public static class Interop
    {
        public static int MCCLIENT_OK = 0;
        public static int MCCLIENT_ERROR = -1;
        public static int MCCLIENT_BAD_STATE = -2;
        public static int MCCLIENT_TIMEOUT = -3;
        public static int MCCLIENT_RESOURCE_BUSY = -4;
        public static int MCCLIENT_INVALID_ARGUMENT = -5;
        public static int MCCLIENT_UNSUPPORTED_SOFT_FRAMERATE = -6;
        public static int MCCLIENT_UNSUPPORTED_FRAME_FORMAT = -7;
        public static int MCCLIENT_INVALID_LICENSE = -7;

        public const string media_capture_client_dll = "MediaCaptureClientLib";

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_init")]
        public static extern int mcclient_init_internal(ref mcclient_license_key license);

        public static int mcclient_init(string license)
        {
            var key = new mcclient_license_key { license_key = license, size_in_bytes = new IntPtr(license.Length * 2) };

            return mcclient_init_internal(ref key);

        }

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_exit")]
        public static extern int mcclient_exit();

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_device_open", CharSet = CharSet.Unicode)]
        public static extern int mcclient_device_open(out IntPtr device, string id, ref mcclient_device_info dev_info );

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_device_close", CharSet = CharSet.Unicode)]
        public static extern int mcclient_device_close(IntPtr device);

        public static int mcclient_get_device_capabilities(IntPtr id, out mcclient_device_capabilities capabilities)
        {
            //IntPtr id_wchar = Marshal.StringToHGlobalUni(id);
            return mcclient_get_device_capabilities_internal(id, out capabilities);
        }

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_get_device_capabilities", CharSet = CharSet.Unicode)]
        private static extern int mcclient_get_device_capabilities_internal(IntPtr id /*[MarshalAs(UnmanagedType.LPWStr)] string id*/, out mcclient_device_capabilities capabilities);

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_regiester_for_device_callbacks", CharSet = CharSet.Unicode)]
        public static extern int mcclient_regiester_for_device_callbacks(out IntPtr device_watcher, mcclient_device_watcher_callbacks callbacks, IntPtr user_data = default(IntPtr));

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_unregiester_device_callbacks")]
        public static extern int mcclient_unregiester_device_callbacks(IntPtr device_watcher);

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_start_streaming")]
        public static extern int mcclient_start_streaming(IntPtr device, mcclient_stream_callback callback, IntPtr user_data = default(IntPtr));

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_start_streaming_ex")]
        public static extern int mcclient_start_streaming_ex(IntPtr device, mcclient_stream_callback callback, IntPtr user_data, mcclient_stream_mode stream_mode);

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_stop_streaming")]
        public static extern int mcclient_stop_streaming(IntPtr device);

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_stop_streaming_ex")]
        public static extern int mcclient_stop_streaming_ex(IntPtr device, mcclient_stream_mode stream_mode);

        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_return_frame")]
        public static extern int mcclient_return_frame(ref mcclient_frame frame);

        public static int mcclient_disable_low_light_compensation(IntPtr device)
        {
            mcclient_dev_property dev_prop = new mcclient_dev_property();
            dev_prop.id = 0x0013;
            dev_prop.value = ((0x0013 << 8) | 0x0002);
            return mcclient_set_dev_property(device, ref dev_prop);
        }

        public static int mcclient_enable_media_type(IntPtr device, mcclient_media_types media_type)
        {
            mcclient_dev_property dev_prop = new mcclient_dev_property();
            dev_prop.id = 0x0009;
            dev_prop.value = (uint)media_type;
            return mcclient_set_dev_property(device, ref dev_prop);
        }
        [DllImport(media_capture_client_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mcclient_set_dev_property", CharSet = CharSet.Unicode)]
        private static extern int mcclient_set_dev_property(IntPtr device, ref mcclient_dev_property dev_property);
    }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void mcclient_device_added_callback(IntPtr id, IntPtr name, IntPtr user_data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void mcclient_device_removed_callback(IntPtr id, IntPtr user_data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void mcclient_device_updated_callback(IntPtr id, IntPtr user_data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool mcclient_stream_callback(ref mcclient_frame frame, IntPtr user_data);

    [StructLayout(LayoutKind.Sequential)]
    public struct mcclient_dev_property
    {
        public uint id;
        public uint value; 
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct mcclient_license_key
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string license_key;

        public IntPtr size_in_bytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct mcclient_frame
    {
        public IntPtr buff;
        public UInt32 width;
        public UInt32 height;
        public UInt32 stride;
        public UInt32 iso_speed;
        public float gain_analog;
        public float gain_digital;
        public UInt64 exposure_time_100ns;
        public UInt64 recv_time_usec;
        public byte illuminated;
        public byte led_on_power;
        public mcclient_frame_format_type frame_format;
        public IntPtr input_reference;
    }

    public enum mcclient_frame_format_type
    {
        MCCLIENT_FRAME_FORMAT_GRAY8 = 62,
        MCCLIENT_FRAME_FORMAT_GRAY16 = 57,
        MCCLIENT_FRAME_FORMAT_NV12 = 103,
        MCCLIENT_FRAME_FORMAT_YUY2 = 107,
        MCCLIENT_FRAME_FORMAT_YUV420 = 204
    }

    public enum mcclient_media_types
    {
        MCCLIENT_1080P_30Hz_COLOR = 18,
        MCCLIENT_720P_30Hz_COLOR = 19,
        MCCLIENT_1080P_60Hz_COLOR = 20,
        MCCLIENT_720P_60Hz_COLOR = 21,
    }

    public enum mcclient_device_capability
    {
        mcclient_device_capability_rgb = 0,
        mcclient_device_capability_ir = 1,
        mcclient_device_capability_depth = 2,
        mcclient_device_capability_unknown = 3,
        mcclient_device_capabilities_length = 4  //<- last element.
    }
	
	public enum mcclient_stream_mode
	{
		MCCLIENT_SHARED_MODE = 0,
		MCCLIENT_EXCLUSIVE_MODE = 1
	}

    [StructLayout(LayoutKind.Sequential)]
    public struct mcclient_device_capabilities
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)mcclient_device_capability.mcclient_device_capabilities_length)]
        public byte[] list;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct mcclient_device_info
    {
        public mcclient_device_capability capability;
        public ushort width;
        public ushort height;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct mcclient_device_watcher_callbacks
    {
        public mcclient_device_added_callback added;
        public mcclient_device_removed_callback removed;
        public mcclient_device_updated_callback updated;
    }
}