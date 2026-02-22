using System.IO;
using LibVLCSharp.Shared;
using VideoWall.Core.Enums;
using VideoWall.Core.Models;

namespace VideoWall.Playback;

/// <summary>
/// Video player engine using LibVLCSharp
/// Manages playback for a single output
/// </summary>
public class VideoPlayer : IDisposable
{
    private readonly LibVLC _libVLC;
    private readonly MediaPlayer _mediaPlayer;
    private Media? _currentMedia;
    private bool _disposed;

    /// <summary>
    /// Unique identifier for this player (typically OutputId)
    /// </summary>
    public string PlayerId { get; }

    /// <summary>
    /// Current playback state
    /// </summary>
    public PlaybackState State { get; private set; } = PlaybackState.Stopped;

    /// <summary>
    /// Current media type
    /// </summary>
    public Core.Enums.MediaType CurrentMediaType { get; private set; } = Core.Enums.MediaType.None;

    /// <summary>
    /// Current media path
    /// </summary>
    public string? CurrentMediaPath { get; private set; }

    /// <summary>
    /// Current position in milliseconds
    /// </summary>
    public long PositionMs => _mediaPlayer.Time;

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public long DurationMs => _mediaPlayer.Length;

    /// <summary>
    /// Volume (0-100)
    /// </summary>
    public int Volume
    {
        get => _mediaPlayer.Volume;
        set => _mediaPlayer.Volume = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Is audio muted?
    /// </summary>
    public bool IsMuted
    {
        get => _mediaPlayer.Mute;
        set => _mediaPlayer.Mute = value;
    }

    /// <summary>
    /// Loop mode
    /// </summary>
    public LoopMode LoopMode { get; set; } = LoopMode.None;

    /// <summary>
    /// Event fired when playback state changes
    /// </summary>
    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event fired when playback position changes
    /// </summary>
    public event EventHandler<PositionChangedEventArgs>? PositionChanged;

    /// <summary>
    /// Event fired when media ends
    /// </summary>
    public event EventHandler? MediaEnded;

    /// <summary>
    /// Event fired on error
    /// </summary>
    public event EventHandler<PlaybackErrorEventArgs>? Error;

    public VideoPlayer(string playerId, LibVLC? sharedLibVLC = null)
    {
        PlayerId = playerId;

        // Use shared LibVLC instance or create new one
        _libVLC = sharedLibVLC ?? new LibVLC(
            "--no-xlib",
            "--quiet",
            "--no-video-title-show"
        );

        _mediaPlayer = new MediaPlayer(_libVLC);

        // Wire up events
        _mediaPlayer.Playing += (s, e) => SetState(PlaybackState.Playing);
        _mediaPlayer.Paused += (s, e) => SetState(PlaybackState.Paused);
        _mediaPlayer.Stopped += (s, e) => SetState(PlaybackState.Stopped);
        _mediaPlayer.Buffering += (s, e) => SetState(PlaybackState.Buffering);
        _mediaPlayer.EndReached += OnEndReached;
        _mediaPlayer.EncounteredError += (s, e) => OnError("Playback error encountered");
        _mediaPlayer.PositionChanged += (s, e) =>
            PositionChanged?.Invoke(this, new PositionChangedEventArgs(PositionMs, DurationMs));
    }

    /// <summary>
    /// Get the underlying MediaPlayer for WPF VideoView binding
    /// </summary>
    public MediaPlayer GetMediaPlayer() => _mediaPlayer;

    /// <summary>
    /// Load and optionally play a media file
    /// </summary>
    public async Task<bool> LoadAsync(string mediaPath, bool autoPlay = true)
    {
        try
        {
            // Dispose previous media
            _currentMedia?.Dispose();

            // Determine if it's a local file or network path
            Uri mediaUri;
            if (mediaPath.StartsWith("\\\\") || mediaPath.Contains(":/"))
            {
                // UNC path or URL
                mediaUri = new Uri(mediaPath);
            }
            else
            {
                // Local file
                mediaUri = new Uri(Path.GetFullPath(mediaPath));
            }

            _currentMedia = new Media(_libVLC, mediaUri);
            CurrentMediaPath = mediaPath;

            // Determine media type
            var ext = Path.GetExtension(mediaPath).ToLowerInvariant();
            CurrentMediaType = IsImageExtension(ext) ? Core.Enums.MediaType.Image : Core.Enums.MediaType.Video;

            // Parse media to get duration/info
            await _currentMedia.Parse(MediaParseOptions.ParseLocal);

            _mediaPlayer.Media = _currentMedia;

            if (autoPlay)
            {
                _mediaPlayer.Play();
            }

            return true;
        }
        catch (Exception ex)
        {
            OnError($"Failed to load media: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Play current media
    /// </summary>
    public void Play()
    {
        _mediaPlayer.Play();
    }

    /// <summary>
    /// Pause playback
    /// </summary>
    public void Pause()
    {
        _mediaPlayer.Pause();
    }

    /// <summary>
    /// Stop playback
    /// </summary>
    public void Stop()
    {
        _mediaPlayer.Stop();
    }

    /// <summary>
    /// Seek to position in milliseconds
    /// </summary>
    public void SeekTo(long positionMs)
    {
        if (_mediaPlayer.IsSeekable)
        {
            _mediaPlayer.Time = positionMs;
        }
    }

    /// <summary>
    /// Seek to position as fraction (0.0 - 1.0)
    /// </summary>
    public void SeekToFraction(float position)
    {
        if (_mediaPlayer.IsSeekable)
        {
            _mediaPlayer.Position = Math.Clamp(position, 0f, 1f);
        }
    }

    /// <summary>
    /// Take a snapshot of current frame
    /// </summary>
    public async Task<byte[]?> TakeSnapshotAsync(int width = 320, int height = 180)
    {
        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"snapshot_{PlayerId}_{Guid.NewGuid()}.png");

            bool result = _mediaPlayer.TakeSnapshot(0, tempPath, (uint)width, (uint)height);

            if (result)
            {
                // Wait a bit for file to be written
                await Task.Delay(100);

                if (File.Exists(tempPath))
                {
                    var data = await File.ReadAllBytesAsync(tempPath);
                    File.Delete(tempPath);
                    return data;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Snapshot failed: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Set video output window handle
    /// </summary>
    public void SetWindowHandle(IntPtr hwnd)
    {
        _mediaPlayer.Hwnd = hwnd;
    }

    /// <summary>
    /// Set aspect ratio
    /// </summary>
    public void SetAspectRatio(string? aspectRatio)
    {
        _mediaPlayer.AspectRatio = aspectRatio;
    }

    /// <summary>
    /// Set scale mode
    /// </summary>
    public void SetScaleMode(ScaleMode mode)
    {
        switch (mode)
        {
            case ScaleMode.Fit:
                _mediaPlayer.AspectRatio = null; // Default behavior
                break;
            case ScaleMode.Fill:
                // Calculate aspect ratio to fill
                _mediaPlayer.AspectRatio = null;
                _mediaPlayer.Scale = 0; // Auto-scale
                break;
            case ScaleMode.Stretch:
                // Stretch to fill - this requires video filter
                break;
            case ScaleMode.Original:
                _mediaPlayer.Scale = 1.0f; // 100%
                break;
        }
    }

    private void SetState(PlaybackState newState)
    {
        var oldState = State;
        State = newState;
        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(oldState, newState));
    }

    private void OnEndReached(object? sender, EventArgs e)
    {
        SetState(PlaybackState.EndOfMedia);
        MediaEnded?.Invoke(this, EventArgs.Empty);

        // Handle looping
        if (LoopMode == LoopMode.Single && CurrentMediaPath != null)
        {
            // Must use ThreadPool because VLC events are on the VLC thread
            ThreadPool.QueueUserWorkItem(_ =>
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Play();
            });
        }
    }

    private void OnError(string message)
    {
        SetState(PlaybackState.Error);
        Error?.Invoke(this, new PlaybackErrorEventArgs(message));
    }

    private static bool IsImageExtension(string ext)
    {
        return ext switch
        {
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".tiff" or ".tif" => true,
            _ => false
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _mediaPlayer.Stop();
        _currentMedia?.Dispose();
        _mediaPlayer.Dispose();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for playback state changes
/// </summary>
public class PlaybackStateChangedEventArgs : EventArgs
{
    public PlaybackState OldState { get; }
    public PlaybackState NewState { get; }

    public PlaybackStateChangedEventArgs(PlaybackState oldState, PlaybackState newState)
    {
        OldState = oldState;
        NewState = newState;
    }
}

/// <summary>
/// Event args for position changes
/// </summary>
public class PositionChangedEventArgs : EventArgs
{
    public long PositionMs { get; }
    public long DurationMs { get; }
    public double Progress => DurationMs > 0 ? (double)PositionMs / DurationMs : 0;

    public PositionChangedEventArgs(long positionMs, long durationMs)
    {
        PositionMs = positionMs;
        DurationMs = durationMs;
    }
}

/// <summary>
/// Event args for playback errors
/// </summary>
public class PlaybackErrorEventArgs : EventArgs
{
    public string Message { get; }

    public PlaybackErrorEventArgs(string message)
    {
        Message = message;
    }
}
