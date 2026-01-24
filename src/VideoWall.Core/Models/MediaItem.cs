using VideoWall.Core.Enums;

namespace VideoWall.Core.Models;

/// <summary>
/// Represents a media item (video or image) that can be played
/// </summary>
public class MediaItem
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string MediaId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of media
    /// </summary>
    public MediaType Type { get; set; } = MediaType.None;

    /// <summary>
    /// Full path to the media file (UNC or local)
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Duration in milliseconds (for video) or display time (for images in slideshow)
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Width in pixels
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height in pixels
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Video codec (for videos)
    /// </summary>
    public string? Codec { get; set; }

    /// <summary>
    /// Frame rate (for videos)
    /// </summary>
    public double? FrameRate { get; set; }

    /// <summary>
    /// Thumbnail image (base64 PNG)
    /// </summary>
    public string? ThumbnailBase64 { get; set; }

    /// <summary>
    /// Custom scaling mode for this item
    /// </summary>
    public ScaleMode ScaleMode { get; set; } = ScaleMode.Fit;

    /// <summary>
    /// Audio enabled for this item?
    /// </summary>
    public bool AudioEnabled { get; set; } = true;

    /// <summary>
    /// Volume level (0-100)
    /// </summary>
    public int Volume { get; set; } = 100;

    /// <summary>
    /// Tags for organization
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// When was this item added
    /// </summary>
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// File last modified timestamp
    /// </summary>
    public DateTime? FileModifiedAt { get; set; }

    /// <summary>
    /// Does the file still exist?
    /// </summary>
    public bool IsAccessible { get; set; } = true;

    /// <summary>
    /// Error message if not accessible
    /// </summary>
    public string? AccessError { get; set; }
}
