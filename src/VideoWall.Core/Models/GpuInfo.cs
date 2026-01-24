namespace VideoWall.Core.Models;

/// <summary>
/// Information about a graphics processing unit
/// </summary>
public class GpuInfo
{
    /// <summary>
    /// Unique identifier: {MachineId}-GPU{Index}
    /// </summary>
    public string GpuId { get; set; } = string.Empty;

    /// <summary>
    /// Machine this GPU belongs to
    /// </summary>
    public string MachineId { get; set; } = string.Empty;

    /// <summary>
    /// GPU index within the machine (0-based)
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// GPU device name (e.g., "NVIDIA GeForce RTX 4090")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Manufacturer (NVIDIA, AMD, Intel)
    /// </summary>
    public string Manufacturer { get; set; } = string.Empty;

    /// <summary>
    /// Driver version string
    /// </summary>
    public string DriverVersion { get; set; } = string.Empty;

    /// <summary>
    /// Driver date
    /// </summary>
    public DateTime? DriverDate { get; set; }

    /// <summary>
    /// Total video memory in MB
    /// </summary>
    public long VideoMemoryMB { get; set; }

    /// <summary>
    /// Available video memory in MB
    /// </summary>
    public long AvailableMemoryMB { get; set; }

    /// <summary>
    /// PCI device ID
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Vendor ID
    /// </summary>
    public string VendorId { get; set; } = string.Empty;

    /// <summary>
    /// List of outputs on this GPU
    /// </summary>
    public List<DisplayOutput> Outputs { get; set; } = new();

    /// <summary>
    /// Maximum supported outputs (physical ports)
    /// </summary>
    public int MaxOutputs { get; set; }

    /// <summary>
    /// Currently active outputs
    /// </summary>
    public int ActiveOutputs { get; set; }

    /// <summary>
    /// Is the GPU functioning correctly?
    /// </summary>
    public bool IsHealthy { get; set; } = true;

    /// <summary>
    /// Error message if not healthy
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Last time information was updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
