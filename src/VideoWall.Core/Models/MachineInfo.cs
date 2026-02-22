using VideoWall.Core.Enums;

namespace VideoWall.Core.Models;

/// <summary>
/// Information about a machine running the VideoWall agent
/// </summary>
public class MachineInfo
{
    /// <summary>
    /// Unique identifier for this machine
    /// </summary>
    public string MachineId { get; set; } = string.Empty;

    /// <summary>
    /// Computer name
    /// </summary>
    public string ComputerName { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly display name
    /// </summary>
    public string FriendlyName { get; set; } = string.Empty;

    /// <summary>
    /// IP address
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Agent port
    /// </summary>
    public int Port { get; set; } = 5050;

    /// <summary>
    /// Operating system version
    /// </summary>
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Agent software version
    /// </summary>
    public string AgentVersion { get; set; } = string.Empty;

    /// <summary>
    /// Agent connection status
    /// </summary>
    public AgentStatus Status { get; set; } = AgentStatus.Unknown;

    /// <summary>
    /// Error message if status is Error
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// List of GPUs in this machine
    /// </summary>
    public List<GpuInfo> Gpus { get; set; } = new();

    /// <summary>
    /// Total display outputs across all GPUs
    /// </summary>
    public int TotalOutputs => Gpus.Sum(g => g.Outputs.Count);

    /// <summary>
    /// Active display outputs
    /// </summary>
    public int ActiveOutputs => Gpus.Sum(g => g.ActiveOutputs);

    /// <summary>
    /// Total RAM in MB
    /// </summary>
    public long TotalRamMB { get; set; }

    /// <summary>
    /// Available RAM in MB
    /// </summary>
    public long AvailableRamMB { get; set; }

    /// <summary>
    /// CPU usage percentage (0-100)
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// Is LibVLC initialized and ready?
    /// </summary>
    public bool IsVlcReady { get; set; }

    /// <summary>
    /// LibVLC version
    /// </summary>
    public string? VlcVersion { get; set; }

    /// <summary>
    /// When was this machine first discovered
    /// </summary>
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last communication timestamp
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last hardware report timestamp
    /// </summary>
    public DateTime? LastHardwareReportAt { get; set; }

    /// <summary>
    /// Is this machine the master controller?
    /// </summary>
    public bool IsMaster { get; set; }

    /// <summary>
    /// Get all outputs across all GPUs
    /// </summary>
    public IEnumerable<DisplayOutput> GetAllOutputs()
    {
        return Gpus.SelectMany(g => g.Outputs);
    }

    /// <summary>
    /// Find an output by its ID
    /// </summary>
    public DisplayOutput? GetOutput(string outputId)
    {
        return GetAllOutputs().FirstOrDefault(o => o.OutputId == outputId);
    }
}
