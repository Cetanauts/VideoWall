namespace VideoWall.Core.Sync;

/// <summary>
/// High-precision synchronized clock for multi-machine playback coordination.
/// Uses NTP-style offset calculation to synchronize clocks across machines.
/// </summary>
public class SyncClock
{
    private readonly object _lock = new();
    private long _offsetTicks = 0;
    private readonly List<long> _offsetSamples = new();
    private const int MaxSamples = 10;

    /// <summary>
    /// Current synchronized time (UTC)
    /// </summary>
    public DateTime Now => DateTime.UtcNow.AddTicks(_offsetTicks);

    /// <summary>
    /// Current synchronized time in milliseconds since epoch
    /// </summary>
    public long NowMs => DateTimeToMs(Now);

    /// <summary>
    /// Current offset from local clock in milliseconds
    /// </summary>
    public double OffsetMs => _offsetTicks / (double)TimeSpan.TicksPerMillisecond;

    /// <summary>
    /// Is the clock synchronized?
    /// </summary>
    public bool IsSynchronized => _offsetSamples.Count >= 3;

    /// <summary>
    /// Number of sync samples collected
    /// </summary>
    public int SampleCount => _offsetSamples.Count;

    /// <summary>
    /// Update clock offset based on a round-trip time measurement.
    /// Uses NTP-style calculation: offset = ((t1 - t0) + (t2 - t3)) / 2
    /// </summary>
    /// <param name="localSendTime">Local time when request was sent (t0)</param>
    /// <param name="remoteReceiveTime">Remote time when request was received (t1)</param>
    /// <param name="remoteSendTime">Remote time when response was sent (t2)</param>
    /// <param name="localReceiveTime">Local time when response was received (t3)</param>
    public void UpdateOffset(DateTime localSendTime, DateTime remoteReceiveTime,
                             DateTime remoteSendTime, DateTime localReceiveTime)
    {
        // NTP offset calculation
        long t0 = localSendTime.Ticks;
        long t1 = remoteReceiveTime.Ticks;
        long t2 = remoteSendTime.Ticks;
        long t3 = localReceiveTime.Ticks;

        long offset = ((t1 - t0) + (t2 - t3)) / 2;
        long roundTrip = (t3 - t0) - (t2 - t1);

        // Only accept samples with reasonable round-trip time (< 500ms)
        if (roundTrip < TimeSpan.TicksPerMillisecond * 500)
        {
            AddSample(offset);
        }
    }

    /// <summary>
    /// Update clock offset directly (when master provides authoritative time)
    /// </summary>
    /// <param name="masterTime">The master's current time</param>
    public void UpdateFromMasterTime(DateTime masterTime)
    {
        long localTicks = DateTime.UtcNow.Ticks;
        long offset = masterTime.Ticks - localTicks;
        AddSample(offset);
    }

    /// <summary>
    /// Add an offset sample and recalculate average
    /// </summary>
    private void AddSample(long offsetTicks)
    {
        lock (_lock)
        {
            _offsetSamples.Add(offsetTicks);

            // Keep only recent samples
            while (_offsetSamples.Count > MaxSamples)
            {
                _offsetSamples.RemoveAt(0);
            }

            // Calculate median offset (more robust than average)
            var sorted = _offsetSamples.OrderBy(x => x).ToList();
            int mid = sorted.Count / 2;
            _offsetTicks = sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2
                : sorted[mid];
        }
    }

    /// <summary>
    /// Reset synchronization
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _offsetTicks = 0;
            _offsetSamples.Clear();
        }
    }

    /// <summary>
    /// Calculate delay until a specific synchronized time
    /// </summary>
    /// <param name="targetTime">Target synchronized time</param>
    /// <returns>Milliseconds until target time (negative if in past)</returns>
    public long MsUntil(DateTime targetTime)
    {
        return (targetTime.Ticks - Now.Ticks) / TimeSpan.TicksPerMillisecond;
    }

    /// <summary>
    /// Schedule an action to execute at a specific synchronized time
    /// </summary>
    /// <param name="targetTime">When to execute</param>
    /// <param name="action">Action to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ExecuteAt(DateTime targetTime, Action action, CancellationToken cancellationToken = default)
    {
        long delayMs = MsUntil(targetTime);

        if (delayMs > 0)
        {
            // Use spin-wait for final milliseconds for precision
            if (delayMs > 50)
            {
                await Task.Delay((int)(delayMs - 20), cancellationToken);
            }

            // Spin-wait for remaining time
            while (Now < targetTime && !cancellationToken.IsCancellationRequested)
            {
                Thread.SpinWait(100);
            }
        }

        if (!cancellationToken.IsCancellationRequested)
        {
            action();
        }
    }

    /// <summary>
    /// Convert DateTime to milliseconds since Unix epoch
    /// </summary>
    public static long DateTimeToMs(DateTime dt)
    {
        return (dt.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;
    }

    /// <summary>
    /// Convert milliseconds since Unix epoch to DateTime
    /// </summary>
    public static DateTime MsToDateTime(long ms)
    {
        return DateTime.UnixEpoch.AddMilliseconds(ms);
    }
}
