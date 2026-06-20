using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;

namespace MiniMetrics.Views;

// Owns the system-tray menu and icon. The NativeMenu/NativeMenuItem wiring and all checked-state lives
// here; App subscribes to the action events and runs its decision logic, then pushes the resulting state
// back through the SetXChecked methods and the update-item helpers. No Avalonia menu types leak back to
// App.
public sealed class TrayMenuController
{
    // The initial checked-state and the platform/installed conditionals, supplied once at construction.
    public sealed record InitialState(
        bool CpuChecked,
        bool GpuChecked,
        bool ClockChecked,
        bool LockChecked,
        bool AlwaysOnTopChecked,
        bool SnapChecked,
        bool ShowRunAtStartup,
        bool RunAtStartupChecked,
        bool ShowUninstall);

    private readonly NativeMenu _menu;
    private readonly NativeMenuItem _cpuShowHideItem;
    private readonly NativeMenuItem _gpuShowHideItem;
    private readonly NativeMenuItem _clockShowHideItem;
    private readonly NativeMenuItem _lockItem;
    private readonly NativeMenuItem _alwaysOnTopItem;
    private readonly NativeMenuItem _snapItem;
    private readonly NativeMenuItem? _runAtStartupItem;

    private TrayIcon? _trayIcon;
    private NativeMenuItem? _updateAvailableItem;
    private string? _updateUrl;

    // lucide icon path data (24x24 grid), rasterized per item by MenuIconRenderer. rect/circle/line
    // elements are expressed as path commands so the whole glyph is one parseable geometry.
    private const string CpuIcon =
        "M12 20v2 M12 2v2 M17 20v2 M17 2v2 M2 12h2 M2 17h2 M2 7h2 M20 12h2 M20 17h2 M20 7h2 M7 20v2 M7 2v2 " +
        "M6 4h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-12a2 2 0 0 1-2-2v-12a2 2 0 0 1 2-2z " +
        "M9 8h6a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1h-6a1 1 0 0 1-1-1v-6a1 1 0 0 1 1-1z";

    private const string GpuIcon =
        "M4 3h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2h-16a2 2 0 0 1-2-2v-10a2 2 0 0 1 2-2z M8 21h8 M12 17v4";

    private const string ClockIcon = "M2 12a10 10 0 1 0 20 0a10 10 0 1 0-20 0z M12 6v6l4 2";

    private const string LockIcon =
        "M5 11h14a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-14a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2z M7 11V7a5 5 0 0 1 10 0v4";

    private const string AlwaysOnTopIcon = "M5 3h14 M18 13l-6-6-6 6 M12 7v14";
    private const string SnapIcon = "M22 6H2 M22 18H2 M6 2V22 M18 2V22";
    private const string RunAtStartupIcon = "M12 2v10 M18.4 6.6a9 9 0 1 1-12.77.04";

    private const string SettingsIcon =
        "M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 " +
        "2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 " +
        "2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051" +
        "a2.34 2.34 0 0 0 3.319-1.915 M9 12a3 3 0 1 0 6 0a3 3 0 1 0-6 0z";

    private const string CheckUpdatesIcon =
        "M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8 M21 3v5h-5 " +
        "M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16 M8 16H3v5";

    private const string UninstallIcon =
        "M10 11v6 M14 11v6 M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6 M3 6h18 M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2";

    private const string QuitIcon = "M16 17l5-5-5-5 M21 12H9 M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4";
    private const string UpdateAvailableIcon = "M12 15V3 M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5";

    private IBrush _iconBrush;

    // Each item paired with its lucide path so SetIconColor can re-rasterize every glyph when the theme
    // changes without re-deriving which icon belongs where.
    private readonly List<(NativeMenuItem Item, string PathData)> _icons = [];

    // Raised when the user clicks a tray entry. App carries out what each action does (widget show/hide,
    // persistence, elevation, startup registration) and then pushes the resulting checked-state back in.
    public event Action? ToggleCpuRequested;
    public event Action? ToggleGpuRequested;
    public event Action? ToggleClockRequested;
    public event Action? ToggleLockRequested;
    public event Action? ToggleAlwaysOnTopRequested;
    public event Action? ToggleSnapRequested;
    public event Action? RunAtStartupRequested;
    public event Action? OpenSettingsRequested;
    public event Action? CheckUpdatesRequested;
    public event Action? UninstallRequested;
    public event Action? QuitRequested;

    // Raised by the update tray item once it has been added through ShowUpdateAvailable. App decides
    // which to honor by passing canApplyInApp; the controller only wires the click to the right event.
    public event Action? ApplyUpdateRequested;
    public event Action<string>? OpenReleasePageRequested;

    public TrayMenuController(InitialState state, IBrush iconBrush)
    {
        _iconBrush = iconBrush;
        _menu = [];

        _cpuShowHideItem = new("Show CPU widget")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.CpuChecked
        };
        _cpuShowHideItem.Click += (_, _) => ToggleCpuRequested?.Invoke();
        AddItem(_cpuShowHideItem, CpuIcon);

        _gpuShowHideItem = new("Show GPU widget")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.GpuChecked
        };
        _gpuShowHideItem.Click += (_, _) => ToggleGpuRequested?.Invoke();
        AddItem(_gpuShowHideItem, GpuIcon);

        _clockShowHideItem = new("Show clock widget")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.ClockChecked
        };
        _clockShowHideItem.Click += (_, _) => ToggleClockRequested?.Invoke();
        AddItem(_clockShowHideItem, ClockIcon);

        _menu.Add(new NativeMenuItemSeparator());

        _lockItem = new("Lock position")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.LockChecked
        };
        _lockItem.Click += (_, _) => ToggleLockRequested?.Invoke();
        AddItem(_lockItem, LockIcon);

        _alwaysOnTopItem = new("Always on top")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.AlwaysOnTopChecked
        };
        _alwaysOnTopItem.Click += (_, _) => ToggleAlwaysOnTopRequested?.Invoke();
        AddItem(_alwaysOnTopItem, AlwaysOnTopIcon);

        _snapItem = new("Snap to edges")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.SnapChecked
        };
        _snapItem.Click += (_, _) => ToggleSnapRequested?.Invoke();
        AddItem(_snapItem, SnapIcon);

        if (state.ShowRunAtStartup)
        {
            _runAtStartupItem = new("Run at startup")
            {
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = state.RunAtStartupChecked
            };
            _runAtStartupItem.Click += (_, _) => RunAtStartupRequested?.Invoke();
            AddItem(_runAtStartupItem, RunAtStartupIcon);
        }

        _menu.Add(new NativeMenuItemSeparator());

        var settingsItem = new NativeMenuItem("Settings...");
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke();
        AddItem(settingsItem, SettingsIcon);

        var checkUpdatesItem = new NativeMenuItem("Check for updates...");
        checkUpdatesItem.Click += (_, _) => CheckUpdatesRequested?.Invoke();
        AddItem(checkUpdatesItem, CheckUpdatesIcon);

        if (state.ShowUninstall)
        {
            var uninstallItem = new NativeMenuItem("Uninstall MiniMetrics...");
            uninstallItem.Click += (_, _) => UninstallRequested?.Invoke();
            AddItem(uninstallItem, UninstallIcon);
        }

        _menu.Add(new NativeMenuItemSeparator());

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => QuitRequested?.Invoke();
        AddItem(quit, QuitIcon);
    }

    // Adds an item to the menu, rasterizes its icon for the current theme, and records the pairing so
    // SetIconColor can re-render every glyph when the theme changes.
    private void AddItem(NativeMenuItem item, string pathData)
    {
        item.Icon = MenuIconRenderer.Render(pathData, _iconBrush);
        _icons.Add((item, pathData));
        _menu.Add(item);
    }

    // Re-rasterizes every icon in the new theme's menu-text color. Called when the resolved theme variant
    // changes so light glyphs do not end up on a light menu (or vice versa).
    public void SetIconColor(IBrush brush)
    {
        _iconBrush = brush;
        foreach ((var item, string pathData) in _icons) item.Icon = MenuIconRenderer.Render(pathData, brush);
    }

    // Installs the tray icon on the application. Kept separate from the constructor so the icon asset is
    // loaded with the same lifetime as the old BuildTray call.
    public void Attach(Avalonia.Application application, WindowIcon icon, string toolTip)
    {
        _trayIcon = new()
        {
            Icon = icon,
            ToolTipText = toolTip,
            Menu = _menu
        };

        // Left-click opens Settings; right-click still shows the context menu above.
        _trayIcon.Clicked += (_, _) => OpenSettingsRequested?.Invoke();

        TrayIcon.SetIcons(application, [_trayIcon]);
    }

    public void SetCpuChecked(bool value) => _cpuShowHideItem.IsChecked = value;

    public void SetGpuChecked(bool value) => _gpuShowHideItem.IsChecked = value;

    public void SetClockChecked(bool value) => _clockShowHideItem.IsChecked = value;

    public void SetLockChecked(bool value) => _lockItem.IsChecked = value;

    public void SetAlwaysOnTopChecked(bool value) => _alwaysOnTopItem.IsChecked = value;

    public void SetSnapChecked(bool value) => _snapItem.IsChecked = value;

    // No-op when the Run at startup item is not present (non-Windows), matching the old guarded access.
    public void SetRunAtStartupChecked(bool value)
    {
        _runAtStartupItem?.IsChecked = value;
    }

    // Adds the persistent "Update available" item at the top of the menu, or refreshes its header when one
    // already exists. canApplyInApp picks between an in-place apply (ApplyUpdateRequested) and opening the
    // release page (OpenReleasePageRequested) when the item is clicked.
    public void ShowUpdateAvailable(string version, string url, bool canApplyInApp)
    {
        _updateUrl = url;

        if (_updateAvailableItem is not null)
        {
            _updateAvailableItem.Header = UpdateItemHeader(version);
            return;
        }

        _updateAvailableItem = new(UpdateItemHeader(version))
        {
            Icon = MenuIconRenderer.Render(UpdateAvailableIcon, _iconBrush)
        };
        _icons.Add((_updateAvailableItem, UpdateAvailableIcon));
        if (canApplyInApp)
            _updateAvailableItem.Click += (_, _) => ApplyUpdateRequested?.Invoke();
        else
            _updateAvailableItem.Click += (_, _) =>
            {
                if (_updateUrl is not null) OpenReleasePageRequested?.Invoke(_updateUrl);
            };

        _menu.Items.Insert(0, _updateAvailableItem);
        _menu.Items.Insert(1, new NativeMenuItemSeparator());
    }

    // Removes the update item and the separator inserted directly after it, if present.
    public void RemoveUpdateItem()
    {
        if (_updateAvailableItem is null) return;

        _icons.RemoveAll(pair => pair.Item == _updateAvailableItem);

        int index = _menu.Items.IndexOf(_updateAvailableItem);
        if (index >= 0)
        {
            _menu.Items.RemoveAt(index);

            // The separator inserted right after the update item slides down into the same index.
            if (index < _menu.Items.Count && _menu.Items[index] is NativeMenuItemSeparator) _menu.Items.RemoveAt(index);
        }

        _updateAvailableItem = null;
        _updateUrl = null;
    }

    private static string UpdateItemHeader(string version) => $"Update available (v{version})";
}
