using System.IO;
using System.Text.Json;

namespace VideoWall.Master.Services;

/// <summary>
/// Handles persistence of overlay configurations per output
/// </summary>
public class OverlayPersistence
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OverlayPersistence()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoWall"
        );

        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }

        _filePath = Path.Combine(appDataPath, "overlays.json");
    }

    /// <summary>
    /// Save overlay state to disk
    /// </summary>
    public async Task SaveAsync(Dictionary<string, List<OverlayStateData>> outputOverlays)
    {
        try
        {
            var data = new OverlaysFile
            {
                Version = 1,
                SavedAt = DateTime.UtcNow,
                OutputOverlays = outputOverlays
            };

            var json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save overlays: {ex.Message}");
        }
    }

    /// <summary>
    /// Load overlay state from disk
    /// </summary>
    public async Task<Dictionary<string, List<OverlayStateData>>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, List<OverlayStateData>>();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var data = JsonSerializer.Deserialize<OverlaysFile>(json, JsonOptions);

            return data?.OutputOverlays ?? new Dictionary<string, List<OverlayStateData>>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load overlays: {ex.Message}");
            return new Dictionary<string, List<OverlayStateData>>();
        }
    }
}

/// <summary>
/// Root object for overlays persistence file
/// </summary>
public class OverlaysFile
{
    public int Version { get; set; }
    public DateTime SavedAt { get; set; }
    public Dictionary<string, List<OverlayStateData>> OutputOverlays { get; set; } = new();
}

/// <summary>
/// Serializable overlay state for a single overlay instance
/// </summary>
public class OverlayStateData
{
    // Identity
    public string OverlayId { get; set; } = "";
    public int TypeIndex { get; set; }          // 0=Clock, 1=DateTime, 2=StaticText, 3=NewsTicker
    public int PositionIndex { get; set; }      // 0-5 matching combo

    // Common styling
    public double FontSize { get; set; } = 32;
    public int Margin { get; set; } = 20;
    public string ForegroundColor { get; set; } = "#FFFFFFFF";
    public string BackgroundColor { get; set; } = "#80000000";

    // Clock
    public bool Use24Hour { get; set; } = true;
    public bool ShowSeconds { get; set; } = true;

    // Clock/DateTime shared
    public string? TimezoneId { get; set; }
    public string? Format { get; set; }

    // StaticText
    public string? Text { get; set; }

    // NewsTicker
    public List<string>? FeedUrls { get; set; }
    public int ScrollSpeed { get; set; } = 100;
    public int RefreshIntervalMin { get; set; } = 15;
}
