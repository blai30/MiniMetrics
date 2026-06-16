using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using MiniMetrics.Lib;
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
    private DateTimeWidgetViewModel _dateTimeViewModel = null!;
    private DateTimeWindow _dateTimeWindow = null!;
    private DesktopWindow _dateTimeDesktop = null!;
    private DispatcherTimer? _clockTimer;
    private DispatcherTimer? _saveTimer;
    private DispatcherTimer? _trimTimer;

    private TrayIcon? _trayIcon;
    private NativeMenuItem _showHideItem = null!;
    private NativeMenuItem _clockShowHideItem = null!;
    private NativeMenuItem _lockItem = null!;
    private NativeMenuItem _alwaysOnTopItem = null!;
    private NativeMenuItem _snapItem = null!;
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
            MigrateVisibility();

            _viewModel = new MainWindowViewModel();
            _viewModel.LoadVisibility(_settings.Visibility);
            _viewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);

            _dateTimeViewModel = new DateTimeWidgetViewModel();
            _dateTimeViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);
            _dateTimeViewModel.SetTimeZone(ResolveTimeZone(_settings.TimeZoneId));

            _source = OperatingSystem.IsWindows()
                ? new LibreHardwareSensorSource()
                : new MockSensorSource();

            // Release any device whose every metric is hidden before the first poll runs.
            ApplyActiveDevices();

            _poller = new MetricsPoller(_source, TimeSpan.FromSeconds(1));
            _poller.SnapshotReady += snapshot =>
                Dispatcher.UIThread.Post(() => _viewModel.ApplySnapshot(snapshot));

            _window = new MainWindow
            {
                DataContext = _viewModel,
                WindowStartupLocation = WindowStartupLocation.Manual,
                IsLocked = _settings.Locked,
                SnapEnabled = _settings.SnapToEdges,
            };

            _desktop = new DesktopWindow(_window);
            _desktop.Attach();
            _desktop.SetAlwaysOnTop(_settings.AlwaysOnTop);

            _dateTimeWindow = new DateTimeWindow
            {
                DataContext = _dateTimeViewModel,
                WindowStartupLocation = WindowStartupLocation.Manual,
                IsLocked = _settings.Locked,
                SnapEnabled = _settings.SnapToEdges,
            };

            _dateTimeDesktop = new DesktopWindow(_dateTimeWindow);
            _dateTimeDesktop.Attach();
            _dateTimeDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);

            if (_settings.DateTimeX is int dtx && _settings.DateTimeY is int dty)
            {
                _dateTimeWindow.Position = new PixelPoint(dtx, dty);
            }

            _dateTimeWindow.PositionChanged += (_, _) =>
            {
                _settings.DateTimeX = _dateTimeWindow.Position.X;
                _settings.DateTimeY = _dateTimeWindow.Position.Y;
                _saveTimer!.Stop();
                _saveTimer.Start();
            };

            _dateTimeWindow.Opened += (_, _) =>
            {
                EnsureWindowOnScreen(_dateTimeWindow,
                    () => _settings.DateTimeX = _dateTimeWindow.Position.X,
                    () => _settings.DateTimeY = _dateTimeWindow.Position.Y);
                _dateTimeDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
                _dateTimeDesktop.SetClickThrough(_settings.Locked);
            };

            // Each widget snaps against the other only while the other is actually shown.
            _window.PeerRects = () => _dateTimeWindow.IsVisible
                ? new[] { RectOf(_dateTimeWindow) }
                : System.Array.Empty<EdgeSnap.Rect>();
            _dateTimeWindow.PeerRects = () => _window.IsVisible
                ? new[] { RectOf(_window) }
                : System.Array.Empty<EdgeSnap.Rect>();

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

            // The CLR, JIT and Avalonia commit far more than the idle widget keeps touching, so the
            // resident set balloons at startup. Trim it back to the working pages once warmup has
            // settled (first tick), then top up periodically. The 60s steady interval keeps a trim
            // from landing mid-drag, where evicted pages would refault and stutter the move.
            _trimTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _trimTimer.Tick += (_, _) =>
            {
                MemoryTrimmer.Trim();
                _trimTimer!.Interval = TimeSpan.FromSeconds(60);
            };
            _trimTimer.Start();

            desktop.MainWindow = _window;
            if (!_settings.Hidden)
            {
                _window.Show();
            }

            if (!_settings.DateTimeHidden)
            {
                _dateTimeWindow.Show();
            }

            _poller.Start();

            // Drive the clock once per second. Tick immediately so the widget shows the time at once.
            _dateTimeViewModel.Tick(DateTimeOffset.Now);
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) => _dateTimeViewModel.Tick(DateTimeOffset.Now);
            _clockTimer.Start();

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

        _showHideItem = new NativeMenuItem(_settings.Hidden ? "Show metrics widget" : "Hide metrics widget");
        _showHideItem.Click += OnToggleShowHide;
        menu.Add(_showHideItem);

        _clockShowHideItem = new NativeMenuItem(_settings.DateTimeHidden ? "Show clock widget" : "Hide clock widget");
        _clockShowHideItem.Click += OnToggleClockShowHide;
        menu.Add(_clockShowHideItem);

        _lockItem = new NativeMenuItem("Lock position")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _settings.Locked,
        };
        _lockItem.Click += OnToggleLock;
        menu.Add(_lockItem);

        _alwaysOnTopItem = new NativeMenuItem("Always on top")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _settings.AlwaysOnTop,
        };
        _alwaysOnTopItem.Click += OnToggleAlwaysOnTop;
        menu.Add(_alwaysOnTopItem);

        _snapItem = new NativeMenuItem("Snap to edges")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _settings.SnapToEdges,
        };
        _snapItem.Click += OnToggleSnap;
        menu.Add(_snapItem);

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

        _showHideItem.Header = _settings.Hidden ? "Show metrics widget" : "Hide metrics widget";
        Save();
    }

    private void OnToggleClockShowHide(object? sender, EventArgs e)
    {
        _settings.DateTimeHidden = !_settings.DateTimeHidden;
        if (_settings.DateTimeHidden)
        {
            _dateTimeWindow.Hide();
        }
        else
        {
            _dateTimeWindow.Show();
            _dateTimeDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
        }

        _clockShowHideItem.Header = _settings.DateTimeHidden ? "Show clock widget" : "Hide clock widget";
        Save();
    }

    private void OnToggleLock(object? sender, EventArgs e)
    {
        _settings.Locked = !_settings.Locked;
        _window.IsLocked = _settings.Locked;
        _desktop.SetClickThrough(_settings.Locked);
        _dateTimeWindow.IsLocked = _settings.Locked;
        _dateTimeDesktop.SetClickThrough(_settings.Locked);
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
        viewModel.TimeZoneChanged += () => OnTimeZoneChanged(viewModel);

        _settingsWindow = new SettingsWindow { DataContext = viewModel };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void OnAppearanceChanged(SettingsViewModel viewModel)
    {
        _settings.BackgroundColor = viewModel.BackgroundColor;
        _settings.Opacity = viewModel.Opacity;
        _viewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);
        _dateTimeViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);

        // Reuse the existing debounce so dragging the opacity slider writes once.
        _saveTimer!.Stop();
        _saveTimer.Start();
    }

    private void OnTimeZoneChanged(SettingsViewModel viewModel)
    {
        _settings.TimeZoneId = viewModel.SelectedTimeZone.Id;
        _dateTimeViewModel.SetTimeZone(viewModel.SelectedTimeZone);

        // Reuse the existing debounce so the write coalesces.
        _saveTimer!.Stop();
        _saveTimer.Start();
    }

    private void OnMetricVisibilityChanged(string key, bool visible)
    {
        _settings.Visibility[key] = visible;
        _viewModel.SetVisibility(key, visible);
        ApplyActiveDevices();
        Save();
    }

    // A device is polled while any of its metrics is visible; once they are all hidden it is
    // released so its sensors stop refreshing.
    private void ApplyActiveDevices()
    {
        bool Visible(string key) => _settings.Visibility.GetValueOrDefault(key, true);

        bool cpu = Visible("cpu.usage") || Visible("cpu.temp") || Visible("cpu.power");
        bool memory = Visible("ram.usage");
        bool gpu = Visible("gpu.usage") || Visible("gpu.temp")
                   || Visible("gpu.power") || Visible("vram.usage");

        _source?.SetActiveDevices(cpu, memory, gpu);
    }

    // Expands the legacy whole-card visibility keys (cpu/ram/gpu/vram) into the per-metric keys so
    // settings saved before this feature keep their hidden cards hidden.
    private void MigrateVisibility()
    {
        Dictionary<string, bool> visibility = _settings.Visibility;

        void Expand(string legacy, params string[] keys)
        {
            if (visibility.TryGetValue(legacy, out bool value))
            {
                foreach (string key in keys)
                {
                    if (!visibility.ContainsKey(key))
                    {
                        visibility[key] = value;
                    }
                }

                visibility.Remove(legacy);
            }
        }

        Expand("cpu", "cpu.usage", "cpu.temp", "cpu.power");
        Expand("ram", "ram.usage");
        Expand("gpu", "gpu.usage", "gpu.temp", "gpu.power");
        Expand("vram", "vram.usage");
    }

    private void OnToggleAlwaysOnTop(object? sender, EventArgs e)
    {
        _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
        _desktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
        _dateTimeDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
        _alwaysOnTopItem.IsChecked = _settings.AlwaysOnTop;
        Save();
    }

    private void OnToggleSnap(object? sender, EventArgs e)
    {
        _settings.SnapToEdges = !_settings.SnapToEdges;
        _window.SnapEnabled = _settings.SnapToEdges;
        _dateTimeWindow.SnapEnabled = _settings.SnapToEdges;
        _snapItem.IsChecked = _settings.SnapToEdges;
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

    // Resolves the saved zone id to a TimeZoneInfo, falling back to local if it is missing or the
    // id is unknown on this machine.
    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    // Physical-pixel rectangle of a window for peer snapping.
    private static EdgeSnap.Rect RectOf(Window window)
    {
        double scale = window.RenderScaling;
        return new EdgeSnap.Rect(
            window.Position.X,
            window.Position.Y,
            (int)Math.Round(window.Width * scale),
            (int)Math.Round(window.Height * scale));
    }

    // Generalized form of EnsureOnScreen for any widget window: if its restored position lands off
    // every monitor, pull it back onto the primary screen and persist the corrected coordinates.
    private void EnsureWindowOnScreen(Window window, Action saveX, Action saveY)
    {
        var screens = window.Screens;
        if (screens is null || screens.All.Count == 0)
        {
            return;
        }

        if (screens.ScreenFromPoint(window.Position) is null)
        {
            var primary = screens.Primary ?? screens.All[0];
            var area = primary.WorkingArea;
            window.Position = new PixelPoint(area.X + 48, area.Y + 48);
            saveX();
            saveY();
            Save();
        }
    }

    private void Save() => _settingsStore.Save(_settings);
}
