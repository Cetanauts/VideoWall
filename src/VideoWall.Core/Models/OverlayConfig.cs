namespace VideoWall.Core.Models;

public enum OverlayType
{
    Clock,
    NewsTicker,
    StaticText,
    DateTime
}

public enum OverlayPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public class OverlayConfig
{
    public string OverlayId { get; set; } = string.Empty;
    public OverlayType Type { get; set; }
    public OverlayPosition Position { get; set; }
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 24;
    public string ForegroundColor { get; set; } = "#FFFFFFFF";
    public string BackgroundColor { get; set; } = "#80000000";
    public int Margin { get; set; } = 20;
    public double Opacity { get; set; } = 1.0;
    public bool IsEnabled { get; set; } = true;

    // Type-specific config
    public ClockConfig? Clock { get; set; }
    public NewsTickerConfig? NewsTicker { get; set; }
    public StaticTextConfig? StaticText { get; set; }
    public DateTimeConfig? DateTime { get; set; }
}

public class ClockConfig
{
    public string? TimeZoneId { get; set; }
    public bool Use24Hour { get; set; } = true;
    public bool ShowSeconds { get; set; } = true;
    public string? Format { get; set; }
}

public class NewsTickerConfig
{
    public List<string> FeedUrls { get; set; } = new();
    public int ScrollSpeedPx { get; set; } = 100;
    public int RefreshIntervalMinutes { get; set; } = 15;
    public string Separator { get; set; } = "  ///  ";
}

public class StaticTextConfig
{
    public string Text { get; set; } = string.Empty;
    public bool WordWrap { get; set; } = true;
}

public class DateTimeConfig
{
    public string Format { get; set; } = "dddd, MMMM d, yyyy h:mm tt";
    public string? TimeZoneId { get; set; }
}
