using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MiniMetrics.Views;

// A one-time prompt shown when a driver-backed metric (CPU temperature or power) is enabled but the
// PawnIO driver those readings need is not installed. "Install PawnIO" opens the official download
// page; the metric stays enabled so it starts working once the driver is present.
public partial class PawnIoPromptWindow : Window
{
    // The official PawnIO site, which hosts the signed installer.
    private const string PawnIoUrl = "https://pawnio.eu/";

    public PawnIoPromptWindow()
    {
        InitializeComponent();
    }

    private void OnInstall(object? sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo { FileName = PawnIoUrl, UseShellExecute = true });
        Close();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
