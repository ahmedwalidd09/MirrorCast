using System.Diagnostics;

namespace MirrorCast.Receiver.Video;

/// <summary>
/// Thread-safe frame rate tracker providing real-time instantaneous and rolling-average FPS metrics.
/// </summary>
public class FpsTracker
{
    private readonly Queue<long> _frameTimestamps = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly double _windowDurationSeconds;
    private readonly object _lock = new();

    /// <summary>
    /// Event raised periodically when the measured frame rate updates.
    /// </summary>
    public event Action<double>? FpsUpdated;

    /// <summary>
    /// Gets the current measured rolling-average frame rate.
    /// </summary>
    public double CurrentFps { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FpsTracker"/> class.
    /// </summary>
    /// <param name="windowDurationSeconds">Sliding window duration in seconds (default 1.0s).</param>
    public FpsTracker(double windowDurationSeconds = 1.0)
    {
        _windowDurationSeconds = Math.Max(0.2, windowDurationSeconds);
    }

    /// <summary>
    /// Records a newly rendered or decoded frame and calculates the updated frame rate.
    /// </summary>
    public void RecordFrame()
    {
        long nowTicks = _stopwatch.ElapsedTicks;
        double currentFps;

        lock (_lock)
        {
            _frameTimestamps.Enqueue(nowTicks);

            long cutoff = nowTicks - (long)(_windowDurationSeconds * Stopwatch.Frequency);
            while (_frameTimestamps.Count > 0 && _frameTimestamps.Peek() < cutoff)
            {
                _frameTimestamps.Dequeue();
            }

            int count = _frameTimestamps.Count;
            currentFps = count / _windowDurationSeconds;
            CurrentFps = currentFps;
        }

        FpsUpdated?.Invoke(currentFps);
    }

    /// <summary>
    /// Resets all frame metrics.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _frameTimestamps.Clear();
            CurrentFps = 0;
        }
        FpsUpdated?.Invoke(0);
    }
}
