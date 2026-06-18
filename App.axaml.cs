using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
    private WidgetCoordinator _widgetCoordinator = null!;
    private DateTimeWidgetViewModel _dateTimeViewModel = null!;
    private IWidgetAppearance[] _appearances = Array.Empty<IWidgetAppearance>();
    private WidgetHost _cpuHost = null!;
    private WidgetHost _gpuHost = null!;
    private WidgetHost _dateTimeHost = null!;
    private WidgetHost[] _hosts = Array.Empty<WidgetHost>();
    private DispatcherTimer? _clockTimer;
    private DispatcherTimer? _trimTimer;

    private TrayMenuController _tray = null!;
    private StartupManager? _startupManager;
    private SettingsWindow? _settingsWindow;
    private PawnIoPromptWindow? _pawnIoPromptWindow;
    private ConfirmUninstallWindow? _confirmUninstallWindow;
    private IUpdateFlow _updateFlow = null!;
    private bool _isInstalled;
    private string? _rootStubPath;
    private Version _currentVersion = null!;
    private UpdatePromptWindow? _updatePromptWindow;

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
            _dateTimeViewModel.SetLocale(ResolveLocale(_settings.ClockLocaleId));
            _dateTimeViewModel.SetFormats(
                _settings.ClockTimeFormat, _settings.ClockDateFormat,
                _settings.ClockTimeFormatHover, _settings.ClockDateFormatHover);

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

            _widgetCoordinator = new WidgetCoordinator(_settingsController, _cpuViewModel, _gpuViewModel, _source);

            // Release any device whose widget is hidden or whose every metric is hidden before the
            // first poll runs.
            _widgetCoordinator.ApplyActiveDevices();

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
        bool showRunAtStartup = false;
        bool runAtStartupChecked = false;

        if (OperatingSystem.IsWindows())
        {
            _startupManager = new StartupManager(
                new WindowsStartupOperations(),
                AutostartTarget.Resolve(_isInstalled, _rootStubPath, Environment.ProcessPath!));

            // Keep a stale run-key path corrected, but never prompt for elevation at launch.
            _startupManager.RefreshRunKeyPath();

            // If we are already elevated (relaunched on demand, or started by the scheduled task) and
            // startup is on, migrate the registration to match the current elevation need. Because the
            // process is already elevated, this creates or removes the scheduled task with no prompt,
            // which is what keeps enabling a CPU sensor to a single UAC prompt overall.
            if (_elevation.IsElevated() && _startupManager.IsEnabled())
            {
                _startupManager.Sync(true, RequiresElevation());
            }

            showRunAtStartup = true;
            runAtStartupChecked = _startupManager.IsEnabled();
        }

        _tray = new TrayMenuController(new TrayMenuController.InitialState(
            CpuChecked: !_settings.Hidden,
            GpuChecked: !_settings.GpuHidden,
            ClockChecked: !_settings.DateTimeHidden,
            LockChecked: _settings.Locked,
            AlwaysOnTopChecked: _settings.AlwaysOnTop,
            SnapChecked: _settings.SnapToEdges,
            ShowRunAtStartup: showRunAtStartup,
            RunAtStartupChecked: runAtStartupChecked,
            ShowUninstall: OperatingSystem.IsWindows() && _isInstalled));

        _tray.ToggleCpuRequested += OnToggleCpuShowHide;
        _tray.ToggleGpuRequested += OnToggleGpuShowHide;
        _tray.ToggleClockRequested += OnToggleClockShowHide;
        _tray.ToggleLockRequested += OnToggleLock;
        _tray.ToggleAlwaysOnTopRequested += OnToggleAlwaysOnTop;
        _tray.ToggleSnapRequested += OnToggleSnap;
        _tray.RunAtStartupRequested += OnToggleRunAtStartup;
        _tray.OpenSettingsRequested += OnOpenSettings;
        _tray.CheckUpdatesRequested += () => RunUpdateCheck(manual: true);
        if (OperatingSystem.IsWindows() && _isInstalled)
        {
            // The controller only surfaces the uninstall item under this same condition, so the handler
            // is only ever reachable on the platform it supports.
            _tray.UninstallRequested += OnUninstall;
        }

        _tray.QuitRequested += () =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        };
        _tray.ApplyUpdateRequested += ApplyUpdateInApp;
        _tray.OpenReleasePageRequested += OpenReleasePage;

        _tray.Attach(
            this,
            new WindowIcon(AssetLoader.Open(new Uri("avares://MiniMetrics/Assets/minimetrics.ico"))),
            "Mini Metrics");
    }

    private void OnToggleCpuShowHide()
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

        _tray.SetCpuChecked(!hidden);
        _widgetCoordinator.ApplyActiveDevices();
    }

    private void OnToggleGpuShowHide()
    {
        bool hidden = _settingsController.ToggleGpuHidden();
        UpdateGpuWindowVisibility();
        _tray.SetGpuChecked(!hidden);
        _widgetCoordinator.ApplyActiveDevices();
    }

    private void OnToggleClockShowHide()
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

        _tray.SetClockChecked(!hidden);
    }

    private void OnToggleLock()
    {
        bool locked = _settingsController.ToggleLocked();
        foreach (WidgetHost host in _hosts)
        {
            host.SetLocked(locked);
        }

        _tray.SetLockChecked(locked);
    }

    private void OnOpenSettings()
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
        viewModel.ClockFormatsChanged += () => OnClockFormatsChanged(viewModel);
        viewModel.ClockLocaleChanged += () => OnClockLocaleChanged(viewModel);
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
        // keeping this consistent with the startup path. SelectedTimeZone can be momentarily null while
        // the user types in the search box, which also maps to local.
        string? id = viewModel.UseLocalTime || viewModel.SelectedTimeZone is null ? null : viewModel.SelectedTimeZone.Id;
        _settingsController.SetTimeZone(id);
        _dateTimeViewModel.SetTimeZone(ResolveTimeZone(id));
    }

    private void OnClockFormatsChanged(SettingsViewModel viewModel)
    {
        _settingsController.SetClockFormats(
            viewModel.ClockTimeFormat, viewModel.ClockDateFormat,
            viewModel.ClockTimeFormatHover, viewModel.ClockDateFormatHover);
        _dateTimeViewModel.SetFormats(
            viewModel.ClockTimeFormat, viewModel.ClockDateFormat,
            viewModel.ClockTimeFormatHover, viewModel.ClockDateFormatHover);
    }

    private void OnClockLocaleChanged(SettingsViewModel viewModel)
    {
        // SelectedLocale always comes from the list, so its Name is a valid culture name to persist.
        _settingsController.SetClockLocale(viewModel.SelectedLocale.Name);
        _dateTimeViewModel.SetLocale(viewModel.SelectedLocale);
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

        // Persist the change, re-render the owning widget, and release any device whose metrics are now
        // all hidden, as one step so render, polling, and saved state cannot drift apart.
        _widgetCoordinator.SetMetricVisibility(key, visible);

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
        // no longer wanted, and RemoveTask tries a non-elevated delete first, only prompting (runas) as a
        // fallback for tasks left by older versions that an administrator alone can delete.
        // Turning a metric on while unelevated returns earlier into the relaunch path and never reaches
        // here, so this block only ever reduces or keeps the elevation requirement.
        if (_startupManager is not null && _startupManager.IsEnabled())
        {
            _startupManager.Sync(true, RequiresElevation());
            _tray.SetRunAtStartupChecked(_startupManager.IsEnabled());
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

        _widgetCoordinator.SetMetricVisibility(key, false);
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

    private void OnToggleAlwaysOnTop()
    {
        bool onTop = _settingsController.ToggleAlwaysOnTop();
        foreach (WidgetHost host in _hosts)
        {
            host.SetAlwaysOnTop(onTop);
        }

        _tray.SetAlwaysOnTopChecked(onTop);
    }

    private void OnToggleSnap()
    {
        bool snap = _settingsController.ToggleSnapToEdges();
        foreach (WidgetHost host in _hosts)
        {
            host.SetSnapEnabled(snap);
        }

        _tray.SetSnapChecked(snap);
    }

    private void OnToggleRunAtStartup()
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
        _tray.SetRunAtStartupChecked(_startupManager.IsEnabled());
    }

    // Opens the uninstall confirmation. Installed builds only; the menu item is not shown otherwise.
    // Reuses a single window so repeated clicks focus the existing prompt rather than stacking duplicates.
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void OnUninstall()
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

    // Runs the ordered in-app uninstall: remove the scheduled task first, then the run key, then hand off to
    // Velopack's uninstaller. Tasks created by this version delete without a prompt; a task left by an older
    // version is admin-only, so its removal prompts for UAC and a declined prompt aborts the whole thing and
    // leaves everything in place. On success the app shuts itself down so Velopack can delete the install
    // directory, shortcuts, and Add/Remove Programs entry; while this process is alive those files stay
    // locked and the uninstall only partially completes. An aborted outcome leaves everything in place and
    // keeps running.
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private void RunUninstall()
    {
        var coordinator = new UninstallCoordinator(
            new WindowsStartupOperations(),
            LaunchVelopackUninstaller);

        if (coordinator.Run() == UninstallOutcome.Completed
            && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private static void LaunchVelopackUninstaller()
    {
        string updateExe = VelopackPaths.ResolveUpdateExe(AppContext.BaseDirectory);
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
        _tray.ShowUpdateAvailable(version, url, _updateFlow.CanApplyInApp);

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
            _tray.RemoveUpdateItem();
        };
        _updatePromptWindow.InstallRequested += (_, _) => ApplyUpdateInApp();
        _updatePromptWindow.Closed += (_, _) => _updatePromptWindow = null;
        _updatePromptWindow.Show();
    }

    // Applies the pending update in place and restarts; on failure surfaces the failure prompt. Shared by
    // the update prompt's install button and the tray's update item.
    private async void ApplyUpdateInApp()
    {
        try
        {
            await _updateFlow.ApplyAndRestartAsync();
        }
        catch (Exception)
        {
            ShowUpdateInfo(UpdatePromptViewModel.ForFailed());
        }
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

    // Resolves the saved locale id to a CultureInfo, falling back to the machine's current culture if
    // it is missing or unknown on this machine.
    private static CultureInfo ResolveLocale(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return CultureInfo.CurrentCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(id);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.CurrentCulture;
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
