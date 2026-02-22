using System.Timers;
using VideoWall.Core.Enums;
using VideoWall.Core.Models;

namespace VideoWall.Playback;

/// <summary>
/// Manages slideshow playback with transitions for a single output
/// </summary>
public class SlideshowPlayer : IDisposable
{
    private Slideshow? _slideshow;
    private int _currentIndex = -1;
    private System.Timers.Timer? _advanceTimer;
    private bool _isPaused;
    private bool _disposed;
    private readonly object _lock = new();

    /// <summary>
    /// Player ID (typically OutputId)
    /// </summary>
    public string PlayerId { get; }

    /// <summary>
    /// Current slideshow
    /// </summary>
    public Slideshow? CurrentSlideshow => _slideshow;

    /// <summary>
    /// Current slide index
    /// </summary>
    public int CurrentIndex => _currentIndex;

    /// <summary>
    /// Current slide
    /// </summary>
    public Slide? CurrentSlide => _slideshow != null && _currentIndex >= 0 && _currentIndex < _slideshow.Slides.Count
        ? _slideshow.Slides[_currentIndex]
        : null;

    /// <summary>
    /// Is the slideshow playing?
    /// </summary>
    public bool IsPlaying => _advanceTimer?.Enabled == true && !_isPaused;

    /// <summary>
    /// Is the slideshow paused?
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Event fired when slide changes
    /// </summary>
    public event EventHandler<SlideChangedEventArgs>? SlideChanged;

    /// <summary>
    /// Event fired when transition starts
    /// </summary>
    public event EventHandler<TransitionEventArgs>? TransitionStarted;

    /// <summary>
    /// Event fired when transition ends
    /// </summary>
    public event EventHandler<TransitionEventArgs>? TransitionEnded;

    /// <summary>
    /// Event fired when slideshow loops
    /// </summary>
    public event EventHandler? SlideshowLooped;

    /// <summary>
    /// Event fired when slideshow ends
    /// </summary>
    public event EventHandler? SlideshowEnded;

    public SlideshowPlayer(string playerId)
    {
        PlayerId = playerId;
    }

    /// <summary>
    /// Load a slideshow
    /// </summary>
    public void Load(Slideshow slideshow)
    {
        lock (_lock)
        {
            Stop();
            _slideshow = slideshow;
            _currentIndex = -1;
        }
    }

    /// <summary>
    /// Start or resume the slideshow
    /// </summary>
    public void Play()
    {
        lock (_lock)
        {
            if (_slideshow == null || _slideshow.Slides.Count == 0) return;

            if (_currentIndex < 0)
            {
                // Start from beginning
                GoToSlide(0);
            }
            else if (_isPaused)
            {
                // Resume
                Resume();
            }

            StartTimer();
        }
    }

    /// <summary>
    /// Pause the slideshow
    /// </summary>
    public void Pause()
    {
        lock (_lock)
        {
            _isPaused = true;
            _advanceTimer?.Stop();
        }
    }

    /// <summary>
    /// Resume the slideshow
    /// </summary>
    public void Resume()
    {
        lock (_lock)
        {
            _isPaused = false;
            StartTimer();
        }
    }

    /// <summary>
    /// Stop the slideshow
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _advanceTimer?.Stop();
            _advanceTimer?.Dispose();
            _advanceTimer = null;
            _isPaused = false;
            _currentIndex = -1;
        }
    }

    /// <summary>
    /// Go to next slide
    /// </summary>
    public void Next()
    {
        lock (_lock)
        {
            if (_slideshow == null) return;

            int nextIndex = _currentIndex + 1;

            if (nextIndex >= _slideshow.Slides.Count)
            {
                // End of slideshow
                if (_slideshow.LoopMode == LoopMode.All)
                {
                    if (_slideshow.ShuffleOnLoop)
                    {
                        ShuffleSlides();
                    }
                    nextIndex = 0;
                    SlideshowLooped?.Invoke(this, EventArgs.Empty);
                }
                else if (_slideshow.LoopMode == LoopMode.Shuffle)
                {
                    ShuffleSlides();
                    nextIndex = 0;
                    SlideshowLooped?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Stop();
                    SlideshowEnded?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }

            GoToSlide(nextIndex);
        }
    }

    /// <summary>
    /// Go to previous slide
    /// </summary>
    public void Previous()
    {
        lock (_lock)
        {
            if (_slideshow == null) return;

            int prevIndex = _currentIndex - 1;
            if (prevIndex < 0)
            {
                prevIndex = _slideshow.Slides.Count - 1;
            }

            GoToSlide(prevIndex);
        }
    }

    /// <summary>
    /// Go to a specific slide
    /// </summary>
    public void GoToSlide(int index)
    {
        lock (_lock)
        {
            if (_slideshow == null) return;
            if (index < 0 || index >= _slideshow.Slides.Count) return;

            var previousIndex = _currentIndex;
            var previousSlide = CurrentSlide;
            _currentIndex = index;
            var currentSlide = CurrentSlide;

            if (currentSlide == null) return;

            // Determine transition
            var transition = currentSlide.Transition ?? _slideshow.DefaultTransition;
            var transitionDuration = currentSlide.TransitionDurationMs > 0
                ? currentSlide.TransitionDurationMs
                : _slideshow.DefaultTransitionDurationMs;

            // Fire transition started
            TransitionStarted?.Invoke(this, new TransitionEventArgs(
                previousSlide,
                currentSlide,
                transition,
                transitionDuration
            ));

            // Fire slide changed (after transition would complete in real implementation)
            SlideChanged?.Invoke(this, new SlideChangedEventArgs(
                previousIndex,
                _currentIndex,
                previousSlide,
                currentSlide
            ));

            // Schedule transition end
            Task.Delay(transitionDuration).ContinueWith(_ =>
            {
                TransitionEnded?.Invoke(this, new TransitionEventArgs(
                    previousSlide,
                    currentSlide,
                    transition,
                    transitionDuration
                ));
            });

            // Restart timer for next slide
            if (_slideshow.AutoAdvance && !_isPaused)
            {
                StartTimer();
            }
        }
    }

    private void StartTimer()
    {
        if (_slideshow == null) return;

        _advanceTimer?.Stop();
        _advanceTimer?.Dispose();

        var currentSlide = CurrentSlide;
        if (currentSlide == null) return;

        var duration = currentSlide.DurationMs > 0
            ? currentSlide.DurationMs
            : _slideshow.DefaultDurationMs;

        var transitionDuration = currentSlide.TransitionDurationMs > 0
            ? currentSlide.TransitionDurationMs
            : _slideshow.DefaultTransitionDurationMs;

        // Total time = display duration + transition duration
        var totalInterval = duration + transitionDuration;

        _advanceTimer = new System.Timers.Timer(totalInterval);
        _advanceTimer.AutoReset = false;
        _advanceTimer.Elapsed += OnTimerElapsed;
        _advanceTimer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (!_isPaused)
        {
            Next();
        }
    }

    private void ShuffleSlides()
    {
        if (_slideshow == null) return;

        var random = new Random();
        var slides = _slideshow.Slides;
        int n = slides.Count;

        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            (slides[k], slides[n]) = (slides[n], slides[k]);
        }

        // Update order indices
        for (int i = 0; i < slides.Count; i++)
        {
            slides[i].Order = i;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Event args for slide changes
/// </summary>
public class SlideChangedEventArgs : EventArgs
{
    public int PreviousIndex { get; }
    public int CurrentIndex { get; }
    public Slide? PreviousSlide { get; }
    public Slide? CurrentSlide { get; }

    public SlideChangedEventArgs(int previousIndex, int currentIndex, Slide? previousSlide, Slide? currentSlide)
    {
        PreviousIndex = previousIndex;
        CurrentIndex = currentIndex;
        PreviousSlide = previousSlide;
        CurrentSlide = currentSlide;
    }
}

/// <summary>
/// Event args for transitions
/// </summary>
public class TransitionEventArgs : EventArgs
{
    public Slide? FromSlide { get; }
    public Slide? ToSlide { get; }
    public TransitionType TransitionType { get; }
    public int DurationMs { get; }

    public TransitionEventArgs(Slide? fromSlide, Slide? toSlide, TransitionType transitionType, int durationMs)
    {
        FromSlide = fromSlide;
        ToSlide = toSlide;
        TransitionType = transitionType;
        DurationMs = durationMs;
    }
}
