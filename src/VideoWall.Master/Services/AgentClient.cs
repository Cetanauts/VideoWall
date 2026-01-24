using System.Net.Http;
using Grpc.Net.Client;
using VideoWall.Network.Grpc;
using Google.Protobuf.WellKnownTypes;

namespace VideoWall.Master.Services;

/// <summary>
/// Client for communicating with VideoWall agents
/// </summary>
public class AgentClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly AgentService.AgentServiceClient _client;
    private bool _disposed;

    public string MachineId { get; private set; } = string.Empty;
    public string IpAddress { get; }
    public int Port { get; }
    public bool IsConnected { get; private set; }
    public DateTime LastContact { get; private set; }
    public string? LastError { get; private set; }

    public AgentClient(string ipAddress, int port)
    {
        IpAddress = ipAddress;
        Port = port;

        var address = $"http://{ipAddress}:{port}";
        _channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                EnableMultipleHttp2Connections = true
            }
        });

        _client = new AgentService.AgentServiceClient(_channel);
    }

    /// <summary>
    /// Test connection to the agent
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.PingAsync(new Empty(), cancellationToken: cancellationToken);
            IsConnected = response.Success;
            LastContact = DateTime.UtcNow;
            LastError = null;
            return IsConnected;
        }
        catch (Exception ex)
        {
            IsConnected = false;
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Get agent status
    /// </summary>
    public async Task<AgentStatusResponse?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetStatusAsync(new Empty(), cancellationToken: cancellationToken);
            MachineId = response.MachineId;
            LastContact = DateTime.UtcNow;
            return response;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Get hardware information
    /// </summary>
    public async Task<HardwareInfoResponse?> GetHardwareInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetHardwareInfoAsync(new Empty(), cancellationToken: cancellationToken);
            MachineId = response.MachineId;
            LastContact = DateTime.UtcNow;
            return response;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Play media on an output
    /// </summary>
    public async Task<PlayResponse?> PlayAsync(string outputId, string mediaPath, bool autoPlay = true,
        long startPositionMs = 0, int volume = 100, bool loop = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PlayRequest
            {
                OutputId = outputId,
                MediaPath = mediaPath,
                AutoPlay = autoPlay,
                StartPositionMs = startPositionMs,
                Volume = volume,
                Loop = loop
            };

            var response = await _client.PlayAsync(request, cancellationToken: cancellationToken);
            LastContact = DateTime.UtcNow;
            return response;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Pause playback on an output
    /// </summary>
    public async Task<bool> PauseAsync(string outputId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.PauseAsync(new OutputRequest { OutputId = outputId }, cancellationToken: cancellationToken);
            LastContact = DateTime.UtcNow;
            return response.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Stop playback on an output
    /// </summary>
    public async Task<bool> StopAsync(string outputId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.StopAsync(new OutputRequest { OutputId = outputId }, cancellationToken: cancellationToken);
            LastContact = DateTime.UtcNow;
            return response.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Resume playback on an output
    /// </summary>
    public async Task<bool> ResumeAsync(string outputId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.ResumeAsync(new OutputRequest { OutputId = outputId }, cancellationToken: cancellationToken);
            LastContact = DateTime.UtcNow;
            return response.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Seek to position
    /// </summary>
    public async Task<bool> SeekAsync(string outputId, long positionMs, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.SeekAsync(new SeekRequest
            {
                OutputId = outputId,
                PositionMs = positionMs
            }, cancellationToken: cancellationToken);
            LastContact = DateTime.UtcNow;
            return response.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Set volume
    /// </summary>
    public async Task<bool> SetVolumeAsync(string outputId, int volume, bool mute = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.SetVolumeAsync(new VolumeRequest
            {
                OutputId = outputId,
                Volume = volume,
                Mute = mute
            }, cancellationToken: cancellationToken);
            LastContact = DateTime.UtcNow;
            return response.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Start a slideshow
    /// </summary>
    public async Task<bool> StartSlideshowAsync(string outputId, IEnumerable<(string path, int durationMs)> slides,
        int defaultDurationMs = 5000, TransitionType transition = TransitionType.TransitionFade,
        int transitionDurationMs = 1000, bool loop = true, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new SlideshowRequest
            {
                OutputId = outputId,
                DefaultDurationMs = defaultDurationMs,
                DefaultTransition = transition,
                DefaultTransitionDurationMs = transitionDurationMs,
                Loop = loop,
                AutoAdvance = true
            };

            foreach (var (path, duration) in slides)
            {
                request.Slides.Add(new SlideInfo
                {
                    MediaPath = path,
                    DurationMs = duration,
                    Transition = transition,
                    TransitionDurationMs = transitionDurationMs
                });
            }

            var response = await _client.StartSlideshowAsync(request, cancellationToken: cancellationToken);
            LastContact = DateTime.UtcNow;
            return response.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Show an image with optional transition
    /// </summary>
    public async Task<bool> ShowImageAsync(string outputId, string imagePath, TransitionType transition = TransitionType.TransitionFade,
        int transitionDurationMs = 1000, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use slideshow with a single image that doesn't auto-advance
            var request = new SlideshowRequest
            {
                OutputId = outputId,
                DefaultDurationMs = int.MaxValue, // Very long duration
                DefaultTransition = transition,
                DefaultTransitionDurationMs = transitionDurationMs,
                Loop = false,
                AutoAdvance = false
            };

            request.Slides.Add(new SlideInfo
            {
                MediaPath = imagePath,
                DurationMs = int.MaxValue,
                Transition = transition,
                TransitionDurationMs = transitionDurationMs
            });

            var response = await _client.StartSlideshowAsync(request, cancellationToken: cancellationToken);
            LastContact = DateTime.UtcNow;
            return response.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Run a test pattern
    /// </summary>
    public async Task<bool> RunTestPatternAsync(string outputId, TestPatternType pattern, int durationSeconds = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.RunTestPatternAsync(new TestPatternRequest
            {
                OutputId = outputId,
                Pattern = pattern,
                DurationSeconds = durationSeconds
            }, cancellationToken: cancellationToken);
            LastContact = DateTime.UtcNow;
            return response.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Get a thumbnail from an output
    /// </summary>
    public async Task<byte[]?> GetThumbnailAsync(string outputId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetThumbnailAsync(new OutputRequest { OutputId = outputId },
                cancellationToken: cancellationToken);

            if (response.Success)
            {
                LastContact = DateTime.UtcNow;
                return response.ImageData.ToByteArray();
            }
            return null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// Prepare synchronized playback
    /// </summary>
    public async Task<bool> PrepareSyncAsync(string syncGroupId, Dictionary<string, string> outputMedia,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new PrepareSyncRequest
            {
                SyncGroupId = syncGroupId
            };

            foreach (var (outputId, mediaPath) in outputMedia)
            {
                request.Outputs.Add(new SyncOutputConfig
                {
                    OutputId = outputId,
                    MediaPath = mediaPath
                });
            }

            var response = await _client.PrepareSyncAsync(request, cancellationToken: cancellationToken);
            LastContact = DateTime.UtcNow;
            return response.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Execute synchronized playback at specified time
    /// </summary>
    public async Task<bool> ExecuteSyncAsync(string syncGroupId, DateTime executeAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.ExecuteSyncAsync(new ExecuteSyncRequest
            {
                SyncGroupId = syncGroupId,
                ExecuteAt = Timestamp.FromDateTime(executeAt.ToUniversalTime())
            }, cancellationToken: cancellationToken);
            LastContact = DateTime.UtcNow;
            return response.Success;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _channel.Dispose();
    }
}
