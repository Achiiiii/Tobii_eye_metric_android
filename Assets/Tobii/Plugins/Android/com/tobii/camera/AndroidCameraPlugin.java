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

//package com.tobii.camera;
package com.tobii.camera;

import android.app.Activity;
import android.content.Context;
import android.content.res.Configuration;
import android.content.ComponentCallbacks2;
import android.hardware.camera2.CameraAccessException;
import android.hardware.camera2.CameraCaptureSession;
import android.hardware.camera2.CameraCharacteristics;
import android.hardware.camera2.CameraDevice;
import android.hardware.camera2.CameraManager;
import android.hardware.camera2.CaptureRequest;
import android.media.Image;
import android.media.ImageReader;
import android.os.Handler;
import android.os.HandlerThread;
import android.util.Log;
import android.util.Range;
import android.graphics.ImageFormat;
import android.view.Surface;
import com.unity3d.player.UnityPlayer;
import java.nio.ByteBuffer;
import java.util.Collections;
import java.util.ArrayList;
import java.util.List;
import android.graphics.Rect;
import android.hardware.camera2.params.StreamConfigurationMap;
import static android.hardware.camera2.CameraCharacteristics.SCALER_STREAM_CONFIGURATION_MAP;
import java.text.DecimalFormat;
import java.math.RoundingMode;
import android.util.Size;
import java.lang.Math;
import com.unity3d.player.UnityPlayerActivity;
import java.util.Collections;
import java.util.Comparator;

public class AndroidCameraPlugin {
    private static final String TAG = "AndroidCameraPlugin";

    private static int IMAGE_WIDTH = 1280;
    private static int IMAGE_HEIGHT = 720;

    private static int latestImageWidth = 0;
    private static int latestImageHeight = 0;

    private static CameraDevice cameraDevice;
    private static CameraCaptureSession captureSession;
    private static ImageReader imageReader;
    private static Handler backgroundHandler;
    private static HandlerThread backgroundThread;
    
    // Declare the latestImageData as a byte array
    private static byte[] latestImageData = new byte[IMAGE_WIDTH * IMAGE_HEIGHT];
    private static boolean newFrameAvailable = false;
    
    private static final Object imageLock = new Object();
    private static int rotationCompensation = 0;
    private static String selectedCameraId;

    public static void registerConfigurationChangeListener() {
        
        return;
    }

    public static Object[] getLatestImageData() {
        synchronized (imageLock) {
            if (newFrameAvailable) {
                //Log.d(TAG, "Returning new frame data");
                byte[] image_buffer = new byte[latestImageData.length];
                System.arraycopy(latestImageData, 0, image_buffer, 0, latestImageData.length);
                newFrameAvailable = false;
                int[] widthAndHeight = new int[]{latestImageWidth, latestImageHeight};
                return new Object[]{image_buffer, widthAndHeight};
            }
            else{
                return null;
            }
        }
    }

    private static byte[] removeStride(byte[] original, int width, int height, int stride)
    {
        byte[] strideless = new byte[width * height];
        for (int y = 0; y < height; y++) {
            int srcPos = y * stride;
            int dstPos = y * width;
            int available = original.length - srcPos;
            if (available <= 0) {
                Log.w(TAG, "removeStride: Buffer exhausted at row " + y + "/" + height + 
                      " (srcPos=" + srcPos + ", bufferLen=" + original.length + ")");
                break;
            }
            int copyLen = Math.min(width, available);
            if (copyLen < width) {
                Log.w(TAG, "removeStride: Partial row copy at row " + y + 
                      " (copied " + copyLen + "/" + width + " bytes)");
            }
            System.arraycopy(original, srcPos, strideless, dstPos, copyLen);
        }
        return strideless;
    }
    public static byte[] rotateGrayscale90(byte[] original, int width, int height, int stride) {
        byte[] rotated = new byte[width * height];
        int newWidth = height;
        boolean loggedWarning = false;

        for (int y = 0; y < height; y++) {
            int rowStart = y * stride;
            int available = original.length - rowStart;
            if (available <= 0) {
                if (!loggedWarning) {
                    Log.w(TAG, "rotateGrayscale90: Buffer exhausted at row " + y + "/" + height +
                          " (rowStart=" + rowStart + ", bufferLen=" + original.length + ")");
                    loggedWarning = true;
                }
                break;
            }
            int pixelsToRead = Math.min(width, available);
            if (pixelsToRead < width && !loggedWarning) {
                Log.w(TAG, "rotateGrayscale90: Partial row at row " + y +
                      " (available " + pixelsToRead + "/" + width + " pixels)");
                loggedWarning = true;
            }

            for (int x = 0; x < pixelsToRead; x++) {
                rotated[x * newWidth + (newWidth - y - 1)] = original[rowStart + x];
            }
        }
        return rotated;
    }
    public static byte[] rotateGrayscale270(byte[] original, int width, int height, int stride) {
        byte[] rotated = new byte[width * height];
        int newWidth = height;
        int newHeight = width;
        boolean loggedWarning = false;

        for (int y = 0; y < height; y++) {
            int rowStart = y * stride;
            int available = original.length - rowStart;
            if (available <= 0) {
                if (!loggedWarning) {
                    Log.w(TAG, "rotateGrayscale270: Buffer exhausted at row " + y + "/" + height +
                          " (rowStart=" + rowStart + ", bufferLen=" + original.length + ")");
                    loggedWarning = true;
                }
                break;
            }
            int pixelsToRead = Math.min(width, available);
            if (pixelsToRead < width && !loggedWarning) {
                Log.w(TAG, "rotateGrayscale270: Partial row at row " + y +
                      " (available " + pixelsToRead + "/" + width + " pixels)");
                loggedWarning = true;
            }

            for (int x = 0; x < pixelsToRead; x++) {
                rotated[(newHeight - 1 - x) * newWidth + y] = original[rowStart + x];
            }
        }
        return rotated;
    }
    public static byte[] rotateGrayscale180(byte[] original, int width, int height, int stride) {
        byte[] rotated = new byte[width * height];
        boolean loggedWarning = false;

        for (int y = 0; y < height; y++) {
            int rowStart = y * stride;
            int available = original.length - rowStart;
            if (available <= 0) {
                if (!loggedWarning) {
                    Log.w(TAG, "rotateGrayscale180: Buffer exhausted at row " + y + "/" + height +
                          " (rowStart=" + rowStart + ", bufferLen=" + original.length + ")");
                    loggedWarning = true;
                }
                break;
            }
            int pixelsToRead = Math.min(width, available);
            if (pixelsToRead < width && !loggedWarning) {
                Log.w(TAG, "rotateGrayscale180: Partial row at row " + y +
                      " (available " + pixelsToRead + "/" + width + " pixels)");
                loggedWarning = true;
            }

            for (int x = 0; x < pixelsToRead; x++) {
                rotated[(height - y - 1) * width + (width - x - 1)] = original[rowStart + x];
            }
        }
        return rotated;
    }

    private static int getCurrentOrientationCompensation() {
        Activity unityActivity = UnityPlayer.currentActivity;
        try {
            CameraManager cameraManager = (CameraManager) unityActivity.getSystemService(Context.CAMERA_SERVICE);
            CameraCharacteristics cameraCharacteristics = cameraManager.getCameraCharacteristics(selectedCameraId);
            int sensorOrientation = cameraCharacteristics.get(CameraCharacteristics.SENSOR_ORIENTATION);
            int device_rotation = unityActivity.getWindowManager().getDefaultDisplay().getRotation();
            int rotationDegrees;
            switch (device_rotation) {
                case Surface.ROTATION_0: rotationDegrees = 0; break;
                case Surface.ROTATION_90: rotationDegrees = 90; break;
                case Surface.ROTATION_180: rotationDegrees = 180; break;
                case Surface.ROTATION_270: rotationDegrees = 270; break;
                default: rotationDegrees = 0; break;
            }
                return (rotationDegrees+sensorOrientation)%360;
        }
        catch (CameraAccessException e) {
            Log.e(TAG, "Camera access exception: " + e.getMessage());
            return 0;
        }
    }

    private static ImageReader.OnImageAvailableListener imageListener = new ImageReader.OnImageAvailableListener() {
        @Override
        public void onImageAvailable(ImageReader reader) {
            Image image = null;
            try {
                image = reader.acquireLatestImage();
                if (image == null) {
                    Log.w(TAG, "acquireLatestImage returned null");
                    return;
                }

                int imageWidth = image.getWidth();
                int imageHeight = image.getHeight();

                Image.Plane[] planes = image.getPlanes();
                ByteBuffer buffer = planes[0].getBuffer();
                int stride = planes[0].getRowStride();

                byte[] rawData = new byte[buffer.remaining()];
                buffer.get(rawData);

                Log.d(TAG, "imageListener: Image received - " + imageWidth + "x" + imageHeight + 
                      ", stride=" + stride + ", bufferSize=" + rawData.length);

                int rotation = getCurrentOrientationCompensation();
                Log.d(TAG, "imageListener: Rotation compensation=" + rotation);

                byte[] processedData;
                int finalWidth, finalHeight;

                if (rotation == 90) {
                    Log.d(TAG, "imageListener: Applying 90° rotation");
                    processedData = rotateGrayscale90(rawData, imageWidth, imageHeight, stride);
                    finalWidth = imageHeight;
                    finalHeight = imageWidth;
                } else if (rotation == 270) {
                    Log.d(TAG, "imageListener: Applying 270° rotation");
                    processedData = rotateGrayscale270(rawData, imageWidth, imageHeight, stride);
                    finalWidth = imageHeight;
                    finalHeight = imageWidth;
                } else if (rotation == 180) {
                    Log.d(TAG, "imageListener: Applying 180° rotation");
                    processedData = rotateGrayscale180(rawData, imageWidth, imageHeight, stride);
                    finalWidth = imageWidth;
                    finalHeight = imageHeight;
                } else {
                    // No rotation - but still need to remove stride if present
                    Log.d(TAG, "imageListener: No rotation needed (0°)");
                    if (stride != imageWidth) {
                        Log.d(TAG, "imageListener: Removing stride padding (stride=" + stride + ", width=" + imageWidth + ")");
                        processedData = removeStride(rawData, imageWidth, imageHeight, stride);
                    } else {
                        int expectedSize = imageWidth * imageHeight;
                        processedData = new byte[expectedSize];
                        int copyLen = Math.min(expectedSize, rawData.length);
                        System.arraycopy(rawData, 0, processedData, 0, copyLen);
                    }
                    finalWidth = imageWidth;
                    finalHeight = imageHeight;
                }

                Log.d(TAG, "imageListener: Processed data size=" + processedData.length + 
                      ", finalDimensions=" + finalWidth + "x" + finalHeight);

                synchronized (imageLock) {
                    if (latestImageData.length != processedData.length) {
                        Log.i(TAG, "imageListener: Resizing latestImageData from " + 
                              latestImageData.length + " to " + processedData.length);
                    }
                    latestImageData = processedData;
                    latestImageWidth = finalWidth;
                    latestImageHeight = finalHeight;
                    newFrameAvailable = true;
                }

            } catch (Exception e) {
                Log.e(TAG, "Error in onImageAvailable: " + e.getMessage(), e);
            } finally {
                if (image != null) {
                    image.close();
                }
            }
        }
    };

    public static void startCamera(String cameraId) {
        Log.d(TAG, "AndroidCameraPlugin startCamera called with cameraId: " + cameraId);
        selectedCameraId = cameraId;
        //float[] cameraParameters = initAndGetCameraParameters(cameraId);
        Activity activity = UnityPlayer.currentActivity;
        CameraManager manager = (CameraManager) activity.getSystemService(Context.CAMERA_SERVICE);

        try {
            startBackgroundThread();

            // Query the available sizes
            CameraCharacteristics cameraCharacteristics = manager.getCameraCharacteristics(cameraId);
            StreamConfigurationMap map = cameraCharacteristics.get(SCALER_STREAM_CONFIGURATION_MAP);

            if (map == null) {
                Log.e(TAG, "Camera stream configuration map is null");
                return;
            }

            float[] sensorWidthAndHeight = getSensorAspectRatio(cameraCharacteristics);
            
            // Now we have all we need to find a resolution that matches the
            // sensor's aspect ratio
            Size selectedSize = getBestResolution(cameraCharacteristics, sensorWidthAndHeight);

            if (selectedSize != null) {
                // Create ImageReader with the selected resolution
                imageReader = ImageReader.newInstance(selectedSize.getWidth(), selectedSize.getHeight(), ImageFormat.YUV_420_888, 2);
                imageReader.setOnImageAvailableListener(imageListener, backgroundHandler);

                Log.d(TAG, "Using resolution: " + selectedSize.getWidth() + "x" + selectedSize.getHeight());
            } else {
                Log.e(TAG, "No valid resolution found.");
                return;
            }

            // Open the camera
            manager.openCamera(cameraId, new CameraDevice.StateCallback() {
                @Override
                public void onOpened(CameraDevice camera) {
                    Log.d(TAG, "Camera opened");
                    cameraDevice = camera;

                    try {
                        imageReader = ImageReader.newInstance(selectedSize.getWidth(), selectedSize.getHeight(), ImageFormat.YUV_420_888, 2);
                        imageReader.setOnImageAvailableListener(imageListener, backgroundHandler);
                        CaptureRequest.Builder builder = cameraDevice.createCaptureRequest(CameraDevice.TEMPLATE_PREVIEW);
                        builder.addTarget(imageReader.getSurface());

                        // Set the crop region (if needed, e.g., for sensor alignment)
                        Rect cropRegion = builder.get(CaptureRequest.SCALER_CROP_REGION);
                        builder.set(CaptureRequest.DISTORTION_CORRECTION_MODE, CaptureRequest.DISTORTION_CORRECTION_MODE_OFF);
                        builder.set(CaptureRequest.SCALER_CROP_REGION, cropRegion);

                        // Set control mode and exposure
                        builder.set(CaptureRequest.CONTROL_MODE, CaptureRequest.CONTROL_MODE_AUTO);

                        // Autofocus settings
                        builder.set(CaptureRequest.CONTROL_AF_MODE, CaptureRequest.CONTROL_AF_MODE_CONTINUOUS_PICTURE);

                        // AE FPS range settings (set to 30fps for instance)
                        builder.set(CaptureRequest.CONTROL_AE_TARGET_FPS_RANGE, new Range<>(30, 30));

                        // Create the capture session
                        cameraDevice.createCaptureSession(Collections.singletonList(imageReader.getSurface()),
                            new CameraCaptureSession.StateCallback() {
                                @Override
                                public void onConfigured(CameraCaptureSession session) {
                                    Log.d(TAG, "Capture session configured");
                                    captureSession = session;
                                    try {
                                        // Set the repeating capture request to start preview
                                        captureSession.setRepeatingRequest(builder.build(), null, backgroundHandler);
                                        Log.d(TAG, "Started camera preview");
                                    } catch (CameraAccessException e) {
                                        Log.e(TAG, "Failed to start camera preview: " + e.getMessage());
                                    }
                                }

                                @Override
                                public void onConfigureFailed(CameraCaptureSession session) {
                                    Log.e(TAG, "Camera configuration failed");
                                }
                            }, backgroundHandler);

                    } catch (CameraAccessException e) {
                        Log.e(TAG, "Failed to configure camera: " + e.getMessage());
                    }
                }

                @Override
                public void onDisconnected(CameraDevice camera) {
                    Log.e(TAG, "Camera disconnected");
                    camera.close();
                }

                @Override
                public void onError(CameraDevice camera, int error) {
                    Log.e(TAG, "Camera error: " + error);
                    camera.close();
                }
            }, backgroundHandler);

        } catch (CameraAccessException e) {
            Log.e(TAG, "Camera access exception: " + e.getMessage());
        } catch (RuntimeException e) {
            Log.e(TAG, "Runtime exception: " + e.getMessage());
        }
    }
    private static float[] getSensorAspectRatio(CameraCharacteristics cameraCharacteristics)
    {
            Rect sensorArraySize = cameraCharacteristics.get(CameraCharacteristics.SENSOR_INFO_ACTIVE_ARRAY_SIZE);
            android.util.SizeF sensorSize = cameraCharacteristics.get(CameraCharacteristics.SENSOR_INFO_PHYSICAL_SIZE);

            if (sensorSize == null)
            {
                return new float[]{sensorArraySize.width(), sensorArraySize.height()};
            }
            else
            {
                return new float[]{sensorSize.getWidth(), sensorSize.getHeight()};
            }
    }
    // This function suppose to return the FOV, aspect-ratio-width and aspect-ratio-height
    // And it does that. 
    public static float[] initAndGetCameraParameters(String cameraID)
    {
        Log.d(TAG, "AndroidCameraPlugin initAndGetCameraParameters called");    
        Activity activity = UnityPlayer.currentActivity;

        // Camera Manager Initialization
        CameraManager cameraManager = (CameraManager) activity.getSystemService(Context.CAMERA_SERVICE);
        if (cameraManager == null) {
            // Camera not available
            Log.e(TAG, "Camera not available");
            return new float[]{0.0f, 0.0f, 0.0f};
        }

        try {
            int device_rotation = activity.getWindowManager().getDefaultDisplay().getRotation();
            int rotationDegrees;
            switch (device_rotation) {
                case Surface.ROTATION_0: rotationDegrees = 0; break;
                case Surface.ROTATION_90: rotationDegrees = 90; break;
                case Surface.ROTATION_180: rotationDegrees = 180; break;
                case Surface.ROTATION_270: rotationDegrees = 270; break;
                default: rotationDegrees = 0; break;
            }
            CameraCharacteristics cameraCharacteristics = cameraManager.getCameraCharacteristics(cameraID);
            int sensorOrientation = cameraCharacteristics.get(CameraCharacteristics.SENSOR_ORIENTATION);
            rotationCompensation = (rotationDegrees+sensorOrientation)%360;

            Log.d(TAG, "device rotation= " + rotationDegrees + ", sensor orientation= " + sensorOrientation + ", compensation= " + rotationCompensation);

            float[] sensorWidthAndHeight = getSensorAspectRatio(cameraCharacteristics);

            // Get a resolution that matches the sensor's aspect ratio
            Size selectedSize = getBestResolution(cameraCharacteristics, sensorWidthAndHeight);

            // Calculate FOV
            float[] focalLength = cameraCharacteristics.get(CameraCharacteristics.LENS_INFO_AVAILABLE_FOCAL_LENGTHS);
            float horizontalAngle = (float) ((2.0f * Math.atan((sensorWidthAndHeight[0] / (focalLength[0] * 2.0f)))) * 180.0 / Math.PI);
            float verticalAngle = (float) ((2.0f * Math.atan((sensorWidthAndHeight[1] / (focalLength[0] * 2.0f)))) * 180.0 / Math.PI);
            float diagonalAngle = (float)Math.toDegrees((float)2.0f*Math.atan2(Math.sqrt(sensorWidthAndHeight[0]*sensorWidthAndHeight[0] + sensorWidthAndHeight[1]*sensorWidthAndHeight[1])/2.0f, focalLength[0]));

            Log.d(TAG, "hfov= " + horizontalAngle + ", vfov= " + verticalAngle + ", dfov= " + diagonalAngle + "aspect ratio= " + (sensorWidthAndHeight[0] / sensorWidthAndHeight[1]) + " focal_length= " + focalLength[0] + " sensor array=" + sensorWidthAndHeight[0] + "x" + sensorWidthAndHeight[1]);
            
            if (rotationCompensation == 90 || rotationCompensation == 270)
            {
                return new float[]{diagonalAngle, selectedSize.getHeight(), selectedSize.getWidth()};
            }
            return new float[]{diagonalAngle, selectedSize.getWidth(), selectedSize.getHeight()};            

        } catch (CameraAccessException e) {
            e.printStackTrace();
            return new float[] {0.0f, 0.0f, 0.0f};
        }
    }

    private static String getFrontCameraId(CameraManager manager) throws CameraAccessException {
        for (String cameraId : manager.getCameraIdList()) {
            CameraCharacteristics characteristics = manager.getCameraCharacteristics(cameraId);
            Integer facing = characteristics.get(CameraCharacteristics.LENS_FACING);
            if (facing != null && facing == CameraCharacteristics.LENS_FACING_FRONT) {
                return cameraId;
            }
        }
        throw new RuntimeException("Front camera not found");
    }

    public static String[] getAllFrontFacingCameraIds() {
        Activity activity = UnityPlayer.currentActivity;
        CameraManager manager = (CameraManager) activity.getSystemService(Context.CAMERA_SERVICE);

        try {
            String[] cameraIdList = manager.getCameraIdList();
            ArrayList<String> frontCameraIds = new ArrayList<>();

            for (String cameraId : cameraIdList) {
                CameraCharacteristics characteristics = manager.getCameraCharacteristics(cameraId);
                Integer facing = characteristics.get(CameraCharacteristics.LENS_FACING);
                if (facing != null && facing == CameraCharacteristics.LENS_FACING_FRONT) {
                    frontCameraIds.add(cameraId);
                }
            }

            return frontCameraIds.toArray(new String[0]);

        } catch (CameraAccessException e) {
            Log.e(TAG, "Error accessing camera: " + e.getMessage());
            return new String[0]; // Return empty array if there's an error
        }
    }

    public static String[] getAllFrontAndBackCameraIds() {
        Activity activity = UnityPlayer.currentActivity;
        CameraManager manager = (CameraManager) activity.getSystemService(Context.CAMERA_SERVICE);

        try {
            String[] cameraIdList = manager.getCameraIdList();
            ArrayList<String> cameraIds = new ArrayList<>();

            for (String cameraId : cameraIdList) {
                CameraCharacteristics characteristics = manager.getCameraCharacteristics(cameraId);
                Integer facing = characteristics.get(CameraCharacteristics.LENS_FACING);
                if (facing != null &&
                        (facing == CameraCharacteristics.LENS_FACING_FRONT ||
                         facing == CameraCharacteristics.LENS_FACING_BACK)) {
                    cameraIds.add(cameraId);
                }
            }

            return cameraIds.toArray(new String[0]);

        } catch (CameraAccessException e) {
            Log.e(TAG, "Error accessing camera: " + e.getMessage());
            return new String[0];
        }
    }

    public static String[] getAllCameraIds() 
    {
        Activity activity = UnityPlayer.currentActivity;
        CameraManager manager = (CameraManager) activity.getSystemService(Context.CAMERA_SERVICE);

        try {
            return manager.getCameraIdList();
        } catch (CameraAccessException e) {
            Log.e(TAG, "Error accessing camera: " + e.getMessage());
            return new String[0];
        }
    }
/*
    public static String[] getAllCameraIds() {
        Activity activity = UnityPlayer.currentActivity;
        CameraManager manager = (CameraManager) activity.getSystemService(Context.CAMERA_SERVICE);
        try {
            String[] cameraIdList = manager.getCameraIdList();
            ArrayList<String> allCameraIds = new ArrayList<>();

            for (String cameraId : cameraIdList)
            {
                allCameraIds.add(cameraId);
            }

            return allCameraIds.toArray(new String[0]);

        } catch (CameraAccessException e) {
            Log.e(TAG, "Error accessing camera: " + e.getMessage());
            return new String[0]; // Return empty array if there's an error
        }
    }
*/
    private static Size getBestResolution(CameraCharacteristics cameraCharacteristics, float[] sensorWidthAndHeight) 
    {
        // default resolution
        Size bestSize = new Size(1920, 1080);
        boolean foundMatch = false;

        StreamConfigurationMap map = cameraCharacteristics.get(SCALER_STREAM_CONFIGURATION_MAP);
        if (map == null) {
            Log.e(TAG, "getBestResolution: StreamConfigurationMap is NULL. Hardware might not support Camera2.");
            return bestSize;
        }

        Size[] availableSizes = map.getOutputSizes(ImageFormat.YUV_420_888);
        if (availableSizes == null || availableSizes.length == 0) {
            Log.e(TAG, "getBestResolution: No available sizes found for YUV_420_888 format.");
            return bestSize;
        }

        // Calculate Sensor Ratio
        double rawSensorRatio = (double) sensorWidthAndHeight[0] / sensorWidthAndHeight[1];
        DecimalFormat df = new DecimalFormat("#.##");
        df.setRoundingMode(RoundingMode.FLOOR);
        String sensor_ratio_str = df.format(rawSensorRatio);

        Log.d(TAG, "getBestResolution: --- Target Search Started ---");
        Log.d(TAG, "getBestResolution: Sensor Dimensions: " + sensorWidthAndHeight[0] + "x" + sensorWidthAndHeight[1]);
        Log.d(TAG, "getBestResolution: Target Sensor Ratio (Raw): " + rawSensorRatio + " (Formatted): " + sensor_ratio_str);

        // Iterate through all available sizes
        for (Size size : availableSizes) {
            double rawImageRatio = (double) size.getWidth() / size.getHeight();
            String image_ratio_str = df.format(rawImageRatio);
        
            boolean widthCondition = size.getWidth() >= 1920;
            boolean ratioCondition = image_ratio_str.equals(sensor_ratio_str);

            // Optional: Verbose log for every size checked (Uncomment if deep debugging is needed)
            // Log.v(TAG, "Checking: " + size.getWidth() + "x" + size.getHeight() + " | Ratio: " + image_ratio_str + " | Match: " + (widthCondition && ratioCondition));

            if (widthCondition && ratioCondition) {
                bestSize = size;
                foundMatch = true;
                Log.i(TAG, "getBestResolution: MATCH FOUND! Selected: " + size.getWidth() + "x" + size.getHeight());
                break;
            }
        }

        if (!foundMatch) {
            Log.w(TAG, "getBestResolution: No resolution >= 1920px matched the sensor ratio (" + sensor_ratio_str + "). Falling back to default: " + bestSize.getWidth() + "x" + bestSize.getHeight());
        }

        return bestSize;
    }

    private static void startBackgroundThread() {
        backgroundThread = new HandlerThread("CameraBackground");
        backgroundThread.start();
        backgroundHandler = new Handler(backgroundThread.getLooper());
    }

    public static void stopCamera() {
        if (captureSession != null) {
            captureSession.close();
            captureSession = null;
        }
        if (cameraDevice != null) {
            cameraDevice.close();
            cameraDevice = null;
        }
        if (imageReader != null) {
            imageReader.close();
            imageReader = null;
        }
        stopBackgroundThread();
    }

    private static void stopBackgroundThread() {
        if (backgroundThread != null) {
            backgroundThread.quitSafely();
            try {
                backgroundThread.join();
                backgroundThread = null;
                backgroundHandler = null;
            } catch (InterruptedException e) {
                Log.e(TAG, "Error stopping background thread: " + e.getMessage());
            }
        }
    }
}