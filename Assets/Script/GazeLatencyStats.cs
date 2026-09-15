using System.Diagnostics;
using System.Threading;

// Counters written from the Tobii callback thread and the main thread; read by GazeDebugOverlay.
public static class GazeLatencyStats
{
    public struct Snapshot
    {
        public long Frames;
        public long FrameTicks;
        public long GazeSamples;
        public long DispatchTicks;
        public long FilterLagCount;
        public double FilterLagSum;
        public long SpreadCount;
        public double SpreadSum;
    }

    private static long _frames;
    private static long _frameTicks;
    private static long _gazeSamples;
    private static long _dispatchTicks;
    private static long _filterLagCount;
    private static double _filterLagSum;
    private static long _spreadCount;
    private static double _spreadSum;

    public static long Now()
    {
        return Stopwatch.GetTimestamp();
    }

    public static void RecordFrameProcessed(long startTimestamp)
    {
        Interlocked.Increment(ref _frames);
        Interlocked.Add(ref _frameTicks, Stopwatch.GetTimestamp() - startTimestamp);
    }

    public static void RecordGazeDispatched(long enqueueTimestamp)
    {
        Interlocked.Increment(ref _gazeSamples);
        Interlocked.Add(ref _dispatchTicks, Stopwatch.GetTimestamp() - enqueueTimestamp);
    }

    // Main thread only.
    public static void RecordFilterLag(float pixels)
    {
        _filterLagCount++;
        _filterLagSum += pixels;
    }

    // Main thread only.
    public static void RecordFixationSpread(float pixels)
    {
        _spreadCount++;
        _spreadSum += pixels;
    }

    public static Snapshot Take()
    {
        return new Snapshot
        {
            Frames = Interlocked.Read(ref _frames),
            FrameTicks = Interlocked.Read(ref _frameTicks),
            GazeSamples = Interlocked.Read(ref _gazeSamples),
            DispatchTicks = Interlocked.Read(ref _dispatchTicks),
            FilterLagCount = _filterLagCount,
            FilterLagSum = _filterLagSum,
            SpreadCount = _spreadCount,
            SpreadSum = _spreadSum
        };
    }

    public static double TicksToMilliseconds(long ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }
}
