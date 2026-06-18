using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform;

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

    public TrayMenuController(InitialState state)
    {
        _menu = new NativeMenu();

        _cpuShowHideItem = new NativeMenuItem("Show CPU widget")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.CpuChecked,
        };
        _cpuShowHideItem.Click += (_, _) => ToggleCpuRequested?.Invoke();
        _menu.Add(_cpuShowHideItem);

        _gpuShowHideItem = new NativeMenuItem("Show GPU widget")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.GpuChecked,
        };
        _gpuShowHideItem.Click += (_, _) => ToggleGpuRequested?.Invoke();
        _menu.Add(_gpuShowHideItem);

        _clockShowHideItem = new NativeMenuItem("Show clock widget")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.ClockChecked,
        };
        _clockShowHideItem.Click += (_, _) => ToggleClockRequested?.Invoke();
        _menu.Add(_clockShowHideItem);

        _lockItem = new NativeMenuItem("Lock position")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.LockChecked,
        };
        _lockItem.Click += (_, _) => ToggleLockRequested?.Invoke();
        _menu.Add(_lockItem);

        _alwaysOnTopItem = new NativeMenuItem("Always on top")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.AlwaysOnTopChecked,
        };
        _alwaysOnTopItem.Click += (_, _) => ToggleAlwaysOnTopRequested?.Invoke();
        _menu.Add(_alwaysOnTopItem);

        _snapItem = new NativeMenuItem("Snap to edges")
        {
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = state.SnapChecked,
        };
        _snapItem.Click += (_, _) => ToggleSnapRequested?.Invoke();
        _menu.Add(_snapItem);

        if (state.ShowRunAtStartup)
        {
            _runAtStartupItem = new NativeMenuItem("Run at startup")
            {
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = state.RunAtStartupChecked,
            };
            _runAtStartupItem.Click += (_, _) => RunAtStartupRequested?.Invoke();
            _menu.Add(_runAtStartupItem);
        }

        _menu.Add(new NativeMenuItemSeparator());

        var settingsItem = new NativeMenuItem("Settings...");
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke();
        _menu.Add(settingsItem);

        var checkUpdatesItem = new NativeMenuItem("Check for updates...");
        checkUpdatesItem.Click += (_, _) => CheckUpdatesRequested?.Invoke();
        _menu.Add(checkUpdatesItem);

        if (state.ShowUninstall)
        {
            var uninstallItem = new NativeMenuItem("Uninstall MiniMetrics...");
            uninstallItem.Click += (_, _) => UninstallRequested?.Invoke();
            _menu.Add(uninstallItem);
        }

        _menu.Add(new NativeMenuItemSeparator());

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) => QuitRequested?.Invoke();
        _menu.Add(quit);
    }

    // Installs the tray icon on the application. Kept separate from the constructor so the icon asset is
    // loaded with the same lifetime as the old BuildTray call.
    public void Attach(Avalonia.Application application, WindowIcon icon, string toolTip)
    {
        _trayIcon = new TrayIcon
        {
            Icon = icon,
            ToolTipText = toolTip,
            Menu = _menu,
        };

        TrayIcon.SetIcons(application, new TrayIcons { _trayIcon });
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
        if (_runAtStartupItem is not null)
        {
            _runAtStartupItem.IsChecked = value;
        }
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

        _updateAvailableItem = new NativeMenuItem(UpdateItemHeader(version));
        if (canApplyInApp)
        {
            _updateAvailableItem.Click += (_, _) => ApplyUpdateRequested?.Invoke();
        }
        else
        {
            _updateAvailableItem.Click += (_, _) =>
            {
                if (_updateUrl is not null)
                {
                    OpenReleasePageRequested?.Invoke(_updateUrl);
                }
            };
        }

        _menu.Items.Insert(0, _updateAvailableItem);
        _menu.Items.Insert(1, new NativeMenuItemSeparator());
    }

    // Removes the update item and the separator inserted directly after it, if present.
    public void RemoveUpdateItem()
    {
        if (_updateAvailableItem is null)
        {
            return;
        }

        int index = _menu.Items.IndexOf(_updateAvailableItem);
        foreach (int removeAt in UpdateItemRemovalIndices(index, HasTrailingSeparator(index)))
        {
            _menu.Items.RemoveAt(removeAt);
        }

        _updateAvailableItem = null;
        _updateUrl = null;
    }

    private bool HasTrailingSeparator(int index) =>
        index >= 0
        && index + 1 < _menu.Items.Count
        && _menu.Items[index + 1] is NativeMenuItemSeparator;

    private static string UpdateItemHeader(string version) => $"Update available (v{version})";

    // Pure index math for removing the update item plus its trailing separator. Returns the positions to
    // remove, in order, accounting for the list shrinking after the first removal. Empty when the item is
    // not present.
    public static IReadOnlyList<int> UpdateItemRemovalIndices(int itemIndex, bool hasTrailingSeparator)
    {
        if (itemIndex < 0)
        {
            return Array.Empty<int>();
        }

        // After removing the item at itemIndex, a trailing separator slides down into the same index.
        return hasTrailingSeparator
            ? new[] { itemIndex, itemIndex }
            : new[] { itemIndex };
    }
}
