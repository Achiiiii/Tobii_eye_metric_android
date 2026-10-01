using System;
using System.Runtime.InteropServices;
using System.Threading;
using Tobii.MediaCaptureClientLib;
using Tobii.StreamEngine;

// Runs tobii_process_frame on its own thread. On the robot one frame takes 80-110 ms of CPU, and on
// the main thread that capped rendering - the dot, the dwell ring, touch - at the gaze rate of about
// 10 Hz. The capture client keeps polling the camera on the main thread (JNI) and hands each frame
// here; only the newest frame is kept, so a slow frame never builds a backlog of stale ones.
//
// Gaze and head pose callbacks fire inside tobii_process_frame, so they now arrive on this thread;
// StreamEngineDevice already queues them under a lock for its main-thread Update. Calibration calls
// were already made from thread-pool tasks concurrently with frame processing in the Tobii sample,
// so they are deliberately not serialised against this thread.
public sealed class GazeFrameWorker : IDisposable
{
    // Frames replaced in the mailbox before the worker took them, i.e. skipped to stay current.
    public static long Dropped;

    private readonly StreamEngineDevice _device;
    private readonly Thread _thread;
    private readonly AutoResetEvent _frameReady = new AutoResetEvent(false);
    private readonly object _mailboxLock = new object();
    private volatile bool _running = true;

    private sbyte[] _pending;
    private int _pendingWidth;
    private int _pendingHeight;
    private long _pendingTimestampUs;

    public GazeFrameWorker(StreamEngineDevice device)
    {
        _device = device;
        _thread = new Thread(Run) { Name = "TobiiFrameWorker", IsBackground = true };
        _thread.Start();
    }

    // Called on the main thread. The worker takes ownership of the array.
    public void Submit(sbyte[] gray8, int width, int height, long timestampUs)
    {
        lock (_mailboxLock)
        {
            if (_pending != null)
                Interlocked.Increment(ref Dropped);
            _pending = gray8;
            _pendingWidth = width;
            _pendingHeight = height;
            _pendingTimestampUs = timestampUs;
        }
        _frameReady.Set();
    }

    private void Run()
    {
        while (_running)
        {
            _frameReady.WaitOne(250);
            if (!_running)
                break;

            sbyte[] data;
            int width, height;
            long timestampUs;
            lock (_mailboxLock)
            {
                data = _pending;
                width = _pendingWidth;
                height = _pendingHeight;
                timestampUs = _pendingTimestampUs;
                _pending = null;
            }
            if (data == null)
                continue;

            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                var frame = new tobii_image_frame_t
                {
                    format = 0, //Interop.TOBII_FRAME_FORMAT_GRAY8,
                    width = width,
                    height = height,
                    stride = width,
                    timestamp_us = timestampUs,
                    data_size = new IntPtr(width * height),
                    data = handle.AddrOfPinnedObject()
                };
                _device.ProcessMediaCaptureFrame(frame, mcclient_frame_format_type.MCCLIENT_FRAME_FORMAT_GRAY16);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[GazeFrameWorker] {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                handle.Free();
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _frameReady.Set();
        _thread.Join(1000);
    }
}
