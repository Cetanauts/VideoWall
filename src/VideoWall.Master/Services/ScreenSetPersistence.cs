using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoWall.Master.Services;

/// <summary>
/// Handles persistence of screen sets and assignments
/// </summary>
public class ScreenSetPersistence
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ScreenSetPersistence()
    {
        // Store in AppData/Local/VideoWall/screensets.json
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VideoWall"
        );

        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }

        _filePath = Path.Combine(appDataPath, "screensets.json");
    }

    /// <summary>
    /// Save screen sets to disk
    /// </summary>
    public async Task SaveAsync(IEnumerable<ScreenSetData> screenSets)
    {
        try
        {
            var data = new ScreenSetsFile
            {
                Version = 1,
                SavedAt = DateTime.UtcNow,
                ScreenSets = screenSets.ToList()
            };

            var json = JsonSerializer.Serialize(data, JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save screen sets: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Load screen sets from disk
    /// </summary>
    public async Task<List<ScreenSetData>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return GetDefaultScreenSets();
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var data = JsonSerializer.Deserialize<ScreenSetsFile>(json, JsonOptions);

            return data?.ScreenSets ?? GetDefaultScreenSets();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load screen sets: {ex.Message}");
            return GetDefaultScreenSets();
        }
    }

    /// <summary>
    /// Get default screen sets when file doesn't exist
    /// </summary>
    private static List<ScreenSetData> GetDefaultScreenSets()
    {
        return new List<ScreenSetData>
        {
            new()
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Default Wall",
                Columns = 3,
                Rows = 2,
                Assignments = new List<ScreenAssignmentData>()
            }
        };
    }
}

/// <summary>
/// Root object for screen sets file
/// </summary>
public class ScreenSetsFile
{
    public int Version { get; set; }
    public DateTime SavedAt { get; set; }
    public List<ScreenSetData> ScreenSets { get; set; } = new();
}

/// <summary>
/// Serializable screen set data
/// </summary>
public class ScreenSetData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Columns { get; set; }
    public int Rows { get; set; }
    public List<ScreenAssignmentData> Assignments { get; set; } = new();
}

/// <summary>
/// Serializable screen assignment data
/// </summary>
public class ScreenAssignmentData
{
    public int Column { get; set; }
    public int Row { get; set; }
    public string? OutputId { get; set; }
    public string? AgentMachineId { get; set; }
    public string? AgentIpAddress { get; set; }
    public int AgentPort { get; set; }
    public string? DisplayName { get; set; }
}
