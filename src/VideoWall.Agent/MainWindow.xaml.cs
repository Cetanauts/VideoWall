using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using VideoWall.Core.Enums;
using VideoWall.Core.Sync;
using VideoWall.Hardware;
using VideoWall.Agent.Services;

namespace VideoWall.Agent;

public partial class MainWindow : Window
{
    private readonly string _machineId;
    private readonly DisplayDetector _displayDetector;
    private readonly DisplayWindowManager _displayManager;
    private readonly SyncClock _syncClock;
    private readonly ObservableCollection<OutputViewModel> _outputs = new();
    private WebApplication? _grpcServer;
    private bool _serverRunning;

    public MainWindow()
    {
        InitializeComponent();

        _machineId = $"PC-{Environment.MachineName}";
        _displayDetector = new DisplayDetector(_machineId);
        _syncClock = new SyncClock();

        try
        {
            _displayManager = new DisplayWindowManager(_displayDetector, Dispatcher);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize display manager: {ex.Message}\n\nPlease ensure LibVLC is installed.",
                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _displayManager = null!;
        }

        lstOutputs.ItemsSource = _outputs;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        txtMachineInfo.Text = $"Machine ID: {_machineId} | {Environment.MachineName}";

        await RefreshHardwareAsync();

        // Start gRPC server
        await StartServerAsync();
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_grpcServer != null)
        {
            await _grpcServer.StopAsync();
            await _grpcServer.DisposeAsync();
        }
        _displayManager?.Dispose();
    }

    private async Task RefreshHardwareAsync()
    {
        try
        {
            txtStatus.Text = "Detecting hardware...";

            await Task.Run(() =>
            {
                var machineInfo = _displayDetector.GetMachineInfo();

                Dispatcher.Invoke(() =>
                {
                    _outputs.Clear();

                    foreach (var output in machineInfo.GetAllOutputs())
                    {
                        _outputs.Add(new OutputViewModel
                        {
                            OutputId = output.OutputId,
                            DeviceName = output.DeviceName,
                            Resolution = $"{output.Width}x{output.Height} @ {output.RefreshRate}Hz",
                            IsEnabled = output.IsEnabled,
                            IsConnected = output.Status == OutputStatus.Connected,
                            StatusColor = GetStatusBrush(output.Status),
                            CurrentMedia = output.CurrentMediaPath ?? "No media"
                        });
                    }

                    txtOutputCount.Text = _outputs.Count.ToString();
                    txtStatus.Text = "Ready";
                });
            });
        }
        catch (Exception ex)
        {
            txtStatus.Text = "Error";
            MessageBox.Show($"Failed to detect hardware: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task StartServerAsync()
    {
        try
        {
            const int port = 5100;

            var builder = WebApplication.CreateBuilder();

            // Configure Kestrel to listen on HTTP/2 (required for gRPC)
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(port, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });

            // Add gRPC services
            builder.Services.AddGrpc();

            // Register our gRPC service with its dependencies
            var grpcService = new AgentGrpcService(_machineId, _displayDetector, _displayManager, _syncClock);
            builder.Services.AddSingleton(grpcService);

            _grpcServer = builder.Build();

            // Map gRPC service
            _grpcServer.MapGrpcService<AgentGrpcService>();

            // Start the server in the background
            _ = Task.Run(async () =>
            {
                try
                {
                    await _grpcServer.RunAsync();
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtStatus.Text = "Error";
                        statusIndicator.Fill = FindResource("ErrorBrush") as SolidColorBrush;
                        MessageBox.Show($"gRPC server error: {ex.Message}", "Server Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });

            // Give the server a moment to start
            await Task.Delay(500);

            txtPort.Text = port.ToString();
            txtStatus.Text = "Running";
            statusIndicator.Fill = FindResource("SuccessBrush") as SolidColorBrush;
            btnStartStop.Content = "Stop Server";
            _serverRunning = true;
        }
        catch (Exception ex)
        {
            txtStatus.Text = "Error";
            statusIndicator.Fill = FindResource("ErrorBrush") as SolidColorBrush;
            MessageBox.Show($"Failed to start server: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task StopServerAsync()
    {
        try
        {
            if (_grpcServer != null)
            {
                await _grpcServer.StopAsync();
                await _grpcServer.DisposeAsync();
                _grpcServer = null;
            }

            txtStatus.Text = "Stopped";
            statusIndicator.Fill = FindResource("MutedBrush") as SolidColorBrush;
            btnStartStop.Content = "Start Server";
            _serverRunning = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to stop server: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Brush GetStatusBrush(OutputStatus status)
    {
        return status switch
        {
            OutputStatus.Connected or OutputStatus.Active or OutputStatus.Playing =>
                FindResource("SuccessBrush") as SolidColorBrush ?? Brushes.Green,
            OutputStatus.Error =>
                FindResource("ErrorBrush") as SolidColorBrush ?? Brushes.Red,
            OutputStatus.Disabled =>
                FindResource("MutedBrush") as SolidColorBrush ?? Brushes.Gray,
            _ =>
                FindResource("MutedBrush") as SolidColorBrush ?? Brushes.Gray
        };
    }

    private async void RefreshHardware_Click(object sender, RoutedEventArgs e)
    {
        await RefreshHardwareAsync();
    }

    private void ViewLogs_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Open logs window
        MessageBox.Show("Logs viewer not yet implemented", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void StartStop_Click(object sender, RoutedEventArgs e)
    {
        if (_serverRunning)
        {
            await StopServerAsync();
        }
        else
        {
            await StartServerAsync();
        }
    }

    private async void TestOutput_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string outputId)
        {
            try
            {
                // Show test pattern on this output
                MessageBox.Show($"Testing output: {outputId}\n\nTest pattern display not yet implemented.",
                    "Test Output", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Test failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ToggleOutput_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string outputId)
        {
            var output = _outputs.FirstOrDefault(o => o.OutputId == outputId);
            if (output != null)
            {
                output.IsEnabled = !output.IsEnabled;
                output.StatusColor = output.IsEnabled
                    ? FindResource("SuccessBrush") as SolidColorBrush ?? Brushes.Green
                    : FindResource("MutedBrush") as SolidColorBrush ?? Brushes.Gray;
            }
        }
    }
}

/// <summary>
/// ViewModel for display outputs
/// </summary>
public class OutputViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private string _outputId = string.Empty;
    private string _deviceName = string.Empty;
    private string _resolution = string.Empty;
    private bool _isEnabled;
    private bool _isConnected;
    private Brush _statusColor = Brushes.Gray;
    private string _currentMedia = "No media";

    public string OutputId
    {
        get => _outputId;
        set { _outputId = value; OnPropertyChanged(nameof(OutputId)); }
    }

    public string DeviceName
    {
        get => _deviceName;
        set { _deviceName = value; OnPropertyChanged(nameof(DeviceName)); }
    }

    public string Resolution
    {
        get => _resolution;
        set { _resolution = value; OnPropertyChanged(nameof(Resolution)); }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(nameof(IsConnected)); }
    }

    public Brush StatusColor
    {
        get => _statusColor;
        set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); }
    }

    public string CurrentMedia
    {
        get => _currentMedia;
        set { _currentMedia = value; OnPropertyChanged(nameof(CurrentMedia)); }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
