using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using Grpc.Net.Client;
using VideoWall.Network.Discovery;
using VideoWall.Network.Grpc;
using Google.Protobuf.WellKnownTypes;

namespace VideoWall.Master;

public partial class AddAgentDialog : Window
{
    private readonly AgentDiscoveryListener? _listener;
    private readonly ObservableCollection<DiscoveredAgentViewModel> _discoveredAgents = new();
    private CancellationTokenSource? _scanCts;

    public string IpAddress { get; private set; } = string.Empty;
    public int Port { get; private set; }

    public AddAgentDialog(AgentDiscoveryListener? listener = null)
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;

        _listener = listener;
        lstDiscoveredAgents.ItemsSource = _discoveredAgents;

        if (_listener != null)
        {
            // Pre-populate with already-discovered agents
            foreach (var agent in _listener.DiscoveredAgents)
            {
                _discoveredAgents.Add(ToViewModel(agent));
            }

            UpdateDiscoveryStatus();

            // Wire live events
            _listener.AgentDiscovered += OnAgentDiscovered;
            _listener.AgentUpdated += OnAgentUpdated;
            _listener.AgentLost += OnAgentLost;
        }

        // Always run a TCP subnet scan as fallback (works even if UDP is blocked)
        _scanCts = new CancellationTokenSource();
        _ = ScanSubnetForAgentsAsync(_scanCts.Token);

        Closed += OnDialogClosed;
    }

    private void OnAgentDiscovered(object? sender, DiscoveredAgent agent)
    {
        Dispatcher.Invoke(() =>
        {
            _discoveredAgents.Add(ToViewModel(agent));
            UpdateDiscoveryStatus();
        });
    }

    private void OnAgentUpdated(object? sender, DiscoveredAgent agent)
    {
        Dispatcher.Invoke(() =>
        {
            var existing = FindViewModel(agent.Beacon.MachineId);
            if (existing != null)
            {
                existing.ComputerName = agent.Beacon.ComputerName;
                existing.IpAddress = agent.SourceAddress;
                existing.GrpcPort = agent.Beacon.GrpcPort;
                existing.OutputCount = agent.Beacon.OutputCount;
            }
        });
    }

    private void OnAgentLost(object? sender, DiscoveredAgent agent)
    {
        Dispatcher.Invoke(() =>
        {
            var existing = FindViewModel(agent.Beacon.MachineId);
            if (existing != null)
            {
                _discoveredAgents.Remove(existing);
                UpdateDiscoveryStatus();
            }
        });
    }

    private void DiscoveredAgent_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstDiscoveredAgents.SelectedItem is DiscoveredAgentViewModel vm)
        {
            txtIpAddress.Text = vm.IpAddress;
            txtPort.Text = vm.GrpcPort.ToString();
        }
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtIpAddress.Text))
        {
            MessageBox.Show("Please enter an IP address", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(txtPort.Text, out int port) || port < 1 || port > 65535)
        {
            MessageBox.Show("Please enter a valid port number (1-65535)", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IpAddress = txtIpAddress.Text.Trim();
        Port = port;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();

        if (_listener != null)
        {
            _listener.AgentDiscovered -= OnAgentDiscovered;
            _listener.AgentUpdated -= OnAgentUpdated;
            _listener.AgentLost -= OnAgentLost;
        }
    }

    private void UpdateDiscoveryStatus()
    {
        var count = _discoveredAgents.Count;
        txtDiscoveryStatus.Text = count == 0
            ? "Scanning for agents on LAN..."
            : $"{count} agent{(count != 1 ? "s" : "")} found";
    }

    private DiscoveredAgentViewModel? FindViewModel(string machineId) =>
        _discoveredAgents.FirstOrDefault(a => a.MachineId == machineId);

    private static DiscoveredAgentViewModel ToViewModel(DiscoveredAgent agent) => new()
    {
        MachineId = agent.Beacon.MachineId,
        ComputerName = agent.Beacon.ComputerName,
        IpAddress = agent.SourceAddress,
        GrpcPort = agent.Beacon.GrpcPort,
        OutputCount = agent.Beacon.OutputCount
    };

    /// <summary>
    /// Scan the local subnet(s) for VideoWall agents by probing gRPC port 5100.
    /// This is a TCP-based fallback that works even when UDP broadcast is blocked by firewalls.
    /// </summary>
    private async Task ScanSubnetForAgentsAsync(CancellationToken ct)
    {
        const int grpcPort = 5100;

        try
        {
            // Get all local /24 subnets
            var subnets = new HashSet<string>();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var bytes = addr.Address.GetAddressBytes();
                        subnets.Add($"{bytes[0]}.{bytes[1]}.{bytes[2]}");
                    }
                }
            }

            Dispatcher.Invoke(() => txtDiscoveryStatus.Text = $"Scanning {subnets.Count} subnet(s)...");

            // Limit concurrent probes to avoid overwhelming the network stack
            using var semaphore = new SemaphoreSlim(30);
            var tasks = new List<Task>();
            int scanned = 0;

            foreach (var subnet in subnets)
            {
                for (int i = 1; i < 255; i++)
                {
                    if (ct.IsCancellationRequested) return;

                    var ip = $"{subnet}.{i}";
                    await semaphore.WaitAsync(ct);
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await ProbeAgentAsync(ip, grpcPort, ct);
                        }
                        finally
                        {
                            semaphore.Release();
                            var count = Interlocked.Increment(ref scanned);
                            if (count % 50 == 0)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    if (_discoveredAgents.Count == 0)
                                        txtDiscoveryStatus.Text = $"Scanning... ({count}/254)";
                                });
                            }
                        }
                    }, ct));
                }
            }

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AddAgentDialog] Subnet scan error: {ex.Message}");
        }

        if (!ct.IsCancellationRequested)
        {
            Dispatcher.Invoke(() =>
            {
                if (_discoveredAgents.Count == 0)
                    txtDiscoveryStatus.Text = "No agents found (use manual entry)";
                else
                    UpdateDiscoveryStatus();
            });
        }
    }

    private async Task ProbeAgentAsync(string ip, int port, CancellationToken ct)
    {
        try
        {
            // Quick TCP connect test with proper cancellation
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(500);

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(ip, port, connectCts.Token);

            if (!tcp.Connected) return;
        }
        catch
        {
            return; // Connection refused or timed out — not an agent
        }

        // TCP port is open — try gRPC to confirm it's a VideoWall agent
        try
        {
            using var channel = GrpcChannel.ForAddress($"http://{ip}:{port}", new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    ConnectTimeout = TimeSpan.FromSeconds(3)
                }
            });
            var client = new AgentService.AgentServiceClient(channel);

            using var grpcCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            grpcCts.CancelAfter(3000);
            var status = await client.GetStatusAsync(new Empty(), cancellationToken: grpcCts.Token);

            if (string.IsNullOrEmpty(status.MachineId)) return;

            Dispatcher.Invoke(() =>
            {
                if (FindViewModel(status.MachineId) == null)
                {
                    _discoveredAgents.Add(new DiscoveredAgentViewModel
                    {
                        MachineId = status.MachineId,
                        ComputerName = status.ComputerName,
                        IpAddress = ip,
                        GrpcPort = port,
                        OutputCount = status.Outputs.Count
                    });
                    UpdateDiscoveryStatus();
                }
            });
        }
        catch { }
    }
}

public class DiscoveredAgentViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private string _machineId = string.Empty;
    private string _computerName = string.Empty;
    private string _ipAddress = string.Empty;
    private int _grpcPort;
    private int _outputCount;

    public string MachineId
    {
        get => _machineId;
        set { _machineId = value; OnPropertyChanged(nameof(MachineId)); }
    }

    public string ComputerName
    {
        get => _computerName;
        set { _computerName = value; OnPropertyChanged(nameof(ComputerName)); }
    }

    public string IpAddress
    {
        get => _ipAddress;
        set { _ipAddress = value; OnPropertyChanged(nameof(IpAddress)); }
    }

    public int GrpcPort
    {
        get => _grpcPort;
        set { _grpcPort = value; OnPropertyChanged(nameof(GrpcPort)); }
    }

    public int OutputCount
    {
        get => _outputCount;
        set { _outputCount = value; OnPropertyChanged(nameof(OutputCount)); }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
