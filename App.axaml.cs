using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DesktopMetrics.Services;
using DesktopMetrics.ViewModels;
using DesktopMetrics.Views;

namespace DesktopMetrics;

public partial class App : Application
{
    private MetricsPoller? _poller;
    private ISensorSource? _source;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The widget lives in the tray. Hiding or closing its window must not quit the app.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            DisableAvaloniaDataAnnotationValidation();

            var viewModel = new MainWindowViewModel();

            _source = OperatingSystem.IsWindows()
                ? new LibreHardwareSensorSource()
                : new MockSensorSource();

            _poller = new MetricsPoller(_source, TimeSpan.FromSeconds(1));
            _poller.SnapshotReady += snapshot =>
                Dispatcher.UIThread.Post(() => viewModel.ApplySnapshot(snapshot));

            var window = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = window;
            window.Show();

            _poller.Start();

            desktop.ShutdownRequested += (_, _) =>
            {
                _poller?.Dispose();
                (_source as IDisposable)?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnQuitClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
