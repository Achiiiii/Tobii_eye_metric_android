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

    // Capture time of the frame in latestImageData, from the camera's own clock (nanoseconds).
    private static long latestImageTimestampNs = 0;

    // Per-frame logging costs more than it is worth once the stream is running: at camera rate it
    // was ~180 logcat lines a second. Flip this on only while debugging the capture path.
    private static final boolean VERBOSE_FRAME_LOG = false;

    // Eye tracking does not need many megapixels, and the frame is copied twice per delivery.
    // Prefer the smallest stream at or above MIN_IMAGE_WIDTH; if the camera offers nothing that
    // small at the sensor's aspect ratio, box-average by an integer factor down to TARGET_MAX_WIDTH.
    private static final int MIN_IMAGE_WIDTH = 640;
    private static final int TARGET_MAX_WIDTH = 1280;
    // Frames handed to Tobii are at least this wide. 1050x780 measured a third less gaze error at
    // the centre symbol than 700x520 (35 vs 52 px median, 2026-10-01) for ~20 ms more inference.
    private static final int MIN_TRACKING_WIDTH = 1000;
    private static int downsampleFactor = 1;
    private static int streamWidth = 0;
    // Inference keeps up with about 10 frames a second, so at 30 fps two of every three frames
    // were captured, downsampled and copied only to be skipped - while the robot ran at 81-85 C
    // with its CPUs throttled to 1.6-1.75 of 2.0-2.2 GHz. Ask for 15 fps when offered.
    private static final int TARGET_FPS = 15;
    private static Range<Integer> fpsRange = new Range<>(30, 30);
    private static int streamHeight = 0;

    // Switches the frames handed to Tobii between sizes cut from the same stream, e.g. 2100x1560
    // by 3 (700x520) or by 2 (1050x780). The next frame has a new size, which makes the app
    // create a new Tobii processor and so drops calibration: switch only between sessions.
    public static boolean setDownsampleFactor(int factor) {
        if (factor < 1 || streamWidth == 0 || streamWidth % factor != 0 || streamHeight % factor != 0) {
            Log.w(TAG, "[RES] downsample " + factor + " does not divide " + streamWidth + "x" + streamHeight);
            return false;
        }
        downsampleFactor = factor;
        Log.i(TAG, "[RES] downsample now " + factor + " -> " + (streamWidth / factor) + "x" + (streamHeight / factor));
        return true;
    }

    // A fixed range at TARGET_FPS if the camera offers one, else the lowest one reaching it, else 30.
    private static Range<Integer> chooseFpsRange(CameraCharacteristics characteristics) {
        Range<Integer>[] ranges = characteristics.get(CameraCharacteristics.CONTROL_AE_AVAILABLE_TARGET_FPS_RANGES);
        Range<Integer> best = null;
        StringBuilder all = new StringBuilder();
        if (ranges != null) {
            for (Range<Integer> range : ranges) {
                all.append(range).append(' ');
                if (range.getUpper() < TARGET_FPS)
                    continue;
                boolean fixed = range.getLower().equals(range.getUpper());
                if (best == null
                        || range.getUpper() < best.getUpper()
                        || (range.getUpper().equals(best.getUpper()) && fixed && !best.getLower().equals(best.getUpper())))
                    best = range;
            }
        }
        if (best == null)
            best = new Range<>(30, 30);
        Log.i(TAG, "[RES] fps ranges: " + all + "-> using " + best);
        return best;
    }

    public static int getDownsampleFactor() {
        return downsampleFactor;
    }

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

    // Counters for the periodic throughput line: how often Unity polls vs how many frames it took.
    private static int getLatestCallCount = 0;
    private static int frameDeliveredCount = 0;

    public static Object[] getLatestImageData() {
        synchronized (imageLock) {
            getLatestCallCount++;
            // One line every few seconds is enough to see the poll rate and the drop rate.
            if (getLatestCallCount % 300 == 0) {
                Log.i(TAG, "[RATE] polls=" + getLatestCallCount + " delivered=" + frameDeliveredCount
                        + " camera=" + rawFrameCount + " size=" + latestImageWidth + "x" + latestImageHeight);
            }
            if (newFrameAvailable) {
                frameDeliveredCount++;
                if (VERBOSE_FRAME_LOG) {
                    Log.d(TAG, "[DEBUG] Delivering frame #" + frameDeliveredCount + " size=" + latestImageData.length + " (" + latestImageWidth + "x" + latestImageHeight + ")");
                }
                newFrameAvailable = false;
                int[] widthAndHeight = new int[]{latestImageWidth, latestImageHeight};
                long[] timestampNs = new long[]{latestImageTimestampNs};
                return new Object[]{latestImageData, widthAndHeight, timestampNs};
            }
            else{
                return null;
            }
        }
    }

    // Copies one grayscale plane out of the capture buffer, dropping any row padding.
    private static byte[] readPlanePacked(ByteBuffer buffer, int width, int height, int stride) {
        byte[] out = new byte[width * height];
        if (stride == width) {
            buffer.get(out, 0, Math.min(out.length, buffer.remaining()));
            return out;
        }
        byte[] row = new byte[stride];
        for (int y = 0; y < height && buffer.remaining() > 0; y++) {
            int toRead = Math.min(stride, buffer.remaining());
            buffer.get(row, 0, toRead);
            System.arraycopy(row, 0, out, y * width, Math.min(width, toRead));
        }
        return out;
    }

    // Box-averages factor x factor blocks. Both dimensions divide evenly by factor (see
    // chooseDownsampleFactor), so the aspect ratio the Tobii processor was built with is preserved.
    private static byte[] downsampleGray(ByteBuffer buffer, int width, int height, int stride, int factor) {
        int outW = width / factor;
        int outH = height / factor;
        byte[] rows = new byte[stride * factor];
        byte[] out = new byte[outW * outH];
        int[] acc = new int[outW];
        int divisor = factor * factor;

        for (int oy = 0; oy < outH; oy++) {
            int toRead = Math.min(rows.length, buffer.remaining());
            if (toRead <= 0)
                break;
            buffer.get(rows, 0, toRead);
            java.util.Arrays.fill(acc, 0);

            for (int dy = 0; dy < factor; dy++) {
                int base = dy * stride;
                if (base + width > toRead)
                    break;
                for (int ox = 0; ox < outW; ox++) {
                    int sx = base + ox * factor;
                    int sum = 0;
                    for (int dx = 0; dx < factor; dx++)
                        sum += rows[sx + dx] & 0xFF;
                    acc[ox] += sum;
                }
            }

            int outBase = oy * outW;
            for (int ox = 0; ox < outW; ox++)
                out[outBase + ox] = (byte)(acc[ox] / divisor);
        }
        return out;
    }

    // Largest factor that divides both dimensions evenly and still leaves the frame wide enough.
    private static int chooseDownsampleFactor(int width, int height) {
        if (width <= TARGET_MAX_WIDTH)
            return 1;
        int best = 1;
        for (int factor = 2; factor <= 8; factor++) {
            if (width % factor != 0 || height % factor != 0)
                continue;
            if (width / factor < MIN_TRACKING_WIDTH)
                continue;
            best = factor;
        }
        return best;
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
                    Log.e(TAG, "acquireLatestImage() returned null - camera may have dropped frame");
                    return;
                }

                rawFrameCount++;

                int imgWidth = image.getWidth();
                int imgHeight = image.getHeight();
                long captureTimestampNs = image.getTimestamp();

                Image.Plane[] planes = image.getPlanes();
                if (planes == null || planes.length == 0) {
                    Log.e(TAG, "Image planes are null or empty!");
                    return;
                }
                ByteBuffer buffer = planes[0].getBuffer();
                int stride = planes[0].getRowStride();

                // Read (and optionally shrink) the plane before taking the lock. Both paths return a
                // tightly packed buffer, so from here on the row stride equals the width.
                int factor = downsampleFactor;
                byte[] packed = factor > 1
                        ? downsampleGray(buffer, imgWidth, imgHeight, stride, factor)
                        : readPlanePacked(buffer, imgWidth, imgHeight, stride);
                int packedWidth = imgWidth / factor;
                int packedHeight = imgHeight / factor;

                if (VERBOSE_FRAME_LOG) {
                    Log.d(TAG, "[DEBUG] onImageAvailable: raw=" + imgWidth + "x" + imgHeight
                            + " stride=" + stride + " factor=" + factor
                            + " -> " + packedWidth + "x" + packedHeight + " (" + packed.length + " bytes)");
                }

                synchronized (imageLock) {
                    byte[] processedData;
                    int finalWidth = packedWidth;
                    int finalHeight = packedHeight;

                    if (rotationCompensation == 90) {
                        processedData = rotateGrayscale90(packed, packedWidth, packedHeight, packedWidth);
                        finalWidth = packedHeight;
                        finalHeight = packedWidth;
                    } else if (rotationCompensation == 270) {
                        processedData = rotateGrayscale270(packed, packedWidth, packedHeight, packedWidth);
                        finalWidth = packedHeight;
                        finalHeight = packedWidth;
                    } else if (rotationCompensation == 180) {
                        processedData = rotateGrayscale180(packed, packedWidth, packedHeight, packedWidth);
                    } else {
                        processedData = packed;
                    }

                    latestImageData = processedData;
                    latestImageWidth = finalWidth;
                    latestImageHeight = finalHeight;
                    latestImageTimestampNs = captureTimestampNs;
                    newFrameAvailable = true;
                }

            } catch (Exception e) {
                Log.e(TAG, "Error in onImageAvailable: " + e.getMessage());
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
            fpsRange = chooseFpsRange(cameraCharacteristics);
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

                streamWidth = selectedSize.getWidth();
                streamHeight = selectedSize.getHeight();
                downsampleFactor = chooseDownsampleFactor(selectedSize.getWidth(), selectedSize.getHeight());
                Log.i(TAG, "[RES] stream " + selectedSize.getWidth() + "x" + selectedSize.getHeight()
                        + " downsample=" + downsampleFactor
                        + " -> " + (selectedSize.getWidth() / downsampleFactor)
                        + "x" + (selectedSize.getHeight() / downsampleFactor));
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
                        // The reader was already created (and its listener set) before openCamera;
                        // building a second one here dropped the first without closing it.
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
                        builder.set(CaptureRequest.CONTROL_AE_TARGET_FPS_RANGE, fpsRange);

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
        StreamConfigurationMap map = cameraCharacteristics.get(SCALER_STREAM_CONFIGURATION_MAP);
        assert map != null;
        Size[] availableSizes = map.getOutputSizes(ImageFormat.YUV_420_888);

        // we keep the first two decimals.. cause in some systems there might be
        // small differences (3rd, 4th decimal etc) and then will not be possible
        // to find a resolution that matches the sensor's ratio.
        DecimalFormat df = new DecimalFormat("#.##");
        df.setRoundingMode(RoundingMode.FLOOR);
        String sensor_ratio_str = df.format((double)sensorWidthAndHeight[0]/sensorWidthAndHeight[1]);

        // Printed once per camera start so the choice below can be checked against what this
        // device actually offers.
        StringBuilder all = new StringBuilder();
        for (Size size : availableSizes) {
            all.append(size.getWidth()).append("x").append(size.getHeight())
               .append("(").append(df.format((double)size.getWidth()/(double)size.getHeight())).append(") ");
        }
        Log.i(TAG, "[RES] sensor ratio=" + sensor_ratio_str + " available YUV sizes: " + all);

        // getOutputSizes() is ordered largest first, so taking the first match above 1920 always
        // picked the sensor's full resolution - 4208x3120 on this robot, 12.5 MB per frame, which
        // left the app running at 7 fps. Take the SMALLEST match instead; the aspect ratio still
        // has to equal the sensor's, because the Tobii processor is built from that ratio and FOV.
        Size bestSize = null;
        for (Size size : availableSizes) {
            if (size.getWidth() < MIN_IMAGE_WIDTH)
                continue;
            if (!df.format((double)size.getWidth()/(double)size.getHeight()).equals(sensor_ratio_str))
                continue;
            if (bestSize == null || size.getWidth() < bestSize.getWidth())
                bestSize = size;
        }

        // Nothing at the sensor's ratio is small enough: keep the smallest one that matches and let
        // chooseDownsampleFactor() shrink it by an integer factor instead.
        if (bestSize == null) {
            for (Size size : availableSizes) {
                if (!df.format((double)size.getWidth()/(double)size.getHeight()).equals(sensor_ratio_str))
                    continue;
                if (bestSize == null || size.getWidth() < bestSize.getWidth())
                    bestSize = size;
            }
        }
        if (bestSize == null)
            bestSize = availableSizes[availableSizes.length - 1];

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