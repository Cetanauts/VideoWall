using System.Windows.Threading;
using VideoWall.Agent.Overlays;
using VideoWall.Core.Models;

namespace VideoWall.Agent.Services;

/// <summary>
/// Manages overlay widgets across all display windows
/// </summary>
public class OverlayManager : IDisposable
{
    private readonly DisplayWindowManager _displayManager;
    private readonly Dispatcher _dispatcher;
    // outputId -> (overlayId -> widget)
    private readonly Dictionary<string, Dictionary<string, OverlayWidget>> _overlays = new();
    private readonly object _lock = new();

    public OverlayManager(DisplayWindowManager displayManager, Dispatcher dispatcher)
    {
        _displayManager = displayManager;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Set (create or update) an overlay on a specific output
    /// </summary>
    public void SetOverlay(string outputId, OverlayConfig config)
    {
        lock (_lock)
        {
            if (!_overlays.TryGetValue(outputId, out var outputOverlays))
            {
                outputOverlays = new Dictionary<string, OverlayWidget>();
                _overlays[outputId] = outputOverlays;
            }

            // If overlay already exists, update it
            if (outputOverlays.TryGetValue(config.OverlayId, out var existing))
            {
                _dispatcher.Invoke(() => existing.Update(config));
                return;
            }

            // Create new widget
            _dispatcher.Invoke(() =>
            {
                var widget = CreateWidget(config);
                outputOverlays[config.OverlayId] = widget;

                // Get or create the display window and add the overlay
                var window = _displayManager.GetOrCreateWindow(outputId);
                window.AddOverlay(config.OverlayId, widget.GetElement(), config.Position);
                widget.Start();
            });
        }
    }

    /// <summary>
    /// Remove a specific overlay from an output
    /// </summary>
    public void RemoveOverlay(string outputId, string overlayId)
    {
        lock (_lock)
        {
            if (!_overlays.TryGetValue(outputId, out var outputOverlays))
                return;

            if (!outputOverlays.TryGetValue(overlayId, out var widget))
                return;

            _dispatcher.Invoke(() =>
            {
                widget.Stop();
                widget.Dispose();

                // Remove from display window
                var window = _displayManager.GetOrCreateWindow(outputId);
                window.RemoveOverlay(overlayId);
            });

            outputOverlays.Remove(overlayId);
        }
    }

    /// <summary>
    /// Clear all overlays from an output
    /// </summary>
    public void ClearOverlays(string outputId)
    {
        lock (_lock)
        {
            if (!_overlays.TryGetValue(outputId, out var outputOverlays))
                return;

            _dispatcher.Invoke(() =>
            {
                foreach (var widget in outputOverlays.Values)
                {
                    widget.Stop();
                    widget.Dispose();
                }

                var window = _displayManager.GetOrCreateWindow(outputId);
                window.ClearOverlays();
            });

            outputOverlays.Clear();
        }
    }

    /// <summary>
    /// Get full config for all active overlays on an output
    /// </summary>
    public List<OverlayConfig> GetOverlayConfigs(string outputId)
    {
        lock (_lock)
        {
            var result = new List<OverlayConfig>();

            if (!_overlays.TryGetValue(outputId, out var outputOverlays))
                return result;

            foreach (var (_, widget) in outputOverlays)
            {
                result.Add(widget.Config);
            }

            return result;
        }
    }

    private static OverlayWidget CreateWidget(OverlayConfig config)
    {
        return config.Type switch
        {
            OverlayType.Clock => new ClockWidget(config),
            OverlayType.NewsTicker => new NewsTickerWidget(config),
            OverlayType.StaticText => new StaticTextWidget(config),
            OverlayType.DateTime => new DateTimeWidget(config),
            _ => new StaticTextWidget(config)
        };
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var outputOverlays in _overlays.Values)
            {
                foreach (var widget in outputOverlays.Values)
                {
                    widget.Stop();
                    widget.Dispose();
                }
            }
            _overlays.Clear();
        }
    }
}
