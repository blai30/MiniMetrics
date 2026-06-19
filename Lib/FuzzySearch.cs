using System;
using System.Linq;

namespace MiniMetrics.Lib;

// Pure, lenient matching for the settings searchable dropdowns (locale and time zone). A query matches
// an item when every whitespace- or dash-separated token appears in the item's display name or key, so
// "english uni", "en-US", "en_US" and "enus" all match "English (United States)" / "en-US". An empty
// query matches everything. Comparison is invariant and case-insensitive so results do not depend on
// the OS locale.
public static class FuzzySearch
{
    private static readonly char[] Separators = { ' ', '-' };

    public static bool Matches(string display, string key, string? query)
    {
        string trimmed = (query ?? "").Trim();
        if (trimmed.Length == 0) return true;

        string haystack = $"{display} {key} {key.Replace("-", "")}";
        return trimmed.Replace('_', '-').Split(Separators, StringSplitOptions.RemoveEmptyEntries).All(token =>
            haystack.Contains(token, StringComparison.InvariantCultureIgnoreCase));
    }
}
