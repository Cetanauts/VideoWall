using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using VideoWall.Network.Discovery;

namespace VideoWall.Master;

public partial class AddAgentDialog : Window
{
    private readonly AgentDiscoveryListener? _listener;
    private readonly ObservableCollection<DiscoveredAgentViewModel> _discoveredAgents = new();

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
        else
        {
            txtDiscoveryStatus.Text = "Discovery not available";
        }

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
