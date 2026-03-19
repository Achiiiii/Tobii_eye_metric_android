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
using TMPro;
using Tobii.MediaCaptureClientLib;
using Tobii.StreamEngine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Android;
using System.Collections;
using System.Runtime.InteropServices;

public class AndroidWebcamCaptureClient : MonoBehaviour
{
    private Color32[] _pixels;
    private long _timestamp = 0;

    public MediaCaptureEvent mediaCaptureEvent;
    public UnityEvent<float, float, float> OnInitialized;
    public TMP_Dropdown dropdownCameraSelect;

    private AndroidJavaClass pluginClass;
    private bool isInitialized;

    string[] cameraIDs = new string[0];
    private float aspectRatioWidth = 0;
    private float aspectRatioHeight = 0;

    private void Awake()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            pluginClass = new AndroidJavaClass("com.tobii.camera.AndroidCameraPlugin");
        }
    }

    private IEnumerator Start()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("Requesting camera permission...");
                Permission.RequestUserPermission(Permission.Camera);

                // Wait until permission is granted
                while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    Debug.Log("Waiting for camera permission...");
                    yield return new WaitForSeconds(0.5f);
                }
            }
            try
            {
                pluginClass.CallStatic("registerConfigurationChangeListener");

                // Check for front facing cameras
                // cameraIDs = pluginClass.CallStatic<string[]>("getAllFrontFacingCameraIds");
                cameraIDs = pluginClass.CallStatic<string[]>("getAllCameraIds");

                foreach (var item in cameraIDs)
                {
                    Debug.Log("\ncameraID: " + item);
                }

                if (cameraIDs.Length == 0)
                {
                    Debug.LogError("No front facing cameras found on the device.");
                    yield break;
                }

                // Remove comments to handle multiple front facing cameras
                //// If multiple front facing cameras are found, fill the dropdown list
                //if (cameraIDs.Length > 1)
                //{
                //    Debug.LogWarning("Multiple front facing cameras found on the device. Fill drop down list.");
                //    dropdownCameraSelect.gameObject.SetActive(true);
                //    dropdownCameraSelect.ClearOptions();  // Clear existing options

                //    // Create a new list with "Select Camera" as the first option
                //    List<string> optionsList = new List<string> { "Select Camera" };
                //    optionsList.AddRange(cameraIDs); // Append camera IDs

                //    dropdownCameraSelect.AddOptions(optionsList);
                //    return;
                //}

                // We only have one front facing camera so lets go ahead and use it
                dropdownCameraSelect.gameObject.SetActive(false);
                initAndGetCameraParameters(cameraIDs[0]);

            }
            catch (Exception e)
            {
                Debug.LogError($"Error in Start: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    private void initAndGetCameraParameters(string cameraID)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            try
            {
                if (pluginClass == null)
                    pluginClass = new AndroidJavaClass("com.tobii.camera.AndroidCameraPlugin");

                float[] cameraParameters = pluginClass.CallStatic<float[]>("initAndGetCameraParameters", cameraID);

                if (cameraParameters == null)
                {
                    Debug.LogError("Failed to get camera parameters.");
                    return;
                }

                float dfov = cameraParameters[0];
                aspectRatioWidth = cameraParameters[1];
                aspectRatioHeight = cameraParameters[2];

                // Start camera
                pluginClass.CallStatic("startCamera", cameraID);
                isInitialized = true;
                OnInitialized.Invoke(dfov, aspectRatioWidth, aspectRatioHeight);

                Debug.Log("Camera initialization complete");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in Start: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    public void OnDropDownCameraSelect(int index)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            if (index == 0) // No camera selected
                return;

            initAndGetCameraParameters(cameraIDs[index - 1]);
        }
    }

    private void Update()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            var elapsedTimeInUs = Time.unscaledDeltaTime * 1_000_000;
            if (isInitialized && pluginClass != null)
            {
                try
                {
                    AndroidJavaObject[] rawDataAndWidthHeight = pluginClass.CallStatic<AndroidJavaObject[]>("getLatestImageData");
                    if (rawDataAndWidthHeight != null)
                    {
                        sbyte[] rawData = AndroidJNIHelper.ConvertFromJNIArray<sbyte[]>(rawDataAndWidthHeight[0].GetRawObject());
                        int[] imageWidthAndHeight = AndroidJNIHelper.ConvertFromJNIArray<int[]>(rawDataAndWidthHeight[1].GetRawObject());
                        int width = imageWidthAndHeight[0];
                        int height = imageWidthAndHeight[1];

                        // Use GCHandle to pin the array and obtain its address safely.
                        GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);
                        try
                        {
                            IntPtr dataPtr = handle.AddrOfPinnedObject();
                            _timestamp += (long)elapsedTimeInUs;
                            tobii_image_frame_t frame = new tobii_image_frame_t
                            {
                                format = 0, //Interop.TOBII_FRAME_FORMAT_GRAY8,
                                width = width,
                                height = height,
                                stride = width,
                                timestamp_us = _timestamp,
                                data_size = new IntPtr(width * height),
                                data = dataPtr
                            };

                            // Debug.Log("Frame captured: " + frame.timestamp_us + " " + frame.width + "x" + frame.height + " " +
                            //     frame.stride + " " + frame.data_size + " " + frame.data);

                            mcclient_frame_format_type formatType = mcclient_frame_format_type.MCCLIENT_FRAME_FORMAT_GRAY16;

                            UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
                            {
                                mediaCaptureEvent.Invoke(frame, formatType);
                            });
                        }
                        finally
                        {
                            // Always free the handle
                            handle.Free();
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in Update: {e.Message}\n{e.StackTrace}");
                }
            }
        }
    }
    private void OnDestroy()
    {
        if (mediaCaptureEvent != null)
            mediaCaptureEvent.RemoveAllListeners();
    }
}