using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
using Velopack;
using Velopack.Sources;

namespace MiniMetrics;

public partial class App : Application
{
    private MetricsPoller? _poller;
    private ISensorSource? _source;
    private IElevation _elevation = null!;
    private IDriverProbe _driverProbe = null!;
    private SettingsController _settingsController = null!;
    private Settings _settings = null!;
    private MetricWidgetViewModel _cpuViewModel = null!;
    private MetricWidgetViewModel _gpuViewModel = null!;
    private DateTimeWidgetViewModel _dateTimeViewModel = null!;
    private IWidgetAppearance[] _appearances = Array.Empty<IWidgetAppearance>();
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
    private PawnIoPromptWindow? _pawnIoPromptWindow;
    private ConfirmUninstallWindow? _confirmUninstallWindow;
    private IUpdateFlow _updateFlow = null!;
    private bool _isInstalled;
    private string? _rootStubPath;
    private Version _currentVersion = null!;
    private UpdatePromptWindow? _updatePromptWindow;
    private NativeMenu _trayMenu = null!;
    private NativeMenuItem? _updateAvailableItem;

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

            _currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

            // Decide the update mode once. An installed Velopack build updates in place; a portable or dev
            // build links to the release page. The installed build runs from "<root>\current\", so the
            // stable root stub is one directory up.
            var updateManager = new UpdateManager(new GithubSource("https://github.com/blai30/MiniMetrics", null, false));
            _isInstalled = updateManager.IsInstalled;
            _rootStubPath = _isInstalled
                ? Path.Combine(Directory.GetParent(AppContext.BaseDirectory)!.FullName, "MiniMetrics.exe")
                : null;

            _updateFlow = _isInstalled
                ? new VelopackUpdateFlow(updateManager, _settingsController, () => DateTimeOffset.UtcNow)
                : new NotifyUpdateFlow(new UpdateService(
                    new GitHubReleaseSource(), _currentVersion, _settingsController, () => DateTimeOffset.UtcNow));

            _cpuViewModel = new MetricWidgetViewModel("cpu", "ram");
            _cpuViewModel.BindVisibility(_settings.Visibility);

            _gpuViewModel = new MetricWidgetViewModel("gpu", "vram");
            _gpuViewModel.BindVisibility(_settings.Visibility);

            _dateTimeViewModel = new DateTimeWidgetViewModel();
            _dateTimeViewModel.SetTimeZone(ResolveTimeZone(_settings.TimeZoneId));

            _appearances = new IWidgetAppearance[] { _cpuViewModel, _gpuViewModel, _dateTimeViewModel };
            ApplyAppearanceToWidgets();

            _source = OperatingSystem.IsWindows()
                ? new HardwareSensorSource(new LibreHardwareTree())
                : new MockSensorSource();

            _elevation = OperatingSystem.IsWindows()
                ? new WindowsElevation()
                : new NoopElevation();

            _driverProbe = OperatingSystem.IsWindows()
                ? new WindowsDriverProbe()
                : new NoopDriverProbe();

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

            // A driver-backed metric is enabled but PawnIO is missing: the launch gate did not relaunch
            // elevated (elevation alone cannot read the sensors), so surface the one-time install step
            // rather than leaving the metric silently blank.
            if (OperatingSystem.IsWindows() && RequiresElevation() && !_driverProbe.IsInstalled())
            {
                ShowPawnIoPrompt();
            }

            // Run the launch-time update check a few seconds after startup so it never competes with
            // warmup, and only when enabled and the cadence is due.
            if (_settings.UpdateCheckEnabled
                && UpdatePolicy.IsDue(_settings.LastUpdateCheckUtc, _settings.UpdateFrequency, DateTimeOffset.UtcNow))
            {
                var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                updateTimer.Tick += (_, _) =>
                {
                    updateTimer.Stop();
                    RunUpdateCheck(manual: false);
                };
                updateTimer.Start();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void BuildTray()
    {
        _trayMenu = new NativeMenu();
        NativeMenu menu = _trayMenu;

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
                AutostartTarget.Resolve(_isInstalled, _rootStubPath, Environment.ProcessPath!));

            // Keep a stale run-key path corrected, but never prompt for elevation at launch.
            _startupManager.RefreshRunKeyPath();

            _runAtStartupItem = new NativeMenuItem("Run at startup")
            {
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = _startupManager.IsEnabled(),
            };
            _runAtStartupItem.Click += OnToggleRunAtStartup;
            menu.Add(_runAtStartupItem);

            // If we are already elevated (relaunched on demand, or started by the scheduled task) and
            // startup is on, migrate the registration to match the current elevation need. Because the
            // process is already elevated, this creates or removes the scheduled task with no prompt,
            // which is what keeps enabling a CPU sensor to a single UAC prompt overall.
            if (_elevation.IsElevated() && _startupManager.IsEnabled())
            {
                _startupManager.Sync(true, RequiresElevation());
                _runAtStartupItem.IsChecked = _startupManager.IsEnabled();
            }
        }

        menu.Add(new NativeMenuItemSeparator());

        var settingsItem = new NativeMenuItem("Settings...");
        settingsItem.Click += OnOpenSettings;
        menu.Add(settingsItem);

        var checkUpdatesItem = new NativeMenuItem("Check for updates...");
        checkUpdatesItem.Click += (_, _) => RunUpdateCheck(manual: true);
        menu.Add(checkUpdatesItem);

        if (OperatingSystem.IsWindows() && _isInstalled)
        {
            var uninstallItem = new NativeMenuItem("Uninstall MiniMetrics...");
            uninstallItem.Click += OnUninstall;
            menu.Add(uninstallItem);
        }

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
        viewModel.UpdatePreferencesChanged += () =>
            _settingsController.SetUpdatePreferences(viewModel.UpdateCheckEnabled, viewModel.UpdateFrequency);

        _settingsWindow = new SettingsWindow { DataContext = viewModel };
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void OnAppearanceChanged(SettingsViewModel viewModel)
    {
        _settingsController.SetAppearance(viewModel.BackgroundColor, viewModel.Opacity);
        ApplyAppearanceToWidgets();
    }

    // Pushes the current color and opacity to every widget through the shared appearance seam.
    private void ApplyAppearanceToWidgets()
    {
        foreach (IWidgetAppearance widget in _appearances)
        {
            widget.ApplyAppearance(_settings.BackgroundColor, _settings.Opacity);
        }
    }

    private void OnTimeZoneChanged(SettingsViewModel viewModel)
    {
        // Local time persists as a null id; ResolveTimeZone(null) maps back to the machine zone,
        // keeping this consistent with the startup path.
        string? id = viewModel.UseLocalTime ? null : viewModel.SelectedTimeZone.Id;
        _settingsController.SetTimeZone(id);
        _dateTimeViewModel.SetTimeZone(ResolveTimeZone(id));
    }

    // Re-entrancy guard: RevertMetricToggle flips a toggle back, which re-raises this event; the guard
    // stops that echo from recursing.
    private bool _suppressVisibilityHandler;

    private void OnMetricVisibilityChanged(string key, bool visible)
    {
        if (_suppressVisibilityHandler)
        {
            return;
        }

        // The controller writes the shared Settings.Visibility map; the widgets read from it, and
        // ApplyActiveDevices reads it to release any device whose metrics are now all hidden.
        _settingsController.SetMetricVisibility(key, visible);
        _cpuViewModel.RefreshVisibility(key);
        _gpuViewModel.RefreshVisibility(key);
        ApplyActiveDevices();

        bool isElevationMetric = MetricRegistry.All.Any(entry => entry.Key == key && entry.RequiresElevation);
        if (!isElevationMetric)
        {
            return;
        }

        // Turning an elevation metric on while not elevated: relaunch elevated so we can open the
        // PawnIO driver device. Settings were just persisted, so the elevated instance reads the enabled
        // state from disk and reconciles startup registration itself (one UAC prompt total).
        if (visible && !_elevation.IsElevated())
        {
            // Elevation only helps once PawnIO is installed; its device admits administrators only.
            // Without the driver, relaunching elevated would read nothing, so point the user at the
            // installer instead. The metric stays enabled and renders a placeholder until the driver is
            // present, at which point it starts working.
            if (!_driverProbe.IsInstalled())
            {
                ShowPawnIoPrompt();
                return;
            }

            _settingsController.Flush();
            if (_elevation.RelaunchElevated(Environment.ProcessPath!))
            {
                (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
                return;
            }

            // UAC declined: put the metric back to off and keep running non-elevated.
            RevertMetricToggle(key);
            return;
        }

        // Reconcile startup registration to match the new elevation need. A scheduled task that is no
        // longer needed is removed even while unelevated: Sync only touches the task when one exists and is
        // no longer wanted, and RemoveTask elevates via runas, so a UAC prompt appears only in that case.
        // Turning a metric on while unelevated returns earlier into the relaunch path and never reaches
        // here, so this block only ever reduces or keeps the elevation requirement.
        if (_startupManager is not null && _startupManager.IsEnabled())
        {
            _startupManager.Sync(true, RequiresElevation());
            _runAtStartupItem!.IsChecked = _startupManager.IsEnabled();
        }
    }

    // Puts an elevation metric back to off after a declined UAC prompt: the settings checkbox, the
    // persisted value, and the widget all return to hidden.
    private void RevertMetricToggle(string key)
    {
        _suppressVisibilityHandler = true;
        try
        {
            if (_settingsWindow?.DataContext is SettingsViewModel viewModel)
            {
                viewModel.ToggleFor(key).IsVisible = false;
            }
        }
        finally
        {
            _suppressVisibilityHandler = false;
        }

        _settingsController.SetMetricVisibility(key, false);
        _cpuViewModel.RefreshVisibility(key);
        _gpuViewModel.RefreshVisibility(key);
        ApplyActiveDevices();
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

    // Opens the uninstall confirmation. Installed builds only; the menu item is not shown otherwise.
    // Reuses a single window so repeated clicks focus the existing prompt rather than stacking duplicates.
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void OnUninstall(object? sender, EventArgs e)
    {
        if (_confirmUninstallWindow is not null)
        {
            _confirmUninstallWindow.Activate();
            return;
        }

        _confirmUninstallWindow = new ConfirmUninstallWindow();
        _confirmUninstallWindow.Confirmed += (_, _) => RunUninstall();
        _confirmUninstallWindow.Closed += (_, _) => _confirmUninstallWindow = null;
        _confirmUninstallWindow.Show();
    }

    // Runs the ordered in-app uninstall: remove the elevated scheduled task first (a declined UAC prompt
    // aborts the whole thing and leaves everything in place), then the run key, then hand off to Velopack's
    // uninstaller. Both outcomes are terminal for the app, so the result is not acted on further here.
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void RunUninstall()
    {
        var coordinator = new UninstallCoordinator(
            new WindowsStartupOperations(),
            LaunchVelopackUninstaller);
        coordinator.Run();
    }

    private static void LaunchVelopackUninstaller()
    {
        string root = Directory.GetParent(AppContext.BaseDirectory)!.FullName;
        string updateExe = Path.Combine(root, "Update.exe");
        Process.Start(new ProcessStartInfo
        {
            FileName = updateExe,
            Arguments = "--uninstall",
            UseShellExecute = false,
        });
    }

    // Some metrics are read through the PawnIO driver, whose device only an elevated process can open;
    // elevation is required while any such metric is visible.
    private bool RequiresElevation() => MetricRegistry.RequiresElevation(_settings.Visibility);

    // Surfaces the one-time PawnIO install prompt, reusing a single instance so repeated toggles focus
    // the existing window rather than stacking duplicates.
    private void ShowPawnIoPrompt()
    {
        if (_pawnIoPromptWindow is not null)
        {
            _pawnIoPromptWindow.Activate();
            return;
        }

        _pawnIoPromptWindow = new PawnIoPromptWindow();
        _pawnIoPromptWindow.Closed += (_, _) => _pawnIoPromptWindow = null;
        _pawnIoPromptWindow.Show();
    }

    private async void RunUpdateCheck(bool manual)
    {
        UpdateCheckResult result = await _updateFlow.CheckAsync(manual);

        switch (result.Outcome)
        {
            case UpdateOutcome.UpdateAvailable:
                ShowUpdateAvailable(result.Version!, result.ReleaseUrl!);
                break;
            case UpdateOutcome.UpToDate when manual:
                ShowUpdateInfo(UpdatePromptViewModel.ForUpToDate(CurrentVersionString));
                break;
            case UpdateOutcome.Failed when manual:
                ShowUpdateInfo(UpdatePromptViewModel.ForFailed());
                break;
        }
    }

    private string CurrentVersionString =>
        new Version(_currentVersion.Major, _currentVersion.Minor, _currentVersion.Build < 0 ? 0 : _currentVersion.Build).ToString();

    // Shows the actionable update prompt and adds the persistent tray item. Installed builds offer an
    // in-place install and restart; portable builds offer the release page. Reuses a single window so a
    // launch check followed by a manual check focuses the existing prompt rather than stacking a second.
    private void ShowUpdateAvailable(string version, string url)
    {
        AddUpdateTrayItem(version, url);

        if (_updatePromptWindow is not null)
        {
            _updatePromptWindow.Activate();
            return;
        }

        UpdatePromptViewModel viewModel = _updateFlow.CanApplyInApp
            ? UpdatePromptViewModel.ForInstallReady(version, CurrentVersionString)
            : UpdatePromptViewModel.ForAvailable(version, CurrentVersionString, url);

        _updatePromptWindow = new UpdatePromptWindow(viewModel);
        _updatePromptWindow.SkipRequested += (_, _) =>
        {
            _settingsController.SetSkippedUpdateVersion(version);
            RemoveUpdateTrayItem();
        };
        _updatePromptWindow.InstallRequested += async (_, _) =>
        {
            try
            {
                await _updateFlow.ApplyAndRestartAsync();
            }
            catch (Exception)
            {
                ShowUpdateInfo(UpdatePromptViewModel.ForFailed());
            }
        };
        _updatePromptWindow.Closed += (_, _) => _updatePromptWindow = null;
        _updatePromptWindow.Show();
    }

    private void ShowUpdateInfo(UpdatePromptViewModel viewModel)
    {
        if (_updatePromptWindow is not null)
        {
            _updatePromptWindow.Activate();
            return;
        }

        _updatePromptWindow = new UpdatePromptWindow(viewModel);
        _updatePromptWindow.Closed += (_, _) => _updatePromptWindow = null;
        _updatePromptWindow.Show();
    }

    private void AddUpdateTrayItem(string version, string url)
    {
        if (_updateAvailableItem is not null)
        {
            _updateAvailableItem.Header = $"Update available (v{version})";
            return;
        }

        _updateAvailableItem = new NativeMenuItem($"Update available (v{version})");
        if (_updateFlow.CanApplyInApp)
        {
            _updateAvailableItem.Click += async (_, _) =>
            {
                try
                {
                    await _updateFlow.ApplyAndRestartAsync();
                }
                catch (Exception)
                {
                    ShowUpdateInfo(UpdatePromptViewModel.ForFailed());
                }
            };
        }
        else
        {
            _updateAvailableItem.Click += (_, _) => OpenReleasePage(url);
        }

        _trayMenu.Items.Insert(0, _updateAvailableItem);
        _trayMenu.Items.Insert(1, new NativeMenuItemSeparator());
    }

    // Opens a release page in the default browser. Best effort: a broken shell association must not
    // crash the tray click.
    private static void OpenReleasePage(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
        }
    }

    private void RemoveUpdateTrayItem()
    {
        if (_updateAvailableItem is null)
        {
            return;
        }

        int index = _trayMenu.Items.IndexOf(_updateAvailableItem);
        if (index >= 0)
        {
            _trayMenu.Items.RemoveAt(index);
            // Remove the separator inserted directly after the item.
            if (index < _trayMenu.Items.Count && _trayMenu.Items[index] is NativeMenuItemSeparator)
            {
                _trayMenu.Items.RemoveAt(index);
            }
        }

        _updateAvailableItem = null;
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
