using System.Text.Json.Serialization;

namespace VideoWall.Network.Discovery;

public class AgentBeacon
{
    [JsonPropertyName("machineId")]
    public string MachineId { get; set; } = string.Empty;

    [JsonPropertyName("computerName")]
    public string ComputerName { get; set; } = string.Empty;

    [JsonPropertyName("ipAddresses")]
    public List<string> IpAddresses { get; set; } = new();

    [JsonPropertyName("grpcPort")]
    public int GrpcPort { get; set; }

    [JsonPropertyName("agentVersion")]
    public string AgentVersion { get; set; } = "1.0";

    [JsonPropertyName("outputCount")]
    public int OutputCount { get; set; }
}
