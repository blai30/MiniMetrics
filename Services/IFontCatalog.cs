using System.Collections.Generic;

namespace MiniMetrics.Services;

// Supplies the font family names offered in settings, already arranged (Inter pinned first). Behind an
// interface so the settings view model can be tested without a real font manager.
public interface IFontCatalog
{
    IReadOnlyList<string> AvailableFamilies();
}
