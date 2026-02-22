using System.Windows;
using System.Windows.Controls;

namespace VideoWall.Master;

public partial class ScreenSetDialog : Window
{
    public string ScreenSetName { get; private set; } = string.Empty;
    public int Columns { get; private set; }
    public int Rows { get; private set; }

    public ScreenSetDialog()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;

        txtColumns.TextChanged += UpdatePreview;
        txtRows.TextChanged += UpdatePreview;
        UpdatePreview(null, null);
    }

    private void UpdatePreview(object? sender, TextChangedEventArgs? e)
    {
        if (int.TryParse(txtColumns.Text, out int cols) && int.TryParse(txtRows.Text, out int rows))
        {
            int total = cols * rows;
            txtPreview.Text = $"Grid: {cols} x {rows} = {total} screen{(total != 1 ? "s" : "")}";
        }
        else
        {
            txtPreview.Text = "Enter valid numbers";
        }
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("Please enter a name", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(txtColumns.Text, out int cols) || cols < 1 || cols > 10)
        {
            MessageBox.Show("Columns must be between 1 and 10", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(txtRows.Text, out int rows) || rows < 1 || rows > 10)
        {
            MessageBox.Show("Rows must be between 1 and 10", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ScreenSetName = txtName.Text.Trim();
        Columns = cols;
        Rows = rows;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
