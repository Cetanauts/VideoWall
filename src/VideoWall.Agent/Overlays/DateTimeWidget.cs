using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VideoWall.Core.Models;

namespace VideoWall.Agent.Overlays;

public class DateTimeWidget : OverlayWidget
{
    private readonly StackPanel _panel;
    private readonly TextBlock _primaryLine;
    private readonly TextBlock _secondaryLine;
    private readonly DispatcherTimer _timer;
    private TimeZoneInfo _timeZone;
    private bool _isMultiLine;
    private string _format1 = "";
    private string _format2 = "";

    public DateTimeWidget(OverlayConfig config) : base(config)
    {
        _timeZone = ResolveTimeZone(config.DateTime?.TimeZoneId);

        _primaryLine = new TextBlock
        {
            FontSize = config.FontSize,
            FontFamily = GetFontFamily(),
            Foreground = GetForegroundBrush(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _secondaryLine = new TextBlock
        {
            FontSize = config.FontSize,
            FontFamily = GetFontFamily(),
            Foreground = GetForegroundBrush(),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed
        };

        _panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _panel.Children.Add(_primaryLine);
        _panel.Children.Add(_secondaryLine);

        Container.Child = _panel;

        ParseFormat(config.DateTime?.Format ?? "dddd, MMMM d, yyyy h:mm tt");
        ApplyFontSizes(config.FontSize);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _timer.Tick += (_, _) => UpdateDisplay();
    }

    private void ParseFormat(string format)
    {
        // Support literal \n in the format string for multi-line
        if (format.Contains("\\n"))
        {
            var parts = format.Split("\\n", 2);
            _format1 = parts[0].Trim();
            _format2 = parts.Length > 1 ? parts[1].Trim() : "";
            _isMultiLine = true;
            _secondaryLine.Visibility = Visibility.Visible;
            _primaryLine.FontWeight = FontWeights.Bold;
            _secondaryLine.FontWeight = FontWeights.Normal;
        }
        else
        {
            _format1 = format;
            _format2 = "";
            _isMultiLine = false;
            _secondaryLine.Visibility = Visibility.Collapsed;
            _primaryLine.FontWeight = FontWeights.Normal;
        }
    }

    private void ApplyFontSizes(double baseSize)
    {
        if (_isMultiLine)
        {
            // First line ~10% larger, bold
            _primaryLine.FontSize = baseSize * 1.1;
            _secondaryLine.FontSize = baseSize;
        }
        else
        {
            _primaryLine.FontSize = baseSize;
        }
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

        if (_isMultiLine)
        {
            _primaryLine.Text = now.ToString(_format1);
            _secondaryLine.Text = now.ToString(_format2);
        }
        else
        {
            _primaryLine.Text = now.ToString(_format1);
        }
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
        _timeZone = ResolveTimeZone(config.DateTime?.TimeZoneId);
        var brush = GetForegroundBrush();
        var font = GetFontFamily();

        ParseFormat(config.DateTime?.Format ?? "dddd, MMMM d, yyyy h:mm tt");
        ApplyFontSizes(config.FontSize);

        _primaryLine.FontFamily = font;
        _primaryLine.Foreground = brush;
        _secondaryLine.FontFamily = font;
        _secondaryLine.Foreground = brush;

        UpdateDisplay();
    }

    public override void Dispose()
    {
        _timer.Stop();
        base.Dispose();
    }
}
