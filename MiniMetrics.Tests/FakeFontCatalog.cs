using MiniMetrics.Lib;
using MiniMetrics.Services;

namespace MiniMetrics.Tests;

// A catalog with a fixed family list so view-model tests are deterministic. Runs the same arrangement
// as the real one so Inter is pinned first.
public sealed class FakeFontCatalog : IFontCatalog
{
    private readonly IReadOnlyList<string> _families;

    public FakeFontCatalog(params string[] families) =>
        _families = FontCatalog.Arrange(families.Length > 0 ? families : ["Arial", "Cascadia Code"]);

    public IReadOnlyList<string> AvailableFamilies() => _families;
}
