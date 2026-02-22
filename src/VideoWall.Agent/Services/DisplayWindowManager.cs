using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using VideoWall.Core.Enums;
using VideoWall.Core.Models;
using VideoWall.Hardware;
using VideoWall.Playback;

namespace VideoWall.Agent.Services;

/// <summary>
/// Manages DisplayWindow instances for video playback on physical monitors
/// </summary>
public class DisplayWindowManager : IDisposable
{
    private readonly LibVLC _libVLC;
    private readonly DisplayDetector _displayDetector;
    private readonly Dictionary<string, DisplayWindow> _windows = new();
    private readonly Dictionary<string, DisplayOutput> _outputCache = new();
    private readonly Dictionary<string, SlideshowState> _slideshows = new();
    private readonly Dispatcher _dispatcher;
    private readonly object _lock = new();
    private readonly string _logFile;
    private bool _disposed;
    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;
    private bool _dpiDetected;

    public string VlcVersion { get; }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_SHOWWINDOW = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    /// <summary>
    /// Log to both console and file for diagnostics
    /// </summary>
    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [DisplayWindowManager] {message}";
        Console.WriteLine(line);
        try { File.AppendAllText(_logFile, line + Environment.NewLine); } catch { }
    }

    public DisplayWindowManager(DisplayDetector displayDetector, Dispatcher dispatcher)
    {
        _displayDetector = displayDetector;
        _dispatcher = dispatcher;

        // Set up log file next to the exe
        _logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "videowall-agent.log");
        try { File.WriteAllText(_logFile, $"=== VideoWall Agent Log Started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}"); } catch { }

        // Initialize LibVLC
        LibVLCSharp.Shared.Core.Initialize();
        _libVLC = new LibVLC(
            "--no-xlib",
            "--quiet",
            "--no-video-title-show",
            "--network-caching=1000"
        );
        VlcVersion = _libVLC.Version;

        // Cache output information
        RefreshOutputCache();
    }

    /// <summary>
    /// Detect the system DPI scale factor to convert physical pixels to WPF DIPs.
    /// EnumDisplaySettings returns physical pixel coordinates, but WPF Window.Left/Top/Width/Height
    /// use Device-Independent Pixels (DIPs). Without this conversion, windows are positioned
    /// incorrectly when display scaling is above 100%.
    /// </summary>
    private void DetectDpiScale()
    {
        if (_dpiDetected) return;

        try
        {
            // Try to get DPI from WPF's PresentationSource (most accurate)
            _dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    var source = PresentationSource.FromVisual(mainWindow);
                    if (source?.CompositionTarget != null)
                    {
                        var transform = source.CompositionTarget.TransformFromDevice;
                        _dpiScaleX = transform.M11; // e.g., 0.667 for 150% scaling
                        _dpiScaleY = transform.M22;
                        _dpiDetected = true;
                        Log($"DPI scale detected from PresentationSource: scaleX={_dpiScaleX:F4}, scaleY={_dpiScaleY:F4} ({1.0 / _dpiScaleX * 100:F0}% scaling)");
                        return;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Log($"PresentationSource DPI detection failed: {ex.Message}");
        }

        if (!_dpiDetected)
        {
            // Fallback: use Win32 GetDpiForSystem
            try
            {
                uint systemDpi = GetDpiForSystem();
                _dpiScaleX = 96.0 / systemDpi;
                _dpiScaleY = 96.0 / systemDpi;
                _dpiDetected = true;
                Log($"DPI scale detected from GetDpiForSystem: dpi={systemDpi}, scale={_dpiScaleX:F4} ({systemDpi / 96.0 * 100:F0}% scaling)");
            }
            catch (Exception ex)
            {
                Log($"GetDpiForSystem failed: {ex.Message}, using 1.0 (100% scaling)");
                _dpiScaleX = 1.0;
                _dpiScaleY = 1.0;
                _dpiDetected = true;
            }
        }
    }

    /// <summary>
    /// Refresh the cached output information
    /// </summary>
    public void RefreshOutputCache()
    {
        var machineInfo = _displayDetector.GetMachineInfo();
        lock (_lock)
        {
            _outputCache.Clear();
            Log("=== Output Detection ===");
            Log($"Machine: {machineInfo.MachineId}, GPUs: {machineInfo.Gpus.Count}");
            int outputIndex = 0;
            foreach (var output in machineInfo.GetAllOutputs())
            {
                _outputCache[output.OutputId] = output;
                Log($"Output[{outputIndex}]: id={output.OutputId}, device={output.DeviceName}, " +
                    $"bounds=({output.Bounds.X},{output.Bounds.Y},{output.Bounds.Width}x{output.Bounds.Height}), " +
                    $"primary={output.IsPrimary}, type={output.OutputType}");
                outputIndex++;
            }
            Log($"Total outputs: {_outputCache.Count}");
            Log("========================");
        }
    }

    /// <summary>
    /// Get or create a display window for an output
    /// </summary>
    public DisplayWindow GetOrCreateWindow(string outputId)
    {
        // Detect DPI for diagnostic logging only (positioning uses Win32 SetWindowPos)
        DetectDpiScale();

        lock (_lock)
        {
            if (_windows.TryGetValue(outputId, out var existing))
            {
                // Check if the existing window is still usable
                bool needsRecreation = false;
                _dispatcher.Invoke(() =>
                {
                    if (!existing.IsVisible || !existing.IsLoaded)
                    {
                        Log($"Existing window for {outputId} is hidden/unloaded (IsVisible={existing.IsVisible}, IsLoaded={existing.IsLoaded}). Recreating.");
                        needsRecreation = true;
                    }
                });

                if (!needsRecreation)
                {
                    Log($"Reusing existing visible window for {outputId}");
                    return existing;
                }

                // Close the hidden/broken window and recreate
                _dispatcher.Invoke(() =>
                {
                    try { existing.Close(); } catch { }
                });
                _windows.Remove(outputId);
            }

            // Get output info
            if (!_outputCache.TryGetValue(outputId, out var outputInfo))
            {
                RefreshOutputCache();
                if (!_outputCache.TryGetValue(outputId, out outputInfo))
                {
                    throw new ArgumentException($"Unknown output: {outputId}");
                }
            }

            // Convert physical pixel coordinates to WPF DIPs.
            // At 200% scaling, physical 3840x2160 → DIP 1920x1080.
            // WPF will then scale back to physical pixels when rendering.
            var physBounds = outputInfo.Bounds;
            var dipBounds = new ScreenBounds
            {
                X = (int)(physBounds.X * _dpiScaleX),
                Y = (int)(physBounds.Y * _dpiScaleY),
                Width = (int)(physBounds.Width * _dpiScaleX),
                Height = (int)(physBounds.Height * _dpiScaleY)
            };

            Log($"Creating window for {outputId}:");
            Log($"  Physical: ({physBounds.X},{physBounds.Y}) {physBounds.Width}x{physBounds.Height}");
            Log($"  DPI scale: {_dpiScaleX:F4} x {_dpiScaleY:F4} ({1.0 / _dpiScaleX * 100:F0}% scaling)");
            Log($"  DIP:      ({dipBounds.X},{dipBounds.Y}) {dipBounds.Width}x{dipBounds.Height}");

            // Create window on UI thread
            DisplayWindow? window = null;
            _dispatcher.Invoke(() =>
            {
                window = new DisplayWindow(outputId, _libVLC, dipBounds);
                window.Show();

                // Verify actual physical position via Win32
                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    GetWindowRect(hwnd, out var rect);
                    var physW = rect.Right - rect.Left;
                    var physH = rect.Bottom - rect.Top;
                    Log($"  Actual physical: ({rect.Left},{rect.Top}) {physW}x{physH}");

                    // Check if position matches intended physical position
                    if (Math.Abs(rect.Left - physBounds.X) > 2 || Math.Abs(rect.Top - physBounds.Y) > 2 ||
                        Math.Abs(physW - physBounds.Width) > 2 || Math.Abs(physH - physBounds.Height) > 2)
                    {
                        Log($"  *** MISMATCH *** Expected physical: ({physBounds.X},{physBounds.Y}) {physBounds.Width}x{physBounds.Height}");
                    }
                    else
                    {
                        Log($"  Position MATCH - window is on correct monitor");
                    }
                }
                Log($"  WPF: Left={window.Left}, Top={window.Top}, Size={window.ActualWidth}x{window.ActualHeight}");
                Log($"  IsVisible={window.IsVisible}, WindowState={window.WindowState}");
            });

            if (window == null)
            {
                throw new InvalidOperationException("Failed to create display window");
            }

            _windows[outputId] = window;
            return window;
        }
    }

    /// <summary>
    /// Play video on a specific output
    /// </summary>
    public async Task<bool> PlayVideoAsync(string outputId, string mediaPath, bool autoPlay = true,
        long startPositionMs = 0, int volume = 100, bool loop = false)
    {
        try
        {
            // Clear any previous content first to avoid stale state
            if (_windows.TryGetValue(outputId, out var existingWindow))
            {
                _dispatcher.Invoke(() => existingWindow.ClearContent());
            }

            var window = GetOrCreateWindow(outputId);

            // Set volume synchronously first
            _dispatcher.Invoke(() => window.SetVolume(volume));

            // Then play video - this is an async method, so we await it directly
            // Note: window.PlayVideoAsync handles the dispatcher internally
            await window.PlayVideoAsync(mediaPath, autoPlay, startPositionMs);

            return true;
        }
        catch (Exception ex)
        {
            Log($"PlayVideoAsync failed for {outputId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Show an image on a specific output
    /// </summary>
    public void ShowImage(string outputId, string imagePath)
    {
        var window = GetOrCreateWindow(outputId);
        _dispatcher.Invoke(() => window.ShowImage(imagePath));
    }

    /// <summary>
    /// Show an image with transition
    /// </summary>
    public void ShowImageWithTransition(string outputId, string imagePath, TransitionType transition, int durationMs)
    {
        var window = GetOrCreateWindow(outputId);
        _dispatcher.Invoke(() => window.ShowImageWithTransition(imagePath, transition, durationMs));
    }

    /// <summary>
    /// Start a slideshow on a specific output
    /// </summary>
    public void StartSlideshow(string outputId, List<SlideshowSlide> slides, int defaultDurationMs,
        TransitionType transition, int transitionDurationMs, bool loop)
    {
        Log($"StartSlideshow: outputId={outputId}, slides={slides.Count}, duration={defaultDurationMs}ms");

        // Stop any existing slideshow on this output
        StopSlideshow(outputId);

        if (slides.Count == 0)
            return;

        var window = GetOrCreateWindow(outputId);

        // Clear any previous content (especially important after a failed video load,
        // which leaves the native VLC window visible as a black surface)
        _dispatcher.Invoke(() => window.ClearContent());

        var state = new SlideshowState
        {
            OutputId = outputId,
            Slides = slides,
            DefaultDurationMs = defaultDurationMs,
            Transition = transition,
            TransitionDurationMs = transitionDurationMs,
            Loop = loop,
            CurrentIndex = 0,
            IsRunning = true
        };

        lock (_lock)
        {
            _slideshows[outputId] = state;
        }

        // Show first slide
        ShowSlide(state, 0);

        // Start timer for auto-advance
        state.Timer = new System.Timers.Timer();
        state.Timer.Elapsed += (s, e) => OnSlideshowTimerElapsed(state);
        state.Timer.Interval = slides[0].DurationMs > 0 ? slides[0].DurationMs : defaultDurationMs;
        state.Timer.AutoReset = false;
        state.Timer.Start();

        System.Diagnostics.Debug.WriteLine($"Slideshow started on {outputId} with {slides.Count} slides, interval {state.Timer.Interval}ms");
    }

    private void OnSlideshowTimerElapsed(SlideshowState state)
    {
        if (!state.IsRunning)
            return;

        // Move to next slide
        int nextIndex = state.CurrentIndex + 1;

        if (nextIndex >= state.Slides.Count)
        {
            if (state.Loop)
            {
                nextIndex = 0;
            }
            else
            {
                // Slideshow finished
                state.IsRunning = false;
                return;
            }
        }

        state.CurrentIndex = nextIndex;
        ShowSlide(state, nextIndex);

        // Set timer for next slide
        var slide = state.Slides[nextIndex];
        state.Timer!.Interval = slide.DurationMs > 0 ? slide.DurationMs : state.DefaultDurationMs;
        state.Timer.Start();

        System.Diagnostics.Debug.WriteLine($"Slideshow advanced to slide {nextIndex + 1}/{state.Slides.Count}");
    }

    private static readonly Random _random = new();
    private static readonly TransitionType[] _randomTransitionPool = new[]
    {
        TransitionType.Fade, TransitionType.WipeLeft, TransitionType.WipeRight,
        TransitionType.WipeUp, TransitionType.WipeDown,
        TransitionType.PushLeft, TransitionType.PushRight,
        TransitionType.PushUp, TransitionType.PushDown,
        TransitionType.ZoomIn, TransitionType.ZoomOut,
        TransitionType.Dissolve, TransitionType.Blur
    };

    private static TransitionType PickRandomTransition()
    {
        return _randomTransitionPool[_random.Next(_randomTransitionPool.Length)];
    }

    private void ShowSlide(SlideshowState state, int index)
    {
        if (index < 0 || index >= state.Slides.Count)
            return;

        var slide = state.Slides[index];
        var transition = state.Transition;

        if (transition == TransitionType.Random)
            transition = PickRandomTransition();

        _dispatcher.Invoke(() =>
        {
            if (_windows.TryGetValue(state.OutputId, out var window))
            {
                if (transition != TransitionType.Cut && state.TransitionDurationMs > 0)
                {
                    window.ShowImageWithTransition(slide.MediaPath, transition, state.TransitionDurationMs);
                }
                else
                {
                    window.ShowImage(slide.MediaPath);
                }
            }
        });
    }

    /// <summary>
    /// Stop slideshow on a specific output
    /// </summary>
    public void StopSlideshow(string outputId)
    {
        lock (_lock)
        {
            if (_slideshows.TryGetValue(outputId, out var state))
            {
                state.IsRunning = false;
                state.Timer?.Stop();
                state.Timer?.Dispose();
                _slideshows.Remove(outputId);
            }
        }
    }

    /// <summary>
    /// Advance to next slide
    /// </summary>
    public void NextSlide(string outputId)
    {
        lock (_lock)
        {
            if (_slideshows.TryGetValue(outputId, out var state) && state.IsRunning)
            {
                state.Timer?.Stop();
                int nextIndex = (state.CurrentIndex + 1) % state.Slides.Count;
                state.CurrentIndex = nextIndex;
                ShowSlide(state, nextIndex);

                var slide = state.Slides[nextIndex];
                state.Timer!.Interval = slide.DurationMs > 0 ? slide.DurationMs : state.DefaultDurationMs;
                state.Timer.Start();
            }
        }
    }

    /// <summary>
    /// Go to previous slide
    /// </summary>
    public void PreviousSlide(string outputId)
    {
        lock (_lock)
        {
            if (_slideshows.TryGetValue(outputId, out var state) && state.IsRunning)
            {
                state.Timer?.Stop();
                int prevIndex = state.CurrentIndex - 1;
                if (prevIndex < 0) prevIndex = state.Slides.Count - 1;
                state.CurrentIndex = prevIndex;
                ShowSlide(state, prevIndex);

                var slide = state.Slides[prevIndex];
                state.Timer!.Interval = slide.DurationMs > 0 ? slide.DurationMs : state.DefaultDurationMs;
                state.Timer.Start();
            }
        }
    }

    /// <summary>
    /// Go to specific slide
    /// </summary>
    public void GoToSlide(string outputId, int slideIndex)
    {
        lock (_lock)
        {
            if (_slideshows.TryGetValue(outputId, out var state) && state.IsRunning)
            {
                if (slideIndex >= 0 && slideIndex < state.Slides.Count)
                {
                    state.Timer?.Stop();
                    state.CurrentIndex = slideIndex;
                    ShowSlide(state, slideIndex);

                    var slide = state.Slides[slideIndex];
                    state.Timer!.Interval = slide.DurationMs > 0 ? slide.DurationMs : state.DefaultDurationMs;
                    state.Timer.Start();
                }
            }
        }
    }

    /// <summary>
    /// Pause playback on an output
    /// </summary>
    public void Pause(string outputId)
    {
        if (_windows.TryGetValue(outputId, out var window))
        {
            _dispatcher.Invoke(() => window.Pause());
        }
    }

    /// <summary>
    /// Stop playback on an output and hide the window
    /// </summary>
    public void Stop(string outputId)
    {
        // Also stop any slideshow on this output
        StopSlideshow(outputId);

        lock (_lock)
        {
            if (_windows.TryGetValue(outputId, out var window))
            {
                _dispatcher.Invoke(() =>
                {
                    window.Stop();
                    window.HideWindow();
                });
                _windows.Remove(outputId);
            }
        }
    }

    /// <summary>
    /// Resume playback on an output
    /// </summary>
    public void Resume(string outputId)
    {
        if (_windows.TryGetValue(outputId, out var window))
        {
            _dispatcher.Invoke(() => window.Resume());
        }
    }

    /// <summary>
    /// Seek to position on an output
    /// </summary>
    public void SeekTo(string outputId, long positionMs)
    {
        if (_windows.TryGetValue(outputId, out var window))
        {
            _dispatcher.Invoke(() => window.SeekTo(positionMs));
        }
    }

    /// <summary>
    /// Set volume on an output
    /// </summary>
    public void SetVolume(string outputId, int volume, bool mute = false)
    {
        if (_windows.TryGetValue(outputId, out var window))
        {
            _dispatcher.Invoke(() =>
            {
                window.SetVolume(volume);
                window.SetMute(mute);
            });
        }
    }

    /// <summary>
    /// Run a test pattern on an output
    /// </summary>
    public void RunTestPattern(string outputId, TestPatternGenerator.PatternType pattern, int durationSeconds)
    {
        var window = GetOrCreateWindow(outputId);
        _dispatcher.Invoke(() => window.ShowTestPattern(pattern, durationSeconds));
    }

    /// <summary>
    /// Run sync flash test on an output
    /// </summary>
    public void StartSyncFlash(string outputId, int intervalMs = 500)
    {
        var window = GetOrCreateWindow(outputId);
        _dispatcher.Invoke(() => window.StartSyncFlash(intervalMs));
    }

    /// <summary>
    /// Stop sync flash test on an output
    /// </summary>
    public void StopSyncFlash(string outputId)
    {
        if (_windows.TryGetValue(outputId, out var window))
        {
            _dispatcher.Invoke(() => window.StopSyncFlash());
        }
    }

    /// <summary>
    /// Clear content on an output
    /// </summary>
    public void ClearContent(string outputId)
    {
        if (_windows.TryGetValue(outputId, out var window))
        {
            _dispatcher.Invoke(() => window.ClearContent());
        }
    }

    /// <summary>
    /// Take a snapshot from an output
    /// </summary>
    public async Task<byte[]?> TakeSnapshotAsync(string outputId, int width = 320, int height = 180)
    {
        if (_windows.TryGetValue(outputId, out var window))
        {
            return await _dispatcher.InvokeAsync(async () => await window.TakeSnapshotAsync(width, height)).Task.Unwrap();
        }
        return null;
    }

    /// <summary>
    /// Get playback status for all outputs
    /// </summary>
    public Dictionary<string, WindowPlaybackStatus> GetAllStatus()
    {
        var result = new Dictionary<string, WindowPlaybackStatus>();

        lock (_lock)
        {
            foreach (var (outputId, window) in _windows)
            {
                _dispatcher.Invoke(() =>
                {
                    result[outputId] = new WindowPlaybackStatus
                    {
                        OutputId = outputId,
                        PositionMs = window.GetPosition(),
                        DurationMs = window.GetDuration()
                    };
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Close a specific display window
    /// </summary>
    public void CloseWindow(string outputId)
    {
        lock (_lock)
        {
            if (_windows.TryGetValue(outputId, out var window))
            {
                _dispatcher.Invoke(() => window.Close());
                _windows.Remove(outputId);
            }
        }
    }

    /// <summary>
    /// Close all display windows
    /// </summary>
    public void CloseAllWindows()
    {
        lock (_lock)
        {
            foreach (var window in _windows.Values)
            {
                _dispatcher.Invoke(() => window.Close());
            }
            _windows.Clear();
        }
    }

    /// <summary>
    /// Pause all outputs
    /// </summary>
    public void PauseAll()
    {
        lock (_lock)
        {
            foreach (var window in _windows.Values)
            {
                _dispatcher.Invoke(() => window.Pause());
            }
        }
    }

    /// <summary>
    /// Stop all outputs and hide all windows
    /// </summary>
    public void StopAll()
    {
        // Stop all slideshows first
        lock (_lock)
        {
            foreach (var state in _slideshows.Values)
            {
                state.IsRunning = false;
                state.Timer?.Stop();
                state.Timer?.Dispose();
            }
            _slideshows.Clear();
        }

        // Stop and hide all windows
        lock (_lock)
        {
            foreach (var window in _windows.Values)
            {
                _dispatcher.Invoke(() =>
                {
                    window.Stop();
                    window.HideWindow();
                });
            }
            _windows.Clear();
        }
    }

    /// <summary>
    /// Resume all outputs
    /// </summary>
    public void ResumeAll()
    {
        lock (_lock)
        {
            foreach (var window in _windows.Values)
            {
                _dispatcher.Invoke(() => window.Resume());
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CloseAllWindows();
        _libVLC.Dispose();
    }
}

/// <summary>
/// Playback status for a window
/// </summary>
public class WindowPlaybackStatus
{
    public string OutputId { get; set; } = string.Empty;
    public PlaybackState State { get; set; }
    public string? MediaPath { get; set; }
    public long PositionMs { get; set; }
    public long DurationMs { get; set; }
    public int Volume { get; set; }
}

/// <summary>
/// Slideshow state for an output
/// </summary>
public class SlideshowState
{
    public string OutputId { get; set; } = string.Empty;
    public List<SlideshowSlide> Slides { get; set; } = new();
    public int DefaultDurationMs { get; set; } = 5000;
    public TransitionType Transition { get; set; } = TransitionType.Fade;
    public int TransitionDurationMs { get; set; } = 1000;
    public bool Loop { get; set; } = true;
    public int CurrentIndex { get; set; }
    public bool IsRunning { get; set; }
    public System.Timers.Timer? Timer { get; set; }
}

/// <summary>
/// A single slide in a slideshow
/// </summary>
public class SlideshowSlide
{
    public string MediaPath { get; set; } = string.Empty;
    public int DurationMs { get; set; }
}
