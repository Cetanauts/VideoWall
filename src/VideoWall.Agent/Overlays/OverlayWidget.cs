using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VideoWall.Core.Models;

namespace VideoWall.Agent.Overlays;

/// <summary>
/// Abstract base class for overlay widgets displayed on top of content
/// </summary>
public abstract class OverlayWidget : IDisposable
{
    public string OverlayId { get; }
    public OverlayConfig Config { get; private set; }
    protected Border Container { get; }
    protected bool IsRunning { get; private set; }

    protected OverlayWidget(OverlayConfig config)
    {
        OverlayId = config.OverlayId;
        Config = config;

        Container = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Tag = config.OverlayId
        };

        ApplyCommonStyles(config);
    }

    private void ApplyCommonStyles(OverlayConfig config)
    {
        try
        {
            Container.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(config.BackgroundColor));
        }
        catch
        {
            Container.Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0));
        }

        Container.Opacity = config.Opacity;
        Container.Margin = new Thickness(config.Margin);

        // Set alignment based on position
        switch (config.Position)
        {
            case OverlayPosition.TopLeft:
                Container.HorizontalAlignment = HorizontalAlignment.Left;
                Container.VerticalAlignment = VerticalAlignment.Top;
                break;
            case OverlayPosition.TopCenter:
                Container.HorizontalAlignment = HorizontalAlignment.Center;
                Container.VerticalAlignment = VerticalAlignment.Top;
                break;
            case OverlayPosition.TopRight:
                Container.HorizontalAlignment = HorizontalAlignment.Right;
                Container.VerticalAlignment = VerticalAlignment.Top;
                break;
            case OverlayPosition.BottomLeft:
                Container.HorizontalAlignment = HorizontalAlignment.Left;
                Container.VerticalAlignment = VerticalAlignment.Bottom;
                break;
            case OverlayPosition.BottomCenter:
                Container.HorizontalAlignment = HorizontalAlignment.Center;
                Container.VerticalAlignment = VerticalAlignment.Bottom;
                break;
            case OverlayPosition.BottomRight:
                Container.HorizontalAlignment = HorizontalAlignment.Right;
                Container.VerticalAlignment = VerticalAlignment.Bottom;
                break;
        }
    }

    protected SolidColorBrush GetForegroundBrush()
    {
        try
        {
            return new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(Config.ForegroundColor));
        }
        catch
        {
            return new SolidColorBrush(Colors.White);
        }
    }

    protected FontFamily GetFontFamily()
    {
        return new FontFamily(Config.FontFamily ?? "Segoe UI");
    }

    /// <summary>
    /// Get the visual element to add to the overlay container
    /// </summary>
    public FrameworkElement GetElement() => Container;

    /// <summary>
    /// Start the widget (begin updates, animations, etc.)
    /// </summary>
    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;
        OnStart();
    }

    /// <summary>
    /// Stop the widget
    /// </summary>
    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        OnStop();
    }

    /// <summary>
    /// Update widget with new configuration
    /// </summary>
    public void Update(OverlayConfig config)
    {
        Config = config;
        ApplyCommonStyles(config);
        OnUpdate(config);
    }

    protected abstract void OnStart();
    protected abstract void OnStop();
    protected abstract void OnUpdate(OverlayConfig config);

    public virtual void Dispose()
    {
        Stop();
    }
}
