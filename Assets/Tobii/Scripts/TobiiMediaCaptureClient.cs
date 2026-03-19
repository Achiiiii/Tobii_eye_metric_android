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

using System.Collections.Generic;
using UnityEngine;
using Tobii.MediaCaptureClientLib;
using System.Runtime.InteropServices;
using System;
using TMPro;
using System.Linq;
using Tobii.StreamEngine;
using Interop = Tobii.MediaCaptureClientLib.Interop;
using UnityEngine.Events;
using UnityEditor;
using System.Collections;

[Serializable]
public class MediaCaptureEvent : UnityEvent<tobii_image_frame_t, mcclient_frame_format_type> { };

public class TobiiMediaCaptureClient : MonoBehaviour
{
    private mcclient_device_added_callback _onMediaCaptureDeviceAdded;
    private mcclient_device_removed_callback _onMediaCaptureDeviceRemoved;
    private mcclient_device_updated_callback _onMediaCaptureDeviceUpdated;
    private mcclient_stream_callback _onFrameArrived;

    private static IntPtr mediaCaptureDeviceContext;
    private static IntPtr mediaCaptureDeviceWatcher;
    private static ulong timestamp_0 = 0;

    public MediaCaptureEvent mediaCaptureEvent;
    public TMP_Dropdown dropdownCameraSelect;

    private Dictionary<string, string> _camera_listDictionary;

    // Reused objects
    private byte[] _bytes;


    public IEnumerator Start()
    {
#if UNITY_EDITOR
        // Register play mode state change callback for Editor to allow proper cleanup with editor Play/Stop
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        _camera_listDictionary = new Dictionary<string, string>();

        _onMediaCaptureDeviceAdded = onMediaCaptureDeviceAdded;
        _onMediaCaptureDeviceRemoved = onMediaCaptureDeviceRemoved;
        _onMediaCaptureDeviceUpdated = onMediaCaptureDeviceUpdated;
        _onFrameArrived = onFrameArrived;

        TextAsset seTextAsset = Resources.Load<TextAsset>("se_license_key");
        if (seTextAsset == null)
        {
            Debug.LogError("Failed to load license file");
        }
        string license = System.Text.Encoding.Unicode.GetString(seTextAsset.bytes);

        int result = Interop.mcclient_init(license);

        if (result != Interop.MCCLIENT_OK)
        {
            Debug.LogError("Failed to initialize TobiiMediaCaptureClient: " + ((result == Interop.MCCLIENT_INVALID_LICENSE) ? "Invalid license.\nTemporary work around: Disable 'TobiiMediaCaptureClient' gameobject and enable 'UnityWebcamCaptureClient' in the scene for limited camera data access." : result));
            yield break;
        }
        Debug.Log($"mcclient_init: {result}");

        mcclient_device_watcher_callbacks device_watcher_callbacks = new mcclient_device_watcher_callbacks();
        device_watcher_callbacks.added = _onMediaCaptureDeviceAdded;
        device_watcher_callbacks.removed = _onMediaCaptureDeviceRemoved;
        device_watcher_callbacks.updated = _onMediaCaptureDeviceUpdated;

        mediaCaptureDeviceWatcher = IntPtr.Zero;
        result = Interop.mcclient_regiester_for_device_callbacks(out mediaCaptureDeviceWatcher, device_watcher_callbacks, IntPtr.Zero);
        Debug.Log($"mcclient_regiester_for_device_callbacks: {result}");

        // Wait for web cams to be found
        yield return new WaitForSeconds(1);

        // If there is only one camera, start streaming immediately, else wait for user to select from dropdown list
        if (_camera_listDictionary.Count == 1)
        {
            StartStreaming(new List<string> { _camera_listDictionary.First().Key, _camera_listDictionary.First().Value });
        }
    }

    private void onMediaCaptureDeviceAdded(IntPtr id, IntPtr name, IntPtr user_data)
    {
        string id_str = new string(Marshal.PtrToStringUni(id));
        string name_str = new string(Marshal.PtrToStringUni(name));

        // We filter out SWD devices. For now we allow virtual cameras.
        if (id_str.Contains("SWD", StringComparison.OrdinalIgnoreCase)) return;

        mcclient_device_capabilities dev_cap = new mcclient_device_capabilities();
        int result = Interop.mcclient_get_device_capabilities(id, out dev_cap);

        // We filter out non RGB cameras
        if (dev_cap.list[(int)mcclient_device_capability.mcclient_device_capability_rgb] == 0) return;

        Debug.Log($"onDeviceAdded: {Marshal.PtrToStringUni(name)}");

        List<string> newCamera = new List<string>
        {
            id_str,
            name_str
        };

        UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
        {
            TMP_Dropdown.OptionData newOption = new TMP_Dropdown.OptionData(newCamera[1]);
            dropdownCameraSelect.options.Add(newOption);
        });
        _camera_listDictionary.Add(newCamera[0], newCamera[1]);

        // If there are more than one camera, show the dropdown list
        if (_camera_listDictionary.Count > 1)
        {
            UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
            {
                dropdownCameraSelect.gameObject.SetActive(true);
            });
        }
        else
        {
            UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
            {
                dropdownCameraSelect.gameObject.SetActive(false);
            });
        }
    }

    private void onMediaCaptureDeviceRemoved(IntPtr id, IntPtr user_data)
    {
        Debug.Log($"onDeviceAdded: {Marshal.PtrToStringUni(id)}");
    }

    private void onMediaCaptureDeviceUpdated(IntPtr id, IntPtr user_data)
    {
        Debug.Log($"onDeviceAdded: {Marshal.PtrToStringUni(id)}");
    }

    private bool onFrameArrived(ref mcclient_frame frame, IntPtr user_data)
    {
        if (timestamp_0 != 0)
        {
            uint frameSize = frame.width * frame.height + (frame.width * frame.height) / 2;

            mcclient_frame_format_type formatType = frame.frame_format;

            if (_bytes == null || _bytes.Length != frameSize)
            {
                _bytes = new byte[frameSize];
            }

            // Copy the data from the IntPtr to the byte array
            Marshal.Copy(frame.buff, _bytes, 0, (int)frameSize);
            GCHandle handle = GCHandle.Alloc(_bytes, GCHandleType.Pinned);

            tobii_image_frame_t imageFrame = new tobii_image_frame_t
            {
                format = 0, // TODO: 
                width = (int)frame.width,
                height = (int)frame.height,
                stride = (int)frame.stride,
                timestamp_us = (long)frame.recv_time_usec,
                data_size = new IntPtr(frame.width * frame.height),
                data = handle.AddrOfPinnedObject()
            };

            UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
            {
                mediaCaptureEvent.Invoke(imageFrame, formatType);
            });
        }
        timestamp_0 = frame.recv_time_usec;
        Interop.mcclient_return_frame(ref frame);
        return true;
    }

    public void OnCameraSelected(List<string> selectedCamera)
    {
        if (mediaCaptureDeviceContext != IntPtr.Zero)
        {
            Interop.mcclient_stop_streaming_ex(mediaCaptureDeviceContext, mcclient_stream_mode.MCCLIENT_EXCLUSIVE_MODE);

            Interop.mcclient_device_close(mediaCaptureDeviceContext);
            mediaCaptureDeviceContext = IntPtr.Zero;
        }
        StartStreaming(selectedCamera);
    }

    private void StartStreaming(List<string> selectedCamera)
    { 
        mcclient_device_info dev_info = new mcclient_device_info();
        dev_info.capability = mcclient_device_capability.mcclient_device_capability_rgb;
        dev_info.width = 1280;
        dev_info.height = 720;

        int result = Interop.mcclient_device_open(out mediaCaptureDeviceContext, selectedCamera[0], ref dev_info);
        Debug.Log($"mcclient_device_open: {result}");

        result = Interop.mcclient_disable_low_light_compensation(mediaCaptureDeviceContext);
        Debug.Log($"mcclient_disable_low_light_compensation: {result}");

        result = Interop.mcclient_start_streaming_ex(mediaCaptureDeviceContext, _onFrameArrived, IntPtr.Zero, mcclient_stream_mode.MCCLIENT_EXCLUSIVE_MODE);

        // Select highest media type
        SetHighestMediaType();

    }

    public mcclient_media_types SetHighestMediaType()
    {
        int result = Interop.mcclient_enable_media_type(mediaCaptureDeviceContext, mcclient_media_types.MCCLIENT_1080P_60Hz_COLOR);
        if (result == 0)
        {
            Debug.Log("mcclient_enable_media_type: MCCLIENT_1080P_60Hz_COLOR selected.");
            return mcclient_media_types.MCCLIENT_1080P_60Hz_COLOR;
        }

        result = Interop.mcclient_enable_media_type(mediaCaptureDeviceContext, mcclient_media_types.MCCLIENT_720P_60Hz_COLOR);
        if (result == 0)
        {
            Debug.Log("mcclient_enable_media_type: MCCLIENT_720P_60Hz_COLOR selected.");
            return mcclient_media_types.MCCLIENT_720P_60Hz_COLOR;
        }

        result = Interop.mcclient_enable_media_type(mediaCaptureDeviceContext, mcclient_media_types.MCCLIENT_1080P_30Hz_COLOR);
        if (result == 0)
        {
            Debug.Log("mcclient_enable_media_type: MCCLIENT_1080P_30Hz_COLOR selected.");
            return mcclient_media_types.MCCLIENT_1080P_30Hz_COLOR;
        }

        result = Interop.mcclient_enable_media_type(mediaCaptureDeviceContext, mcclient_media_types.MCCLIENT_720P_30Hz_COLOR);
        if (result == 0)
        {
            Debug.Log("mcclient_enable_media_type: MCCLIENT_720P_30Hz_COLOR selected.");
            return mcclient_media_types.MCCLIENT_720P_30Hz_COLOR;
        }

        Debug.Log($"No acceptable media types found (mcclient_enable_media_types): {result}");
        return 0;
    }

    public void OnDropdownCameraValueChanged(int val)
    {
        if (gameObject.activeSelf == false) return; // Prevents dropdown from being called when the gameobject is disabled

        if (val == 0) return;

        List<string> selectedCamera = new List<string>
        {
            _camera_listDictionary.ElementAt(val-1).Key,
            _camera_listDictionary.ElementAt(val-1).Value
        };

        // Hide the dropdown list
        dropdownCameraSelect.gameObject.SetActive(false);

        OnCameraSelected(selectedCamera);
    }


#if UNITY_EDITOR
    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Cleanup();
        }
    }
#endif
    private void OnApplicationQuit()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        if (mediaCaptureDeviceContext == IntPtr.Zero) return;
        Interop.mcclient_stop_streaming_ex(mediaCaptureDeviceContext, mcclient_stream_mode.MCCLIENT_EXCLUSIVE_MODE);
        Interop.mcclient_device_close(mediaCaptureDeviceContext);
        Interop.mcclient_unregiester_device_callbacks(mediaCaptureDeviceWatcher);
        mediaCaptureDeviceWatcher = IntPtr.Zero;
        mediaCaptureDeviceContext = IntPtr.Zero;
        Interop.mcclient_exit();
    }
}