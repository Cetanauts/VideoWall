using System.Windows;
using System.Windows.Controls;
using VideoWall.Network.Grpc;

namespace VideoWall.Master;

public partial class OverlayDialog : Window
{
    public bool IsRemove { get; private set; }

    /// <summary>
    /// When editing an existing overlay, this is set to the overlay's ID so we can update it in-place.
    /// When null, a new overlay ID will be generated.
    /// </summary>
    public string? EditingOverlayId { get; private set; }

    // Result properties
    public OverlayType SelectedOverlayType { get; private set; }
    public OverlayPosition SelectedPosition { get; private set; }
    public new string FontFamily => "Segoe UI";
    public double OverlayFontSize { get; private set; } = 32;
    public string ForegroundColor { get; private set; } = "#FFFFFFFF";
    public string BackgroundColor { get; private set; } = "#80000000";
    public int OverlayMargin { get; private set; } = 20;

    // Clock
    public bool Use24Hour { get; private set; } = true;
    public bool ShowSeconds { get; private set; } = true;
    public string? ClockTimezone { get; private set; }
    public string? ClockFormat { get; private set; }

    // DateTime
    public string DateTimeFormat { get; private set; } = "dddd, MMMM d, yyyy h:mm tt";
    public string? DateTimeTimezone { get; private set; }

    // Static Text
    public string StaticText { get; private set; } = "";

    // News Ticker
    public List<string> FeedUrls { get; private set; } = new();
    public int ScrollSpeed { get; private set; } = 100;
    public int RefreshIntervalMin { get; private set; } = 15;

    // Preset feed mapping for reverse-lookup
    private static readonly (CheckBox? Checkbox, string Url)[] _presetFeedsTemplate =
    {
        (null, "https://feeds.reuters.com/reuters/topNews"),
        (null, "https://rsshub.app/apnews/topics/apf-topnews"),
        (null, "https://feeds.bbci.co.uk/news/world/rss.xml"),
        (null, "https://feeds.npr.org/1001/rss.xml"),
        (null, "https://www.pbs.org/newshour/feeds/rss/headlines"),
    };

    private (CheckBox Checkbox, string Url)[] PresetFeeds => new[]
    {
        (chkReuters, "https://feeds.reuters.com/reuters/topNews"),
        (chkAPNews,  "https://rsshub.app/apnews/topics/apf-topnews"),
        (chkBBC,     "https://feeds.bbci.co.uk/news/world/rss.xml"),
        (chkNPR,     "https://feeds.npr.org/1001/rss.xml"),
        (chkPBS,     "https://www.pbs.org/newshour/feeds/rss/headlines"),
    };

    /// <summary>
    /// Show Remove button when editing an existing overlay
    /// </summary>
    public bool ShowRemoveButton
    {
        set { btnRemove.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
    }

    private bool _loadedFromOverlay;

    /// <summary>
    /// Pre-populate the dialog from an existing overlay's full config (for editing)
    /// </summary>
    public void LoadFromOverlayInfo(OverlayInfo info)
    {
        _loadedFromOverlay = true;
        EditingOverlayId = info.OverlayId;

        // Type and position by numeric index
        cmbOverlayType.SelectedIndex = info.TypeIndex;
        cmbPosition.SelectedIndex = info.PositionIndex;

        // Common
        txtFontSize.Text = info.FontSize.ToString("0");
        txtMargin.Text = info.Margin.ToString();
        txtForeground.Text = string.IsNullOrEmpty(info.ForegroundColor) ? "#FFFFFFFF" : info.ForegroundColor;
        txtBackground.Text = string.IsNullOrEmpty(info.BackgroundColor) ? "#80000000" : info.BackgroundColor;

        // Type-specific
        switch (info.TypeIndex)
        {
            case 0: // Clock
                chk24Hour.IsChecked = info.Use24Hour;
                chkShowSeconds.IsChecked = info.ShowSeconds;
                txtClockTimezone.Text = info.TimezoneId ?? "";
                txtClockFormat.Text = info.Format ?? "";
                break;
            case 1: // DateTime
                txtDateTimeFormat.Text = string.IsNullOrEmpty(info.Format) ? "dddd, MMMM d, yyyy h:mm tt" : info.Format;
                txtDateTimeTimezone.Text = info.TimezoneId ?? "";
                // Try to match a preset
                SelectDateTimePreset(txtDateTimeFormat.Text);
                break;
            case 2: // StaticText
                txtStaticText.Text = info.Text ?? "";
                break;
            case 3: // NewsTicker
                var savedUrls = new HashSet<string>(info.FeedUrls, StringComparer.OrdinalIgnoreCase);
                var customUrls = new List<string>(info.FeedUrls);
                foreach (var (checkbox, url) in PresetFeeds)
                {
                    checkbox.IsChecked = savedUrls.Contains(url);
                    customUrls.Remove(url);
                }
                txtFeedUrls.Text = string.Join("\n", customUrls);
                txtScrollSpeed.Text = info.ScrollSpeed > 0 ? info.ScrollSpeed.ToString() : "100";
                txtRefreshInterval.Text = info.RefreshIntervalMin > 0 ? info.RefreshIntervalMin.ToString() : "15";
                break;
        }

        ShowRemoveButton = true;
        Title = "Edit Overlay";
    }

    private void SelectDateTimePreset(string format)
    {
        for (int i = 1; i < cmbDateTimePreset.Items.Count; i++)
        {
            if (cmbDateTimePreset.Items[i] is ComboBoxItem item && item.Tag is string tag && tag == format)
            {
                cmbDateTimePreset.SelectedIndex = i;
                return;
            }
        }
        cmbDateTimePreset.SelectedIndex = 0; // Custom
    }

    /// <summary>
    /// Persists last-used overlay settings across dialog instances (session lifetime)
    /// </summary>
    private static class LastSettings
    {
        public static bool HasBeenSet;

        // Common
        public static int OverlayTypeIndex;
        public static int PositionIndex = 5; // Bottom Right
        public static string FontSize = "32";
        public static string Margin = "20";
        public static string Foreground = "#FFFFFFFF";
        public static string Background = "#80000000";

        // Clock
        public static bool Use24Hour = true;
        public static bool ShowSeconds = true;
        public static string ClockTimezone = "";
        public static string ClockFormat = "";

        // DateTime
        public static string DateTimeFormat = "dddd, MMMM d, yyyy h:mm tt";
        public static string DateTimeTimezone = "";

        // Static Text
        public static string StaticText = "";

        // News Ticker
        public static bool PresetReuters = true;
        public static bool PresetAPNews;
        public static bool PresetBBC = true;
        public static bool PresetNPR;
        public static bool PresetPBS;
        public static string FeedUrls = "";
        public static string ScrollSpeed = "100";
        public static string RefreshInterval = "15";
    }

    public OverlayDialog()
    {
        InitializeComponent();
        Loaded += OverlayDialog_Loaded;
    }

    private void OverlayDialog_Loaded(object sender, RoutedEventArgs e)
    {
        // Skip LastSettings restore if we loaded from an existing overlay
        if (_loadedFromOverlay) return;
        if (!LastSettings.HasBeenSet) return;

        cmbOverlayType.SelectedIndex = LastSettings.OverlayTypeIndex;
        cmbPosition.SelectedIndex = LastSettings.PositionIndex;
        txtFontSize.Text = LastSettings.FontSize;
        txtMargin.Text = LastSettings.Margin;
        txtForeground.Text = LastSettings.Foreground;
        txtBackground.Text = LastSettings.Background;

        chk24Hour.IsChecked = LastSettings.Use24Hour;
        chkShowSeconds.IsChecked = LastSettings.ShowSeconds;
        txtClockTimezone.Text = LastSettings.ClockTimezone;
        txtClockFormat.Text = LastSettings.ClockFormat;

        txtDateTimeFormat.Text = LastSettings.DateTimeFormat;
        txtDateTimeTimezone.Text = LastSettings.DateTimeTimezone;

        txtStaticText.Text = LastSettings.StaticText;

        chkReuters.IsChecked = LastSettings.PresetReuters;
        chkAPNews.IsChecked = LastSettings.PresetAPNews;
        chkBBC.IsChecked = LastSettings.PresetBBC;
        chkNPR.IsChecked = LastSettings.PresetNPR;
        chkPBS.IsChecked = LastSettings.PresetPBS;
        txtFeedUrls.Text = LastSettings.FeedUrls;
        txtScrollSpeed.Text = LastSettings.ScrollSpeed;
        txtRefreshInterval.Text = LastSettings.RefreshInterval;
    }

    private void DateTimePreset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (txtDateTimeFormat == null) return; // Not yet initialized
        if (cmbDateTimePreset.SelectedIndex <= 0) return; // Custom — don't overwrite

        if (cmbDateTimePreset.SelectedItem is ComboBoxItem item && item.Tag is string format)
        {
            txtDateTimeFormat.Text = format;
        }
    }

    private void OverlayType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (panelClock == null) return; // Not yet initialized

        panelClock.Visibility = Visibility.Collapsed;
        panelDateTime.Visibility = Visibility.Collapsed;
        panelStaticText.Visibility = Visibility.Collapsed;
        panelNewsTicker.Visibility = Visibility.Collapsed;

        switch (cmbOverlayType.SelectedIndex)
        {
            case 0: panelClock.Visibility = Visibility.Visible; break;
            case 1: panelDateTime.Visibility = Visibility.Visible; break;
            case 2: panelStaticText.Visibility = Visibility.Visible; break;
            case 3: panelNewsTicker.Visibility = Visibility.Visible; break;
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        IsRemove = false;

        // Parse common settings
        if (!double.TryParse(txtFontSize.Text, out var fontSize) || fontSize <= 0) fontSize = 32;
        OverlayFontSize = fontSize;

        if (!int.TryParse(txtMargin.Text, out var margin) || margin < 0) margin = 20;
        OverlayMargin = margin;

        ForegroundColor = string.IsNullOrWhiteSpace(txtForeground.Text) ? "#FFFFFFFF" : txtForeground.Text.Trim();
        BackgroundColor = string.IsNullOrWhiteSpace(txtBackground.Text) ? "#80000000" : txtBackground.Text.Trim();

        // Overlay type
        SelectedOverlayType = cmbOverlayType.SelectedIndex switch
        {
            0 => OverlayType.OverlayClock,
            1 => OverlayType.OverlayDatetime,
            2 => OverlayType.OverlayStaticText,
            3 => OverlayType.OverlayNewsTicker,
            _ => OverlayType.OverlayClock
        };

        // Position
        SelectedPosition = cmbPosition.SelectedIndex switch
        {
            0 => OverlayPosition.OverlayTopLeft,
            1 => OverlayPosition.OverlayTopCenter,
            2 => OverlayPosition.OverlayTopRight,
            3 => OverlayPosition.OverlayBottomLeft,
            4 => OverlayPosition.OverlayBottomCenter,
            5 => OverlayPosition.OverlayBottomRight,
            _ => OverlayPosition.OverlayBottomRight
        };

        // Type-specific
        switch (SelectedOverlayType)
        {
            case OverlayType.OverlayClock:
                Use24Hour = chk24Hour.IsChecked == true;
                ShowSeconds = chkShowSeconds.IsChecked == true;
                ClockTimezone = string.IsNullOrWhiteSpace(txtClockTimezone.Text) ? null : txtClockTimezone.Text.Trim();
                ClockFormat = string.IsNullOrWhiteSpace(txtClockFormat.Text) ? null : txtClockFormat.Text.Trim();
                break;

            case OverlayType.OverlayDatetime:
                DateTimeFormat = string.IsNullOrWhiteSpace(txtDateTimeFormat.Text)
                    ? "dddd, MMMM d, yyyy h:mm tt"
                    : txtDateTimeFormat.Text.Trim();
                DateTimeTimezone = string.IsNullOrWhiteSpace(txtDateTimeTimezone.Text) ? null : txtDateTimeTimezone.Text.Trim();
                break;

            case OverlayType.OverlayStaticText:
                StaticText = txtStaticText.Text ?? "";
                break;

            case OverlayType.OverlayNewsTicker:
                // Collect checked preset URLs
                var allUrls = new List<string>();
                foreach (var (checkbox, url) in PresetFeeds)
                {
                    if (checkbox.IsChecked == true)
                        allUrls.Add(url);
                }
                // Add custom URLs
                var customLines = (txtFeedUrls.Text ?? "")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(u => !string.IsNullOrWhiteSpace(u));
                allUrls.AddRange(customLines);
                FeedUrls = allUrls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (!int.TryParse(txtScrollSpeed.Text, out var speed) || speed <= 0) speed = 100;
                ScrollSpeed = speed;
                if (!int.TryParse(txtRefreshInterval.Text, out var refresh) || refresh <= 0) refresh = 15;
                RefreshIntervalMin = refresh;
                break;
        }

        // Save settings for next dialog invocation
        LastSettings.HasBeenSet = true;
        LastSettings.OverlayTypeIndex = cmbOverlayType.SelectedIndex;
        LastSettings.PositionIndex = cmbPosition.SelectedIndex;
        LastSettings.FontSize = txtFontSize.Text;
        LastSettings.Margin = txtMargin.Text;
        LastSettings.Foreground = txtForeground.Text;
        LastSettings.Background = txtBackground.Text;
        LastSettings.Use24Hour = chk24Hour.IsChecked == true;
        LastSettings.ShowSeconds = chkShowSeconds.IsChecked == true;
        LastSettings.ClockTimezone = txtClockTimezone.Text ?? "";
        LastSettings.ClockFormat = txtClockFormat.Text ?? "";
        LastSettings.DateTimeFormat = txtDateTimeFormat.Text ?? "dddd, MMMM d, yyyy h:mm tt";
        LastSettings.DateTimeTimezone = txtDateTimeTimezone.Text ?? "";
        LastSettings.StaticText = txtStaticText.Text ?? "";
        LastSettings.PresetReuters = chkReuters.IsChecked == true;
        LastSettings.PresetAPNews = chkAPNews.IsChecked == true;
        LastSettings.PresetBBC = chkBBC.IsChecked == true;
        LastSettings.PresetNPR = chkNPR.IsChecked == true;
        LastSettings.PresetPBS = chkPBS.IsChecked == true;
        LastSettings.FeedUrls = txtFeedUrls.Text ?? "";
        LastSettings.ScrollSpeed = txtScrollSpeed.Text ?? "100";
        LastSettings.RefreshInterval = txtRefreshInterval.Text ?? "15";

        DialogResult = true;
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        IsRemove = true;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
