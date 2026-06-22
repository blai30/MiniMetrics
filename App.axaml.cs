using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
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
    private ElevationCoordinator _elevationCoordinator = null!;
    private SettingsController _settingsController = null!;
    private Settings _settings = null!;
    private MetricWidgetViewModel _cpuViewModel = null!;
    private MetricWidgetViewModel _gpuViewModel = null!;
    private SettingsApplier _applier = null!;
    private WidgetCoordinator _widgetCoordinator = null!;
    private MetricActivator _metricActivator = null!;
    private DateTimeWidgetViewModel _dateTimeViewModel = null!;
    private IWidgetDisplay[] _widgets = [];
    private IFontCatalog _fontCatalog = null!;
    private WidgetHost _cpuHost = null!;
    private WidgetHost _gpuHost = null!;
    private WidgetHost _dateTimeHost = null!;
    private WidgetHost[] _hosts = [];
    private WidgetChrome _chrome = null!;
    private DispatcherTimer? _clockTimer;
    private DispatcherTimer? _trimTimer;

    private TrayMenuController _tray = null!;
    private StartupManager? _startupManager;
    private readonly SingleWindowHost<SettingsWindow> _settingsHost = new();
    private readonly SingleWindowHost<PawnIoPromptWindow> _pawnIoHost = new();
    private readonly SingleWindowHost<ConfirmUninstallWindow> _uninstallHost = new();
    private readonly SingleWindowHost<UpdatePromptWindow> _updateHost = new();
    private IUpdateFlow _updateFlow = null!;
    private bool _isInstalled;
    private string? _rootStubPath;
    private Version _currentVersion = null!;

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
            _settingsController = new(
                settingsStore.Load(),
                settingsStore,
                new DispatcherSaveScheduler(TimeSpan.FromMilliseconds(600)));
            _settings = _settingsController.Current;

            ApplyThemeVariant();
            ActualThemeVariantChanged += OnActualThemeVariantChanged;

            _currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

            // Decide the update mode once. An installed Velopack build updates in place; a portable or dev
            // build links to the release page. The installed build runs from "<root>\current\", so the
            // stable root stub is one directory up.
            var updateManager =
                new UpdateManager(new GithubSource("https://github.com/blai30/MiniMetrics", null, false));
            _isInstalled = updateManager.IsInstalled;
            _rootStubPath = _isInstalled
                ? Path.Combine(Directory.GetParent(AppContext.BaseDirectory)!.FullName, "MiniMetrics.exe")
                : null;

            _updateFlow = _isInstalled
                ? new VelopackUpdateFlow(updateManager, _settingsController, () => DateTimeOffset.UtcNow)
                : new UpdateService(
                    new GitHubReleaseSource(), _currentVersion, _settingsController, () => DateTimeOffset.UtcNow);

            _cpuViewModel = new("cpu", "ram");
            _cpuViewModel.BindVisibility(_settings.Visibility);
            _cpuViewModel.IsCompact = _settings.CpuCompact;

            _gpuViewModel = new("gpu", "vram");
            _gpuViewModel.BindVisibility(_settings.Visibility);
            _gpuViewModel.IsCompact = _settings.GpuCompact;

            _dateTimeViewModel = new();
            _dateTimeViewModel.SetTimeZone(SettingsApplier.ResolveTimeZone(_settings.TimeZoneId));
            _dateTimeViewModel.SetLocale(SettingsApplier.ResolveLocale(_settings.ClockLocaleId));
            _dateTimeViewModel.SetFormats(
                _settings.ClockTimeFormat, _settings.ClockDateFormat,
                _settings.ClockTimeFormatHover, _settings.ClockDateFormatHover);
            _dateTimeViewModel.IsCompact = _settings.DateTimeCompact;
            _dateTimeViewModel.SetAlignment(_settings.ClockAlignment);

            _widgets = [_cpuViewModel, _gpuViewModel, _dateTimeViewModel];
            _applier = new(
                _settingsController, _cpuViewModel, _gpuViewModel, _dateTimeViewModel,
                _widgets, ApplyThemeVariant, ResolvedIsDark);
            _applier.ApplyAppearance();

            _fontCatalog = new SystemFontCatalog();
            _applier.ApplyStyle();

            _source = OperatingSystem.IsWindows()
                ? new HardwareSensorSource(new LibreHardwareTree())
                : new MockSensorSource();

            IElevation elevation = OperatingSystem.IsWindows()
                ? new WindowsElevation()
                : new NoopElevation();

            IDriverProbe driverProbe = OperatingSystem.IsWindows()
                ? new WindowsDriverProbe()
                : new NoopDriverProbe();

            _elevationCoordinator = new(elevation, driverProbe);

            _widgetCoordinator = new(_settingsController, _cpuViewModel, _gpuViewModel, _source);

            // Owns the elevation sequence a metric toggle implies. Resolves the startup manager lazily
            // because BuildTray creates it after this point (and only on Windows).
            _metricActivator = new(
                _widgetCoordinator,
                _elevationCoordinator,
                _settingsController,
                () => _startupManager,
                Environment.ProcessPath!);

            // Release any device whose widget is hidden or whose every metric is hidden before the
            // first poll runs.
            _widgetCoordinator.ApplyActiveDevices();

            _poller = new(_source, TimeSpan.FromSeconds(1));
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
                () => _settings is { X: { } x, Y: { } y } ? (x, y) : null,
                _settingsController.SetCpuPosition);

            _gpuHost = CreateHost(
                new MetricWidgetWindow { DataContext = _gpuViewModel },
                () => _settings is { GpuX: { } x, GpuY: { } y } ? (x, y) : null,
                _settingsController.SetGpuPosition);

            _dateTimeHost = CreateHost(
                new DateTimeWindow { DataContext = _dateTimeViewModel },
                () => _settings is { DateTimeX: { } x, DateTimeY: { } y } ? (x, y) : null,
                _settingsController.SetDateTimePosition);

            _hosts = [_cpuHost, _gpuHost, _dateTimeHost];
            _chrome = new(_settingsController, _hosts);

            // On first appearance with no saved position, the GPU widget sits flush-right of the CPU widget.
            _gpuHost.OnFirstPlacement = () =>
            {
                var cpu = _cpuHost.Rect;
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
            _trimTimer = new() { Interval = TimeSpan.FromSeconds(10) };
            _trimTimer.Tick += (_, _) =>
            {
                MemoryTrimmer.Trim();
                _trimTimer!.Interval = TimeSpan.FromSeconds(60);
            };
            _trimTimer.Start();

            desktop.MainWindow = _cpuHost.Window;
            if (!_settings.Hidden) _cpuHost.Show();

            // The GPU window is shown reactively by UpdateGpuWindowVisibility once the first
            // snapshot confirms a GPU is present.

            if (!_settings.DateTimeHidden) _dateTimeHost.Show();

            _poller.Start();

            // Drive the clock once per second. Tick immediately so the widget shows the time at once.
            _dateTimeViewModel.Tick(DateTimeOffset.Now);
            _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
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
            if (OperatingSystem.IsWindows() && _elevationCoordinator.NeedsDriverInstallPrompt(_settings.Visibility))
                ShowPawnIoPrompt();

            // Run the launch-time update check a few seconds after startup so it never competes with the
            // startup burst, and only when enabled and the cadence is due.
            if (_settings.UpdateCheckEnabled
                && UpdatePolicy.IsDue(_settings.LastUpdateCheckUtc, _settings.UpdateFrequency, DateTimeOffset.UtcNow))
            {
                var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                updateTimer.Tick += (_, _) =>
                {
                    updateTimer.Stop();
                    RunUpdateCheck(false);
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
            _startupManager = new(
                new WindowsStartupOperations(),
                AutostartTarget.Resolve(_isInstalled, _rootStubPath, Environment.ProcessPath!));

            // Keep a stale run-key path corrected, but never prompt for elevation at launch.
            _startupManager.RefreshRunKeyPath();

            // If we are already elevated (relaunched on demand, or started by the scheduled task) and
            // startup is on, migrate the registration to match the current elevation need. Because the
            // process is already elevated, this creates or removes the scheduled task with no prompt,
            // which is what keeps enabling a CPU sensor to a single UAC prompt overall.
            if (_elevationCoordinator.IsElevated() && _startupManager.IsEnabled())
                _startupManager.Sync(true, RequiresElevation());

            showRunAtStartup = true;
            runAtStartupChecked = _startupManager.IsEnabled();
        }

        _tray = new(new(
            !_settings.Hidden,
            !_settings.GpuHidden,
            !_settings.DateTimeHidden,
            _settings.Locked,
            _settings.AlwaysOnTop,
            _settings.SnapToEdges,
            showRunAtStartup,
            runAtStartupChecked,
            OperatingSystem.IsWindows() && _isInstalled));

        _tray.ToggleCpuRequested += OnToggleCpuShowHide;
        _tray.ToggleGpuRequested += OnToggleGpuShowHide;
        _tray.ToggleClockRequested += OnToggleClockShowHide;
        _tray.ToggleLockRequested += OnToggleLock;
        _tray.ToggleAlwaysOnTopRequested += OnToggleAlwaysOnTop;
        _tray.ToggleSnapRequested += OnToggleSnap;
        _tray.RunAtStartupRequested += OnToggleRunAtStartup;
        _tray.OpenSettingsRequested += OnOpenSettings;
        _tray.CheckUpdatesRequested += () => RunUpdateCheck(true);
        if (OperatingSystem.IsWindows() && _isInstalled)
            // The controller only surfaces the uninstall item under this same condition, so the handler
            // is only ever reachable on the platform it supports.
            _tray.UninstallRequested += OnUninstall;

        _tray.QuitRequested += () =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
        };
        _tray.ApplyUpdateRequested += ApplyUpdateInApp;
        _tray.OpenReleasePageRequested += OpenReleasePage;

        _tray.Attach(
            this,
            new(AssetLoader.Open(new("avares://MiniMetrics/Assets/minimetrics.ico"))),
            "Mini Metrics");
    }

    private void OnToggleCpuShowHide()
    {
        bool hidden = _settingsController.ToggleCpuHidden();
        if (hidden)
            _cpuHost.Hide();
        else
            _cpuHost.Show();

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
            _dateTimeHost.Hide();
        else
            _dateTimeHost.Show();

        _tray.SetClockChecked(!hidden);
    }

    private void OnToggleLock() => _tray.SetLockChecked(_chrome.ToggleLocked());

    // Single instance: the host focuses the existing window instead of opening a second one.
    private void OnOpenSettings() =>
        _settingsHost.ShowOrActivate(() =>
        {
            var viewModel = new SettingsViewModel(_settings, ResolvedIsDark(), _fontCatalog);
            viewModel.SettingChanged += OnSettingChanged;
            return new() { DataContext = viewModel };
        });

    // Routes one settings change to its persistence and live effect. Metric visibility is handled here
    // because enabling an elevation-flagged metric can relaunch or prompt, an outcome only the host can
    // render; every other change is persisted and reflected by the applier in one place.
    private void OnSettingChanged(SettingChange change)
    {
        if (change is SettingChange.MetricVisibility metric)
        {
            OnMetricVisibilityChanged(metric.Key, metric.Visible);
            return;
        }

        _applier.Apply(change);
    }

    // The effective variant resolved by Avalonia (Default resolves to Light or Dark). Dark is the
    // default when no app exists (design time) so the original look is preserved.
    private bool ResolvedIsDark() => ActualThemeVariant != ThemeVariant.Light;

    private void ApplyThemeVariant()
    {
        RequestedThemeVariant = _settings.Theme switch
        {
            AppTheme.Light => ThemeVariant.Light,
            AppTheme.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    // When the OS theme changes under System, re-apply the per-variant background and refresh accents.
    // Chrome colors update automatically through DynamicResource.
    private void OnActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (_settings.Theme == AppTheme.System)
        {
            _applier.ApplyAppearance();
            _applier.RefreshAccents();
        }
    }

    // Re-entrancy guard: RevertMetricToggle flips a toggle back, which re-raises this event; the guard
    // stops that echo from recursing.
    private bool _suppressVisibilityHandler;

    private void OnMetricVisibilityChanged(string key, bool visible)
    {
        if (_suppressVisibilityHandler) return;

        // The activator persists the change, re-renders the owning widget, reconciles polled devices,
        // and decides the elevation follow-through; App only renders the returned outcome.
        var result = _metricActivator.Apply(key, visible);

        switch (result.Outcome)
        {
            case MetricActivationOutcome.ShowDriverInstallPrompt:
                ShowPawnIoPrompt();
                break;

            case MetricActivationOutcome.Relaunching:
                (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
                break;

            case MetricActivationOutcome.RelaunchDeclined:
                // UAC declined: put the metric back to off and keep running non-elevated.
                RevertMetricToggle(key);
                break;
        }

        if (result.StartupResynced) _tray.SetRunAtStartupChecked(result.StartupEnabled);
    }

    // Puts an elevation metric back to off after a declined UAC prompt: the settings checkbox, the
    // persisted value, and the widget all return to hidden.
    private void RevertMetricToggle(string key)
    {
        _suppressVisibilityHandler = true;
        try
        {
            if (_settingsHost.Current?.DataContext is SettingsViewModel viewModel)
                viewModel.ToggleFor(key).IsVisible = false;
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
            _gpuHost.Show();
        else if (!shouldShow && _gpuHost.IsVisible) _gpuHost.Hide();
    }

    private void OnToggleAlwaysOnTop() => _tray.SetAlwaysOnTopChecked(_chrome.ToggleAlwaysOnTop());

    private void OnToggleSnap() => _tray.SetSnapChecked(_chrome.ToggleSnap());

    private void OnToggleRunAtStartup()
    {
        if (_startupManager is null) return;

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
    private void OnUninstall() =>
        _uninstallHost.ShowOrActivate(() =>
        {
            var window = new ConfirmUninstallWindow();
            window.Confirmed += (_, _) => RunUninstall();
            return window;
        });

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
            desktop.Shutdown();
    }

    private static void LaunchVelopackUninstaller()
    {
        string updateExe = VelopackPaths.ResolveUpdateExe(AppContext.BaseDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = updateExe,
            Arguments = "--uninstall",
            UseShellExecute = false
        });
    }

    // Some metrics are read through the PawnIO driver, whose device only an elevated process can open;
    // elevation is required while any such metric is visible. Asks the coordinator so the predicate
    // lives in one place.
    private bool RequiresElevation() => _elevationCoordinator.RequiresElevation(_settings.Visibility);

    // Surfaces the one-time PawnIO install prompt, reusing a single instance so repeated toggles focus
    // the existing window rather than stacking duplicates.
    private void ShowPawnIoPrompt() => _pawnIoHost.ShowOrActivate(() => new());

    private async void RunUpdateCheck(bool manual)
    {
        // async void: the launch-time check runs unawaited, so an escaping exception would tear down the
        // process. Contain it here the way ApplyUpdateInApp does for its own async void path.
        try
        {
            var result = await _updateFlow.CheckAsync(manual);

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
        catch (Exception exception)
        {
            CrashLog.Write("RunUpdateCheck", exception);
        }
    }

    private string CurrentVersionString =>
        new Version(_currentVersion.Major, _currentVersion.Minor, _currentVersion.Build < 0 ? 0 : _currentVersion.Build)
            .ToString();

    // Shows the actionable update prompt and adds the persistent tray item. Installed builds offer an
    // in-place install and restart; portable builds offer the release page. Reuses a single window so a
    // launch check followed by a manual check focuses the existing prompt rather than stacking a second.
    private void ShowUpdateAvailable(string version, string url)
    {
        _tray.ShowUpdateAvailable(version, url, _updateFlow.CanApplyInApp);

        _updateHost.ShowOrActivate(() =>
        {
            var viewModel = _updateFlow.CanApplyInApp
                ? UpdatePromptViewModel.ForInstallReady(version, CurrentVersionString)
                : UpdatePromptViewModel.ForAvailable(version, CurrentVersionString, url);

            var window = new UpdatePromptWindow(viewModel);
            window.SkipRequested += (_, _) =>
            {
                _settingsController.SetSkippedUpdateVersion(version);
                _tray.RemoveUpdateItem();
            };
            window.InstallRequested += (_, _) => ApplyUpdateInApp();
            return window;
        });
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

    private void ShowUpdateInfo(UpdatePromptViewModel viewModel) =>
        _updateHost.ShowOrActivate(() => new(viewModel));

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

    // Wraps a widget window in a host that owns its desktop integration, position persistence, and
    // on-screen recovery, initialized with the current chrome flags.
    private WidgetHost CreateHost(
        OverlayWindow window,
        Func<(int X, int Y)?> readSavedPosition,
        Action<int, int> persistPosition)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var host = new WidgetHost(window, readSavedPosition, persistPosition, _settingsController.Flush);
        host.Initialize(_settings.Locked, _settings.SnapToEdges, _settings.AlwaysOnTop);
        return host;
    }
}
