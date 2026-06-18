using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MiniMetrics.Views;

public partial class SettingsWindow : Window
{
    private const string FormatDocsUrl =
        "https://learn.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings";

    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnOpenFormatDocs(object? sender, RoutedEventArgs e)
    {
        // Opening the browser is best effort; a broken shell association must not crash settings.
        try
        {
            Process.Start(new ProcessStartInfo { FileName = FormatDocsUrl, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
        }
    }
}
