using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Views;

// Notifies the user about an update. "View release" opens the release page; "Skip this version" raises
// SkipRequested so the host can persist the skip; the rest just close. In informational mode (up to
// date or check failed) only "Close" is shown.
public partial class UpdatePromptWindow : Window
{
    // Raised when the user chooses to skip the offered version.
    public event EventHandler? SkipRequested;

    public UpdatePromptWindow()
    {
        InitializeComponent();
    }

    public UpdatePromptWindow(UpdatePromptViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnViewRelease(object? sender, RoutedEventArgs e)
    {
        if (DataContext is UpdatePromptViewModel { Url: { } url })
        {
            // Opening the browser is best effort; a broken shell association must not crash the prompt.
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
            {
            }
        }

        Close();
    }

    private void OnSkip(object? sender, RoutedEventArgs e)
    {
        SkipRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private void OnDismiss(object? sender, RoutedEventArgs e) => Close();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
