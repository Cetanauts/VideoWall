using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace VideoWall.Network.Discovery;

public class AgentAdvertiser : IDisposable
{
    private readonly string _machineId;
    private readonly string _computerName;
    private readonly int _grpcPort;
    private int _outputCount;
    private CancellationTokenSource? _cts;
    private Task? _broadcastTask;
    private bool _disposed;

    public AgentAdvertiser(string machineId, string computerName, int grpcPort, int outputCount)
    {
        _machineId = machineId;
        _computerName = computerName;
        _grpcPort = grpcPort;
        _outputCount = outputCount;
    }

    public void Start()
    {
        if (_cts != null) return;

        _cts = new CancellationTokenSource();
        _broadcastTask = BroadcastLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _broadcastTask?.Wait(2000); } catch { }
        _cts?.Dispose();
        _cts = null;
        _broadcastTask = null;
    }

    public void UpdateOutputCount(int count)
    {
        _outputCount = count;
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var udp = new UdpClient();
                udp.EnableBroadcast = true;

                var beacon = new AgentBeacon
                {
                    MachineId = _machineId,
                    ComputerName = _computerName,
                    GrpcPort = _grpcPort,
                    OutputCount = _outputCount,
                    IpAddresses = GetLocalIpAddresses()
                };

                var json = JsonSerializer.Serialize(beacon);
                var payload = Encoding.UTF8.GetBytes(DiscoveryConstants.ProtocolMagic + json);
                var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryConstants.DiscoveryPort);

                await udp.SendAsync(payload, payload.Length, endpoint);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AgentAdvertiser] Broadcast error: {ex.Message}");
            }

            try
            {
                await Task.Delay(DiscoveryConstants.BroadcastIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static List<string> GetLocalIpAddresses()
    {
        var addresses = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        addresses.Add(addr.Address.ToString());
                    }
                }
            }
        }
        catch { }
        return addresses;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
