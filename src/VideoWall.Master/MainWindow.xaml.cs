using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using VideoWall.Master.Services;
using VideoWall.Network.Grpc;

namespace VideoWall.Master;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<AgentViewModel> _agents = new();
    private readonly ObservableCollection<ScreenSetViewModel> _screenSets = new();
    private readonly DispatcherTimer _statusTimer;
    private readonly AgentManager _agentManager;
    private readonly ScreenSetPersistence _persistence;

    public MainWindow()
    {
        InitializeComponent();

        lstAgents.ItemsSource = _agents;
        lstScreenSets.ItemsSource = _screenSets;

        // Initialize agent manager
        _agentManager = new AgentManager();
        _agentManager.AgentConnected += OnAgentConnected;
        _agentManager.AgentDisconnected += OnAgentDisconnected;
        _agentManager.AgentStatusChanged += OnAgentStatusChanged;

        // Initialize persistence
        _persistence = new ScreenSetPersistence();

        // Update time
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _statusTimer.Tick += (s, e) =>
        {
            txtTime.Text = DateTime.Now.ToString("HH:mm:ss");
            UpdateAgentStatus();
        };
        _statusTimer.Start();

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void OnAgentConnected(object? sender, AgentEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var hardware = _agentManager.GetHardwareInfo(e.MachineId);
            var agent = new AgentViewModel
            {
                MachineId = e.MachineId,
                ComputerName = hardware?.ComputerName ?? e.MachineId,
                IpAddress = e.Client.IpAddress,
                Port = e.Client.Port,
                OutputCount = hardware?.Gpus.Sum(g => g.Outputs.Count) ?? 0,
                IsOnline = true,
                StatusColor = FindResource("SuccessBrush") as SolidColorBrush ?? Brushes.Green
            };

            // Populate outputs from hardware info
            if (hardware != null)
            {
                foreach (var gpu in hardware.Gpus)
                {
                    foreach (var output in gpu.Outputs)
                    {
                        agent.Outputs.Add(new OutputInfo
                        {
                            OutputId = output.OutputId,
                            DisplayName = output.FriendlyName,
                            Resolution = $"{output.Width}x{output.Height}",
                            AgentName = agent.ComputerName
                        });
                    }
                }
            }

            _agents.Add(agent);
            UpdateAgentStatus();
            UpdateStatusBar($"Connected to {agent.ComputerName} ({agent.OutputCount} outputs)");
        });
    }

    private void OnAgentDisconnected(object? sender, AgentEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var agent = _agents.FirstOrDefault(a => a.MachineId == e.MachineId);
            if (agent != null)
            {
                _agents.Remove(agent);
                UpdateAgentStatus();
                UpdateStatusBar($"Disconnected from {agent.ComputerName}");
            }
        });
    }

    private void OnAgentStatusChanged(object? sender, AgentStatusEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var agent = _agents.FirstOrDefault(a => a.MachineId == e.MachineId);
            if (agent != null)
            {
                agent.IsOnline = e.Status.State == AgentState.Ready;
                agent.StatusColor = agent.IsOnline
                    ? FindResource("SuccessBrush") as SolidColorBrush ?? Brushes.Green
                    : FindResource("ErrorBrush") as SolidColorBrush ?? Brushes.Red;
            }
        });
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Load screen sets from persistence
        try
        {
            var savedScreenSets = await _persistence.LoadAsync();
            foreach (var data in savedScreenSets)
            {
                var screenSet = new ScreenSetViewModel
                {
                    ScreenSetId = data.Id,
                    Name = data.Name,
                    Columns = data.Columns,
                    Rows = data.Rows,
                    SizeText = $"{data.Columns}x{data.Rows} ({data.Columns * data.Rows} screens)"
                };

                // Restore assignments
                foreach (var assignmentData in data.Assignments)
                {
                    var key = screenSet.GetKey(assignmentData.Column, assignmentData.Row);
                    screenSet.Assignments[key] = new ScreenAssignment
                    {
                        Column = assignmentData.Column,
                        Row = assignmentData.Row,
                        OutputId = assignmentData.OutputId,
                        AgentMachineId = assignmentData.AgentMachineId,
                        DisplayName = assignmentData.DisplayName
                    };
                }

                _screenSets.Add(screenSet);
            }

            // Select first screen set if available
            if (_screenSets.Count > 0)
            {
                lstScreenSets.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load screen sets: {ex.Message}");
        }

        UpdateStatusBar("Ready - waiting for agents to connect");
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _statusTimer.Stop();

        // Save screen sets
        try
        {
            var dataToSave = _screenSets.Select(ss => new ScreenSetData
            {
                Id = ss.ScreenSetId,
                Name = ss.Name,
                Columns = ss.Columns,
                Rows = ss.Rows,
                Assignments = ss.Assignments.Values.Select(a => new ScreenAssignmentData
                {
                    Column = a.Column,
                    Row = a.Row,
                    OutputId = a.OutputId,
                    AgentMachineId = a.AgentMachineId,
                    DisplayName = a.DisplayName
                }).ToList()
            }).ToList();

            await _persistence.SaveAsync(dataToSave);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save screen sets: {ex.Message}");
        }

        _agentManager.Dispose();
    }

    private void UpdateAgentStatus()
    {
        // Update agent count
        int onlineCount = _agents.Count(a => a.IsOnline);
        int totalOutputs = _agents.Sum(a => a.OutputCount);

        txtAgentCount.Text = $"{onlineCount} Agent{(onlineCount != 1 ? "s" : "")}";
        txtOutputCount.Text = $"{totalOutputs} Output{(totalOutputs != 1 ? "s" : "")}";

        agentStatusDot.Fill = onlineCount > 0
            ? FindResource("SuccessBrush") as SolidColorBrush
            : FindResource("MutedBrush") as SolidColorBrush;
    }

    private void UpdateStatusBar(string message)
    {
        txtStatusMessage.Text = message;
    }

    private void AddAgent_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddAgentDialog();
        if (dialog.ShowDialog() == true)
        {
            // Connect to agent
            ConnectToAgent(dialog.IpAddress, dialog.Port);
        }
    }

    private async void ConnectToAgent(string ipAddress, int port)
    {
        try
        {
            UpdateStatusBar($"Connecting to {ipAddress}:{port}...");

            var client = await _agentManager.ConnectAsync(ipAddress, port);

            if (client == null)
            {
                UpdateStatusBar($"Failed to connect to {ipAddress}:{port}");
                MessageBox.Show($"Could not connect to agent at {ipAddress}:{port}\n\nMake sure the VideoWall Agent is running on the remote machine and the firewall allows port {port}.",
                    "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            // Note: Success is handled by OnAgentConnected event handler
        }
        catch (Exception ex)
        {
            UpdateStatusBar($"Failed to connect: {ex.Message}");
            MessageBox.Show($"Failed to connect to agent at {ipAddress}:{port}\n\n{ex.Message}",
                "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Settings dialog not yet implemented", "Settings",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AddScreenSet_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ScreenSetDialog();
        if (dialog.ShowDialog() == true)
        {
            var screenSet = new ScreenSetViewModel
            {
                ScreenSetId = Guid.NewGuid().ToString(),
                Name = dialog.ScreenSetName,
                Columns = dialog.Columns,
                Rows = dialog.Rows,
                SizeText = $"{dialog.Columns}x{dialog.Rows} ({dialog.Columns * dialog.Rows} screens)"
            };

            _screenSets.Add(screenSet);
            UpdateStatusBar($"Created screen set: {screenSet.Name}");
        }
    }

    private void EditScreenSet_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string screenSetId)
        {
            var screenSet = _screenSets.FirstOrDefault(s => s.ScreenSetId == screenSetId);
            if (screenSet != null)
            {
                MessageBox.Show($"Edit screen set: {screenSet.Name}\n\nEditor not yet implemented",
                    "Edit Screen Set", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void ScreenSet_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstScreenSets.SelectedItem is ScreenSetViewModel screenSetVm)
        {
            txtScreenSetName.Text = $" - {screenSetVm.Name}";
            txtNoScreens.Visibility = Visibility.Collapsed;

            // Build screen grid UI
            BuildScreenGrid(screenSetVm);

            UpdateStatusBar($"Selected screen set: {screenSetVm.Name}");
        }
    }

    private void BuildScreenGrid(ScreenSetViewModel screenSetVm)
    {
        screenGrid.Children.Clear();
        screenGrid.RowDefinitions.Clear();
        screenGrid.ColumnDefinitions.Clear();

        // Add row/column definitions
        for (int r = 0; r < screenSetVm.Rows; r++)
        {
            screenGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }
        for (int c = 0; c < screenSetVm.Columns; c++)
        {
            screenGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // Add screen tiles
        int screenIndex = 0;
        for (int r = 0; r < screenSetVm.Rows; r++)
        {
            for (int c = 0; c < screenSetVm.Columns; c++)
            {
                var tile = CreateScreenTile(screenIndex, c, r);
                Grid.SetRow(tile, r);
                Grid.SetColumn(tile, c);
                screenGrid.Children.Add(tile);
                screenIndex++;
            }
        }
    }

    private Border CreateScreenTile(int index, int col, int row)
    {
        var selectedScreenSet = lstScreenSets.SelectedItem as ScreenSetViewModel;
        var key = selectedScreenSet?.GetKey(col, row) ?? "";
        var assignment = selectedScreenSet?.Assignments.GetValueOrDefault(key);

        var tile = new Border
        {
            Background = assignment != null
                ? new SolidColorBrush(Color.FromRgb(0x1A, 0x2A, 0x3A))
                : new SolidColorBrush(Color.FromRgb(0x0D, 0x14, 0x22)),
            BorderBrush = assignment != null
                ? FindResource("PrimaryBrush") as SolidColorBrush
                : FindResource("BorderBrush") as SolidColorBrush,
            BorderThickness = new Thickness(assignment != null ? 2 : 1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(4),
            MinHeight = 80,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = new ScreenTileInfo { Index = index, Column = col, Row = row }
        };

        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        content.Children.Add(new TextBlock
        {
            Text = $"Screen {index + 1}",
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        content.Children.Add(new TextBlock
        {
            Text = $"[{col},{row}]",
            Foreground = FindResource("MutedBrush") as SolidColorBrush,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center
        });

        var statusText = new TextBlock
        {
            Text = assignment?.DisplayName ?? "Click to assign output",
            Foreground = assignment != null
                ? FindResource("SuccessBrush") as SolidColorBrush
                : FindResource("MutedBrush") as SolidColorBrush,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        };
        content.Children.Add(statusText);

        // Show media filename if playing
        if (assignment != null && !string.IsNullOrEmpty(assignment.MediaFileName))
        {
            var mediaText = new TextBlock
            {
                Text = $"Playing: {assignment.MediaFileName}",
                Foreground = FindResource("PrimaryBrush") as SolidColorBrush,
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                MaxWidth = 150
            };
            content.Children.Add(mediaText);
        }

        tile.Child = content;

        // Click to assign output
        tile.MouseLeftButtonUp += ScreenTile_Click;

        // Add hover effect
        tile.MouseEnter += (s, e) =>
        {
            tile.BorderBrush = FindResource("PrimaryBrush") as SolidColorBrush;
        };
        tile.MouseLeave += (s, e) =>
        {
            if (assignment == null)
                tile.BorderBrush = FindResource("BorderBrush") as SolidColorBrush;
            else
                tile.BorderBrush = FindResource("PrimaryBrush") as SolidColorBrush;
        };

        return tile;
    }

    private void ScreenTile_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Border tile || tile.Tag is not ScreenTileInfo tileInfo)
            return;

        var selectedScreenSet = lstScreenSets.SelectedItem as ScreenSetViewModel;
        if (selectedScreenSet == null)
            return;

        // Get all available outputs from connected agents
        var availableOutputs = new List<(AgentViewModel agent, OutputInfo output)>();
        foreach (var agent in _agents.Where(a => a.IsOnline))
        {
            foreach (var output in agent.Outputs)
            {
                availableOutputs.Add((agent, output));
            }
        }

        if (availableOutputs.Count == 0)
        {
            MessageBox.Show("No outputs available.\n\nConnect to an agent first to see available display outputs.",
                "No Outputs", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Create context menu with available outputs
        var menu = new ContextMenu();
        var key = selectedScreenSet.GetKey(tileInfo.Column, tileInfo.Row);
        var assignment = selectedScreenSet.Assignments.GetValueOrDefault(key);

        // If tile has an output assigned, show media options first
        if (assignment != null)
        {
            // Play Media option
            var playMediaItem = new MenuItem { Header = "Play Media...", FontWeight = FontWeights.SemiBold };
            playMediaItem.Click += async (s, args) =>
            {
                await PlayMediaOnTileAsync(assignment, tileInfo.Index);
            };
            menu.Items.Add(playMediaItem);

            // Play Slideshow option
            var playSlideshowItem = new MenuItem { Header = "Play Slideshow..." };
            playSlideshowItem.Click += async (s, args) =>
            {
                await PlaySlideshowOnTileAsync(assignment, tileInfo.Index);
            };
            menu.Items.Add(playSlideshowItem);

            // Show Image with Transition submenu
            var showImageMenu = new MenuItem { Header = "Show Image with Transition" };
            var transitions = new[]
            {
                ("Fade", VideoWall.Network.Grpc.TransitionType.TransitionFade),
                ("Wipe Left", VideoWall.Network.Grpc.TransitionType.TransitionWipeLeft),
                ("Wipe Right", VideoWall.Network.Grpc.TransitionType.TransitionWipeRight),
                ("Zoom In", VideoWall.Network.Grpc.TransitionType.TransitionZoomIn),
                ("Dissolve", VideoWall.Network.Grpc.TransitionType.TransitionDissolve),
                ("Blur", VideoWall.Network.Grpc.TransitionType.TransitionBlur)
            };
            foreach (var (name, transition) in transitions)
            {
                var transitionItem = new MenuItem { Header = name, Tag = transition };
                transitionItem.Click += async (s, args) =>
                {
                    await ShowImageWithTransitionAsync(assignment, tileInfo.Index, transition);
                };
                showImageMenu.Items.Add(transitionItem);
            }
            menu.Items.Add(showImageMenu);

            menu.Items.Add(new Separator());

            // Stop option (if media is playing)
            if (!string.IsNullOrEmpty(assignment.MediaPath))
            {
                var stopItem = new MenuItem { Header = "Stop Playback" };
                stopItem.Click += async (s, args) =>
                {
                    await StopMediaOnTileAsync(assignment, tileInfo.Index);
                };
                menu.Items.Add(stopItem);

                var pauseItem = new MenuItem { Header = "Pause" };
                pauseItem.Click += async (s, args) =>
                {
                    await PauseMediaOnTileAsync(assignment, tileInfo.Index);
                };
                menu.Items.Add(pauseItem);
            }

            menu.Items.Add(new Separator());

            // Clear assignment option
            var clearItem = new MenuItem { Header = "Clear Assignment" };
            clearItem.Click += (s, args) =>
            {
                selectedScreenSet.Assignments.Remove(key);
                BuildScreenGrid(selectedScreenSet);
                UpdateStatusBar($"Cleared assignment for Screen {tileInfo.Index + 1}");
            };
            menu.Items.Add(clearItem);
            menu.Items.Add(new Separator());
        }

        // Add "Assign Output" submenu
        var assignMenu = new MenuItem { Header = "Assign Output" };
        foreach (var (agent, output) in availableOutputs)
        {
            var item = new MenuItem
            {
                Header = $"{agent.ComputerName}: {output.DisplayName} ({output.Resolution})",
                Tag = new OutputAssignmentInfo { Agent = agent, Output = output }
            };
            item.Click += (s, args) =>
            {
                if (s is MenuItem menuItem && menuItem.Tag is OutputAssignmentInfo info)
                {
                    selectedScreenSet.Assignments[key] = new ScreenAssignment
                    {
                        Column = tileInfo.Column,
                        Row = tileInfo.Row,
                        OutputId = info.Output.OutputId,
                        AgentMachineId = info.Agent.MachineId,
                        DisplayName = $"{info.Agent.ComputerName}\n{info.Output.DisplayName}"
                    };
                    BuildScreenGrid(selectedScreenSet);
                    UpdateStatusBar($"Assigned {info.Output.DisplayName} to Screen {tileInfo.Index + 1}");
                }
            };
            assignMenu.Items.Add(item);
        }
        menu.Items.Add(assignMenu);

        menu.PlacementTarget = tile;
        menu.IsOpen = true;
    }

    private async Task PlayMediaOnTileAsync(ScreenAssignment assignment, int screenIndex)
    {
        if (string.IsNullOrEmpty(assignment.OutputId))
            return;

        var dialog = new OpenFileDialog
        {
            Title = $"Select Media for Screen {screenIndex + 1}",
            Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm|Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Media|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*",
            FilterIndex = 3
        };

        if (dialog.ShowDialog() == true)
        {
            var mediaPath = dialog.FileName;

            // For remote agents, the path needs to be accessible from the agent machine
            // This could be a UNC path or a path on the agent's local filesystem
            UpdateStatusBar($"Playing {Path.GetFileName(mediaPath)} on Screen {screenIndex + 1}...");

            try
            {
                var success = await _agentManager.PlayAsync(assignment.OutputId, mediaPath);

                if (success)
                {
                    assignment.MediaPath = mediaPath;
                    assignment.MediaFileName = Path.GetFileName(mediaPath);

                    // Refresh the grid to show media info
                    if (lstScreenSets.SelectedItem is ScreenSetViewModel screenSet)
                    {
                        BuildScreenGrid(screenSet);
                    }

                    UpdateStatusBar($"Playing {assignment.MediaFileName} on Screen {screenIndex + 1}");
                }
                else
                {
                    MessageBox.Show($"Failed to play media on Screen {screenIndex + 1}.\n\nMake sure the file path is accessible from the agent machine.",
                        "Playback Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    UpdateStatusBar("Playback failed");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error playing media: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatusBar("Playback error");
            }
        }
    }

    private async Task StopMediaOnTileAsync(ScreenAssignment assignment, int screenIndex)
    {
        if (string.IsNullOrEmpty(assignment.OutputId))
            return;

        try
        {
            var client = _agentManager.GetAgentForOutput(assignment.OutputId);
            if (client != null)
            {
                await client.StopAsync(assignment.OutputId);
                assignment.MediaPath = null;
                assignment.MediaFileName = null;

                if (lstScreenSets.SelectedItem is ScreenSetViewModel screenSet)
                {
                    BuildScreenGrid(screenSet);
                }

                UpdateStatusBar($"Stopped playback on Screen {screenIndex + 1}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error stopping playback: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task PauseMediaOnTileAsync(ScreenAssignment assignment, int screenIndex)
    {
        if (string.IsNullOrEmpty(assignment.OutputId))
            return;

        try
        {
            var client = _agentManager.GetAgentForOutput(assignment.OutputId);
            if (client != null)
            {
                await client.PauseAsync(assignment.OutputId);
                UpdateStatusBar($"Paused playback on Screen {screenIndex + 1}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error pausing playback: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task PlaySlideshowOnTileAsync(ScreenAssignment assignment, int screenIndex)
    {
        if (string.IsNullOrEmpty(assignment.OutputId))
            return;

        var dialog = new SlideshowDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            UpdateStatusBar($"Starting slideshow with {dialog.ImagePaths.Count} images on Screen {screenIndex + 1}...");

            try
            {
                var client = _agentManager.GetAgentForOutput(assignment.OutputId);
                if (client != null)
                {
                    var slides = dialog.ImagePaths.Select(p => (p, dialog.DurationMs)).ToList();
                    var success = await client.StartSlideshowAsync(
                        assignment.OutputId,
                        slides,
                        dialog.DurationMs,
                        dialog.Transition,
                        dialog.TransitionDurationMs,
                        dialog.Loop);

                    if (success)
                    {
                        assignment.MediaPath = "slideshow";
                        assignment.MediaFileName = $"Slideshow ({dialog.ImagePaths.Count} images)";

                        if (lstScreenSets.SelectedItem is ScreenSetViewModel screenSet)
                        {
                            BuildScreenGrid(screenSet);
                        }

                        UpdateStatusBar($"Slideshow started on Screen {screenIndex + 1}");
                    }
                    else
                    {
                        MessageBox.Show($"Failed to start slideshow on Screen {screenIndex + 1}.\n\nMake sure the image paths are accessible from the agent machine.",
                            "Slideshow Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        UpdateStatusBar("Slideshow failed");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting slideshow: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatusBar("Slideshow error");
            }
        }
    }

    private async Task ShowImageWithTransitionAsync(ScreenAssignment assignment, int screenIndex, VideoWall.Network.Grpc.TransitionType transition)
    {
        if (string.IsNullOrEmpty(assignment.OutputId))
            return;

        var dialog = new OpenFileDialog
        {
            Title = $"Select Image for Screen {screenIndex + 1}",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All Files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            var imagePath = dialog.FileName;
            UpdateStatusBar($"Showing {Path.GetFileName(imagePath)} with {transition} transition on Screen {screenIndex + 1}...");

            try
            {
                var client = _agentManager.GetAgentForOutput(assignment.OutputId);
                if (client != null)
                {
                    var success = await client.ShowImageAsync(assignment.OutputId, imagePath, transition, 1000);

                    if (success)
                    {
                        assignment.MediaPath = imagePath;
                        assignment.MediaFileName = Path.GetFileName(imagePath);

                        if (lstScreenSets.SelectedItem is ScreenSetViewModel screenSet)
                        {
                            BuildScreenGrid(screenSet);
                        }

                        UpdateStatusBar($"Showing {assignment.MediaFileName} on Screen {screenIndex + 1}");
                    }
                    else
                    {
                        MessageBox.Show($"Failed to show image on Screen {screenIndex + 1}.",
                            "Display Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        UpdateStatusBar("Display failed");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatusBar("Display error");
            }
        }
    }

    private class ScreenTileInfo
    {
        public int Index { get; set; }
        public int Column { get; set; }
        public int Row { get; set; }
    }

    private class OutputAssignmentInfo
    {
        public AgentViewModel Agent { get; set; } = null!;
        public OutputInfo Output { get; set; } = null!;
    }

    private void AgentMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string machineId)
        {
            var agent = _agents.FirstOrDefault(a => a.MachineId == machineId);
            if (agent != null)
            {
                var menu = new ContextMenu();
                menu.Items.Add(new MenuItem { Header = "View Details", Tag = machineId });
                menu.Items.Add(new MenuItem { Header = "Refresh Hardware", Tag = machineId });
                menu.Items.Add(new Separator());
                menu.Items.Add(new MenuItem { Header = "Run Test Pattern", Tag = machineId });
                menu.Items.Add(new Separator());
                menu.Items.Add(new MenuItem { Header = "Disconnect", Tag = machineId });

                foreach (MenuItem item in menu.Items.OfType<MenuItem>())
                {
                    item.Click += AgentMenuItem_Click;
                }

                menu.IsOpen = true;
            }
        }
    }

    private void AgentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item)
        {
            var machineId = item.Tag as string;
            var header = item.Header.ToString();

            switch (header)
            {
                case "Disconnect":
                    var agent = _agents.FirstOrDefault(a => a.MachineId == machineId);
                    if (agent != null)
                    {
                        _agents.Remove(agent);
                        UpdateAgentStatus();
                        UpdateStatusBar($"Disconnected from {agent.ComputerName}");
                    }
                    break;
                default:
                    MessageBox.Show($"{header} for {machineId} - Not yet implemented",
                        "Agent Action", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }
    }

    private async void PlayMediaOnAll_Click(object sender, RoutedEventArgs e)
    {
        // Get current screen set
        if (lstScreenSets.SelectedItem is not ScreenSetViewModel screenSet)
        {
            MessageBox.Show("Please select a Screen Set first.", "No Screen Set",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Get all assigned outputs
        var assignments = screenSet.Assignments.Values.Where(a => !string.IsNullOrEmpty(a.OutputId)).ToList();
        if (assignments.Count == 0)
        {
            MessageBox.Show("No outputs assigned.\n\nClick on screen tiles to assign outputs first.",
                "No Outputs Assigned", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Open file dialog
        var dialog = new OpenFileDialog
        {
            Title = $"Select Media for All {assignments.Count} Assigned Outputs",
            Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm|Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Media|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.jpg;*.jpeg;*.png;*.bmp;*.gif|All Files|*.*",
            FilterIndex = 3
        };

        if (dialog.ShowDialog() == true)
        {
            var mediaPath = dialog.FileName;
            var fileName = Path.GetFileName(mediaPath);

            UpdateStatusBar($"Starting synchronized playback of {fileName} on {assignments.Count} outputs...");

            try
            {
                // Build the output-to-media mapping
                var outputMediaMap = new Dictionary<string, string>();
                foreach (var assignment in assignments)
                {
                    if (!string.IsNullOrEmpty(assignment.OutputId))
                    {
                        outputMediaMap[assignment.OutputId] = mediaPath;
                    }
                }

                // Play synchronized
                await _agentManager.PlaySynchronizedAsync(outputMediaMap);

                // Update UI with media info
                foreach (var assignment in assignments)
                {
                    assignment.MediaPath = mediaPath;
                    assignment.MediaFileName = fileName;
                }
                BuildScreenGrid(screenSet);

                UpdateStatusBar($"Playing {fileName} on {assignments.Count} outputs (synchronized)");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start synchronized playback: {ex.Message}",
                    "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatusBar("Synchronized playback failed");
            }
        }
    }

    private async void PlayAll_Click(object sender, RoutedEventArgs e)
    {
        // Resume all paused outputs
        UpdateStatusBar("Resuming all outputs...");
        try
        {
            await _agentManager.ResumeAllAsync();
            UpdateStatusBar("Resumed all outputs");
        }
        catch (Exception ex)
        {
            UpdateStatusBar($"Error: {ex.Message}");
        }
    }

    private async void PauseAll_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatusBar("Pausing all outputs...");
        try
        {
            await _agentManager.PauseAllAsync();
            UpdateStatusBar("Paused all outputs");
        }
        catch (Exception ex)
        {
            UpdateStatusBar($"Error: {ex.Message}");
        }
    }

    private async void StopAll_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatusBar("Stopping all outputs...");
        try
        {
            await _agentManager.StopAllAsync();

            // Clear media assignments from UI
            if (lstScreenSets.SelectedItem is ScreenSetViewModel screenSet)
            {
                foreach (var assignment in screenSet.Assignments.Values)
                {
                    assignment.MediaPath = null;
                    assignment.MediaFileName = null;
                }
                BuildScreenGrid(screenSet);
            }

            UpdateStatusBar("Stopped all outputs");
        }
        catch (Exception ex)
        {
            UpdateStatusBar($"Error: {ex.Message}");
        }
    }

    private async void TestSyncFlash_Click(object sender, RoutedEventArgs e)
    {
        if (_agents.Count == 0)
        {
            MessageBox.Show("No agents connected.\n\nConnect to at least one agent first.",
                "No Agents", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        UpdateStatusBar("Running sync flash test...");
        try
        {
            await _agentManager.RunSyncFlashTestAsync(5);
            UpdateStatusBar("Sync flash test completed");
        }
        catch (Exception ex)
        {
            UpdateStatusBar($"Sync flash test failed: {ex.Message}");
        }
    }

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        // Show diagnostic info
        var info = new System.Text.StringBuilder();
        info.AppendLine("VideoWall Diagnostics");
        info.AppendLine("=====================");
        info.AppendLine();
        info.AppendLine($"Connected Agents: {_agents.Count}");
        info.AppendLine($"Total Outputs: {_agents.Sum(a => a.OutputCount)}");
        info.AppendLine();

        foreach (var agent in _agents)
        {
            info.AppendLine($"Agent: {agent.ComputerName}");
            info.AppendLine($"  IP: {agent.IpAddress}:{agent.Port}");
            info.AppendLine($"  Status: {(agent.IsOnline ? "Online" : "Offline")}");
            info.AppendLine($"  Outputs: {agent.OutputCount}");
            foreach (var output in agent.Outputs)
            {
                info.AppendLine($"    - {output.DisplayName} ({output.Resolution})");
            }
            info.AppendLine();
        }

        if (lstScreenSets.SelectedItem is ScreenSetViewModel screenSet)
        {
            info.AppendLine($"Current Screen Set: {screenSet.Name}");
            info.AppendLine($"Grid Size: {screenSet.Columns}x{screenSet.Rows}");
            info.AppendLine($"Assigned Tiles: {screenSet.Assignments.Count}");
            foreach (var (key, assignment) in screenSet.Assignments)
            {
                info.AppendLine($"  [{key}] -> {assignment.DisplayName?.Replace("\n", " - ")}");
                if (!string.IsNullOrEmpty(assignment.MediaFileName))
                {
                    info.AppendLine($"         Playing: {assignment.MediaFileName}");
                }
            }
        }

        MessageBox.Show(info.ToString(), "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

// ViewModels
public class AgentViewModel
{
    public string MachineId { get; set; } = string.Empty;
    public string ComputerName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public int OutputCount { get; set; }
    public bool IsOnline { get; set; }
    public System.Windows.Media.Brush StatusColor { get; set; } = System.Windows.Media.Brushes.Gray;
    public List<OutputInfo> Outputs { get; set; } = new();
}

public class OutputInfo
{
    public string OutputId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Resolution { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
}

public class ScreenSetViewModel
{
    public string ScreenSetId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Columns { get; set; }
    public int Rows { get; set; }
    public string SizeText { get; set; } = string.Empty;
    public Dictionary<string, ScreenAssignment> Assignments { get; set; } = new();

    public string GetKey(int col, int row) => $"{col},{row}";
}

public class ScreenAssignment
{
    public int Column { get; set; }
    public int Row { get; set; }
    public string? OutputId { get; set; }
    public string? AgentMachineId { get; set; }
    public string? DisplayName { get; set; }
    public string? MediaPath { get; set; }
    public string? MediaFileName { get; set; }
}
