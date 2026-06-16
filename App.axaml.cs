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
    private MetricWidgetViewModel _cpuViewModel = null!;
    private MetricWidgetViewModel _gpuViewModel = null!;
    private MetricWidgetWindow _cpuWindow = null!;
    private MetricWidgetWindow _gpuWindow = null!;
    private DesktopWindow _cpuDesktop = null!;
    private DesktopWindow _gpuDesktop = null!;
    private DateTimeWidgetViewModel _dateTimeViewModel = null!;
    private DateTimeWindow _dateTimeWindow = null!;
    private DesktopWindow _dateTimeDesktop = null!;
    private DispatcherTimer? _clockTimer;
    private DispatcherTimer? _saveTimer;
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

            _settingsStore = new SettingsStore(SettingsStore.DefaultPath);
            _settings = _settingsStore.Load();
            MigrateVisibility();

            _cpuViewModel = new MetricWidgetViewModel("cpu", "ram");
            _cpuViewModel.LoadVisibility(_settings.Visibility);
            _cpuViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);

            _gpuViewModel = new MetricWidgetViewModel("gpu", "vram");
            _gpuViewModel.LoadVisibility(_settings.Visibility);
            _gpuViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);

            _dateTimeViewModel = new DateTimeWidgetViewModel();
            _dateTimeViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);
            _dateTimeViewModel.SetTimeZone(ResolveTimeZone(_settings.TimeZoneId));

            _source = OperatingSystem.IsWindows()
                ? new LibreHardwareSensorSource()
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

            _cpuWindow = new MetricWidgetWindow
            {
                DataContext = _cpuViewModel,
                WindowStartupLocation = WindowStartupLocation.Manual,
                IsLocked = _settings.Locked,
                SnapEnabled = _settings.SnapToEdges,
            };
            _cpuDesktop = new DesktopWindow(_cpuWindow);
            _cpuDesktop.Attach();
            _cpuDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);

            _gpuWindow = new MetricWidgetWindow
            {
                DataContext = _gpuViewModel,
                WindowStartupLocation = WindowStartupLocation.Manual,
                IsLocked = _settings.Locked,
                SnapEnabled = _settings.SnapToEdges,
            };
            _gpuDesktop = new DesktopWindow(_gpuWindow);
            _gpuDesktop.Attach();
            _gpuDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);

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

            // Restore saved positions before each window appears, if one exists.
            if (_settings.X is int x && _settings.Y is int y)
            {
                _cpuWindow.Position = new PixelPoint(x, y);
            }

            if (_settings.GpuX is int gx && _settings.GpuY is int gy)
            {
                _gpuWindow.Position = new PixelPoint(gx, gy);
            }

            if (_settings.DateTimeX is int dtx && _settings.DateTimeY is int dty)
            {
                _dateTimeWindow.Position = new PixelPoint(dtx, dty);
            }

            // Each widget snaps against the others only while they are actually shown.
            _cpuWindow.PeerRects = () => VisiblePeerRects(_gpuWindow, _dateTimeWindow);
            _gpuWindow.PeerRects = () => VisiblePeerRects(_cpuWindow, _dateTimeWindow);
            _dateTimeWindow.PeerRects = () => VisiblePeerRects(_cpuWindow, _gpuWindow);

            // Throttle position saves so a drag results in one write, not hundreds.
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _saveTimer.Tick += (_, _) =>
            {
                _saveTimer!.Stop();
                Save();
            };

            _cpuWindow.PositionChanged += (_, _) =>
            {
                _settings.X = _cpuWindow.Position.X;
                _settings.Y = _cpuWindow.Position.Y;
                _saveTimer!.Stop();
                _saveTimer.Start();
            };

            _gpuWindow.PositionChanged += (_, _) =>
            {
                _settings.GpuX = _gpuWindow.Position.X;
                _settings.GpuY = _gpuWindow.Position.Y;
                _saveTimer!.Stop();
                _saveTimer.Start();
            };

            _dateTimeWindow.PositionChanged += (_, _) =>
            {
                _settings.DateTimeX = _dateTimeWindow.Position.X;
                _settings.DateTimeY = _dateTimeWindow.Position.Y;
                _saveTimer!.Stop();
                _saveTimer.Start();
            };

            _cpuWindow.Opened += (_, _) =>
            {
                EnsureWindowOnScreen(_cpuWindow,
                    () => _settings.X = _cpuWindow.Position.X,
                    () => _settings.Y = _cpuWindow.Position.Y);
                _cpuDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
                _cpuDesktop.SetClickThrough(_settings.Locked);
            };

            _gpuWindow.Opened += (_, _) =>
            {
                // On first appearance with no saved position, sit flush-right of the CPU widget.
                if (_settings.GpuX is null || _settings.GpuY is null)
                {
                    PlaceGpuWindowDefault();
                }

                EnsureWindowOnScreen(_gpuWindow,
                    () => _settings.GpuX = _gpuWindow.Position.X,
                    () => _settings.GpuY = _gpuWindow.Position.Y);
                _gpuDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
                _gpuDesktop.SetClickThrough(_settings.Locked);
            };

            _dateTimeWindow.Opened += (_, _) =>
            {
                EnsureWindowOnScreen(_dateTimeWindow,
                    () => _settings.DateTimeX = _dateTimeWindow.Position.X,
                    () => _settings.DateTimeY = _dateTimeWindow.Position.Y);
                _dateTimeDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
                _dateTimeDesktop.SetClickThrough(_settings.Locked);
            };

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

            desktop.MainWindow = _cpuWindow;
            if (!_settings.Hidden)
            {
                _cpuWindow.Show();
            }

            // The GPU window is shown reactively by UpdateGpuWindowVisibility once the first
            // snapshot confirms a GPU is present.

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
        _settings.Hidden = !_settings.Hidden;
        if (_settings.Hidden)
        {
            _cpuWindow.Hide();
        }
        else
        {
            _cpuWindow.Show();
            _cpuDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
        }

        _cpuShowHideItem.IsChecked = !_settings.Hidden;
        ApplyActiveDevices();
        Save();
    }

    private void OnToggleGpuShowHide(object? sender, EventArgs e)
    {
        _settings.GpuHidden = !_settings.GpuHidden;
        UpdateGpuWindowVisibility();
        _gpuShowHideItem.IsChecked = !_settings.GpuHidden;
        ApplyActiveDevices();
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

        _clockShowHideItem.IsChecked = !_settings.DateTimeHidden;
        Save();
    }

    private void OnToggleLock(object? sender, EventArgs e)
    {
        _settings.Locked = !_settings.Locked;
        _cpuWindow.IsLocked = _settings.Locked;
        _cpuDesktop.SetClickThrough(_settings.Locked);
        _gpuWindow.IsLocked = _settings.Locked;
        _gpuDesktop.SetClickThrough(_settings.Locked);
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
        _cpuViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);
        _gpuViewModel.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);
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
        _cpuViewModel.SetVisibility(key, visible);
        _gpuViewModel.SetVisibility(key, visible);
        ApplyActiveDevices();
        Save();

        // Toggling CPU temp/power flips whether autostart must be elevated; re-register if on.
        if ((key == "cpu.temp" || key == "cpu.power")
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
        if (shouldShow && !_gpuWindow.IsVisible)
        {
            _gpuWindow.Show();
            _gpuDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
        }
        else if (!shouldShow && _gpuWindow.IsVisible)
        {
            _gpuWindow.Hide();
        }
    }

    // Places the GPU widget flush-right of the CPU widget and persists the position.
    private void PlaceGpuWindowDefault()
    {
        EdgeSnap.Rect cpu = RectOf(_cpuWindow);
        _gpuWindow.Position = new PixelPoint(cpu.X + cpu.Width, cpu.Y);
        _settings.GpuX = _gpuWindow.Position.X;
        _settings.GpuY = _gpuWindow.Position.Y;
        Save();
    }

    // The physical-pixel rectangles of any peers that are currently shown, for edge snapping.
    private static EdgeSnap.Rect[] VisiblePeerRects(Window first, Window second)
    {
        if (first.IsVisible && second.IsVisible)
        {
            return new[] { RectOf(first), RectOf(second) };
        }

        if (first.IsVisible)
        {
            return new[] { RectOf(first) };
        }

        if (second.IsVisible)
        {
            return new[] { RectOf(second) };
        }

        return Array.Empty<EdgeSnap.Rect>();
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
        _cpuDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
        _gpuDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
        _dateTimeDesktop.SetAlwaysOnTop(_settings.AlwaysOnTop);
        _alwaysOnTopItem.IsChecked = _settings.AlwaysOnTop;
        Save();
    }

    private void OnToggleSnap(object? sender, EventArgs e)
    {
        _settings.SnapToEdges = !_settings.SnapToEdges;
        _cpuWindow.SnapEnabled = _settings.SnapToEdges;
        _gpuWindow.SnapEnabled = _settings.SnapToEdges;
        _dateTimeWindow.SnapEnabled = _settings.SnapToEdges;
        _snapItem.IsChecked = _settings.SnapToEdges;
        Save();
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

    // CPU temperature and CPU power need the ring0 driver, which only an elevated process can load.
    private bool RequiresElevation()
    {
        bool Visible(string key) => _settings.Visibility.GetValueOrDefault(key, true);
        return Visible("cpu.temp") || Visible("cpu.power");
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
