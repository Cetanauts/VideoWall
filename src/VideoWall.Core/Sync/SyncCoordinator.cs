namespace VideoWall.Core.Sync;

/// <summary>
/// Coordinates synchronized playback across multiple outputs and machines.
/// </summary>
public class SyncCoordinator
{
    private readonly SyncClock _clock;
    private readonly Dictionary<string, SyncGroup> _groups = new();
    private readonly object _lock = new();

    /// <summary>
    /// Event fired when a sync group is ready to execute
    /// </summary>
    public event EventHandler<SyncExecuteEventArgs>? SyncExecute;

    public SyncCoordinator(SyncClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Create a new sync group
    /// </summary>
    /// <param name="groupId">Unique group identifier</param>
    /// <returns>The created sync group</returns>
    public SyncGroup CreateGroup(string groupId)
    {
        lock (_lock)
        {
            var group = new SyncGroup(groupId, _clock);
            _groups[groupId] = group;
            return group;
        }
    }

    /// <summary>
    /// Get an existing sync group
    /// </summary>
    public SyncGroup? GetGroup(string groupId)
    {
        lock (_lock)
        {
            return _groups.TryGetValue(groupId, out var group) ? group : null;
        }
    }

    /// <summary>
    /// Remove a sync group
    /// </summary>
    public void RemoveGroup(string groupId)
    {
        lock (_lock)
        {
            if (_groups.TryGetValue(groupId, out var group))
            {
                group.Cancel();
                _groups.Remove(groupId);
            }
        }
    }

    /// <summary>
    /// Schedule synchronized playback for a group
    /// </summary>
    /// <param name="groupId">Group ID</param>
    /// <param name="executeAt">Synchronized time to execute</param>
    /// <param name="mediaAssignments">Dictionary of outputId -> media path</param>
    /// <param name="startPositionMs">Starting position for all media</param>
    public async Task ScheduleSyncPlayback(
        string groupId,
        DateTime executeAt,
        Dictionary<string, string> mediaAssignments,
        long startPositionMs = 0)
    {
        var group = GetGroup(groupId) ?? CreateGroup(groupId);

        group.SetMediaAssignments(mediaAssignments);
        group.SetStartPosition(startPositionMs);
        group.SetExecuteTime(executeAt);

        // Wait until execute time
        await _clock.ExecuteAt(executeAt, () =>
        {
            SyncExecute?.Invoke(this, new SyncExecuteEventArgs(
                groupId,
                mediaAssignments,
                startPositionMs
            ));
        });
    }

    /// <summary>
    /// Calculate optimal execute time for sync playback.
    /// Adds buffer time to ensure all machines are ready.
    /// </summary>
    /// <param name="bufferMs">Buffer time in milliseconds</param>
    /// <returns>Optimal execute time</returns>
    public DateTime CalculateExecuteTime(int bufferMs = 500)
    {
        return _clock.Now.AddMilliseconds(bufferMs);
    }
}

/// <summary>
/// A group of outputs that should play in sync
/// </summary>
public class SyncGroup
{
    private readonly string _groupId;
    private readonly SyncClock _clock;
    private readonly Dictionary<string, string> _mediaAssignments = new();
    private readonly HashSet<string> _readyOutputs = new();
    private CancellationTokenSource? _cts;

    public string GroupId => _groupId;
    public DateTime? ExecuteTime { get; private set; }
    public long StartPositionMs { get; private set; }
    public SyncGroupState State { get; private set; } = SyncGroupState.Created;
    public int OutputCount => _mediaAssignments.Count;
    public int ReadyCount => _readyOutputs.Count;
    public bool AllReady => _readyOutputs.Count >= _mediaAssignments.Count;

    public SyncGroup(string groupId, SyncClock clock)
    {
        _groupId = groupId;
        _clock = clock;
    }

    public void SetMediaAssignments(Dictionary<string, string> assignments)
    {
        _mediaAssignments.Clear();
        foreach (var (outputId, mediaPath) in assignments)
        {
            _mediaAssignments[outputId] = mediaPath;
        }
        State = SyncGroupState.Preparing;
    }

    public void SetStartPosition(long positionMs)
    {
        StartPositionMs = positionMs;
    }

    public void SetExecuteTime(DateTime executeAt)
    {
        ExecuteTime = executeAt;
        State = SyncGroupState.Scheduled;
    }

    public void MarkOutputReady(string outputId)
    {
        if (_mediaAssignments.ContainsKey(outputId))
        {
            _readyOutputs.Add(outputId);
            if (AllReady)
            {
                State = SyncGroupState.Ready;
            }
        }
    }

    public string? GetMediaForOutput(string outputId)
    {
        return _mediaAssignments.TryGetValue(outputId, out var path) ? path : null;
    }

    public IReadOnlyDictionary<string, string> GetAllAssignments()
    {
        return _mediaAssignments;
    }

    public void Cancel()
    {
        _cts?.Cancel();
        State = SyncGroupState.Cancelled;
    }

    public void Complete()
    {
        State = SyncGroupState.Executed;
    }
}

/// <summary>
/// State of a sync group
/// </summary>
public enum SyncGroupState
{
    Created,
    Preparing,
    Scheduled,
    Ready,
    Executing,
    Executed,
    Cancelled,
    Error
}

/// <summary>
/// Event args for sync execution
/// </summary>
public class SyncExecuteEventArgs : EventArgs
{
    public string GroupId { get; }
    public Dictionary<string, string> MediaAssignments { get; }
    public long StartPositionMs { get; }

    public SyncExecuteEventArgs(string groupId, Dictionary<string, string> mediaAssignments, long startPositionMs)
    {
        GroupId = groupId;
        MediaAssignments = mediaAssignments;
        StartPositionMs = startPositionMs;
    }
}
