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

package com.tobii;

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
import android.hardware.camera2.CaptureResult;
import android.hardware.camera2.TotalCaptureResult;
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
    private static CaptureRequest.Builder captureRequestBuilder;
    private static long lastAfTriggerTimeMillis = 0;

    public static void triggerAutoFocus() {
        if (captureSession == null || captureRequestBuilder == null || backgroundHandler == null) return;
        try {
            Log.d(TAG, "[AF] Forcing autofocus re-trigger");
            captureRequestBuilder.set(CaptureRequest.CONTROL_AF_TRIGGER, CaptureRequest.CONTROL_AF_TRIGGER_CANCEL);
            captureRequestBuilder.set(CaptureRequest.CONTROL_AF_MODE, CaptureRequest.CONTROL_AF_MODE_OFF);
            captureSession.capture(captureRequestBuilder.build(), null, backgroundHandler);

            captureRequestBuilder.set(CaptureRequest.CONTROL_AF_MODE, CaptureRequest.CONTROL_AF_MODE_CONTINUOUS_VIDEO);
            captureRequestBuilder.set(CaptureRequest.CONTROL_AF_TRIGGER, CaptureRequest.CONTROL_AF_TRIGGER_START);
            captureSession.capture(captureRequestBuilder.build(), null, backgroundHandler);

            captureRequestBuilder.set(CaptureRequest.CONTROL_AF_TRIGGER, CaptureRequest.CONTROL_AF_TRIGGER_IDLE);
            captureSession.setRepeatingRequest(captureRequestBuilder.build(), afCaptureCallback, backgroundHandler);
        } catch (CameraAccessException e) {
            Log.e(TAG, "[AF] Failed to re-trigger autofocus: " + e.getMessage());
        } catch (Exception e) {
            Log.e(TAG, "[AF] Exception in triggerAutoFocus: " + e.getMessage());
        }
    }

    // CaptureCallback to monitor AF state and re-trigger autofocus when focus is lost
    private static CameraCaptureSession.CaptureCallback afCaptureCallback = new CameraCaptureSession.CaptureCallback() {
        @Override
        public void onCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result) {
            super.onCaptureCompleted(session, request, result);
            Integer afState = result.get(CaptureResult.CONTROL_AF_STATE);
            if (afState == null) return;

            // If AF reports that it failed to focus or became inactive, re-trigger
            if (afState == CaptureResult.CONTROL_AF_STATE_NOT_FOCUSED_LOCKED
                    || afState == CaptureResult.CONTROL_AF_STATE_INACTIVE
                    || afState == CaptureResult.CONTROL_AF_STATE_PASSIVE_UNFOCUSED) {
                long currentTime = System.currentTimeMillis();
                if (currentTime - lastAfTriggerTimeMillis > 2000) {
                    lastAfTriggerTimeMillis = currentTime;
                    Log.d(TAG, "[AF] Focus lost or inactive (state=" + afState + "), re-triggering AF");
                    triggerAutoFocus();
                }
            }
        }
    };

    public static void registerConfigurationChangeListener() {
        // Get the current Unity Activity
        Activity unityActivity = UnityPlayer.currentActivity;

        if (unityActivity != null) {
            // Register a listener for configuration changes
            unityActivity.registerComponentCallbacks(new ComponentCallbacks2() {
                @Override
                public void onConfigurationChanged(Configuration newConfig) {
                    Log.d(TAG, "Configuration Changed! " + newConfig.orientation);
                    if (selectedCameraId == null || selectedCameraId.isEmpty())
                    {
                        return;
                    }
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
                        synchronized (imageLock) {
                            rotationCompensation = (rotationDegrees+sensorOrientation)%360;
                            Log.d(TAG, "Configuration Changed! " + rotationCompensation);
                        }
                    }
                    catch (CameraAccessException e) {
                        Log.e(TAG, "Camera access exception: " + e.getMessage());
                    }
                    // Send the orientation change event to Unity
                    //UnityPlayer.UnitySendMessage("GameManager", "OnOrientationChanged", orientation);
                }
                @Override
                public void onLowMemory() {
                    // Handle low memory if needed
                }

                @Override
                public void onTrimMemory(int level) {
                    // Handle memory trimming if needed
                }
            });
        } else {
            Log.e(TAG, "Unity Activity is null.");
        }
    }

    // [DEBUG] Counter to check how often getLatestImageData is called
    private static int getLatestCallCount = 0;
    private static int frameDeliveredCount = 0;

    public static Object[] getLatestImageData() {
        synchronized (imageLock) {
            getLatestCallCount++;
            // Log every 60 calls (~every 2 sec at 30fps polling) to avoid spam
            if (getLatestCallCount % 60 == 0) {
                Log.d(TAG, "[DEBUG] getLatestImageData called " + getLatestCallCount + " times, frames delivered: " + frameDeliveredCount + ", newFrameAvailable=" + newFrameAvailable);
            }
            if (newFrameAvailable) {
                frameDeliveredCount++;
                Log.d(TAG, "[DEBUG] Delivering frame #" + frameDeliveredCount + " size=" + latestImageData.length + " (" + latestImageWidth + "x" + latestImageHeight + ")");
                newFrameAvailable = false;
                int[] widthAndHeight = new int[]{latestImageWidth, latestImageHeight};
                return new Object[]{latestImageData, widthAndHeight};
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
            if (width >= 0)
                System.arraycopy(original, y * stride + 0, strideless, y * width + 0, width);
        }
        return strideless;
    }
    public static byte[] rotateGrayscale90(byte[] original, int width, int height, int stride) {
        byte[] rotated = new byte[width * height];
        int newWidth = height;  // After 90-degree rotation
        int newHeight = width;

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                rotated[x * newWidth + (newWidth - y - 1)] = original[y * stride + x];
            }
        }

        return rotated;
    }
    public static byte[] rotateGrayscale270(byte[] original, int width, int height, int stride) {
        byte[] rotated = new byte[width * height];
        int newWidth = width;  // After 270-degree rotation
        int newHeight = height;

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                rotated[(newWidth - x -1 ) * newHeight + y] = original[y * stride + x];
            }
        }

        return rotated;
    }
    public static byte[] rotateGrayscale180(byte[] original, int width, int height, int stride) {
        byte[] rotated = new byte[width * height];
        int newWidth = width;  // After 90-degree rotation
        int newHeight = height;

        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                rotated[(newHeight - y - 1) * newWidth + (newWidth - x - 1)] = original[y * stride + x];
            }
        }

        return rotated;
    }

    // [DEBUG] Count how many raw frames arrive from the camera
    private static int rawFrameCount = 0;

    private static ImageReader.OnImageAvailableListener imageListener = new ImageReader.OnImageAvailableListener() {
        @Override
        public void onImageAvailable(ImageReader reader) {
            Image image = null;
            try {
                image = reader.acquireLatestImage();
                if (image == null) {
                    Log.e(TAG, "[DEBUG] acquireLatestImage() returned null - camera may have dropped frame");
                    return;
                }

                rawFrameCount++;
                // [DEBUG] Log every 30 raw frames (~1 sec) to confirm camera is actually sending data
                if (rawFrameCount % 30 == 0) {
                    Log.d(TAG, "[DEBUG] Raw frames received from camera: " + rawFrameCount);
                }

                // Get image size
                int imgWidth = image.getWidth();
                int imgHeight = image.getHeight();
                Log.d(TAG, "[DEBUG] onImageAvailable: rawSize=" + imgWidth + "x" + imgHeight + " planes=" + image.getPlanes().length + " rotation=" + rotationCompensation);

                Image.Plane[] planes = image.getPlanes();
                if (planes == null || planes.length == 0) {
                    Log.e(TAG, "[DEBUG] Image planes are null or empty!");
                    return;
                }
                ByteBuffer buffer = planes[0].getBuffer();
                int stride = planes[0].getRowStride();
                int bufferCapacity = buffer.remaining();
                Log.d(TAG, "[DEBUG] Y-plane: stride=" + stride + " bufferCapacity=" + bufferCapacity + " expected=" + (imgWidth * imgHeight));

                // FIX: Read raw bytes from buffer FIRST, then rotate.
                // Previously the code rotated old (stale) data before reading the new buffer.
                byte[] rawBytes = new byte[bufferCapacity];
                buffer.get(rawBytes);

                synchronized (imageLock) {
                    byte[] processedData;
                    int finalWidth = imgWidth;
                    int finalHeight = imgHeight;

                    if (rotationCompensation == 90) {
                        Log.d(TAG, "[DEBUG] Applying 90-degree rotation");
                        processedData = rotateGrayscale90(rawBytes, imgWidth, imgHeight, stride);
                        finalWidth = imgHeight;
                        finalHeight = imgWidth;
                    } else if (rotationCompensation == 270) {
                        Log.d(TAG, "[DEBUG] Applying 270-degree rotation");
                        processedData = rotateGrayscale270(rawBytes, imgWidth, imgHeight, stride);
                        finalWidth = imgHeight;
                        finalHeight = imgWidth;
                    } else if (rotationCompensation == 180) {
                        Log.d(TAG, "[DEBUG] Applying 180-degree rotation");
                        processedData = rotateGrayscale180(rawBytes, imgWidth, imgHeight, stride);
                        finalWidth = imgWidth;
                        finalHeight = imgHeight;
                    } else {
                        // 0 degrees - just remove stride padding if any
                        if (stride != imgWidth) {
                            Log.d(TAG, "[DEBUG] Removing stride padding (stride=" + stride + " width=" + imgWidth + ")");
                            processedData = removeStride(rawBytes, imgWidth, imgHeight, stride);
                        } else {
                            processedData = rawBytes;
                        }
                        finalWidth = imgWidth;
                        finalHeight = imgHeight;
                    }

                    latestImageData = processedData;
                    latestImageWidth = finalWidth;
                    latestImageHeight = finalHeight;
                    newFrameAvailable = true;

                    Log.d(TAG, "[DEBUG] Frame processed and stored: " + latestImageWidth + "x" + latestImageHeight + " dataLen=" + latestImageData.length);
                }

            } catch (Exception e) {
                Log.e(TAG, "[DEBUG] Error in onImageAvailable: " + e.getMessage());
                e.printStackTrace();
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

                        // Autofocus settings - use CONTINUOUS_VIDEO for more aggressive continuous refocusing
                        builder.set(CaptureRequest.CONTROL_AF_MODE, CaptureRequest.CONTROL_AF_MODE_CONTINUOUS_VIDEO);

                        // Store builder reference for AF re-trigger callback
                        captureRequestBuilder = builder;

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
                                        // Jump-start autofocus
                                        builder.set(CaptureRequest.CONTROL_AF_TRIGGER, CaptureRequest.CONTROL_AF_TRIGGER_START);
                                        captureSession.capture(builder.build(), null, backgroundHandler);

                                        // Set the repeating capture request to start preview (back to IDLE)
                                        builder.set(CaptureRequest.CONTROL_AF_TRIGGER, CaptureRequest.CONTROL_AF_TRIGGER_IDLE);
                                        captureSession.setRepeatingRequest(builder.build(), afCaptureCallback, backgroundHandler);
                                        Log.d(TAG, "Started camera preview with AF monitoring");
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

    // Method to find the best resolution less than or equal to 1920x1080
    private static Size getBestResolution(CameraCharacteristics cameraCharacteristics, float[] sensorWidthAndHeight) {
        // default resolution
        Size bestSize = new Size(1920, 1080);

        StreamConfigurationMap map = cameraCharacteristics.get(SCALER_STREAM_CONFIGURATION_MAP);
        assert map != null;
        // Get available output sizes for YUV_420_888
        Size[] availableSizes = map.getOutputSizes(ImageFormat.YUV_420_888);            

        // we keep the first two decimals.. cause in some systems there might be
        // small differences (3rd, 4th decimal etc) and then will not be possible
        // to find a resolution that matches the sensor's ratio.    
        DecimalFormat df = new DecimalFormat("#.##");
        df.setRoundingMode(RoundingMode.FLOOR);
        String sensor_ratio_str = df.format((double)sensorWidthAndHeight[0]/sensorWidthAndHeight[1]);

        // Iterate through all available sizes
        for (Size size : availableSizes) {
            String image_ratio_str = df.format((double)size.getWidth()/(double)size.getHeight());
            if (size.getWidth() >= 1920 )
            {
                if (image_ratio_str.equals(sensor_ratio_str))
                {
                    bestSize = size;
                    break;
                }
            }
        }
        
        return bestSize; // Returns the best matching size or null if none found
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