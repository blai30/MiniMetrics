using System;
using System.Collections.Generic;
using System.Linq;
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
    private SettingsController _settingsController = null!;
    private Settings _settings = null!;
    private MetricWidgetViewModel _cpuViewModel = null!;
    private MetricWidgetViewModel _gpuViewModel = null!;
    private DateTimeWidgetViewModel _dateTimeViewModel = null!;
    private WidgetHost _cpuHost = null!;
    private WidgetHost _gpuHost = null!;
    private WidgetHost _dateTimeHost = null!;
    private WidgetHost[] _hosts = Array.Empty<WidgetHost>();
    private DispatcherTimer? _clockTimer;
    private DispatcherTimer? _trimTimer;

    private TrayIcon? _trayIcon;
    private NativeMenuItem _cpuShowHideItem = null!;
    private NativeMenuItem _gpuShowHideItem = null!;
    private NativeMenuItem _clockShowHideItem = null!;
    private NativeMenuItem _lockItem = null!;
    private NativeMenuItem _alwaysOnTopItem = null!;
    private NativeMenuItem _snapItem = null!;
    private NativeMenuItem? _runAtStartupItem;
    private StartupManager? _startupManager;
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

            var settingsStore = new SettingsStore(SettingsStore.DefaultPath);
            _settingsController = new SettingsController(
                settingsStore.Load(),
                settingsStore,
                new DispatcherSaveScheduler(TimeSpan.FromMilliseconds(600)));
            _settings = _settingsController.Current;

            _cpuViewModel = new MetricWidgetViewModel("cpu", "ram");
            _cpuViewModel.BindVisibility(_settings.Visibility);
            _cpuViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);

            _gpuViewModel = new MetricWidgetViewModel("gpu", "vram");
            _gpuViewModel.BindVisibility(_settings.Visibility);
            _gpuViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);

            _dateTimeViewModel = new DateTimeWidgetViewModel();
            _dateTimeViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);
            _dateTimeViewModel.SetTimeZone(ResolveTimeZone(_settings.TimeZoneId));

            _source = OperatingSystem.IsWindows()
                ? new HardwareSensorSource(new LibreHardwareTree())
                : new MockSensorSource();

            // Release any device whose widget is hidden or whose every metric is hidden before the
            // first poll runs.
            ApplyActiveDevices();

            _poller = new MetricsPoller(_source, TimeSpan.FromSeconds(1));
            _poller.SnapshotReady += snapshot =>
                Dispatcher.UIThread.Post(() =>
                {
                    _cpuViewModel.ApplySnapshot(snapshot);
                    _gpuViewModel.ApplySnapshot(snapshot);

                    // The GPU widget appears only once a GPU is actually present and not hidden.
                    UpdateGpuWindowVisibility();
                });

            _cpuHost = CreateHost(
                new MetricWidgetWindow { DataContext = _cpuViewModel },
                () => _settings.X is int x && _settings.Y is int y ? (x, y) : null,
                _settingsController.SetCpuPosition);

            _gpuHost = CreateHost(
                new MetricWidgetWindow { DataContext = _gpuViewModel },
                () => _settings.GpuX is int x && _settings.GpuY is int y ? (x, y) : null,
                _settingsController.SetGpuPosition);

            _dateTimeHost = CreateHost(
                new DateTimeWindow { DataContext = _dateTimeViewModel },
                () => _settings.DateTimeX is int x && _settings.DateTimeY is int y ? (x, y) : null,
                _settingsController.SetDateTimePosition);

            _hosts = new[] { _cpuHost, _gpuHost, _dateTimeHost };

            // On first appearance with no saved position, the GPU widget sits flush-right of the CPU widget.
            _gpuHost.OnFirstPlacement = () =>
            {
                EdgeSnap.Rect cpu = _cpuHost.Rect;
                _gpuHost.MoveTo(cpu.X + cpu.Width, cpu.Y);
            };

            // Each widget snaps against the others only while they are actually shown.
            _cpuHost.SnapAgainst(_gpuHost, _dateTimeHost);
            _gpuHost.SnapAgainst(_cpuHost, _dateTimeHost);
            _dateTimeHost.SnapAgainst(_cpuHost, _gpuHost);

            // The CLR, JIT and Avalonia commit far more than the idle widgets keep touching, so the
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

            desktop.MainWindow = _cpuHost.Window;
            if (!_settings.Hidden)
            {
                _cpuHost.Show();
            }

            // The GPU window is shown reactively by UpdateGpuWindowVisibility once the first
            // snapshot confirms a GPU is present.

            if (!_settings.DateTimeHidden)
            {
                _dateTimeHost.Show();
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
                _settingsController.Flush();
                _poller?.Dispose();
                (_source as IDisposable)?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void BuildTray()
    {
        var menu = new NativeMenu();

        _cpuShowHideItem = new NativeMenuItem("Show CPU widget")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = !_settings.Hidden,
        };
        _cpuShowHideItem.Click += OnToggleCpuShowHide;
        menu.Add(_cpuShowHideItem);

        _gpuShowHideItem = new NativeMenuItem("Show GPU widget")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = !_settings.GpuHidden,
        };
        _gpuShowHideItem.Click += OnToggleGpuShowHide;
        menu.Add(_gpuShowHideItem);

        _clockShowHideItem = new NativeMenuItem("Show clock widget")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = !_settings.DateTimeHidden,
        };
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

        if (OperatingSystem.IsWindows())
        {
            _startupManager = new StartupManager(
                new WindowsStartupOperations(),
                Environment.ProcessPath!);

            // Keep a stale run-key path corrected, but never prompt for elevation at launch.
            _startupManager.RefreshRunKeyPath();

            _runAtStartupItem = new NativeMenuItem("Run at startup")
            {
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = _startupManager.IsEnabled(),
            };
            _runAtStartupItem.Click += OnToggleRunAtStartup;
            menu.Add(_runAtStartupItem);
        }

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
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://MiniMetrics/Assets/minimetrics.ico"))),
            ToolTipText = "Mini Metrics",
            Menu = menu,
        };

        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
    }

    private void OnToggleCpuShowHide(object? sender, EventArgs e)
    {
        bool hidden = _settingsController.ToggleCpuHidden();
        if (hidden)
        {
            _cpuHost.Hide();
        }
        else
        {
            _cpuHost.Show();
        }

        _cpuShowHideItem.IsChecked = !hidden;
        ApplyActiveDevices();
    }

    private void OnToggleGpuShowHide(object? sender, EventArgs e)
    {
        bool hidden = _settingsController.ToggleGpuHidden();
        UpdateGpuWindowVisibility();
        _gpuShowHideItem.IsChecked = !hidden;
        ApplyActiveDevices();
    }

    private void OnToggleClockShowHide(object? sender, EventArgs e)
    {
        bool hidden = _settingsController.ToggleDateTimeHidden();
        if (hidden)
        {
            _dateTimeHost.Hide();
        }
        else
        {
            _dateTimeHost.Show();
        }

        _clockShowHideItem.IsChecked = !hidden;
    }

    private void OnToggleLock(object? sender, EventArgs e)
    {
        bool locked = _settingsController.ToggleLocked();
        foreach (WidgetHost host in _hosts)
        {
            host.SetLocked(locked);
        }

        _lockItem.IsChecked = locked;
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
        _settingsController.SetAppearance(viewModel.BackgroundColor, viewModel.Opacity);
        _cpuViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);
        _gpuViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);
        _dateTimeViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);
    }

    private void OnTimeZoneChanged(SettingsViewModel viewModel)
    {
        _settingsController.SetTimeZone(viewModel.SelectedTimeZone.Id);
        _dateTimeViewModel.SetTimeZone(viewModel.SelectedTimeZone);
    }

    private void OnMetricVisibilityChanged(string key, bool visible)
    {
        // The controller writes the shared Settings.Visibility map; the widgets read from it, and
        // ApplyActiveDevices reads it to release any device whose metrics are now all hidden.
        _settingsController.SetMetricVisibility(key, visible);
        _cpuViewModel.RefreshVisibility(key);
        _gpuViewModel.RefreshVisibility(key);
        ApplyActiveDevices();

        // Toggling an elevation-requiring metric flips whether autostart must be elevated; re-register if on.
        bool affectsElevation = MetricRegistry.All.Any(entry => entry.Key == key && entry.RequiresElevation);
        if (affectsElevation
            && _startupManager is not null
            && _startupManager.IsEnabled())
        {
            _startupManager.Sync(true, RequiresElevation());
            _runAtStartupItem!.IsChecked = _startupManager.IsEnabled();
        }
    }

    // A device is polled while its widget is shown and any of its metrics is visible; otherwise it
    // is released so its sensors stop refreshing.
    private void ApplyActiveDevices()
    {
        DeviceActivation.Result result = DeviceActivation.Compute(
            _settings.Visibility,
            cpuWidgetShown: !_settings.Hidden,
            gpuWidgetShown: !_settings.GpuHidden);

        _source?.SetActiveDevices(result.Cpu, result.Memory, result.Gpu);
    }

    // Shows the GPU window only when a GPU is present and the widget is not hidden; idempotent so it
    // is safe to call on every snapshot.
    private void UpdateGpuWindowVisibility()
    {
        bool shouldShow = !_settings.GpuHidden && _gpuViewModel.HasContent;
        if (shouldShow && !_gpuHost.IsVisible)
        {
            _gpuHost.Show();
        }
        else if (!shouldShow && _gpuHost.IsVisible)
        {
            _gpuHost.Hide();
        }
    }

    private void OnToggleAlwaysOnTop(object? sender, EventArgs e)
    {
        bool onTop = _settingsController.ToggleAlwaysOnTop();
        foreach (WidgetHost host in _hosts)
        {
            host.SetAlwaysOnTop(onTop);
        }

        _alwaysOnTopItem.IsChecked = onTop;
    }

    private void OnToggleSnap(object? sender, EventArgs e)
    {
        bool snap = _settingsController.ToggleSnapToEdges();
        foreach (WidgetHost host in _hosts)
        {
            host.SetSnapEnabled(snap);
        }

        _snapItem.IsChecked = snap;
    }

    private void OnToggleRunAtStartup(object? sender, EventArgs e)
    {
        if (_startupManager is null)
        {
            return;
        }

        // Compute the target from reality so the result is correct regardless of any
        // framework-side checkbox auto-toggle.
        bool target = !_startupManager.IsEnabled();
        _startupManager.Sync(target, RequiresElevation());

        // Reflect what is actually registered, so a declined UAC prompt reverts the checkmark.
        _runAtStartupItem!.IsChecked = _startupManager.IsEnabled();
    }

    // Some metrics need the ring0 driver, which only an elevated process can load; elevation is
    // required while any such metric is visible.
    private bool RequiresElevation()
    {
        return MetricRegistry.All
            .Where(entry => entry.RequiresElevation)
            .Any(entry => _settings.Visibility.GetValueOrDefault(entry.Key, true));
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

    // Wraps a widget window in a host that owns its desktop integration, position persistence, and
    // on-screen recovery, initialized with the current chrome flags.
    private WidgetHost CreateHost(
        OverlayWindow window,
        Func<(int X, int Y)?> readSavedPosition,
        Action<int, int> persistPosition)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var host = new WidgetHost(
            window,
            new PositionSlot(readSavedPosition, persistPosition, _settingsController.Flush));
        host.Initialize(_settings.Locked, _settings.SnapToEdges, _settings.AlwaysOnTop);
        return host;
    }
}
