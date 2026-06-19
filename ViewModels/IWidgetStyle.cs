using MiniMetrics.Lib;

namespace MiniMetrics.ViewModels;

// A widget view model that takes its typeface, size scale, and weights from the shared font settings.
// Lets App push one resolved profile to every widget through a single collection, mirroring
// IWidgetAppearance.
public interface IWidgetStyle
{
    void ApplyStyle(WidgetStyleProfile profile);
}
