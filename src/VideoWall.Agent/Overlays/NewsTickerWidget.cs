using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml.Linq;
using VideoWall.Core.Models;

namespace VideoWall.Agent.Overlays;

public class NewsTickerWidget : OverlayWidget
{
    private readonly Canvas _canvas;
    private readonly TextBlock _textBlock;
    private readonly DispatcherTimer _refreshTimer;
    private readonly HttpClient _httpClient;
    private readonly TranslateTransform _translateTransform;
    private Storyboard? _scrollStoryboard;
    private string _tickerText = "Loading news...";
    private CancellationTokenSource? _cts;

    public NewsTickerWidget(OverlayConfig config) : base(config)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VideoWall/1.0");

        _translateTransform = new TranslateTransform();

        _textBlock = new TextBlock
        {
            FontSize = config.FontSize,
            FontFamily = GetFontFamily(),
            Foreground = GetForegroundBrush(),
            FontWeight = FontWeights.SemiBold,
            RenderTransform = _translateTransform,
            TextWrapping = TextWrapping.NoWrap
        };

        _canvas = new Canvas
        {
            ClipToBounds = true,
            Height = config.FontSize * 1.6
        };
        _canvas.Children.Add(_textBlock);
        Canvas.SetTop(_textBlock, 0);

        // Stretch canvas to fill container width
        Container.Child = _canvas;
        Container.HorizontalAlignment = HorizontalAlignment.Stretch;

        // Position override: ticker should span the full width at top or bottom
        switch (config.Position)
        {
            case OverlayPosition.TopLeft:
            case OverlayPosition.TopCenter:
            case OverlayPosition.TopRight:
                Container.VerticalAlignment = VerticalAlignment.Top;
                break;
            default:
                Container.VerticalAlignment = VerticalAlignment.Bottom;
                break;
        }

        var refreshMinutes = config.NewsTicker?.RefreshIntervalMinutes ?? 15;
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(Math.Max(1, refreshMinutes))
        };
        _refreshTimer.Tick += async (_, _) => await FetchFeedsAsync();
    }

    private async Task FetchFeedsAsync()
    {
        var feedUrls = Config.NewsTicker?.FeedUrls ?? new List<string>();
        if (feedUrls.Count == 0)
        {
            _tickerText = "No RSS feed URLs configured";
            UpdateScrollAnimation();
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        var separator = Config.NewsTicker?.Separator ?? "  ///  ";
        var allTitles = new List<string>();

        foreach (var url in feedUrls)
        {
            try
            {
                var xml = await _httpClient.GetStringAsync(url, token);
                var doc = XDocument.Parse(xml);

                // Parse RSS <item><title> elements
                var titles = doc.Descendants("item")
                    .Select(item => item.Element("title")?.Value)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Take(20)
                    .Cast<string>();

                allTitles.AddRange(titles);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NewsTickerWidget] Failed to fetch {url}: {ex.Message}");
            }
        }

        if (allTitles.Count > 0)
        {
            _tickerText = string.Join(separator, allTitles);
        }
        else
        {
            _tickerText = "Unable to load news feeds";
        }

        _textBlock.Dispatcher.Invoke(() =>
        {
            _textBlock.Text = _tickerText;
            UpdateScrollAnimation();
        });
    }

    private void UpdateScrollAnimation()
    {
        _scrollStoryboard?.Stop();

        // Measure text width
        _textBlock.Text = _tickerText;
        _textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var textWidth = _textBlock.DesiredSize.Width;
        var containerWidth = Container.ActualWidth > 0 ? Container.ActualWidth : 800;

        var scrollSpeed = Config.NewsTicker?.ScrollSpeedPx ?? 100;
        if (scrollSpeed <= 0) scrollSpeed = 100;

        var totalDistance = containerWidth + textWidth;
        var durationSeconds = totalDistance / scrollSpeed;

        var animation = new DoubleAnimation
        {
            From = containerWidth,
            To = -textWidth,
            Duration = TimeSpan.FromSeconds(durationSeconds),
            RepeatBehavior = RepeatBehavior.Forever
        };

        _scrollStoryboard = new Storyboard();
        _scrollStoryboard.Children.Add(animation);
        Storyboard.SetTarget(animation, _textBlock);
        Storyboard.SetTargetProperty(animation, new PropertyPath("RenderTransform.(TranslateTransform.X)"));

        _scrollStoryboard.Begin();
    }

    protected override async void OnStart()
    {
        await FetchFeedsAsync();
        _refreshTimer.Start();
    }

    protected override void OnStop()
    {
        _refreshTimer.Stop();
        _scrollStoryboard?.Stop();
        _cts?.Cancel();
    }

    protected override void OnUpdate(OverlayConfig config)
    {
        _textBlock.FontSize = config.FontSize;
        _textBlock.FontFamily = GetFontFamily();
        _textBlock.Foreground = GetForegroundBrush();
        _canvas.Height = config.FontSize * 1.6;

        var refreshMinutes = config.NewsTicker?.RefreshIntervalMinutes ?? 15;
        _refreshTimer.Interval = TimeSpan.FromMinutes(Math.Max(1, refreshMinutes));

        UpdateScrollAnimation();
    }

    public override void Dispose()
    {
        _scrollStoryboard?.Stop();
        _refreshTimer.Stop();
        _cts?.Cancel();
        _cts?.Dispose();
        _httpClient.Dispose();
        base.Dispose();
    }
}
