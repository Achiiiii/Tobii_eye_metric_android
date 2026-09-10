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
using Tobii.StreamEngine;
using UnityEngine;
using UnityEngine.UI;

public class FrameDataPreview : MonoBehaviour
{
    public RawImage displayImage; // Can be used to display the camera feed
    public Shader grayscaleShader; // Shader to convert the camera feed to grayscale
    private Texture2D texture;

    public void OnFrameDataForPreview(tobii_image_frame_t frame, Tobii.MediaCaptureClientLib.mcclient_frame_format_type formatType)
    {
        if (displayImage == null || grayscaleShader == null)
            return;

        if (frame.data == IntPtr.Zero)
        {
            Debug.LogWarning("Received frame with null data pointer");
            return;
        }

        if (frame.width <= 0 || frame.height <= 0)
        {
            Debug.LogWarning($"Invalid frame dimensions: {frame.width}x{frame.height}");
            return;
        }

        try
        {
            if (texture == null || texture.width != frame.width || texture.height != frame.height)
            {
                SetupRawImage(frame.width, frame.height);
            }

            texture.LoadRawTextureData(frame.data, frame.width * frame.height);
            texture.Apply();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing frame data: {e.Message}");
        }
    }

    private void SetupRawImage(int w, int h)
    {
        // If we want to see the images on a RawImage
        if (displayImage != null && grayscaleShader != null)
        {
            // Create texture first
            texture = new Texture2D(w, h, TextureFormat.R8, false);
            texture.filterMode = FilterMode.Point;

            // Create material with the assigned shader
            Material grayMaterial = new Material(grayscaleShader);
            displayImage.material = grayMaterial;

            // Assign to RawImage
            displayImage.uvRect = new Rect(0, 1, -1, -1);  // Flips the UV coordinates
            displayImage.texture = texture;

            // Adjust the aspect ratio of the RawImage
            float aspectRatio = (float)w / (float)h;  // Cast to float for proper division
            displayImage.rectTransform.sizeDelta = new Vector2(
                displayImage.rectTransform.sizeDelta.x,
                displayImage.rectTransform.sizeDelta.x / aspectRatio
            );
        }
    }
}
