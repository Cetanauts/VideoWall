using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VideoWall.Core.Models;

namespace VideoWall.Agent.Overlays;

public class ClockWidget : OverlayWidget
{
    private readonly TextBlock _textBlock;
    private readonly DispatcherTimer _timer;
    private TimeZoneInfo _timeZone;

    public ClockWidget(OverlayConfig config) : base(config)
    {
        _timeZone = ResolveTimeZone(config.Clock?.TimeZoneId);

        _textBlock = new TextBlock
        {
            FontSize = config.FontSize,
            FontFamily = GetFontFamily(),
            Foreground = GetForegroundBrush(),
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        Container.Child = _textBlock;

        var intervalMs = (config.Clock?.ShowSeconds ?? true) ? 1000 : 60000;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
        _timer.Tick += (_, _) => UpdateDisplay();
    }

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId))
            return TimeZoneInfo.Local;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            return TimeZoneInfo.Local;
        }
    }

    private void UpdateDisplay()
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _timeZone);
        var clockConfig = Config.Clock;

        string format;
        if (!string.IsNullOrEmpty(clockConfig?.Format))
        {
            format = clockConfig.Format;
        }
        else
        {
            var use24 = clockConfig?.Use24Hour ?? true;
            var showSec = clockConfig?.ShowSeconds ?? true;

            format = (use24, showSec) switch
            {
                (true, true) => "HH:mm:ss",
                (true, false) => "HH:mm",
                (false, true) => "h:mm:ss tt",
                (false, false) => "h:mm tt"
            };
        }

        _textBlock.Text = now.ToString(format);
    }

    protected override void OnStart()
    {
        UpdateDisplay();
        _timer.Start();
    }

    protected override void OnStop()
    {
        _timer.Stop();
    }

    protected override void OnUpdate(OverlayConfig config)
    {
        _timeZone = ResolveTimeZone(config.Clock?.TimeZoneId);
        _textBlock.FontSize = config.FontSize;
        _textBlock.FontFamily = GetFontFamily();
        _textBlock.Foreground = GetForegroundBrush();

        var intervalMs = (config.Clock?.ShowSeconds ?? true) ? 1000 : 60000;
        _timer.Interval = TimeSpan.FromMilliseconds(intervalMs);

        UpdateDisplay();
    }

    public override void Dispose()
    {
        _timer.Stop();
        base.Dispose();
    }
}
