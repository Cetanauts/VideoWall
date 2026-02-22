using System.Windows;
using System.Windows.Controls;
using VideoWall.Core.Models;

namespace VideoWall.Agent.Overlays;

public class StaticTextWidget : OverlayWidget
{
    private readonly TextBlock _textBlock;

    public StaticTextWidget(OverlayConfig config) : base(config)
    {
        _textBlock = new TextBlock
        {
            Text = config.StaticText?.Text ?? "",
            FontSize = config.FontSize,
            FontFamily = GetFontFamily(),
            Foreground = GetForegroundBrush(),
            TextWrapping = (config.StaticText?.WordWrap ?? true) ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MaxWidth = 600,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        Container.Child = _textBlock;
    }

    protected override void OnStart() { }
    protected override void OnStop() { }

    protected override void OnUpdate(OverlayConfig config)
    {
        _textBlock.Text = config.StaticText?.Text ?? "";
        _textBlock.FontSize = config.FontSize;
        _textBlock.FontFamily = GetFontFamily();
        _textBlock.Foreground = GetForegroundBrush();
        _textBlock.TextWrapping = (config.StaticText?.WordWrap ?? true) ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }
}
