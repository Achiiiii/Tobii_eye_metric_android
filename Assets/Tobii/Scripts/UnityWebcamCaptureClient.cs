/*
  COPYRIGHT 2024 - PROPERTY OF TOBII AB
  -------------------------------------
  2024 TOBII AB - KARLSROVAGEN 2D, DANDERYD 182 53, SWEDEN - All Rights Reserved.

  NOTICE:  All information contained herein is, and remains, the property of Tobii AB and its suppliers, if any.
  The intellectual and technical concepts contained herein are proprietary to Tobii AB and its suppliers and may be
  covered by U.S.and Foreign Patents, patent applications, and are protected by trade secret or copyright law.
  Dissemination of this information or reproduction of this material is strictly forbidden unless prior written
  permission is obtained from Tobii AB.
*/

using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TMPro;
using Tobii.MediaCaptureClientLib;
using Tobii.StreamEngine;
using UnityEngine;

public class UnityWebcamCaptureClient : MonoBehaviour
{
    private byte[] _greyscaleBytes;
    private byte[] _greyscaleUpscaledBytes = new byte[1920 * 1080];
    private GCHandle _gcHandle = new GCHandle();
    private Color32[] _pixels;
    private long _timestamp = 0;

    private WebCamTexture webcamTexture;

    public MediaCaptureEvent mediaCaptureEvent;
    public TMP_Dropdown dropdownCameraSelect;

    private void Start()
    {
        // Start the webcam
        webcamTexture = new WebCamTexture();

        // Get the list of available webcams
        WebCamDevice[] devices = WebCamTexture.devices;
        foreach (WebCamDevice device in devices)
        {
            TMP_Dropdown.OptionData newOption = new TMP_Dropdown.OptionData(device.name);
            dropdownCameraSelect.options.Add(newOption);
        }

        if (devices.Length == 1)
        {
            // Hide the dropdown
            dropdownCameraSelect.gameObject.SetActive(false);

            // Start only webcam
            webcamTexture = new WebCamTexture(1920, 1080);
            webcamTexture.Play();
        }

        // Start the processing of the webcam frames
        StartCoroutine(ProcessFrames());
    }

    public void OnDropDownCameraSelect(int index)
    {
        if (webcamTexture == null)
            return;

        if (index == 0) // No camera selected
            return;

        // Stop the current webcam
        webcamTexture.Stop();

        // Start the selected webcam
        webcamTexture = new WebCamTexture(WebCamTexture.devices[index-1].name, 1920, 1080);
        webcamTexture.Play();
    }

    private IEnumerator ProcessFrames()
    {
        while (true)
        {
            if (!webcamTexture.didUpdateThisFrame)
                yield return null;

            yield return ProcessWebCamFrameOnOwnThread();
        }
    }

    private IEnumerator ProcessWebCamFrameOnOwnThread()
    {
        var elapsedTimeInUs = Time.unscaledDeltaTime * 1_000_000;
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            // Extract the pixels from the texture
            _pixels = webcamTexture.GetPixels32();

            int w = webcamTexture.width;
            int h = webcamTexture.height;

            var task = Task.Run(() => process_image(w, h, elapsedTimeInUs));

            while (task.IsCompleted == false)
            {
                yield return null;
            }
        }
    }

    private Task<bool> process_image(int width, int height, float elapsedTimeInUs)
    {
        _timestamp += (long)elapsedTimeInUs;

        _greyscaleBytes = ConvertToGreyscale(_pixels, width, height);

        if (width != 1920)
        {
            // UpScale
            _greyscaleUpscaledBytes = gray_bilinear_scale(_greyscaleBytes, width, height, 1920, 1080);
            // Pin the object in memory
            _gcHandle = GCHandle.Alloc(_greyscaleUpscaledBytes, GCHandleType.Pinned);
        }
        else
        {
            // Pin the object in memory
            _gcHandle = GCHandle.Alloc(_greyscaleBytes, GCHandleType.Pinned);
        }

        tobii_image_frame_t frame = new tobii_image_frame_t
        {
            format = 0,//Interop.TOBII_FRAME_FORMAT_GRAY8,
            width = 1920,
            height = 1080,
            stride = 1920,
            timestamp_us = _timestamp,
            data_size = new IntPtr(1920 * 1080),
            data = _gcHandle.AddrOfPinnedObject()
        };

        mcclient_frame_format_type formatType = mcclient_frame_format_type.MCCLIENT_FRAME_FORMAT_GRAY16;

        UnityMainThreadDispatcher.Dispatcher.Enqueue(() =>
        {
            mediaCaptureEvent.Invoke(frame, formatType);
        });

        _gcHandle.Free();

        return Task.FromResult(true);
    }

    private byte[] ConvertToGreyscale(Color32[] pixels, int width, int height)
    {
        byte[] greyscaleBytes = new byte[pixels.Length];
        int index = 0;
        int offset = 0;

        for (int h = height - 1; h >= 0; h--)
        {
            for (int w = 0; w < width; w++)
            {
                offset = h * width + w;
                float greyValue = 0.2125f * pixels[offset].r + 0.7154f * pixels[offset].g + 0.0721f * pixels[offset].b; // based on current trained model
                greyscaleBytes[index++] = (byte)greyValue;
            }
        }
        return greyscaleBytes;
    }
    private byte[] gray_bilinear_scale(byte[] src, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
    {
        int x, y;
        int ox, oy;
        int tmpx, tmpy;
        int xratio = (srcWidth << 8) / dstWidth;
        int yratio = (srcHeight << 8) / dstHeight;
        byte[] dst_y = _greyscaleUpscaledBytes;
        byte[] src_y = src;

        byte[,] y_plane_color = new byte[2, 2];
        int j, i;
        int size = srcWidth * srcHeight;
        int offsetY;

        tmpy = 0;
        for (j = 0; j < (dstHeight & ~7); ++j)
        {
            // tmpy = j * yratio;
            oy = tmpy >> 8;
            y = tmpy & 0xFF;

            tmpx = 0;
            for (i = 0; i < (dstWidth & ~7); ++i)
            {
                // tmpx = i * xratio;
                ox = tmpx >> 8;
                x = tmpx & 0xFF;

                offsetY = oy * srcWidth;
                // YYYYYYYYYYYYYYYY
                y_plane_color[0, 0] = src[offsetY + ox];
                y_plane_color[1, 0] = src[offsetY + ox + 1];
                y_plane_color[0, 1] = src[offsetY + srcWidth + ox];
                y_plane_color[1, 1] = src[offsetY + srcWidth + ox + 1];

                int y_final = (0x100 - x) * (0x100 - y) * y_plane_color[0, 0] + x * (0x100 - y) * y_plane_color[1, 0] +
                              (0x100 - x) * y * y_plane_color[0, 1] + x * y * y_plane_color[1, 1];
                y_final = y_final >> 16;
                if (y_final > 255) y_final = 255;
                if (y_final < 0) y_final = 0;
                dst_y[j * dstWidth + i] = (byte)y_final;  // set Y in dest array
                                                          // UVUVUVUVUVUV

                tmpx += xratio;
            }
            tmpy += yratio;
        }
        return dst_y;
    }

    private void OnDestroy()
    {
        if (_gcHandle.IsAllocated)
            _gcHandle.Free();

        if (webcamTexture != null)
            webcamTexture.Stop();

        if (mediaCaptureEvent != null)
            mediaCaptureEvent.RemoveAllListeners();
    }
}