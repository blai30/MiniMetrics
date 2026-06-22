using System.Collections.Generic;
using MiniMetrics.Services;

namespace MiniMetrics.Views;

// Owns the chrome flags that apply to every widget at once: lock position, always-on-top, snap-to-edges.
// Each toggle flips the persisted flag through the controller and pushes the new value to every host, then
// returns it so the caller can echo the applied state to the tray. Keeps "a chrome flag fans across all
// hosts" in one place instead of repeating the flip-fan-echo triple per flag in App.
public sealed class WidgetChrome(SettingsController controller, IReadOnlyList<IWidgetChromeTarget> targets)
{
    public bool ToggleLocked()
    {
        bool locked = controller.ToggleLocked();
        foreach (var target in targets) target.SetLocked(locked);
        return locked;
    }

    public bool ToggleAlwaysOnTop()
    {
        bool onTop = controller.ToggleAlwaysOnTop();
        foreach (var target in targets) target.SetAlwaysOnTop(onTop);
        return onTop;
    }

    public bool ToggleSnap()
    {
        bool snap = controller.ToggleSnapToEdges();
        foreach (var target in targets) target.SetSnapEnabled(snap);
        return snap;
    }
}
