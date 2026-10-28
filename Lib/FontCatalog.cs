using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniMetrics.Lib;

// Arranges the family names for the picker: the bundled Inter is always first, the rest follow
// sorted case-insensitively with any duplicate Inter removed. Pure so it can be unit-tested without a
// real font manager; the raw names come from IFontCatalog.
public static class FontCatalog
{
    public static IReadOnlyList<string> Arrange(IEnumerable<string> systemFamilies)
    {
        var rest = systemFamilies
            .Where(IsUsableName)
            .Where(name =>
                !string.Equals(name, WidgetStyleProfile.DefaultFamilyName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

        return [WidgetStyleProfile.DefaultFamilyName, .. rest];
    }

    // Loading fonts makes Avalonia register extra family names as a side effect, some of which its own
    // typeface parser rejects (a doubled space throws FormatException during layout) or which are
    // garbled (NUL-interleaved names from symbol fonts). Those never reach the picker.
    private static bool IsUsableName(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && name.Length == name.Trim().Length
        && !name.Contains("  ", StringComparison.Ordinal)
        && !name.Any(char.IsControl);
}
