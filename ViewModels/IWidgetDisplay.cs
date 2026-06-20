using MiniMetrics.Lib;

namespace MiniMetrics.ViewModels;

// A widget view model whose card background and typeface follow the shared appearance and style
// settings. Lets App push a color/opacity or style change to every widget through one collection
// instead of naming each by hand. The two facets always change together at startup and from settings,
// so they live behind one seam.
public interface IWidgetDisplay
{
    // Derives the card background from the shared appearance color and opacity.
    void ApplyAppearance(string backgroundColor, int opacity);

    // Takes the typeface, size scale, and weights from the shared widget style profile.
    void ApplyStyle(WidgetStyleProfile profile);
}
