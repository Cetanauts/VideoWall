using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace VideoWall.Network.Discovery;

public class AgentDiscoveryListener : IDisposable
{
    private readonly ConcurrentDictionary<string, DiscoveredAgent> _agents = new();
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _pruneTask;
    private bool _disposed;

    public event EventHandler<DiscoveredAgent>? AgentDiscovered;
    public event EventHandler<DiscoveredAgent>? AgentUpdated;
    public event EventHandler<DiscoveredAgent>? AgentLost;

    public IReadOnlyList<DiscoveredAgent> DiscoveredAgents =>
        _agents.Values.ToList().AsReadOnly();

    public void Start()
    {
        if (_cts != null) return;

        _cts = new CancellationTokenSource();

        _udp = new UdpClient();
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryConstants.DiscoveryPort));

        _listenTask = ListenLoopAsync(_cts.Token);
        _pruneTask = PruneLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _udp?.Close();
        try { _listenTask?.Wait(2000); } catch { }
        try { _pruneTask?.Wait(2000); } catch { }
        _udp?.Dispose();
        _udp = null;
        _cts?.Dispose();
        _cts = null;
        _listenTask = null;
        _pruneTask = null;
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udp!.ReceiveAsync(ct);
                ProcessDatagram(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DiscoveryListener] Receive error: {ex.Message}");
            }
        }
    }

    private void ProcessDatagram(byte[] data, IPEndPoint remoteEndPoint)
    {
        try
        {
            var text = Encoding.UTF8.GetString(data);
            if (!text.StartsWith(DiscoveryConstants.ProtocolMagic))
                return;

            var json = text.Substring(DiscoveryConstants.ProtocolMagic.Length);
            var beacon = JsonSerializer.Deserialize<AgentBeacon>(json);
            if (beacon == null || string.IsNullOrEmpty(beacon.MachineId))
                return;

            var now = DateTime.UtcNow;
            var sourceAddress = remoteEndPoint.Address.ToString();

            if (_agents.TryGetValue(beacon.MachineId, out var existing))
            {
                existing.Beacon = beacon;
                existing.SourceAddress = sourceAddress;
                existing.LastSeen = now;
                AgentUpdated?.Invoke(this, existing);
            }
            else
            {
                var discovered = new DiscoveredAgent
                {
                    Beacon = beacon,
                    SourceAddress = sourceAddress,
                    FirstSeen = now,
                    LastSeen = now
                };
                _agents[beacon.MachineId] = discovered;
                AgentDiscovered?.Invoke(this, discovered);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DiscoveryListener] Parse error: {ex.Message}");
        }
    }

    private async Task PruneLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var cutoff = DateTime.UtcNow.AddMilliseconds(-DiscoveryConstants.AgentTimeoutMs);
            foreach (var kvp in _agents)
            {
                if (kvp.Value.LastSeen < cutoff)
                {
                    if (_agents.TryRemove(kvp.Key, out var removed))
                    {
                        AgentLost?.Invoke(this, removed);
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
