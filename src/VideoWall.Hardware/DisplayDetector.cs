using System.Management;
using System.Runtime.InteropServices;
using VideoWall.Core.Enums;
using VideoWall.Core.Models;

namespace VideoWall.Hardware;

/// <summary>
/// Detects and enumerates display outputs, GPUs, and monitors
/// </summary>
public class DisplayDetector
{
    private readonly string _machineId;

    public DisplayDetector(string machineId)
    {
        _machineId = machineId;
    }

    /// <summary>
    /// Get complete hardware information for this machine
    /// </summary>
    public MachineInfo GetMachineInfo()
    {
        var info = new MachineInfo
        {
            MachineId = _machineId,
            ComputerName = Environment.MachineName,
            FriendlyName = Environment.MachineName,
            OsVersion = Environment.OSVersion.ToString(),
            TotalRamMB = GetTotalRam(),
            AvailableRamMB = GetAvailableRam(),
            LastHardwareReportAt = DateTime.UtcNow
        };

        info.Gpus = GetGpuList();
        return info;
    }

    /// <summary>
    /// Get list of all GPUs and their outputs
    /// </summary>
    public List<GpuInfo> GetGpuList()
    {
        var gpus = new List<GpuInfo>();
        int gpuIndex = 0;

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var gpu = new GpuInfo
                {
                    GpuId = $"{_machineId}-GPU{gpuIndex}",
                    MachineId = _machineId,
                    Index = gpuIndex,
                    Name = obj["Name"]?.ToString() ?? "Unknown GPU",
                    Manufacturer = GetManufacturer(obj["Name"]?.ToString()),
                    DriverVersion = obj["DriverVersion"]?.ToString() ?? "Unknown",
                    DeviceId = obj["PNPDeviceID"]?.ToString() ?? "",
                    VideoMemoryMB = Convert.ToInt64(obj["AdapterRAM"] ?? 0) / (1024 * 1024)
                };

                // Parse driver date
                if (obj["DriverDate"] != null)
                {
                    var dateStr = obj["DriverDate"].ToString();
                    if (dateStr?.Length >= 8)
                    {
                        if (DateTime.TryParseExact(dateStr.Substring(0, 8), "yyyyMMdd",
                            null, System.Globalization.DateTimeStyles.None, out var driverDate))
                        {
                            gpu.DriverDate = driverDate;
                        }
                    }
                }

                gpus.Add(gpu);
                gpuIndex++;
            }
        }
        catch (Exception ex)
        {
            // Return at least one placeholder GPU on error
            gpus.Add(new GpuInfo
            {
                GpuId = $"{_machineId}-GPU0",
                MachineId = _machineId,
                Index = 0,
                Name = "Detection Failed",
                IsHealthy = false,
                ErrorMessage = ex.Message
            });
        }

        // Enumerate displays and assign to GPUs
        var displays = GetDisplayOutputs();
        AssignDisplaysToGpus(gpus, displays);

        return gpus;
    }

    /// <summary>
    /// Get all active display outputs using Windows API
    /// </summary>
    public List<DisplayOutput> GetDisplayOutputs()
    {
        var outputs = new List<DisplayOutput>();

        try
        {
            var device = new DISPLAY_DEVICE();
            device.cb = Marshal.SizeOf(device);

            uint deviceIndex = 0;
            while (EnumDisplayDevices(null, deviceIndex, ref device, 0))
            {
                // Skip mirroring/pseudo devices
                if ((device.StateFlags & DisplayDeviceStateFlags.AttachedToDesktop) != 0)
                {
                    var output = new DisplayOutput
                    {
                        MachineId = _machineId,
                        DeviceName = device.DeviceName,
                        FriendlyName = device.DeviceString,
                        IsPrimary = (device.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) != 0,
                        Status = OutputStatus.Connected
                    };

                    // Get display settings
                    var devMode = new DEVMODE();
                    devMode.dmSize = (short)Marshal.SizeOf(devMode);

                    if (EnumDisplaySettings(device.DeviceName, ENUM_CURRENT_SETTINGS, ref devMode))
                    {
                        output.Width = devMode.dmPelsWidth;
                        output.Height = devMode.dmPelsHeight;
                        output.RefreshRate = devMode.dmDisplayFrequency;
                        output.Bounds = new ScreenBounds(
                            devMode.dmPositionX,
                            devMode.dmPositionY,
                            devMode.dmPelsWidth,
                            devMode.dmPelsHeight
                        );
                    }

                    // Determine output type from device string
                    output.OutputType = DetermineOutputType(device.DeviceString, device.DeviceName);
                    output.OutputIndex = outputs.Count(o => o.OutputType == output.OutputType) + 1;

                    outputs.Add(output);
                }

                deviceIndex++;
                device.cb = Marshal.SizeOf(device);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error enumerating displays: {ex.Message}");
        }

        return outputs;
    }

    /// <summary>
    /// Assign display outputs to their respective GPUs
    /// </summary>
    private void AssignDisplaysToGpus(List<GpuInfo> gpus, List<DisplayOutput> displays)
    {
        // For now, distribute displays across GPUs based on index
        // In a more sophisticated implementation, we would use DXGI to get actual GPU-display mappings
        int gpuCount = gpus.Count;
        if (gpuCount == 0) return;

        for (int i = 0; i < displays.Count; i++)
        {
            int gpuIndex = i / 8; // Assume max 8 outputs per GPU
            if (gpuIndex >= gpuCount) gpuIndex = gpuCount - 1;

            var display = displays[i];
            display.GpuIndex = gpuIndex;
            display.OutputId = $"{_machineId}-GPU{gpuIndex}-{display.OutputType}{display.OutputIndex}";

            gpus[gpuIndex].Outputs.Add(display);
            gpus[gpuIndex].ActiveOutputs++;
        }
    }

    /// <summary>
    /// Determine output type from device information
    /// </summary>
    private string DetermineOutputType(string deviceString, string deviceName)
    {
        var combined = (deviceString + deviceName).ToUpperInvariant();

        if (combined.Contains("HDMI")) return "HDMI";
        if (combined.Contains("DISPLAYPORT") || combined.Contains("DP")) return "DP";
        if (combined.Contains("DVI")) return "DVI";
        if (combined.Contains("VGA")) return "VGA";

        return "HDMI"; // Default assumption
    }

    /// <summary>
    /// Get GPU manufacturer from name
    /// </summary>
    private string GetManufacturer(string? gpuName)
    {
        if (string.IsNullOrEmpty(gpuName)) return "Unknown";

        var upper = gpuName.ToUpperInvariant();
        if (upper.Contains("NVIDIA") || upper.Contains("GEFORCE") || upper.Contains("QUADRO"))
            return "NVIDIA";
        if (upper.Contains("AMD") || upper.Contains("RADEON"))
            return "AMD";
        if (upper.Contains("INTEL"))
            return "Intel";

        return "Unknown";
    }

    /// <summary>
    /// Get total RAM in MB
    /// </summary>
    private long GetTotalRam()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                return Convert.ToInt64(obj["TotalVisibleMemorySize"]) / 1024;
            }
        }
        catch { }
        return 0;
    }

    /// <summary>
    /// Get available RAM in MB
    /// </summary>
    private long GetAvailableRam()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                return Convert.ToInt64(obj["FreePhysicalMemory"]) / 1024;
            }
        }
        catch { }
        return 0;
    }

    #region Windows API Declarations

    private const int ENUM_CURRENT_SETTINGS = -1;

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum,
        ref DEVMODE lpDevMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public DisplayDeviceStateFlags StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [Flags]
    private enum DisplayDeviceStateFlags
    {
        AttachedToDesktop = 0x1,
        MultiDriver = 0x2,
        PrimaryDevice = 0x4,
        MirroringDriver = 0x8,
        VGACompatible = 0x10,
        Removable = 0x20,
        ModesPruned = 0x8000000,
        Remote = 0x4000000,
        Disconnect = 0x2000000
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    #endregion
}
