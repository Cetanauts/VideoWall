namespace VideoWall.Core.Enums;

/// <summary>
/// Playback state of a media player
/// </summary>
public enum PlaybackState
{
    Stopped = 0,
    Playing = 1,
    Paused = 2,
    Buffering = 3,
    Error = 4,
    EndOfMedia = 5
}

/// <summary>
/// Type of media being played
/// </summary>
public enum MediaType
{
    None = 0,
    Video = 1,
    Image = 2,
    Slideshow = 3,
    TestPattern = 4,
    Stream = 5
}

/// <summary>
/// Output connection status
/// </summary>
public enum OutputStatus
{
    Unknown = 0,
    Disconnected = 1,
    Connected = 2,
    Active = 3,      // Has content assigned
    Playing = 4,     // Currently playing content
    Error = 5,
    Disabled = 6     // Manually disabled by user
}

/// <summary>
/// Agent connection status
/// </summary>
public enum AgentStatus
{
    Unknown = 0,
    Offline = 1,
    Connecting = 2,
    Online = 3,
    Busy = 4,        // Processing a command
    Error = 5
}

/// <summary>
/// Loop mode for playback
/// </summary>
public enum LoopMode
{
    None = 0,        // Play once and stop
    Single = 1,      // Loop current item
    All = 2,         // Loop entire playlist/slideshow
    Shuffle = 3      // Shuffle and loop
}

/// <summary>
/// Scaling mode for content
/// </summary>
public enum ScaleMode
{
    Fit = 0,         // Fit within bounds, maintain aspect ratio (letterbox/pillarbox)
    Fill = 1,        // Fill bounds, maintain aspect ratio (crop)
    Stretch = 2,     // Stretch to fill (distort if necessary)
    Original = 3,    // Original size, centered
    Tile = 4         // Tile the content
}
