using System;
using Avalonia.Controls;

namespace MiniMetrics.Views;

// Owns the reuse-or-create lifecycle for a window that must never stack a second copy. A repeat request
// focuses the live window instead of opening another; closing it clears the reference so the next request
// builds a fresh one. App holds one host per such window (settings, the prompts) rather than repeating the
// nullable-field-plus-Closed-handler dance at every call site.
public sealed class SingleWindowHost<TWindow> where TWindow : Window
{
    private TWindow? _window;

    // The open window, or null when none is shown. Lets a caller reach into a live window (e.g. its
    // DataContext) without holding its own reference.
    public TWindow? Current => _window;

    // Focuses the open window if there is one; otherwise builds it through create, wires the close cleanup,
    // shows it, and returns it. create runs only when no window is open, so the per-window event wiring done
    // inside it happens once per shown instance.
    public TWindow ShowOrActivate(Func<TWindow> create)
    {
        if (_window is not null)
        {
            _window.Activate();
            return _window;
        }

        var window = create();
        _window = window;
        window.Closed += (_, _) => _window = null;
        window.Show();
        return window;
    }
}
