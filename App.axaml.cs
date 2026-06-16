using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using MiniMetrics.Models;
using MiniMetrics.Services;
using MiniMetrics.ViewModels;
using MiniMetrics.Views;

namespace MiniMetrics;

public partial class App : Application
{
    private MetricsPoller? _poller;
    private ISensorSource? _source;
    private SettingsStore _settingsStore = null!;
    private Settings _settings = null!;
    private MainWindowViewModel _viewModel = null!;
    private MainWindow _window = null!;
    private DesktopWindow _desktop = null!;
    private DispatcherTimer? _saveTimer;

    private TrayIcon? _trayIcon;
    private NativeMenuItem _showHideItem = null!;
    private NativeMenuItem _lockItem = null!;
    private SettingsWindow? _settingsWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _settingsStore = new SettingsStore(SettingsStore.DefaultPath);
            _settings = _settingsStore.Load();

            _viewModel = new MainWindowViewModel();
            _viewModel.LoadVisibility(_settings.Visibility);
            _viewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);

            _source = OperatingSystem.IsWindows()
                ? new LibreHardwareSensorSource()
                : new MockSensorSource();

            _poller = new MetricsPoller(_source, TimeSpan.FromSeconds(1));
            _poller.SnapshotReady += snapshot =>
                Dispatcher.UIThread.Post(() => _viewModel.ApplySnapshot(snapshot));

            _window = new MainWindow
            {
                DataContext = _viewModel,
                WindowStartupLocation = WindowStartupLocation.Manual,
                IsLocked = _settings.Locked,
            };

            _desktop = new DesktopWindow(_window);
            _desktop.Attach();
            _desktop.SetAlwaysOnTop(_settings.AlwaysOnTop);

            // Restore the saved position before the window appears, if one exists.
            if (_settings.X is int x && _settings.Y is int y)
            {
                _window.Position = new PixelPoint(x, y);
            }

            // Throttle position saves so a drag results in one write, not hundreds.
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _saveTimer.Tick += (_, _) =>
            {
                _saveTimer!.Stop();
                Save();
            };
            _window.PositionChanged += (_, _) =>
            {
                _settings.X = _window.Position.X;
                _settings.Y = _window.Position.Y;
                _saveTimer!.Stop();
                _saveTimer.Start();
            };

            _window.Opened += (_, _) =>
            {
                EnsureOnScreen();
                _desktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
                _desktop.SetClickThrough(_settings.Locked);
            };

            desktop.MainWindow = _window;
            if (!_settings.Hidden)
            {
                _window.Show();
            }

            _poller.Start();

            BuildTray();

            desktop.ShutdownRequested += (_, _) =>
            {
                Save();
                _poller?.Dispose();
                (_source as IDisposable)?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void BuildTray()
    {
        var menu = new NativeMenu();

        _showHideItem = new NativeMenuItem(_settings.Hidden ? "Show widget" : "Hide widget");
        _showHideItem.Click += OnToggleShowHide;
        menu.Add(_showHideItem);

        _lockItem = new NativeMenuItem("Lock position")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _settings.Locked,
        };
        _lockItem.Click += OnToggleLock;
        menu.Add(_lockItem);

        menu.Add(new NativeMenuItemSeparator());

        var settingsItem = new NativeMenuItem("Settings...");
        settingsItem.Click += OnOpenSettings;
        menu.Add(settingsItem);

        menu.Add(new NativeMenuItemSeparator());

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        };
        menu.Add(quit);

        _trayIcon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://MiniMetrics/Assets/avalonia-logo.ico"))),
            ToolTipText = "Mini Metrics",
            Menu = menu,
        };

        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
    }

    private void OnToggleShowHide(object? sender, EventArgs e)
    {
        _settings.Hidden = !_settings.Hidden;
        if (_settings.Hidden)
        {
            _window.Hide();
        }
        else
        {
            _window.Show();
            _desktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
        }

        _showHideItem.Header = _settings.Hidden ? "Show widget" : "Hide widget";
        Save();
    }

    private void OnToggleLock(object? sender, EventArgs e)
    {
        _settings.Locked = !_settings.Locked;
        _window.IsLocked = _settings.Locked;
        _desktop.SetClickThrough(_settings.Locked);
        _lockItem.IsChecked = _settings.Locked;
        Save();
    }

    private void OnOpenSettings(object? sender, EventArgs e)
    {
        // Single instance: focus the existing window instead of opening a second one.
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var viewModel = new SettingsViewModel(_settings);
        viewModel.AppearanceChanged += () => OnAppearanceChanged(viewModel);
        viewModel.MetricVisibilityChanged += OnMetricVisibilityChanged;
        viewModel.AlwaysOnTopChanged += OnAlwaysOnTopChanged;

        _settingsWindow = new SettingsWindow { DataContext = viewModel };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void OnAppearanceChanged(SettingsViewModel viewModel)
    {
        _settings.BackgroundColor = viewModel.BackgroundColor;
        _settings.Opacity = viewModel.Opacity;
        _viewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);

        // Reuse the existing debounce so dragging the opacity slider writes once.
        _saveTimer!.Stop();
        _saveTimer.Start();
    }

    private void OnMetricVisibilityChanged(string key, bool visible)
    {
        _settings.Visibility[key] = visible;
        _viewModel.SetVisibility(key, visible);
        Save();
    }

    private void OnAlwaysOnTopChanged(bool enabled)
    {
        _settings.AlwaysOnTop = enabled;
        _desktop.SetAlwaysOnTop(enabled);
        Save();
    }

    // If the restored position lands off every monitor (display unplugged or resolution changed),
    // pull the widget back onto the primary screen so it cannot get lost.
    private void EnsureOnScreen()
    {
        var screens = _window.Screens;
        if (screens is null || screens.All.Count == 0)
        {
            return;
        }

        if (screens.ScreenFromPoint(_window.Position) is null)
        {
            var primary = screens.Primary ?? screens.All[0];
            var area = primary.WorkingArea;
            _window.Position = new PixelPoint(area.X + 48, area.Y + 48);
            _settings.X = _window.Position.X;
            _settings.Y = _window.Position.Y;
            Save();
        }
    }

    private void Save() => _settingsStore.Save(_settings);
}
