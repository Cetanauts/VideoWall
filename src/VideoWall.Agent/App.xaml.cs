using System.Runtime.InteropServices;
using System.Windows;

namespace VideoWall.Agent;

public partial class App : Application
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleTitle(string title);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Allocate a console window for diagnostic output
        AllocConsole();
        SetConsoleTitle("VideoWall Agent - Diagnostic Console");
        Console.WriteLine("=== VideoWall Agent Diagnostic Console ===");
        Console.WriteLine($"Started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Machine: {Environment.MachineName}");
        Console.WriteLine("Console.WriteLine output will appear here.");
        Console.WriteLine();

        // Handle unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Console.WriteLine($"[FATAL] Unhandled exception: {ex}");
            MessageBox.Show($"Unhandled exception: {ex?.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Console.WriteLine($"[ERROR] Dispatcher exception: {args.Exception}");
            MessageBox.Show($"Dispatcher exception: {args.Exception.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}
