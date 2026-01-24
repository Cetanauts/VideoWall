using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using VideoWall.Core.Enums;
using VideoWall.Core.Models;
using VideoWall.Playback;
using VideoWall.Transitions;

namespace VideoWall.Agent;

/// <summary>
/// Window for displaying content on a specific monitor output
/// </summary>
public partial class DisplayWindow : Window
{
    private readonly string _outputId;
    private readonly LibVLC _libVLC;
    private readonly LibVLCSharp.Shared.MediaPlayer _mediaPlayer;
    private readonly TransitionRenderer _transitionRenderer;
    private readonly DispatcherTimer _updateTimer;
    private readonly TaskCompletionSource<bool> _loadedTcs = new();
    private Media? _currentMedia;
    private ContentType _currentContentType = ContentType.None;
    private bool _showDebug;
    private bool _disposed;
    private bool _isLoaded;

    public string OutputId => _outputId;

    /// <summary>
    /// Event fired when playback state changes
    /// </summary>
    public event EventHandler<PlaybackState>? PlaybackStateChanged;

    /// <summary>
    /// Event fired when media ends
    /// </summary>
    public event EventHandler? MediaEnded;

    public DisplayWindow(string outputId, LibVLC libVLC, ScreenBounds bounds)
    {
        InitializeComponent();

        _outputId = outputId;
        _libVLC = libVLC;
        _transitionRenderer = new TransitionRenderer();

        // Create media player
        _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);

        // Wire up events
        _mediaPlayer.Playing += (s, e) => Dispatcher.Invoke(() => OnPlaybackStateChanged(PlaybackState.Playing));
        _mediaPlayer.Paused += (s, e) => Dispatcher.Invoke(() => OnPlaybackStateChanged(PlaybackState.Paused));
        _mediaPlayer.Stopped += (s, e) => Dispatcher.Invoke(() => OnPlaybackStateChanged(PlaybackState.Stopped));
        _mediaPlayer.EndReached += (s, e) => Dispatcher.Invoke(OnMediaEnded);
        _mediaPlayer.EncounteredError += (s, e) => Dispatcher.Invoke(() => OnPlaybackStateChanged(PlaybackState.Error));

        // Position window on the correct monitor
        Left = bounds.X;
        Top = bounds.Y;
        Width = bounds.Width;
        Height = bounds.Height;

        // Set output ID display
        txtOutputId.Text = outputId;
        txtResolution.Text = $"{bounds.Width}x{bounds.Height}";

        // Update timer for debug info
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        // Key handlers
        KeyDown += DisplayWindow_KeyDown;

        Closed += DisplayWindow_Closed;

        // Set MediaPlayer after window is loaded (required for LibVLCSharp.WPF)
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[DisplayWindow] Window Loaded - OutputId: {_outputId}, Size: {ActualWidth}x{ActualHeight}");

        // Assign MediaPlayer to VideoView - this MUST happen after the window is loaded
        videoView.MediaPlayer = _mediaPlayer;
        _isLoaded = true;
        _loadedTcs.TrySetResult(true);

        // Ensure keyboard focus for Escape key to work
        Focus();
        Activate();

        // Show output ID briefly to confirm window is working
        outputIdOverlay.Visibility = Visibility.Visible;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (s, args) =>
        {
            timer.Stop();
            outputIdOverlay.Visibility = Visibility.Collapsed;
        };
        timer.Start();

        System.Diagnostics.Debug.WriteLine($"[DisplayWindow] VideoView size: {videoView.ActualWidth}x{videoView.ActualHeight}");
    }

    /// <summary>
    /// Wait for the window to be fully loaded before performing operations
    /// </summary>
    private async Task EnsureLoadedAsync()
    {
        if (_isLoaded) return;
        await _loadedTcs.Task;
    }

    #region Video Playback

    /// <summary>
    /// Play a video file
    /// </summary>
    public async Task PlayVideoAsync(string mediaPath, bool autoPlay = true, long startPositionMs = 0)
    {
        System.Diagnostics.Debug.WriteLine($"[DisplayWindow] PlayVideoAsync called: {mediaPath}");

        // Ensure window is loaded and VideoView.MediaPlayer is set
        await EnsureLoadedAsync();
        System.Diagnostics.Debug.WriteLine($"[DisplayWindow] Window loaded, IsVisible={IsVisible}");

        // Make sure window is visible and active
        await Dispatcher.InvokeAsync(() =>
        {
            if (!IsVisible)
            {
                Show();
            }
            Activate();
            Focus();

            // Show debug info temporarily
            ShowDebugOverlay($"Loading: {System.IO.Path.GetFileName(mediaPath)}");
        });

        try
        {
            // Verify file exists for local paths
            string resolvedPath = mediaPath;
            if (!mediaPath.StartsWith("\\\\") && !mediaPath.Contains("://"))
            {
                resolvedPath = System.IO.Path.GetFullPath(mediaPath);
                if (!System.IO.File.Exists(resolvedPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[DisplayWindow] ERROR: File not found: {resolvedPath}");
                    await Dispatcher.InvokeAsync(() => ShowDebugOverlay($"ERROR: File not found\n{resolvedPath}"));
                    return;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[DisplayWindow] Resolved path: {resolvedPath}");

            await Task.Run(async () =>
            {
                _currentMedia?.Dispose();

                Uri mediaUri;
                if (mediaPath.StartsWith("\\\\") || mediaPath.Contains("://"))
                {
                    mediaUri = new Uri(mediaPath);
                }
                else
                {
                    mediaUri = new Uri(resolvedPath);
                }

                System.Diagnostics.Debug.WriteLine($"[DisplayWindow] Creating media from URI: {mediaUri}");
                _currentMedia = new Media(_libVLC, mediaUri);

                // Parse media to verify it can be loaded
                var parseResult = await _currentMedia.Parse(MediaParseOptions.ParseLocal | MediaParseOptions.ParseNetwork);
                System.Diagnostics.Debug.WriteLine($"[DisplayWindow] Media parse result: {parseResult}, Duration: {_currentMedia.Duration}ms");

                if (parseResult != MediaParsedStatus.Done)
                {
                    System.Diagnostics.Debug.WriteLine($"[DisplayWindow] WARNING: Media parse status is {parseResult}");
                }

                _mediaPlayer.Media = _currentMedia;
            });

            await Dispatcher.InvokeAsync(() =>
            {
                System.Diagnostics.Debug.WriteLine($"[DisplayWindow] Setting up VideoView, MediaPlayer assigned: {videoView.MediaPlayer != null}");

                // Ensure VideoView has MediaPlayer assigned
                if (videoView.MediaPlayer == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[DisplayWindow] Assigning MediaPlayer to VideoView");
                    videoView.MediaPlayer = _mediaPlayer;
                }

                ShowContent(ContentType.Video);
                _currentContentType = ContentType.Video;

                System.Diagnostics.Debug.WriteLine($"[DisplayWindow] VideoView Visibility: {videoView.Visibility}, ActualWidth: {videoView.ActualWidth}, ActualHeight: {videoView.ActualHeight}");

                if (autoPlay)
                {
                    System.Diagnostics.Debug.WriteLine($"[DisplayWindow] Starting playback");
                    _mediaPlayer.Play();
                }

                if (startPositionMs > 0)
                {
                    _mediaPlayer.Time = startPositionMs;
                }

                // Hide debug after a delay
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (s, e) => { timer.Stop(); HideDebugOverlay(); };
                timer.Start();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DisplayWindow] ERROR in PlayVideoAsync: {ex.Message}");
            await Dispatcher.InvokeAsync(() => ShowDebugOverlay($"ERROR: {ex.Message}"));
        }
    }

    private void ShowDebugOverlay(string message)
    {
        txtDebugStatus.Text = message;
        debugOverlay.Visibility = Visibility.Visible;
    }

    private void HideDebugOverlay()
    {
        debugOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Pause video
    /// </summary>
    public void Pause()
    {
        _mediaPlayer.Pause();
    }

    /// <summary>
    /// Stop video
    /// </summary>
    public void Stop()
    {
        _mediaPlayer.Stop();
    }

    /// <summary>
    /// Resume video
    /// </summary>
    public void Resume()
    {
        _mediaPlayer.Play();
    }

    /// <summary>
    /// Seek to position
    /// </summary>
    public void SeekTo(long positionMs)
    {
        if (_mediaPlayer.IsSeekable)
        {
            _mediaPlayer.Time = positionMs;
        }
    }

    /// <summary>
    /// Set volume (0-100)
    /// </summary>
    public void SetVolume(int volume)
    {
        _mediaPlayer.Volume = Math.Clamp(volume, 0, 100);
    }

    /// <summary>
    /// Set mute
    /// </summary>
    public void SetMute(bool mute)
    {
        _mediaPlayer.Mute = mute;
    }

    /// <summary>
    /// Get current position in milliseconds
    /// </summary>
    public long GetPosition() => _mediaPlayer.Time;

    /// <summary>
    /// Get duration in milliseconds
    /// </summary>
    public long GetDuration() => _mediaPlayer.Length;

    #endregion

    #region Image Display

    /// <summary>
    /// Display an image
    /// </summary>
    public async Task ShowImageAsync(string imagePath)
    {
        System.Diagnostics.Debug.WriteLine($"[DisplayWindow] ShowImageAsync called: {imagePath}");

        await EnsureLoadedAsync();

        // Make sure window is visible and active
        await Dispatcher.InvokeAsync(() =>
        {
            if (!IsVisible)
            {
                Show();
            }
            Activate();
            Focus();
            ShowDebugOverlay($"Loading: {System.IO.Path.GetFileName(imagePath)}");
        });

        try
        {
            // Ensure we have an absolute path
            string absolutePath = imagePath;
            if (!imagePath.StartsWith("\\\\") && !imagePath.Contains("://"))
            {
                absolutePath = System.IO.Path.GetFullPath(imagePath);
            }

            // Check if file exists
            if (!imagePath.StartsWith("\\\\") && !imagePath.Contains("://"))
            {
                if (!System.IO.File.Exists(absolutePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[DisplayWindow] ERROR: Image file not found: {absolutePath}");
                    await Dispatcher.InvokeAsync(() => ShowDebugOverlay($"ERROR: Image not found\n{absolutePath}"));
                    return;
                }
            }

            System.Diagnostics.Debug.WriteLine($"[DisplayWindow] Loading image: {absolutePath}");

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(absolutePath);
            bitmap.EndInit();
            bitmap.Freeze();

            System.Diagnostics.Debug.WriteLine($"[DisplayWindow] Image loaded: {bitmap.PixelWidth}x{bitmap.PixelHeight}");

            await Dispatcher.InvokeAsync(() =>
            {
                imageDisplay.Source = bitmap;
                imageDisplay.Visibility = Visibility.Visible;
                ShowContent(ContentType.Image);
                _currentContentType = ContentType.Image;
                System.Diagnostics.Debug.WriteLine($"[DisplayWindow] Image displayed, imageDisplay.ActualWidth={imageDisplay.ActualWidth}, ActualHeight={imageDisplay.ActualHeight}");

                // Hide debug after a delay
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (s, e) => { timer.Stop(); HideDebugOverlay(); };
                timer.Start();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DisplayWindow] ERROR loading image '{imagePath}': {ex.Message}");
            await Dispatcher.InvokeAsync(() => ShowDebugOverlay($"ERROR: {ex.Message}"));
        }
    }

    /// <summary>
    /// Display an image (synchronous wrapper for backwards compatibility)
    /// </summary>
    public void ShowImage(string imagePath)
    {
        _ = ShowImageAsync(imagePath);
    }

    /// <summary>
    /// Display an image with transition from previous content
    /// </summary>
    public async Task ShowImageWithTransitionAsync(string imagePath, TransitionType transition, int durationMs)
    {
        await EnsureLoadedAsync();

        // Make sure window is visible and active
        await Dispatcher.InvokeAsync(() =>
        {
            if (!IsVisible)
            {
                Show();
            }
            Activate();
            Focus();
        });

        try
        {
            // Ensure we have an absolute path
            string absolutePath = imagePath;
            if (!imagePath.StartsWith("\\\\") && !imagePath.Contains("://"))
            {
                absolutePath = System.IO.Path.GetFullPath(imagePath);
            }

            System.Diagnostics.Debug.WriteLine($"Loading image with transition: {absolutePath}");

            // Load new image
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(absolutePath);
            bitmap.EndInit();
            bitmap.Freeze();

            await Dispatcher.InvokeAsync(() =>
            {
                // Capture current content to previous layer
                if (_currentContentType == ContentType.Image && imageDisplay.Source != null)
                {
                    previousImage.Source = imageDisplay.Source;
                    previousContentLayer.Visibility = Visibility.Visible;
                }

                imageDisplay.Source = bitmap;
                imageDisplay.Visibility = Visibility.Visible;
                ShowContent(ContentType.Image);

                // Create and run transition
                var storyboard = _transitionRenderer.CreateTransition(
                    transition,
                    durationMs,
                    previousContentLayer,
                    imageDisplay
                );

                storyboard.Completed += (s, e) =>
                {
                    previousContentLayer.Visibility = Visibility.Collapsed;
                };

                storyboard.Begin(this);
                _currentContentType = ContentType.Image;
                System.Diagnostics.Debug.WriteLine($"Image with transition displayed successfully: {imagePath}");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to show image with transition '{imagePath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Display an image with transition (synchronous wrapper for backwards compatibility)
    /// </summary>
    public void ShowImageWithTransition(string imagePath, TransitionType transition, int durationMs)
    {
        _ = ShowImageWithTransitionAsync(imagePath, transition, durationMs);
    }

    #endregion

    #region Window Control

    /// <summary>
    /// Hide the display window and return to desktop
    /// </summary>
    public void HideWindow()
    {
        Dispatcher.Invoke(() =>
        {
            _mediaPlayer.Stop();
            Hide();
        });
    }

    /// <summary>
    /// Close the display window permanently
    /// </summary>
    public void CloseWindow()
    {
        Dispatcher.Invoke(() =>
        {
            _mediaPlayer.Stop();
            Close();
        });
    }

    #endregion

    #region Test Patterns

    /// <summary>
    /// Show a test pattern
    /// </summary>
    public void ShowTestPattern(TestPatternGenerator.PatternType patternType, int? durationSeconds = null)
    {
        Dispatcher.Invoke(() =>
        {
            var pattern = TestPatternGenerator.CreatePattern(
                patternType,
                (int)Width,
                (int)Height,
                _outputId
            );

            testPatternContainer.Content = pattern;
            ShowContent(ContentType.TestPattern);
            _currentContentType = ContentType.TestPattern;

            if (durationSeconds.HasValue)
            {
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(durationSeconds.Value)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    HideTestPattern();
                };
                timer.Start();
            }
        });
    }

    /// <summary>
    /// Hide test pattern
    /// </summary>
    public void HideTestPattern()
    {
        Dispatcher.Invoke(() =>
        {
            testPatternContainer.Content = null;
            testPatternContainer.Visibility = Visibility.Collapsed;
            _currentContentType = ContentType.None;
        });
    }

    /// <summary>
    /// Run sync flash test
    /// </summary>
    public void StartSyncFlash(int intervalMs = 500)
    {
        Dispatcher.Invoke(() =>
        {
            var flashElement = TestPatternGenerator.CreatePattern(
                TestPatternGenerator.PatternType.SyncFlash,
                (int)Width,
                (int)Height
            );

            testPatternContainer.Content = flashElement;
            ShowContent(ContentType.TestPattern);
            _currentContentType = ContentType.TestPattern;

            var controller = TestPatternGenerator.CreateSyncFlashController(flashElement as FrameworkElement ?? new Border(), intervalMs);
            controller.Start();

            // Store controller for later stop
            testPatternContainer.Tag = controller;
        });
    }

    /// <summary>
    /// Stop sync flash test
    /// </summary>
    public void StopSyncFlash()
    {
        Dispatcher.Invoke(() =>
        {
            if (testPatternContainer.Tag is SyncFlashController controller)
            {
                controller.Stop();
                controller.Dispose();
            }
            HideTestPattern();
        });
    }

    #endregion

    #region Content Management

    private void ShowContent(ContentType type)
    {
        System.Diagnostics.Debug.WriteLine($"[DisplayWindow] ShowContent called with type: {type}");

        // Set visibility for all content types
        videoView.Visibility = type == ContentType.Video ? Visibility.Visible : Visibility.Collapsed;
        imageDisplay.Visibility = type == ContentType.Image ? Visibility.Visible : Visibility.Collapsed;
        slideshowContainer.Visibility = type == ContentType.Slideshow ? Visibility.Visible : Visibility.Collapsed;
        testPatternContainer.Visibility = type == ContentType.TestPattern ? Visibility.Visible : Visibility.Collapsed;

        System.Diagnostics.Debug.WriteLine($"[DisplayWindow] videoView.Visibility={videoView.Visibility}, imageDisplay.Visibility={imageDisplay.Visibility}");

        // Reset opacity for elements that might have been animated
        if (type == ContentType.Image)
        {
            imageDisplay.Opacity = 1.0;
        }
        else if (type == ContentType.Video)
        {
            videoView.Opacity = 1.0;
        }

        // Stop video if switching away
        if (type != ContentType.Video && _mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Stop();
        }

        // Force layout update
        UpdateLayout();
    }

    /// <summary>
    /// Clear all content
    /// </summary>
    public void ClearContent()
    {
        Dispatcher.Invoke(() =>
        {
            _mediaPlayer.Stop();
            imageDisplay.Source = null;
            testPatternContainer.Content = null;
            ShowContent(ContentType.None);
            _currentContentType = ContentType.None;
        });
    }

    /// <summary>
    /// Take a snapshot of current content
    /// </summary>
    public async Task<byte[]?> TakeSnapshotAsync(int width = 320, int height = 180)
    {
        if (_currentContentType == ContentType.Video)
        {
            try
            {
                var tempPath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"snapshot_{_outputId}_{Guid.NewGuid()}.png"
                );

                bool result = _mediaPlayer.TakeSnapshot(0, tempPath, (uint)width, (uint)height);

                if (result)
                {
                    await Task.Delay(100);
                    if (System.IO.File.Exists(tempPath))
                    {
                        var data = await System.IO.File.ReadAllBytesAsync(tempPath);
                        System.IO.File.Delete(tempPath);
                        return data;
                    }
                }
            }
            catch { }
        }
        return null;
    }

    #endregion

    #region Event Handlers

    private void OnPlaybackStateChanged(PlaybackState state)
    {
        if (_showDebug)
        {
            txtDebugStatus.Text = $"Status: {state}";
        }
        PlaybackStateChanged?.Invoke(this, state);
    }

    private void OnMediaEnded()
    {
        MediaEnded?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_showDebug && _currentContentType == ContentType.Video)
        {
            var position = TimeSpan.FromMilliseconds(_mediaPlayer.Time);
            var duration = TimeSpan.FromMilliseconds(_mediaPlayer.Length);
            txtDebugPosition.Text = $"Position: {position:mm\\:ss} / {duration:mm\\:ss}";
        }
    }

    private void DisplayWindow_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                // Return to main window (don't close)
                Hide();
                break;

            case Key.D:
                // Toggle debug overlay
                _showDebug = !_showDebug;
                debugOverlay.Visibility = _showDebug ? Visibility.Visible : Visibility.Collapsed;
                if (_showDebug) _updateTimer.Start();
                else _updateTimer.Stop();
                break;

            case Key.I:
                // Toggle output ID overlay
                outputIdOverlay.Visibility = outputIdOverlay.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                break;

            case Key.Space:
                // Toggle play/pause
                if (_mediaPlayer.IsPlaying)
                    _mediaPlayer.Pause();
                else
                    _mediaPlayer.Play();
                break;
        }
    }

    private void DisplayWindow_Closed(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            _disposed = true;
            _updateTimer.Stop();
            _mediaPlayer.Stop();
            _currentMedia?.Dispose();
            _mediaPlayer.Dispose();
        }
    }

    #endregion

    private enum ContentType
    {
        None,
        Video,
        Image,
        Slideshow,
        TestPattern
    }
}
