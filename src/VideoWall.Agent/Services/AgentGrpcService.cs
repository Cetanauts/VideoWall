using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using VideoWall.Hardware;
using VideoWall.Network.Grpc;
using VideoWall.Playback;

namespace VideoWall.Agent.Services;

/// <summary>
/// gRPC service implementation for the VideoWall Agent
/// </summary>
public class AgentGrpcService : AgentService.AgentServiceBase
{
    private readonly string _machineId;
    private readonly DisplayDetector _displayDetector;
    private readonly DisplayWindowManager _displayManager;
    private readonly OverlayManager _overlayManager;
    private readonly Core.Sync.SyncClock _syncClock;
    private readonly DateTime _startTime;
    private readonly System.Timers.Timer _connectionTimeoutTimer;
    private DateTime _lastCommandTime;
    private bool _hasActiveConnection;
    private const int ConnectionTimeoutSeconds = 15; // Time without commands before assuming disconnect

    public AgentGrpcService(
        string machineId,
        DisplayDetector displayDetector,
        DisplayWindowManager displayManager,
        OverlayManager overlayManager,
        Core.Sync.SyncClock syncClock)
    {
        _machineId = machineId;
        _displayDetector = displayDetector;
        _displayManager = displayManager;
        _overlayManager = overlayManager;
        _syncClock = syncClock;
        _startTime = DateTime.UtcNow;
        _lastCommandTime = DateTime.UtcNow;
        _hasActiveConnection = false;

        // Setup connection timeout timer
        _connectionTimeoutTimer = new System.Timers.Timer(5000); // Check every 5 seconds
        _connectionTimeoutTimer.Elapsed += CheckConnectionTimeout;
        _connectionTimeoutTimer.AutoReset = true;
        _connectionTimeoutTimer.Start();
    }

    private void UpdateLastCommandTime()
    {
        _lastCommandTime = DateTime.UtcNow;
        _hasActiveConnection = true;
    }

    private void CheckConnectionTimeout(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_hasActiveConnection)
            return;

        var timeSinceLastCommand = DateTime.UtcNow - _lastCommandTime;
        if (timeSinceLastCommand.TotalSeconds > ConnectionTimeoutSeconds)
        {
            // Connection lost - close all display windows
            System.Diagnostics.Debug.WriteLine($"Master connection timeout detected ({timeSinceLastCommand.TotalSeconds}s). Closing display windows.");
            _hasActiveConnection = false;
            _displayManager.CloseAllWindows();
        }
    }

    public override Task<PingResponse> Ping(Empty request, ServerCallContext context)
    {
        UpdateLastCommandTime();
        return Task.FromResult(new PingResponse
        {
            Success = true,
            AgentVersion = "1.0.0",
            ServerTime = Timestamp.FromDateTime(_syncClock.Now)
        });
    }

    public override Task<AgentStatusResponse> GetStatus(Empty request, ServerCallContext context)
    {
        UpdateLastCommandTime();
        var machineInfo = _displayDetector.GetMachineInfo();
        var windowStatus = _displayManager.GetAllStatus();

        var response = new AgentStatusResponse
        {
            MachineId = _machineId,
            ComputerName = machineInfo.ComputerName,
            AgentVersion = "1.0.0",
            State = AgentState.Ready,
            UptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds,
            CpuUsage = 0, // TODO: Implement CPU monitoring
            AvailableMemoryMb = machineInfo.AvailableRamMB
        };

        foreach (var output in machineInfo.GetAllOutputs())
        {
            var status = windowStatus.TryGetValue(output.OutputId, out var ws) ? ws : null;

            response.Outputs.Add(new OutputStatus
            {
                OutputId = output.OutputId,
                FriendlyName = output.FriendlyName,
                IsEnabled = output.IsEnabled,
                IsConnected = output.Status == Core.Enums.OutputStatus.Connected,
                PlaybackState = ConvertPlaybackState(status?.State ?? Core.Enums.PlaybackState.Stopped),
                CurrentMedia = status?.MediaPath ?? "",
                PositionMs = status?.PositionMs ?? 0,
                DurationMs = status?.DurationMs ?? 0,
                Volume = status?.Volume ?? 100
            });
        }

        return Task.FromResult(response);
    }

    public override Task<HardwareInfoResponse> GetHardwareInfo(Empty request, ServerCallContext context)
    {
        var machineInfo = _displayDetector.GetMachineInfo();

        var response = new HardwareInfoResponse
        {
            MachineId = _machineId,
            ComputerName = machineInfo.ComputerName,
            OsVersion = machineInfo.OsVersion,
            TotalRamMb = machineInfo.TotalRamMB,
            VlcVersion = _displayManager.VlcVersion
        };

        foreach (var gpu in machineInfo.Gpus)
        {
            var gpuInfo = new Network.Grpc.GpuInfo
            {
                GpuId = gpu.GpuId,
                Name = gpu.Name,
                Manufacturer = gpu.Manufacturer,
                DriverVersion = gpu.DriverVersion,
                VideoMemoryMb = gpu.VideoMemoryMB
            };

            foreach (var output in gpu.Outputs)
            {
                gpuInfo.Outputs.Add(new DisplayOutputInfo
                {
                    OutputId = output.OutputId,
                    DeviceName = output.DeviceName,
                    FriendlyName = output.FriendlyName,
                    OutputType = output.OutputType,
                    Width = output.Width,
                    Height = output.Height,
                    RefreshRate = output.RefreshRate,
                    IsPrimary = output.IsPrimary,
                    IsConnected = output.Status == Core.Enums.OutputStatus.Connected,
                    Bounds = new Network.Grpc.ScreenBounds
                    {
                        X = output.Bounds.X,
                        Y = output.Bounds.Y,
                        Width = output.Bounds.Width,
                        Height = output.Bounds.Height
                    }
                });
            }

            response.Gpus.Add(gpuInfo);
        }

        return Task.FromResult(response);
    }

    public override async Task<PlayResponse> Play(PlayRequest request, ServerCallContext context)
    {
        UpdateLastCommandTime();
        Console.WriteLine($"[AgentGrpcService] Play received: output={request.OutputId}, media={request.MediaPath}, autoPlay={request.AutoPlay}");
        try
        {
            bool success = await _displayManager.PlayVideoAsync(
                request.OutputId,
                request.MediaPath,
                request.AutoPlay,
                request.StartPositionMs,
                request.Volume > 0 ? request.Volume : 100,
                request.Loop
            );

            Console.WriteLine($"[AgentGrpcService] Play result: success={success}");
            return new PlayResponse
            {
                Success = success,
                Message = success ? "Playback started" : "Failed to load media",
                DurationMs = 0 // Duration will be available once playing
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AgentGrpcService] Play error: {ex.Message}");
            return new PlayResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public override Task<OperationResponse> Pause(OutputRequest request, ServerCallContext context)
    {
        try
        {
            _displayManager.Pause(request.OutputId);
            return Task.FromResult(new OperationResponse { Success = true, Message = "Paused" });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<OperationResponse> Stop(OutputRequest request, ServerCallContext context)
    {
        try
        {
            _displayManager.Stop(request.OutputId);
            return Task.FromResult(new OperationResponse { Success = true, Message = "Stopped" });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<OperationResponse> Resume(OutputRequest request, ServerCallContext context)
    {
        try
        {
            _displayManager.Resume(request.OutputId);
            return Task.FromResult(new OperationResponse { Success = true, Message = "Resumed" });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<OperationResponse> Seek(SeekRequest request, ServerCallContext context)
    {
        try
        {
            _displayManager.SeekTo(request.OutputId, request.PositionMs);
            return Task.FromResult(new OperationResponse { Success = true, Message = "Seeked" });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<OperationResponse> SetVolume(VolumeRequest request, ServerCallContext context)
    {
        try
        {
            _displayManager.SetVolume(request.OutputId, request.Volume, request.Mute);
            return Task.FromResult(new OperationResponse { Success = true, Message = "Volume set" });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<OperationResponse> StartSlideshow(SlideshowRequest request, ServerCallContext context)
    {
        UpdateLastCommandTime();
        Console.WriteLine($"[AgentGrpcService] StartSlideshow received: output={request.OutputId}, slides={request.Slides.Count}, duration={request.DefaultDurationMs}ms");
        try
        {
            if (request.Slides.Count == 0)
            {
                return Task.FromResult(new OperationResponse { Success = false, Message = "No slides provided" });
            }

            // Convert proto slides to SlideshowSlide objects
            var slides = request.Slides.Select(s => new SlideshowSlide
            {
                MediaPath = s.MediaPath,
                DurationMs = s.DurationMs > 0 ? s.DurationMs : request.DefaultDurationMs
            }).ToList();

            // Convert transition type
            var transition = ConvertTransition(request.DefaultTransition);

            // Start the slideshow
            _displayManager.StartSlideshow(
                request.OutputId,
                slides,
                request.DefaultDurationMs,
                transition,
                request.DefaultTransitionDurationMs,
                request.Loop);

            return Task.FromResult(new OperationResponse
            {
                Success = true,
                Message = $"Slideshow started with {slides.Count} slides"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<OperationResponse> NextSlide(OutputRequest request, ServerCallContext context)
    {
        try
        {
            _displayManager.NextSlide(request.OutputId);
            return Task.FromResult(new OperationResponse { Success = true, Message = "Next slide" });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<OperationResponse> PreviousSlide(OutputRequest request, ServerCallContext context)
    {
        try
        {
            _displayManager.PreviousSlide(request.OutputId);
            return Task.FromResult(new OperationResponse { Success = true, Message = "Previous slide" });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<OperationResponse> GoToSlide(GoToSlideRequest request, ServerCallContext context)
    {
        try
        {
            _displayManager.GoToSlide(request.OutputId, request.SlideIndex);
            return Task.FromResult(new OperationResponse { Success = true, Message = $"Jumped to slide {request.SlideIndex}" });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override async Task<OperationResponse> PrepareSync(PrepareSyncRequest request, ServerCallContext context)
    {
        try
        {
            // Prepare media for each output (create windows but don't play)
            var tasks = new List<Task>();
            foreach (var config in request.Outputs)
            {
                // Load media on each output - must await to ensure media is ready before ExecuteSync
                tasks.Add(_displayManager.PlayVideoAsync(config.OutputId, config.MediaPath, autoPlay: false));
            }

            await Task.WhenAll(tasks);

            return new OperationResponse { Success = true, Message = "Sync prepared" };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AgentGrpcService] PrepareSync failed: {ex.Message}");
            return new OperationResponse { Success = false, Message = ex.Message };
        }
    }

    public override async Task<OperationResponse> ExecuteSync(ExecuteSyncRequest request, ServerCallContext context)
    {
        try
        {
            var executeTime = request.ExecuteAt.ToDateTime();

            // Wait until execute time
            await _syncClock.ExecuteAt(executeTime, () =>
            {
                // Start all prepared players
                _displayManager.ResumeAll();
            });

            return new OperationResponse { Success = true, Message = "Sync executed" };
        }
        catch (Exception ex)
        {
            return new OperationResponse { Success = false, Message = ex.Message };
        }
    }

    public override Task<OperationResponse> EnableOutput(OutputRequest request, ServerCallContext context)
    {
        // Create/show the window for this output
        try
        {
            _displayManager.GetOrCreateWindow(request.OutputId);
            return Task.FromResult(new OperationResponse { Success = true, Message = "Output enabled" });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<OperationResponse> DisableOutput(OutputRequest request, ServerCallContext context)
    {
        _displayManager.CloseWindow(request.OutputId);
        return Task.FromResult(new OperationResponse { Success = true, Message = "Output disabled" });
    }

    public override Task<OperationResponse> RunTestPattern(TestPatternRequest request, ServerCallContext context)
    {
        try
        {
            var patternType = request.Pattern switch
            {
                TestPatternType.TestPatternColorBars => TestPatternGenerator.PatternType.ColorBars,
                TestPatternType.TestPatternGrid => TestPatternGenerator.PatternType.Grid,
                TestPatternType.TestPatternSolidWhite => TestPatternGenerator.PatternType.SolidWhite,
                TestPatternType.TestPatternSolidBlack => TestPatternGenerator.PatternType.SolidBlack,
                TestPatternType.TestPatternSolidRed => TestPatternGenerator.PatternType.SolidRed,
                TestPatternType.TestPatternSolidGreen => TestPatternGenerator.PatternType.SolidGreen,
                TestPatternType.TestPatternSolidBlue => TestPatternGenerator.PatternType.SolidBlue,
                TestPatternType.TestPatternSyncFlash => TestPatternGenerator.PatternType.SyncFlash,
                TestPatternType.TestPatternOutputId => TestPatternGenerator.PatternType.OutputIdentifier,
                _ => TestPatternGenerator.PatternType.ColorBars
            };

            if (patternType == TestPatternGenerator.PatternType.SyncFlash)
            {
                _displayManager.StartSyncFlash(request.OutputId);

                // Auto-stop after duration
                if (request.DurationSeconds > 0)
                {
                    Task.Delay(TimeSpan.FromSeconds(request.DurationSeconds)).ContinueWith(_ =>
                    {
                        _displayManager.StopSyncFlash(request.OutputId);
                    });
                }
            }
            else
            {
                _displayManager.RunTestPattern(request.OutputId, patternType, request.DurationSeconds);
            }

            return Task.FromResult(new OperationResponse
            {
                Success = true,
                Message = $"Test pattern {request.Pattern} displayed on {request.OutputId}"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override async Task<ThumbnailResponse> GetThumbnail(OutputRequest request, ServerCallContext context)
    {
        try
        {
            var imageData = await _displayManager.TakeSnapshotAsync(request.OutputId, 320, 180);

            if (imageData != null)
            {
                return new ThumbnailResponse
                {
                    Success = true,
                    ImageData = Google.Protobuf.ByteString.CopyFrom(imageData),
                    ContentType = "image/png",
                    Width = 320,
                    Height = 180
                };
            }

            return new ThumbnailResponse { Success = false };
        }
        catch
        {
            return new ThumbnailResponse { Success = false };
        }
    }

    public override async Task StreamStatus(Empty request, IServerStreamWriter<StatusUpdate> responseStream, ServerCallContext context)
    {
        UpdateLastCommandTime();

        try
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                UpdateLastCommandTime(); // Keep connection alive
                var status = _displayManager.GetAllStatus();

                foreach (var (outputId, windowStatus) in status)
                {
                    await responseStream.WriteAsync(new StatusUpdate
                    {
                        OutputId = outputId,
                        State = ConvertPlaybackState(windowStatus.State),
                        PositionMs = windowStatus.PositionMs,
                        Timestamp = Timestamp.FromDateTime(_syncClock.Now)
                    });
                }

                await Task.Delay(1000, context.CancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Stream was cancelled (Master disconnected) - close display windows
            System.Diagnostics.Debug.WriteLine("Status stream cancelled. Master likely disconnected.");
        }
    }

    #region Overlay Operations

    public override Task<OperationResponse> SetOverlay(SetOverlayRequest request, ServerCallContext context)
    {
        UpdateLastCommandTime();
        try
        {
            var config = ConvertOverlayRequest(request);
            _overlayManager.SetOverlay(request.OutputId, config);

            return Task.FromResult(new OperationResponse
            {
                Success = true,
                Message = $"Overlay '{request.OverlayId}' set on {request.OutputId}"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<OperationResponse> RemoveOverlay(RemoveOverlayRequest request, ServerCallContext context)
    {
        UpdateLastCommandTime();
        try
        {
            _overlayManager.RemoveOverlay(request.OutputId, request.OverlayId);

            return Task.FromResult(new OperationResponse
            {
                Success = true,
                Message = $"Overlay '{request.OverlayId}' removed from {request.OutputId}"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<OperationResponse> ClearOverlays(OutputRequest request, ServerCallContext context)
    {
        UpdateLastCommandTime();
        try
        {
            _overlayManager.ClearOverlays(request.OutputId);

            return Task.FromResult(new OperationResponse
            {
                Success = true,
                Message = $"All overlays cleared from {request.OutputId}"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResponse { Success = false, Message = ex.Message });
        }
    }

    public override Task<ListOverlaysResponse> ListOverlays(OutputRequest request, ServerCallContext context)
    {
        UpdateLastCommandTime();
        try
        {
            var response = new ListOverlaysResponse();
            var configs = _overlayManager.GetOverlayConfigs(request.OutputId);

            foreach (var config in configs)
            {
                var info = new Network.Grpc.OverlayInfo
                {
                    OverlayId = config.OverlayId,
                    Type = config.Type.ToString(),
                    Position = FormatPosition(config.Position),
                    ForegroundColor = config.ForegroundColor,
                    FontSize = config.FontSize,
                    BackgroundColor = config.BackgroundColor,
                    Margin = config.Margin,
                    FontFamily = config.FontFamily ?? "Segoe UI",
                    PositionIndex = (int)config.Position,
                    // Map Core enum order to dialog combo order:
                    // Core: Clock=0, NewsTicker=1, StaticText=2, DateTime=3
                    // Dialog: Clock=0, Date/Time=1, Static Text=2, News Ticker=3
                    TypeIndex = config.Type switch
                    {
                        Core.Models.OverlayType.Clock => 0,
                        Core.Models.OverlayType.DateTime => 1,
                        Core.Models.OverlayType.StaticText => 2,
                        Core.Models.OverlayType.NewsTicker => 3,
                        _ => 0
                    }
                };

                switch (config.Type)
                {
                    case Core.Models.OverlayType.Clock:
                        info.Use24Hour = config.Clock?.Use24Hour ?? true;
                        info.ShowSeconds = config.Clock?.ShowSeconds ?? true;
                        info.TimezoneId = config.Clock?.TimeZoneId ?? "";
                        info.Format = config.Clock?.Format ?? "";
                        break;
                    case Core.Models.OverlayType.DateTime:
                        info.Format = config.DateTime?.Format ?? "dddd, MMMM d, yyyy h:mm tt";
                        info.TimezoneId = config.DateTime?.TimeZoneId ?? "";
                        break;
                    case Core.Models.OverlayType.StaticText:
                        info.Text = config.StaticText?.Text ?? "";
                        break;
                    case Core.Models.OverlayType.NewsTicker:
                        if (config.NewsTicker?.FeedUrls != null)
                            info.FeedUrls.AddRange(config.NewsTicker.FeedUrls);
                        info.ScrollSpeed = config.NewsTicker?.ScrollSpeedPx ?? 100;
                        info.RefreshIntervalMin = config.NewsTicker?.RefreshIntervalMinutes ?? 15;
                        break;
                }

                response.Overlays.Add(info);
            }

            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AgentGrpcService] ListOverlays error: {ex.Message}");
            return Task.FromResult(new ListOverlaysResponse());
        }
    }

    #endregion

    #region Conversion Helpers

    private static string FormatPosition(Core.Models.OverlayPosition pos)
    {
        return pos switch
        {
            Core.Models.OverlayPosition.TopLeft => "Top Left",
            Core.Models.OverlayPosition.TopCenter => "Top Center",
            Core.Models.OverlayPosition.TopRight => "Top Right",
            Core.Models.OverlayPosition.BottomLeft => "Bottom Left",
            Core.Models.OverlayPosition.BottomCenter => "Bottom Center",
            Core.Models.OverlayPosition.BottomRight => "Bottom Right",
            _ => pos.ToString()
        };
    }

    private static Network.Grpc.PlaybackState ConvertPlaybackState(Core.Enums.PlaybackState state)
    {
        return state switch
        {
            Core.Enums.PlaybackState.Stopped => Network.Grpc.PlaybackState.Stopped,
            Core.Enums.PlaybackState.Playing => Network.Grpc.PlaybackState.Playing,
            Core.Enums.PlaybackState.Paused => Network.Grpc.PlaybackState.Paused,
            Core.Enums.PlaybackState.Buffering => Network.Grpc.PlaybackState.Buffering,
            Core.Enums.PlaybackState.Error => Network.Grpc.PlaybackState.Error,
            _ => Network.Grpc.PlaybackState.Stopped
        };
    }

    private static Core.Enums.TransitionType ConvertTransition(Network.Grpc.TransitionType type)
    {
        return type switch
        {
            Network.Grpc.TransitionType.TransitionCut => Core.Enums.TransitionType.Cut,
            Network.Grpc.TransitionType.TransitionFade => Core.Enums.TransitionType.Fade,
            Network.Grpc.TransitionType.TransitionWipeLeft => Core.Enums.TransitionType.WipeLeft,
            Network.Grpc.TransitionType.TransitionWipeRight => Core.Enums.TransitionType.WipeRight,
            Network.Grpc.TransitionType.TransitionWipeUp => Core.Enums.TransitionType.WipeUp,
            Network.Grpc.TransitionType.TransitionWipeDown => Core.Enums.TransitionType.WipeDown,
            Network.Grpc.TransitionType.TransitionPushLeft => Core.Enums.TransitionType.PushLeft,
            Network.Grpc.TransitionType.TransitionPushRight => Core.Enums.TransitionType.PushRight,
            Network.Grpc.TransitionType.TransitionPushUp => Core.Enums.TransitionType.PushUp,
            Network.Grpc.TransitionType.TransitionPushDown => Core.Enums.TransitionType.PushDown,
            Network.Grpc.TransitionType.TransitionZoomIn => Core.Enums.TransitionType.ZoomIn,
            Network.Grpc.TransitionType.TransitionZoomOut => Core.Enums.TransitionType.ZoomOut,
            Network.Grpc.TransitionType.TransitionDissolve => Core.Enums.TransitionType.Dissolve,
            Network.Grpc.TransitionType.TransitionBlur => Core.Enums.TransitionType.Blur,
            Network.Grpc.TransitionType.TransitionRandom => Core.Enums.TransitionType.Random,
            _ => Core.Enums.TransitionType.Fade
        };
    }

    private static Core.Models.OverlayConfig ConvertOverlayRequest(SetOverlayRequest request)
    {
        var config = new Core.Models.OverlayConfig
        {
            OverlayId = request.OverlayId,
            Type = ConvertOverlayType(request.Type),
            Position = ConvertOverlayPosition(request.Position),
            FontFamily = string.IsNullOrEmpty(request.FontFamily) ? "Segoe UI" : request.FontFamily,
            FontSize = request.FontSize > 0 ? request.FontSize : 24,
            ForegroundColor = string.IsNullOrEmpty(request.ForegroundColor) ? "#FFFFFFFF" : request.ForegroundColor,
            BackgroundColor = string.IsNullOrEmpty(request.BackgroundColor) ? "#80000000" : request.BackgroundColor,
            Margin = request.Margin > 0 ? request.Margin : 20,
            Opacity = request.Opacity > 0 ? request.Opacity : 1.0,
            IsEnabled = true
        };

        switch (config.Type)
        {
            case Core.Models.OverlayType.Clock:
                config.Clock = new Core.Models.ClockConfig
                {
                    Format = string.IsNullOrEmpty(request.Format) ? null : request.Format,
                    Use24Hour = request.Use24Hour,
                    ShowSeconds = request.ShowSeconds,
                    TimeZoneId = string.IsNullOrEmpty(request.TimezoneId) ? null : request.TimezoneId
                };
                break;

            case Core.Models.OverlayType.DateTime:
                config.DateTime = new Core.Models.DateTimeConfig
                {
                    Format = string.IsNullOrEmpty(request.Format) ? "dddd, MMMM d, yyyy h:mm tt" : request.Format,
                    TimeZoneId = string.IsNullOrEmpty(request.TimezoneId) ? null : request.TimezoneId
                };
                break;

            case Core.Models.OverlayType.StaticText:
                config.StaticText = new Core.Models.StaticTextConfig
                {
                    Text = request.Text ?? "",
                    WordWrap = true
                };
                break;

            case Core.Models.OverlayType.NewsTicker:
                config.NewsTicker = new Core.Models.NewsTickerConfig
                {
                    FeedUrls = request.FeedUrls.ToList(),
                    ScrollSpeedPx = request.ScrollSpeed > 0 ? request.ScrollSpeed : 100,
                    RefreshIntervalMinutes = request.RefreshIntervalMin > 0 ? request.RefreshIntervalMin : 15
                };
                break;
        }

        return config;
    }

    private static Core.Models.OverlayType ConvertOverlayType(Network.Grpc.OverlayType type)
    {
        return type switch
        {
            Network.Grpc.OverlayType.OverlayClock => Core.Models.OverlayType.Clock,
            Network.Grpc.OverlayType.OverlayNewsTicker => Core.Models.OverlayType.NewsTicker,
            Network.Grpc.OverlayType.OverlayStaticText => Core.Models.OverlayType.StaticText,
            Network.Grpc.OverlayType.OverlayDatetime => Core.Models.OverlayType.DateTime,
            _ => Core.Models.OverlayType.StaticText
        };
    }

    private static Core.Models.OverlayPosition ConvertOverlayPosition(Network.Grpc.OverlayPosition position)
    {
        return position switch
        {
            Network.Grpc.OverlayPosition.OverlayTopLeft => Core.Models.OverlayPosition.TopLeft,
            Network.Grpc.OverlayPosition.OverlayTopCenter => Core.Models.OverlayPosition.TopCenter,
            Network.Grpc.OverlayPosition.OverlayTopRight => Core.Models.OverlayPosition.TopRight,
            Network.Grpc.OverlayPosition.OverlayBottomLeft => Core.Models.OverlayPosition.BottomLeft,
            Network.Grpc.OverlayPosition.OverlayBottomCenter => Core.Models.OverlayPosition.BottomCenter,
            Network.Grpc.OverlayPosition.OverlayBottomRight => Core.Models.OverlayPosition.BottomRight,
            _ => Core.Models.OverlayPosition.BottomRight
        };
    }

    #endregion
}
