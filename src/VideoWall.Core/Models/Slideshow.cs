using VideoWall.Core.Enums;

namespace VideoWall.Core.Models;

/// <summary>
/// Slideshow configuration
/// </summary>
public class Slideshow
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string SlideshowId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of slides in order
    /// </summary>
    public List<Slide> Slides { get; set; } = new();

    /// <summary>
    /// Default display duration per slide in milliseconds
    /// </summary>
    public int DefaultDurationMs { get; set; } = 5000;

    /// <summary>
    /// Default transition between slides
    /// </summary>
    public TransitionType DefaultTransition { get; set; } = TransitionType.Fade;

    /// <summary>
    /// Default transition duration in milliseconds
    /// </summary>
    public int DefaultTransitionDurationMs { get; set; } = 1000;

    /// <summary>
    /// Loop mode
    /// </summary>
    public LoopMode LoopMode { get; set; } = LoopMode.All;

    /// <summary>
    /// Auto-advance to next slide?
    /// </summary>
    public bool AutoAdvance { get; set; } = true;

    /// <summary>
    /// Shuffle slides on each loop?
    /// </summary>
    public bool ShuffleOnLoop { get; set; } = false;

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Modified timestamp
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total number of slides
    /// </summary>
    public int SlideCount => Slides.Count;

    /// <summary>
    /// Estimated total runtime in milliseconds
    /// </summary>
    public long EstimatedRuntimeMs
    {
        get
        {
            long total = 0;
            foreach (var slide in Slides)
            {
                total += slide.DurationMs > 0 ? slide.DurationMs : DefaultDurationMs;
                total += slide.TransitionDurationMs > 0 ? slide.TransitionDurationMs : DefaultTransitionDurationMs;
            }
            return total;
        }
    }
}

/// <summary>
/// A single slide in a slideshow
/// </summary>
public class Slide
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string SlideId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Order index in the slideshow
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// The media item for this slide
    /// </summary>
    public MediaItem Media { get; set; } = new();

    /// <summary>
    /// Display duration in milliseconds (0 = use slideshow default)
    /// </summary>
    public int DurationMs { get; set; } = 0;

    /// <summary>
    /// Transition to use when entering this slide (null = use slideshow default)
    /// </summary>
    public TransitionType? Transition { get; set; }

    /// <summary>
    /// Transition duration in milliseconds (0 = use slideshow default)
    /// </summary>
    public int TransitionDurationMs { get; set; } = 0;

    /// <summary>
    /// Ken Burns effect settings (pan/zoom during display)
    /// </summary>
    public KenBurnsEffect? KenBurns { get; set; }

    /// <summary>
    /// Optional text overlay
    /// </summary>
    public TextOverlay? TextOverlay { get; set; }

    /// <summary>
    /// Is this slide enabled?
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Ken Burns effect (pan/zoom animation during image display)
/// </summary>
public class KenBurnsEffect
{
    /// <summary>
    /// Is the effect enabled?
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Starting zoom level (1.0 = 100%)
    /// </summary>
    public double StartZoom { get; set; } = 1.0;

    /// <summary>
    /// Ending zoom level
    /// </summary>
    public double EndZoom { get; set; } = 1.2;

    /// <summary>
    /// Starting X position (0.0 = left, 0.5 = center, 1.0 = right)
    /// </summary>
    public double StartX { get; set; } = 0.5;

    /// <summary>
    /// Starting Y position (0.0 = top, 0.5 = center, 1.0 = bottom)
    /// </summary>
    public double StartY { get; set; } = 0.5;

    /// <summary>
    /// Ending X position
    /// </summary>
    public double EndX { get; set; } = 0.5;

    /// <summary>
    /// Ending Y position
    /// </summary>
    public double EndY { get; set; } = 0.5;

    /// <summary>
    /// Easing function
    /// </summary>
    public EasingType Easing { get; set; } = EasingType.Linear;
}

/// <summary>
/// Text overlay for a slide
/// </summary>
public class TextOverlay
{
    /// <summary>
    /// The text to display
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Font family
    /// </summary>
    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>
    /// Font size in points
    /// </summary>
    public double FontSize { get; set; } = 24;

    /// <summary>
    /// Text color (ARGB hex)
    /// </summary>
    public string Color { get; set; } = "#FFFFFFFF";

    /// <summary>
    /// Background color (ARGB hex, empty for transparent)
    /// </summary>
    public string BackgroundColor { get; set; } = "#80000000";

    /// <summary>
    /// Horizontal alignment
    /// </summary>
    public TextHorizontalAlignment HAlign { get; set; } = TextHorizontalAlignment.Center;

    /// <summary>
    /// Vertical alignment
    /// </summary>
    public TextVerticalAlignment VAlign { get; set; } = TextVerticalAlignment.Bottom;

    /// <summary>
    /// Margin from edges in pixels
    /// </summary>
    public int Margin { get; set; } = 20;
}

/// <summary>
/// Easing functions for animations
/// </summary>
public enum EasingType
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad,
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic,
    EaseInElastic,
    EaseOutElastic,
    EaseInBounce,
    EaseOutBounce
}

/// <summary>
/// Horizontal alignment for text overlays
/// </summary>
public enum TextHorizontalAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// Vertical alignment for text overlays
/// </summary>
public enum TextVerticalAlignment
{
    Top,
    Center,
    Bottom
}
