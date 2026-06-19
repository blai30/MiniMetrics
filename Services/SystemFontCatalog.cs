using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using MiniMetrics.Lib;

namespace MiniMetrics.Services;

// Reads the installed font families from Avalonia's font manager and arranges them with Inter pinned
// first. FontManager is available on every platform Avalonia runs on, so no platform split is needed.
public sealed class SystemFontCatalog : IFontCatalog
{
    public IReadOnlyList<string> AvailableFamilies() =>
        FontCatalog.Arrange(FontManager.Current.SystemFonts.Select(font => font.Name));
}
