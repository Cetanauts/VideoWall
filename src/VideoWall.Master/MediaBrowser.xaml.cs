using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace VideoWall.Master;

public partial class MediaBrowser : Window
{
    private readonly ObservableCollection<QuickAccessItem> _quickAccess = new();
    private readonly ObservableCollection<FileItem> _files = new();
    private string _currentPath = string.Empty;
    private FileFilterType _currentFilter = FileFilterType.All;

    private static readonly string[] VideoExtensions = { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".flv" };
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tiff", ".tif" };

    /// <summary>
    /// Selected file path (null if cancelled)
    /// </summary>
    public string? SelectedPath { get; private set; }

    /// <summary>
    /// Allow multiple selection
    /// </summary>
    public bool MultiSelect { get; set; }

    /// <summary>
    /// Selected paths when MultiSelect is true
    /// </summary>
    public List<string> SelectedPaths { get; } = new();

    public MediaBrowser()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;

        lstQuickAccess.ItemsSource = _quickAccess;
        lstFiles.ItemsSource = _files;

        InitializeQuickAccess();

        // Start at a default location
        if (Directory.Exists(@"\\"))
        {
            NavigateTo(@"\\");
        }
        else
        {
            NavigateTo(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        }
    }

    private void InitializeQuickAccess()
    {
        _quickAccess.Clear();

        // Add common locations
        _quickAccess.Add(new QuickAccessItem("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop)));
        _quickAccess.Add(new QuickAccessItem("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));
        _quickAccess.Add(new QuickAccessItem("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)));
        _quickAccess.Add(new QuickAccessItem("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)));

        // Add drives
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                var name = string.IsNullOrEmpty(drive.VolumeLabel)
                    ? $"Drive ({drive.Name.TrimEnd('\\')})"
                    : $"{drive.VolumeLabel} ({drive.Name.TrimEnd('\\')})";
                _quickAccess.Add(new QuickAccessItem(name, drive.Name));
            }
        }

        // Add network placeholder
        _quickAccess.Add(new QuickAccessItem("Network", @"\\"));
    }

    public void AddQuickAccess(string name, string path)
    {
        _quickAccess.Add(new QuickAccessItem(name, path));
    }

    public void SetInitialPath(string path)
    {
        NavigateTo(path);
    }

    private void NavigateTo(string path)
    {
        try
        {
            _currentPath = path;
            txtPath.Text = path;
            _files.Clear();

            if (string.IsNullOrEmpty(path)) return;

            // Check if it's a UNC path root (network browser)
            if (path == @"\\")
            {
                txtItemCount.Text = "Enter a UNC path like \\\\server\\share";
                return;
            }

            if (!Directory.Exists(path))
            {
                txtItemCount.Text = "Path not found";
                return;
            }

            // Get directories
            var directories = Directory.GetDirectories(path)
                .Select(d => new DirectoryInfo(d))
                .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                .OrderBy(d => d.Name);

            foreach (var dir in directories)
            {
                _files.Add(new FileItem
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true,
                    Icon = "📁",
                    Type = "Folder"
                });
            }

            // Get files
            var files = Directory.GetFiles(path)
                .Select(f => new FileInfo(f))
                .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                .Where(f => ShouldShowFile(f))
                .OrderBy(f => f.Name);

            foreach (var file in files)
            {
                _files.Add(new FileItem
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    IsDirectory = false,
                    Icon = GetFileIcon(file.Extension),
                    Type = GetFileType(file.Extension),
                    Size = file.Length,
                    SizeText = FormatSize(file.Length)
                });
            }

            txtItemCount.Text = $"{_files.Count} item{(_files.Count != 1 ? "s" : "")}";
        }
        catch (UnauthorizedAccessException)
        {
            txtItemCount.Text = "Access denied";
        }
        catch (Exception ex)
        {
            txtItemCount.Text = $"Error: {ex.Message}";
        }
    }

    private bool ShouldShowFile(FileInfo file)
    {
        var ext = file.Extension.ToLowerInvariant();

        return _currentFilter switch
        {
            FileFilterType.All => VideoExtensions.Contains(ext) || ImageExtensions.Contains(ext),
            FileFilterType.Videos => VideoExtensions.Contains(ext),
            FileFilterType.Images => ImageExtensions.Contains(ext),
            _ => true
        };
    }

    private string GetFileIcon(string extension)
    {
        var ext = extension.ToLowerInvariant();

        if (VideoExtensions.Contains(ext)) return "🎬";
        if (ImageExtensions.Contains(ext)) return "🖼️";
        return "📄";
    }

    private string GetFileType(string extension)
    {
        var ext = extension.ToLowerInvariant();

        if (VideoExtensions.Contains(ext)) return "Video";
        if (ImageExtensions.Contains(ext)) return "Image";
        return ext.TrimStart('.');
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.#} {sizes[order]}";
    }

    private void UpdatePreview(FileItem? item)
    {
        if (item == null || item.IsDirectory)
        {
            imgPreview.Source = null;
            txtFileName.Text = "";
            txtFileInfo.Text = "";
            return;
        }

        txtFileName.Text = item.Name;
        txtFileInfo.Text = $"Type: {item.Type}\nSize: {item.SizeText}\nPath: {item.FullPath}";

        // Try to load image preview
        var ext = Path.GetExtension(item.Name).ToLowerInvariant();
        if (ImageExtensions.Contains(ext))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 200;
                bitmap.UriSource = new Uri(item.FullPath);
                bitmap.EndInit();
                imgPreview.Source = bitmap;
            }
            catch
            {
                imgPreview.Source = null;
            }
        }
        else
        {
            imgPreview.Source = null;
        }
    }

    #region Event Handlers

    private void GoUp_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentPath)) return;

        var parent = Directory.GetParent(_currentPath);
        if (parent != null)
        {
            NavigateTo(parent.FullName);
        }
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(txtPath.Text);
    }

    private void PathTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateTo(txtPath.Text);
        }
    }

    private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentFilter = cmbFilter.SelectedIndex switch
        {
            1 => FileFilterType.Videos,
            2 => FileFilterType.Images,
            _ => FileFilterType.All
        };

        NavigateTo(_currentPath);
    }

    private void QuickAccess_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstQuickAccess.SelectedItem is QuickAccessItem item)
        {
            NavigateTo(item.Path);
            lstQuickAccess.SelectedItem = null;
        }
    }

    private void File_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstFiles.SelectedItem is FileItem item)
        {
            UpdatePreview(item);

            if (!item.IsDirectory)
            {
                txtSelectedFile.Text = item.FullPath;
                btnSelect.IsEnabled = true;

                if (MultiSelect)
                {
                    SelectedPaths.Clear();
                    foreach (FileItem selected in lstFiles.SelectedItems)
                    {
                        if (!selected.IsDirectory)
                        {
                            SelectedPaths.Add(selected.FullPath);
                        }
                    }
                }
            }
            else
            {
                txtSelectedFile.Text = "";
                btnSelect.IsEnabled = false;
            }
        }
        else
        {
            UpdatePreview(null);
            txtSelectedFile.Text = "";
            btnSelect.IsEnabled = false;
        }
    }

    private void File_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (lstFiles.SelectedItem is FileItem item)
        {
            if (item.IsDirectory)
            {
                NavigateTo(item.FullPath);
            }
            else
            {
                // Select and close
                SelectedPath = item.FullPath;
                DialogResult = true;
                Close();
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        if (lstFiles.SelectedItem is FileItem item && !item.IsDirectory)
        {
            SelectedPath = item.FullPath;
            DialogResult = true;
            Close();
        }
    }

    #endregion
}

public class QuickAccessItem
{
    public string Name { get; }
    public string Path { get; }

    public QuickAccessItem(string name, string path)
    {
        Name = name;
        Path = path;
    }
}

public class FileItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string Icon { get; set; } = "";
    public string Type { get; set; } = "";
    public long Size { get; set; }
    public string SizeText { get; set; } = "";
}

public enum FileFilterType
{
    All,
    Videos,
    Images
}
