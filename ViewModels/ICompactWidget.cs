using System.ComponentModel;

namespace MiniMetrics.ViewModels;

// A widget view model that drives its window between a full and a single-line compact layout. The
// overlay window reads IsCompact and re-fits on its change without knowing the concrete view model.
public interface ICompactWidget : INotifyPropertyChanged
{
    bool IsCompact { get; }

    // The full (non-compact) window size, scaled with the widget. The overlay restores these on leaving
    // compact so the full layout is sized to its content at any scale rather than clipped to a constant.
    double ScaledWidth { get; }
    double ScaledHeight { get; }
}
