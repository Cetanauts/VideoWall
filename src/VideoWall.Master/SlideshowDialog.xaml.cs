using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VideoWall.Network.Grpc;

namespace VideoWall.Master;

public partial class SlideshowDialog : Window
{
    private readonly ObservableCollection<ImageItem> _images = new();

    public List<string> ImagePaths { get; private set; } = new();
    public int DurationMs { get; private set; } = 5000;
    public TransitionType Transition { get; private set; } = TransitionType.TransitionFade;
    public int TransitionDurationMs { get; private set; } = 1000;
    public bool Loop { get; private set; } = true;

    public SlideshowDialog()
    {
        InitializeComponent();
        lstImages.ItemsSource = _images;
        UpdateImageCount();
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Images for Slideshow",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                AddImageIfNotExists(file);
            }
            UpdateImageCount();
        }
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        // Use OpenFolderDialog which is available in .NET 8 WPF
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder containing images"
        };

        if (dialog.ShowDialog() == true)
        {
            var folderPath = dialog.FolderName;
            var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
            var files = Directory.GetFiles(folderPath)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();

            foreach (var file in files)
            {
                AddImageIfNotExists(file);
            }

            UpdateImageCount();

            if (files.Count == 0)
            {
                MessageBox.Show($"No images found in the selected folder.\n\nSupported formats: JPG, PNG, BMP, GIF, WEBP",
                    "No Images Found", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void AddImageIfNotExists(string filePath)
    {
        if (_images.Any(i => i.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
            return;

        _images.Add(new ImageItem
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            ThumbnailPath = filePath
        });
    }

    private void ClearImages_Click(object sender, RoutedEventArgs e)
    {
        _images.Clear();
        UpdateImageCount();
    }

    private void RemoveImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string filePath)
        {
            var item = _images.FirstOrDefault(i => i.FilePath == filePath);
            if (item != null)
            {
                _images.Remove(item);
                UpdateImageCount();
            }
        }
    }

    private void UpdateImageCount()
    {
        txtImageCount.Text = $"{_images.Count} image{(_images.Count != 1 ? "s" : "")}";
        btnStart.IsEnabled = _images.Count > 0;
        txtNoImages.Visibility = _images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_images.Count == 0)
        {
            MessageBox.Show("Please add at least one image.", "No Images",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Gather settings
        ImagePaths = _images.Select(i => i.FilePath).ToList();

        if (cmbDuration.SelectedItem is ComboBoxItem durationItem)
        {
            DurationMs = int.Parse(durationItem.Tag?.ToString() ?? "5000");
        }

        if (cmbTransition.SelectedItem is ComboBoxItem transitionItem)
        {
            var transitionValue = int.Parse(transitionItem.Tag?.ToString() ?? "1");
            Transition = (TransitionType)transitionValue;
        }

        if (cmbTransitionDuration.SelectedItem is ComboBoxItem transitionDurationItem)
        {
            TransitionDurationMs = int.Parse(transitionDurationItem.Tag?.ToString() ?? "1000");
        }

        Loop = chkLoop.IsChecked ?? true;

        DialogResult = true;
        Close();
    }
}

public class ImageItem
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
}
