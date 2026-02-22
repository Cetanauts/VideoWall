namespace VideoWall.Network.Discovery;

public class DiscoveredAgent
{
    public AgentBeacon Beacon { get; set; } = new();
    public string SourceAddress { get; set; } = string.Empty;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
}
