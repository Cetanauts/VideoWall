using System.Windows;

namespace VideoWall.Master;

public partial class AddAgentDialog : Window
{
    public string IpAddress { get; private set; } = string.Empty;
    public int Port { get; private set; }

    public AddAgentDialog()
    {
        InitializeComponent();
        Owner = Application.Current.MainWindow;
    }

    private void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtIpAddress.Text))
        {
            MessageBox.Show("Please enter an IP address", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(txtPort.Text, out int port) || port < 1 || port > 65535)
        {
            MessageBox.Show("Please enter a valid port number (1-65535)", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IpAddress = txtIpAddress.Text.Trim();
        Port = port;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
