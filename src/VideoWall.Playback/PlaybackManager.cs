using LibVLCSharp.Shared;
using VideoWall.Core.Enums;
using VideoWall.Core.Models;

namespace VideoWall.Playback;

/// <summary>
/// Manages multiple video players for all outputs on a machine
/// Handles synchronized playback across outputs
/// </summary>
public class PlaybackManager : IDisposable
{
    private readonly LibVLC _libVLC;
    private readonly Dictionary<string, VideoPlayer> _players = new();
    private readonly Dictionary<string, SlideshowPlayer> _slideshowPlayers = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// LibVLC version string
    /// </summary>
    public string VlcVersion { get; }

    /// <summary>
    /// Is LibVLC initialized?
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Event fired when any player's state changes
    /// </summary>
    public event EventHandler<PlayerStateChangedEventArgs>? PlayerStateChanged;

    public PlaybackManager()
    {
        try
        {
            // Initialize LibVLC
            LibVLCSharp.Shared.Core.Initialize();

            _libVLC = new LibVLC(
                "--no-xlib",
                "--quiet",
                "--no-video-title-show",
                "--network-caching=1000"
            );

            VlcVersion = _libVLC.Version;
            IsInitialized = true;
        }
        catch (Exception ex)
        {
            VlcVersion = "Not Available";
            IsInitialized = false;
            System.Diagnostics.Debug.WriteLine($"LibVLC initialization failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Create or get a video player for an output
    /// </summary>
    public VideoPlayer GetOrCreatePlayer(string outputId)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(outputId, out var existing))
            {
                return existing;
            }

            var player = new VideoPlayer(outputId, _libVLC);
            player.StateChanged += (s, e) =>
            {
                PlayerStateChanged?.Invoke(this, new PlayerStateChangedEventArgs(outputId, e.NewState));
            };

            _players[outputId] = player;
            return player;
        }
    }

    /// <summary>
    /// Get a slideshow player for an output
    /// </summary>
    public SlideshowPlayer GetOrCreateSlideshowPlayer(string outputId)
    {
        lock (_lock)
        {
            if (_slideshowPlayers.TryGetValue(outputId, out var existing))
            {
                return existing;
            }

            var player = new SlideshowPlayer(outputId);
            _slideshowPlayers[outputId] = player;
            return player;
        }
    }

    /// <summary>
    /// Play video on a specific output
    /// </summary>
    public async Task<bool> PlayVideoAsync(string outputId, string mediaPath, bool autoPlay = true)
    {
        var player = GetOrCreatePlayer(outputId);
        return await player.LoadAsync(mediaPath, autoPlay);
    }

    /// <summary>
    /// Play synchronized video on multiple outputs
    /// </summary>
    public async Task PlaySynchronizedAsync(Dictionary<string, string> outputMediaMap, long startPositionMs = 0)
    {
        var loadTasks = new List<Task<bool>>();
        var players = new List<VideoPlayer>();

        // Load all media first (without auto-play)
        foreach (var (outputId, mediaPath) in outputMediaMap)
        {
            var player = GetOrCreatePlayer(outputId);
            players.Add(player);
            loadTasks.Add(player.LoadAsync(mediaPath, autoPlay: false));
        }

        // Wait for all to load
        await Task.WhenAll(loadTasks);

        // Seek all to start position
        foreach (var player in players)
        {
            player.SeekTo(startPositionMs);
        }

        // Start all simultaneously
        var startTime = DateTime.UtcNow.AddMilliseconds(100); // Small delay to sync
        foreach (var player in players)
        {
            player.Play();
        }
    }

    /// <summary>
    /// Stop playback on a specific output
    /// </summary>
    public void Stop(string outputId)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(outputId, out var player))
            {
                player.Stop();
            }
            if (_slideshowPlayers.TryGetValue(outputId, out var ssPlayer))
            {
                ssPlayer.Stop();
            }
        }
    }

    /// <summary>
    /// Stop all playback
    /// </summary>
    public void StopAll()
    {
        lock (_lock)
        {
            foreach (var player in _players.Values)
            {
                player.Stop();
            }
            foreach (var ssPlayer in _slideshowPlayers.Values)
            {
                ssPlayer.Stop();
            }
        }
    }

    /// <summary>
    /// Pause playback on a specific output
    /// </summary>
    public void Pause(string outputId)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(outputId, out var player))
            {
                player.Pause();
            }
            if (_slideshowPlayers.TryGetValue(outputId, out var ssPlayer))
            {
                ssPlayer.Pause();
            }
        }
    }

    /// <summary>
    /// Pause all playback
    /// </summary>
    public void PauseAll()
    {
        lock (_lock)
        {
            foreach (var player in _players.Values)
            {
                player.Pause();
            }
            foreach (var ssPlayer in _slideshowPlayers.Values)
            {
                ssPlayer.Pause();
            }
        }
    }

    /// <summary>
    /// Resume playback on a specific output
    /// </summary>
    public void Resume(string outputId)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(outputId, out var player))
            {
                player.Play();
            }
            if (_slideshowPlayers.TryGetValue(outputId, out var ssPlayer))
            {
                ssPlayer.Resume();
            }
        }
    }

    /// <summary>
    /// Set volume on a specific output
    /// </summary>
    public void SetVolume(string outputId, int volume)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(outputId, out var player))
            {
                player.Volume = volume;
            }
        }
    }

    /// <summary>
    /// Set volume on all outputs
    /// </summary>
    public void SetVolumeAll(int volume)
    {
        lock (_lock)
        {
            foreach (var player in _players.Values)
            {
                player.Volume = volume;
            }
        }
    }

    /// <summary>
    /// Get current state of an output
    /// </summary>
    public PlaybackState GetState(string outputId)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(outputId, out var player))
            {
                return player.State;
            }
            return PlaybackState.Stopped;
        }
    }

    /// <summary>
    /// Get status of all players
    /// </summary>
    public Dictionary<string, PlayerStatus> GetAllStatus()
    {
        var result = new Dictionary<string, PlayerStatus>();

        lock (_lock)
        {
            foreach (var (outputId, player) in _players)
            {
                result[outputId] = new PlayerStatus
                {
                    OutputId = outputId,
                    State = player.State,
                    MediaType = player.CurrentMediaType,
                    MediaPath = player.CurrentMediaPath,
                    PositionMs = player.PositionMs,
                    DurationMs = player.DurationMs,
                    Volume = player.Volume,
                    IsMuted = player.IsMuted
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Remove a player (free resources)
    /// </summary>
    public void RemovePlayer(string outputId)
    {
        lock (_lock)
        {
            if (_players.TryGetValue(outputId, out var player))
            {
                player.Dispose();
                _players.Remove(outputId);
            }
            if (_slideshowPlayers.TryGetValue(outputId, out var ssPlayer))
            {
                ssPlayer.Dispose();
                _slideshowPlayers.Remove(outputId);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        lock (_lock)
        {
            foreach (var player in _players.Values)
            {
                player.Dispose();
            }
            _players.Clear();

            foreach (var ssPlayer in _slideshowPlayers.Values)
            {
                ssPlayer.Dispose();
            }
            _slideshowPlayers.Clear();
        }

        _libVLC.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for player state changes
/// </summary>
public class PlayerStateChangedEventArgs : EventArgs
{
    public string OutputId { get; }
    public PlaybackState NewState { get; }

    public PlayerStateChangedEventArgs(string outputId, PlaybackState newState)
    {
        OutputId = outputId;
        NewState = newState;
    }
}

/// <summary>
/// Status of a player
/// </summary>
public class PlayerStatus
{
    public string OutputId { get; set; } = string.Empty;
    public PlaybackState State { get; set; }
    public Core.Enums.MediaType MediaType { get; set; }
    public string? MediaPath { get; set; }
    public long PositionMs { get; set; }
    public long DurationMs { get; set; }
    public int Volume { get; set; }
    public bool IsMuted { get; set; }
}
