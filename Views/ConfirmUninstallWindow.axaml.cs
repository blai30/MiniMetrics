using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MiniMetrics.Views;

// Confirms the destructive in-app uninstall before anything is removed. "Uninstall" raises Confirmed and
// closes; "Cancel" just closes. Mirrors the other prompt windows (shown with Show(), event-driven).
public partial class ConfirmUninstallWindow : Window
{
    // Raised when the user confirms the uninstall.
    public event EventHandler? Confirmed;

    public ConfirmUninstallWindow()
    {
        InitializeComponent();
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        Confirmed?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
