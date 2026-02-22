using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VideoWall.Playback;

/// <summary>
/// Generates various test patterns for display calibration and diagnostics
/// </summary>
public class TestPatternGenerator
{
    /// <summary>
    /// Test pattern types
    /// </summary>
    public enum PatternType
    {
        ColorBars,
        Grid,
        SolidWhite,
        SolidBlack,
        SolidRed,
        SolidGreen,
        SolidBlue,
        SolidCyan,
        SolidMagenta,
        SolidYellow,
        Gradient,
        Checkerboard,
        CrossHatch,
        OutputIdentifier,
        SyncFlash,
        Resolution
    }

    /// <summary>
    /// Create a test pattern as a WPF element
    /// </summary>
    public static FrameworkElement CreatePattern(PatternType type, int width, int height, string? identifier = null)
    {
        return type switch
        {
            PatternType.ColorBars => CreateColorBars(width, height),
            PatternType.Grid => CreateGrid(width, height),
            PatternType.SolidWhite => CreateSolid(Colors.White),
            PatternType.SolidBlack => CreateSolid(Colors.Black),
            PatternType.SolidRed => CreateSolid(Colors.Red),
            PatternType.SolidGreen => CreateSolid(Colors.Lime),
            PatternType.SolidBlue => CreateSolid(Colors.Blue),
            PatternType.SolidCyan => CreateSolid(Colors.Cyan),
            PatternType.SolidMagenta => CreateSolid(Colors.Magenta),
            PatternType.SolidYellow => CreateSolid(Colors.Yellow),
            PatternType.Gradient => CreateGradient(width, height),
            PatternType.Checkerboard => CreateCheckerboard(width, height),
            PatternType.CrossHatch => CreateCrossHatch(width, height),
            PatternType.OutputIdentifier => CreateOutputIdentifier(width, height, identifier ?? "OUTPUT"),
            PatternType.SyncFlash => CreateSyncFlash(width, height),
            PatternType.Resolution => CreateResolutionPattern(width, height),
            _ => CreateColorBars(width, height)
        };
    }

    /// <summary>
    /// Create SMPTE-style color bars
    /// </summary>
    private static FrameworkElement CreateColorBars(int width, int height)
    {
        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.Black
        };

        // Standard color bar colors (SMPTE)
        var colors = new[]
        {
            Color.FromRgb(192, 192, 192), // 75% White
            Color.FromRgb(192, 192, 0),   // Yellow
            Color.FromRgb(0, 192, 192),   // Cyan
            Color.FromRgb(0, 192, 0),     // Green
            Color.FromRgb(192, 0, 192),   // Magenta
            Color.FromRgb(192, 0, 0),     // Red
            Color.FromRgb(0, 0, 192)      // Blue
        };

        double barWidth = (double)width / colors.Length;
        double mainHeight = height * 0.67;

        // Main color bars (top 67%)
        for (int i = 0; i < colors.Length; i++)
        {
            var rect = new Rectangle
            {
                Width = barWidth,
                Height = mainHeight,
                Fill = new SolidColorBrush(colors[i])
            };
            Canvas.SetLeft(rect, i * barWidth);
            Canvas.SetTop(rect, 0);
            canvas.Children.Add(rect);
        }

        // Reverse bars (next 8%)
        var reverseColors = new[]
        {
            Color.FromRgb(0, 0, 192),     // Blue
            Color.FromRgb(19, 19, 19),    // Black
            Color.FromRgb(192, 0, 192),   // Magenta
            Color.FromRgb(19, 19, 19),    // Black
            Color.FromRgb(0, 192, 192),   // Cyan
            Color.FromRgb(19, 19, 19),    // Black
            Color.FromRgb(192, 192, 192)  // 75% White
        };

        double reverseHeight = height * 0.08;
        for (int i = 0; i < reverseColors.Length; i++)
        {
            var rect = new Rectangle
            {
                Width = barWidth,
                Height = reverseHeight,
                Fill = new SolidColorBrush(reverseColors[i])
            };
            Canvas.SetLeft(rect, i * barWidth);
            Canvas.SetTop(rect, mainHeight);
            canvas.Children.Add(rect);
        }

        // Bottom section - PLUGE and gray bars (25%)
        double bottomY = mainHeight + reverseHeight;
        double bottomHeight = height - bottomY;

        // Dark bars for PLUGE
        var bottomColors = new[]
        {
            Color.FromRgb(0, 33, 76),     // -I
            Color.FromRgb(255, 255, 255), // White
            Color.FromRgb(50, 0, 106),    // +Q
            Color.FromRgb(19, 19, 19),    // Black
            Color.FromRgb(9, 9, 9),       // -4%
            Color.FromRgb(19, 19, 19),    // Black
            Color.FromRgb(29, 29, 29)     // +4%
        };

        for (int i = 0; i < 4; i++)
        {
            var rect = new Rectangle
            {
                Width = barWidth,
                Height = bottomHeight,
                Fill = new SolidColorBrush(bottomColors[i])
            };
            Canvas.SetLeft(rect, i * barWidth);
            Canvas.SetTop(rect, bottomY);
            canvas.Children.Add(rect);
        }

        // PLUGE section (last 3 bars combined)
        double plugeWidth = barWidth * 3;
        double plugeX = 4 * barWidth;
        double subWidth = plugeWidth / 3;

        for (int i = 0; i < 3; i++)
        {
            var rect = new Rectangle
            {
                Width = subWidth,
                Height = bottomHeight,
                Fill = new SolidColorBrush(bottomColors[4 + i])
            };
            Canvas.SetLeft(rect, plugeX + i * subWidth);
            Canvas.SetTop(rect, bottomY);
            canvas.Children.Add(rect);
        }

        return canvas;
    }

    /// <summary>
    /// Create a calibration grid
    /// </summary>
    private static FrameworkElement CreateGrid(int width, int height)
    {
        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.Black
        };

        int gridSize = 64;
        var gridBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50));
        var majorBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));

        // Draw grid lines
        for (int x = 0; x <= width; x += gridSize)
        {
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = (x % (gridSize * 4) == 0) ? majorBrush : gridBrush,
                StrokeThickness = (x % (gridSize * 4) == 0) ? 2 : 1
            };
            canvas.Children.Add(line);
        }

        for (int y = 0; y <= height; y += gridSize)
        {
            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = (y % (gridSize * 4) == 0) ? majorBrush : gridBrush,
                StrokeThickness = (y % (gridSize * 4) == 0) ? 2 : 1
            };
            canvas.Children.Add(line);
        }

        // Center crosshair
        var centerX = width / 2;
        var centerY = height / 2;
        var crosshairBrush = Brushes.Cyan;

        canvas.Children.Add(new Line { X1 = centerX - 50, Y1 = centerY, X2 = centerX + 50, Y2 = centerY, Stroke = crosshairBrush, StrokeThickness = 2 });
        canvas.Children.Add(new Line { X1 = centerX, Y1 = centerY - 50, X2 = centerX, Y2 = centerY + 50, Stroke = crosshairBrush, StrokeThickness = 2 });

        // Corner markers
        var corners = new[] { (0, 0), (width, 0), (0, height), (width, height) };
        foreach (var (cx, cy) in corners)
        {
            var circle = new Ellipse
            {
                Width = 40,
                Height = 40,
                Stroke = Brushes.Red,
                StrokeThickness = 2
            };
            Canvas.SetLeft(circle, cx - 20);
            Canvas.SetTop(circle, cy - 20);
            canvas.Children.Add(circle);
        }

        return canvas;
    }

    /// <summary>
    /// Create a solid color pattern
    /// </summary>
    private static FrameworkElement CreateSolid(Color color)
    {
        return new Border
        {
            Background = new SolidColorBrush(color)
        };
    }

    /// <summary>
    /// Create a gradient pattern
    /// </summary>
    private static FrameworkElement CreateGradient(int width, int height)
    {
        var canvas = new Canvas
        {
            Width = width,
            Height = height
        };

        // Horizontal gradient (top half)
        var hGradient = new Rectangle
        {
            Width = width,
            Height = height / 2,
            Fill = new LinearGradientBrush(Colors.Black, Colors.White, 0)
        };
        canvas.Children.Add(hGradient);

        // RGB gradients (bottom half, split into 3)
        double bottomY = height / 2;
        double stripHeight = height / 6;

        var redGradient = new Rectangle
        {
            Width = width,
            Height = stripHeight,
            Fill = new LinearGradientBrush(Colors.Black, Colors.Red, 0)
        };
        Canvas.SetTop(redGradient, bottomY);
        canvas.Children.Add(redGradient);

        var greenGradient = new Rectangle
        {
            Width = width,
            Height = stripHeight,
            Fill = new LinearGradientBrush(Colors.Black, Colors.Lime, 0)
        };
        Canvas.SetTop(greenGradient, bottomY + stripHeight);
        canvas.Children.Add(greenGradient);

        var blueGradient = new Rectangle
        {
            Width = width,
            Height = stripHeight,
            Fill = new LinearGradientBrush(Colors.Black, Colors.Blue, 0)
        };
        Canvas.SetTop(blueGradient, bottomY + stripHeight * 2);
        canvas.Children.Add(blueGradient);

        return canvas;
    }

    /// <summary>
    /// Create a checkerboard pattern
    /// </summary>
    private static FrameworkElement CreateCheckerboard(int width, int height)
    {
        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.Black
        };

        int squareSize = 64;

        for (int y = 0; y < height; y += squareSize)
        {
            for (int x = 0; x < width; x += squareSize)
            {
                if (((x / squareSize) + (y / squareSize)) % 2 == 0)
                {
                    var rect = new Rectangle
                    {
                        Width = squareSize,
                        Height = squareSize,
                        Fill = Brushes.White
                    };
                    Canvas.SetLeft(rect, x);
                    Canvas.SetTop(rect, y);
                    canvas.Children.Add(rect);
                }
            }
        }

        return canvas;
    }

    /// <summary>
    /// Create a crosshatch pattern
    /// </summary>
    private static FrameworkElement CreateCrossHatch(int width, int height)
    {
        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.Black
        };

        int spacing = 32;
        var lineBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));

        // Diagonal lines (both directions)
        for (int i = -height; i < width + height; i += spacing)
        {
            canvas.Children.Add(new Line
            {
                X1 = i,
                Y1 = 0,
                X2 = i + height,
                Y2 = height,
                Stroke = lineBrush,
                StrokeThickness = 1
            });

            canvas.Children.Add(new Line
            {
                X1 = i,
                Y1 = height,
                X2 = i + height,
                Y2 = 0,
                Stroke = lineBrush,
                StrokeThickness = 1
            });
        }

        return canvas;
    }

    /// <summary>
    /// Create an output identifier pattern
    /// </summary>
    private static FrameworkElement CreateOutputIdentifier(int width, int height, string identifier)
    {
        var grid = new Grid
        {
            Width = width,
            Height = height,
            Background = new SolidColorBrush(Color.FromRgb(10, 15, 25))
        };

        // Border
        var border = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(53, 212, 255)),
            BorderThickness = new Thickness(8)
        };
        grid.Children.Add(border);

        // Content stack
        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // Main identifier text
        stack.Children.Add(new TextBlock
        {
            Text = identifier,
            FontSize = Math.Min(width, height) / 6,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = new FontFamily("Consolas")
        });

        // Resolution text
        stack.Children.Add(new TextBlock
        {
            Text = $"{width} x {height}",
            FontSize = Math.Min(width, height) / 12,
            Foreground = new SolidColorBrush(Color.FromRgb(169, 191, 216)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            FontFamily = new FontFamily("Consolas")
        });

        grid.Children.Add(stack);

        // Corner labels
        var corners = new[]
        {
            ("TL", HorizontalAlignment.Left, VerticalAlignment.Top),
            ("TR", HorizontalAlignment.Right, VerticalAlignment.Top),
            ("BL", HorizontalAlignment.Left, VerticalAlignment.Bottom),
            ("BR", HorizontalAlignment.Right, VerticalAlignment.Bottom)
        };

        foreach (var (label, hAlign, vAlign) in corners)
        {
            var cornerText = new TextBlock
            {
                Text = label,
                FontSize = 24,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Margin = new Thickness(20),
                FontFamily = new FontFamily("Consolas")
            };
            grid.Children.Add(cornerText);
        }

        return grid;
    }

    /// <summary>
    /// Create a sync flash pattern (for testing synchronization)
    /// </summary>
    private static FrameworkElement CreateSyncFlash(int width, int height)
    {
        var border = new Border
        {
            Width = width,
            Height = height,
            Background = Brushes.White
        };

        return border;
    }

    /// <summary>
    /// Create a resolution test pattern with pixel-level detail
    /// </summary>
    private static FrameworkElement CreateResolutionPattern(int width, int height)
    {
        var canvas = new Canvas
        {
            Width = width,
            Height = height,
            Background = Brushes.Black
        };

        // Single pixel lines at different spacings
        int[] spacings = { 1, 2, 4, 8 };
        int sectionWidth = width / spacings.Length;

        for (int s = 0; s < spacings.Length; s++)
        {
            int spacing = spacings[s];
            int startX = s * sectionWidth;

            for (int x = startX; x < startX + sectionWidth; x += spacing * 2)
            {
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = height,
                    Stroke = Brushes.White,
                    StrokeThickness = spacing
                };
                canvas.Children.Add(line);
            }
        }

        // Label
        var label = new TextBlock
        {
            Text = $"{width}x{height}",
            FontSize = 20,
            Foreground = Brushes.Yellow,
            FontFamily = new FontFamily("Consolas")
        };
        Canvas.SetLeft(label, 20);
        Canvas.SetTop(label, 20);
        canvas.Children.Add(label);

        return canvas;
    }

    /// <summary>
    /// Create a sync flash controller that alternates between black and white
    /// </summary>
    public static SyncFlashController CreateSyncFlashController(FrameworkElement targetElement, int intervalMs = 500)
    {
        return new SyncFlashController(targetElement, intervalMs);
    }
}

/// <summary>
/// Controller for sync flash animation
/// </summary>
public class SyncFlashController : IDisposable
{
    private readonly FrameworkElement _target;
    private readonly DispatcherTimer _timer;
    private bool _isWhite;
    private bool _disposed;

    public bool IsRunning => _timer.IsEnabled;
    public int IntervalMs { get; set; }
    public int FlashCount { get; private set; }

    public SyncFlashController(FrameworkElement target, int intervalMs)
    {
        _target = target;
        IntervalMs = intervalMs;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(intervalMs)
        };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        FlashCount = 0;
        _isWhite = true;
        UpdateColor();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
        if (_target is Border border)
        {
            border.Background = Brushes.Black;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _isWhite = !_isWhite;
        UpdateColor();
        if (_isWhite) FlashCount++;
    }

    private void UpdateColor()
    {
        if (_target is Border border)
        {
            border.Background = _isWhite ? Brushes.White : Brushes.Black;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
