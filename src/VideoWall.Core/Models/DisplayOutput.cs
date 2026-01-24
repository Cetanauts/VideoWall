using VideoWall.Core.Enums;

namespace VideoWall.Core.Models;

/// <summary>
/// Represents a single display output (one HDMI/DP port)
/// </summary>
public class DisplayOutput
{
    /// <summary>
    /// Unique identifier: {MachineId}-{GpuIndex}-{OutputType}{OutputIndex}
    /// Example: "PC1-GPU0-HDMI1"
    /// </summary>
    public string OutputId { get; set; } = string.Empty;

    /// <summary>
    /// Machine this output belongs to
    /// </summary>
    public string MachineId { get; set; } = string.Empty;

    /// <summary>
    /// GPU index within the machine (0-based)
    /// </summary>
    public int GpuIndex { get; set; }

    /// <summary>
    /// Output type (HDMI, DisplayPort, DVI, VGA)
    /// </summary>
    public string OutputType { get; set; } = "HDMI";

    /// <summary>
    /// Output index within the GPU for this type (1-based for user display)
    /// </summary>
    public int OutputIndex { get; set; }

    /// <summary>
    /// Windows display device name (e.g., "\\.\DISPLAY1")
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Friendly name for the output (user-configurable)
    /// </summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>
    /// Current connection status
    /// </summary>
    public OutputStatus Status { get; set; } = OutputStatus.Unknown;

    /// <summary>
    /// Resolution width in pixels
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Resolution height in pixels
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Refresh rate in Hz
    /// </summary>
    public int RefreshRate { get; set; }

    /// <summary>
    /// Screen bounds (position in Windows virtual desktop)
    /// </summary>
    public ScreenBounds Bounds { get; set; } = new();

    /// <summary>
    /// Is this the primary Windows display?
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// User-controlled enable/disable for resource management
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Current media being played on this output
    /// </summary>
    public string? CurrentMediaPath { get; set; }

    /// <summary>
    /// Last captured thumbnail (base64 PNG, low resolution)
    /// </summary>
    public string? ThumbnailBase64 { get; set; }

    /// <summary>
    /// Timestamp of last status update
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Screen bounds in the Windows virtual desktop
/// </summary>
public class ScreenBounds
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public ScreenBounds() { }

    public ScreenBounds(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}
