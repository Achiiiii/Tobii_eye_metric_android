/*
COPYRIGHT 2023 - PROPERTY OF TOBII AB
-------------------------------------
2023 TOBII AB - KARLSROVAGEN 2D, DANDERYD 182 53, SWEDEN - All Rights Reserved.

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
    public static class InteropFD
    {
        public const string stream_engine_dll = "tobii_stream_engine";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int tobii_get_fd_t(string name, int mode, IntPtr user_data);


        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate bool tobii_release_fd_t(int fd, IntPtr user_data);

        [StructLayout(LayoutKind.Sequential)]
        public struct tobii_fd_device_context_t
        {
            public InteropFD.tobii_get_fd_t get_fd;
            public InteropFD.tobii_release_fd_t release_fd;
            public IntPtr user_data;
        };

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_fd_device_create_ex")]
        private static extern tobii_error_t tobii_fd_device_create_ex_internal(IntPtr api, string url, [In] ref tobii_fd_device_context_t ctx, tobii_field_of_use_t field_of_use, tobii_license_key_t[] license_keys, int license_count, [MarshalAs(UnmanagedType.LPArray)] tobii_license_validation_result_t[] licenseResults, out IntPtr device);

        public static tobii_error_t tobii_fd_device_create_ex(IntPtr api, string url, [In] ref tobii_fd_device_context_t ctx, tobii_field_of_use_t field_of_use, string[] license_keys, List<tobii_license_validation_result_t> license_results, out IntPtr device)
        {
            var keys = new List<tobii_license_key_t>();

            foreach (var key in license_keys)
            {
                keys.Add(new tobii_license_key_t { license_key = key, size_in_bytes = new IntPtr(key.Length * 2) });
            }

            var license_results_array = new tobii_license_validation_result_t[license_keys.Length];
            var tobii_error = tobii_fd_device_create_ex_internal(api, url, ref ctx, field_of_use, keys.ToArray(), keys.Count, license_results_array, out device);

            if (license_results != null)
            {
                license_results.InsertRange(0, license_results_array);
            }

            return tobii_error;
        }

        [DllImport(stream_engine_dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "tobii_fd_device_create")]
	      public static extern tobii_error_t tobii_fd_device_create(IntPtr api, string url, [In] ref tobii_fd_device_context_t ctx, tobii_field_of_use_t field_of_use, out IntPtr device);
    }
}
