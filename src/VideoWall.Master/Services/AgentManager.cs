using System.Collections.Concurrent;
using VideoWall.Core.Sync;
using VideoWall.Network.Grpc;

namespace VideoWall.Master.Services;

/// <summary>
/// Manages all connected VideoWall agents
/// </summary>
public class AgentManager : IDisposable
{
    private readonly ConcurrentDictionary<string, AgentClient> _agents = new();
    private readonly ConcurrentDictionary<string, HardwareInfoResponse> _hardwareCache = new();
    private readonly SyncClock _masterClock = new();
    private readonly System.Timers.Timer _heartbeatTimer;
    private bool _disposed;

    /// <summary>
    /// Event fired when an agent connects
    /// </summary>
    public event EventHandler<AgentEventArgs>? AgentConnected;

    /// <summary>
    /// Event fired when an agent disconnects
    /// </summary>
    public event EventHandler<AgentEventArgs>? AgentDisconnected;

    /// <summary>
    /// Event fired when an agent's status changes
    /// </summary>
    public event EventHandler<AgentStatusEventArgs>? AgentStatusChanged;

    /// <summary>
    /// All connected agents
    /// </summary>
    public IReadOnlyDictionary<string, AgentClient> Agents => _agents;

    /// <summary>
    /// Total number of connected agents
    /// </summary>
    public int AgentCount => _agents.Count;

    /// <summary>
    /// Total number of outputs across all agents
    /// </summary>
    public int TotalOutputCount => _hardwareCache.Values.Sum(h => h.Gpus.Sum(g => g.Outputs.Count));

    /// <summary>
    /// Master clock for synchronization
    /// </summary>
    public SyncClock MasterClock => _masterClock;

    public AgentManager()
    {
        _heartbeatTimer = new System.Timers.Timer(5000); // 5 second heartbeat
        _heartbeatTimer.Elapsed += async (s, e) => await CheckHeartbeatsAsync();
        _heartbeatTimer.Start();
    }

    /// <summary>
    /// Connect to an agent
    /// </summary>
    public async Task<AgentClient?> ConnectAsync(string ipAddress, int port, CancellationToken cancellationToken = default)
    {
        var client = new AgentClient(ipAddress, port);

        if (await client.ConnectAsync(cancellationToken))
        {
            // Get hardware info
            var hardware = await client.GetHardwareInfoAsync(cancellationToken);
            if (hardware != null)
            {
                var machineId = hardware.MachineId;

                // Check if already connected
                if (_agents.ContainsKey(machineId))
                {
                    client.Dispose();
                    return _agents[machineId];
                }

                _agents[machineId] = client;
                _hardwareCache[machineId] = hardware;

                AgentConnected?.Invoke(this, new AgentEventArgs(machineId, client));

                return client;
            }
        }

        client.Dispose();
        return null;
    }

    /// <summary>
    /// Disconnect an agent
    /// </summary>
    public void Disconnect(string machineId)
    {
        if (_agents.TryRemove(machineId, out var client))
        {
            _hardwareCache.TryRemove(machineId, out _);
            AgentDisconnected?.Invoke(this, new AgentEventArgs(machineId, client));
            client.Dispose();
        }
    }

    /// <summary>
    /// Get hardware info for an agent
    /// </summary>
    public HardwareInfoResponse? GetHardwareInfo(string machineId)
    {
        return _hardwareCache.TryGetValue(machineId, out var info) ? info : null;
    }

    /// <summary>
    /// Get all outputs across all agents
    /// </summary>
    public IEnumerable<(string machineId, DisplayOutputInfo output)> GetAllOutputs()
    {
        foreach (var (machineId, hardware) in _hardwareCache)
        {
            foreach (var gpu in hardware.Gpus)
            {
                foreach (var output in gpu.Outputs)
                {
                    yield return (machineId, output);
                }
            }
        }
    }

    /// <summary>
    /// Find the agent that owns a specific output
    /// </summary>
    public AgentClient? GetAgentForOutput(string outputId)
    {
        foreach (var (machineId, hardware) in _hardwareCache)
        {
            foreach (var gpu in hardware.Gpus)
            {
                if (gpu.Outputs.Any(o => o.OutputId == outputId))
                {
                    return _agents.TryGetValue(machineId, out var client) ? client : null;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Play media on a specific output (finds the correct agent automatically)
    /// </summary>
    public async Task<bool> PlayAsync(string outputId, string mediaPath, CancellationToken cancellationToken = default)
    {
        var client = GetAgentForOutput(outputId);
        if (client == null) return false;

        var response = await client.PlayAsync(outputId, mediaPath, cancellationToken: cancellationToken);
        return response?.Success ?? false;
    }

    /// <summary>
    /// Pause all outputs
    /// </summary>
    public async Task PauseAllAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();

        foreach (var (machineId, client) in _agents)
        {
            var hardware = GetHardwareInfo(machineId);
            if (hardware == null) continue;

            foreach (var gpu in hardware.Gpus)
            {
                foreach (var output in gpu.Outputs)
                {
                    tasks.Add(client.PauseAsync(output.OutputId, cancellationToken));
                }
            }
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Stop all outputs
    /// </summary>
    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();

        foreach (var (machineId, client) in _agents)
        {
            var hardware = GetHardwareInfo(machineId);
            if (hardware == null) continue;

            foreach (var gpu in hardware.Gpus)
            {
                foreach (var output in gpu.Outputs)
                {
                    tasks.Add(client.StopAsync(output.OutputId, cancellationToken));
                }
            }
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Resume all outputs
    /// </summary>
    public async Task ResumeAllAsync(CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();

        foreach (var (machineId, client) in _agents)
        {
            var hardware = GetHardwareInfo(machineId);
            if (hardware == null) continue;

            foreach (var gpu in hardware.Gpus)
            {
                foreach (var output in gpu.Outputs)
                {
                    tasks.Add(client.ResumeAsync(output.OutputId, cancellationToken));
                }
            }
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Execute synchronized playback across multiple outputs
    /// </summary>
    public async Task<bool> PlaySynchronizedAsync(Dictionary<string, string> outputMediaMap,
        CancellationToken cancellationToken = default)
    {
        var syncGroupId = Guid.NewGuid().ToString();

        // Group outputs by agent
        var agentOutputs = new Dictionary<AgentClient, Dictionary<string, string>>();

        foreach (var (outputId, mediaPath) in outputMediaMap)
        {
            var client = GetAgentForOutput(outputId);
            if (client == null) continue;

            if (!agentOutputs.ContainsKey(client))
            {
                agentOutputs[client] = new Dictionary<string, string>();
            }
            agentOutputs[client][outputId] = mediaPath;
        }

        // Prepare on all agents
        var prepareTasks = agentOutputs.Select(kv =>
            kv.Key.PrepareSyncAsync(syncGroupId, kv.Value, cancellationToken));

        await Task.WhenAll(prepareTasks);

        // Calculate execute time (500ms in the future to allow for network latency)
        var executeAt = _masterClock.Now.AddMilliseconds(500);

        // Execute on all agents
        var executeTasks = agentOutputs.Keys.Select(client =>
            client.ExecuteSyncAsync(syncGroupId, executeAt, cancellationToken));

        await Task.WhenAll(executeTasks);

        return true;
    }

    /// <summary>
    /// Run sync flash test on all outputs
    /// </summary>
    public async Task RunSyncFlashTestAsync(int durationSeconds = 5, CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task>();

        foreach (var (_, output) in GetAllOutputs())
        {
            var client = GetAgentForOutput(output.OutputId);
            if (client != null)
            {
                tasks.Add(client.RunTestPatternAsync(output.OutputId, TestPatternType.TestPatternSyncFlash, durationSeconds, cancellationToken));
            }
        }

        await Task.WhenAll(tasks);
    }

    private async Task CheckHeartbeatsAsync()
    {
        var disconnected = new List<string>();

        foreach (var (machineId, client) in _agents)
        {
            if (!await client.ConnectAsync())
            {
                disconnected.Add(machineId);
            }
            else
            {
                // Get updated status
                var status = await client.GetStatusAsync();
                if (status != null)
                {
                    AgentStatusChanged?.Invoke(this, new AgentStatusEventArgs(machineId, status));
                }
            }
        }

        foreach (var machineId in disconnected)
        {
            Disconnect(machineId);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _heartbeatTimer.Stop();
        _heartbeatTimer.Dispose();

        foreach (var client in _agents.Values)
        {
            client.Dispose();
        }
        _agents.Clear();
    }
}

/// <summary>
/// Event args for agent events
/// </summary>
public class AgentEventArgs : EventArgs
{
    public string MachineId { get; }
    public AgentClient Client { get; }

    public AgentEventArgs(string machineId, AgentClient client)
    {
        MachineId = machineId;
        Client = client;
    }
}

/// <summary>
/// Event args for agent status changes
/// </summary>
public class AgentStatusEventArgs : EventArgs
{
    public string MachineId { get; }
    public AgentStatusResponse Status { get; }

    public AgentStatusEventArgs(string machineId, AgentStatusResponse status)
    {
        MachineId = machineId;
        Status = status;
    }
}
