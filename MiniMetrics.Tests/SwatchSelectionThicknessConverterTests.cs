using System.Globalization;
using Avalonia;
using MiniMetrics.Converters;

namespace MiniMetrics.Tests;

[TestClass]
public class SwatchSelectionThicknessConverterTests
{
    private static readonly SwatchSelectionThicknessConverter Converter = new();

    private static object? Convert(object? swatch, object? selected) =>
        Converter.Convert([swatch, selected], typeof(Thickness), null, CultureInfo.InvariantCulture);

    [TestMethod]
    public void Matching_hex_returns_a_visible_ring()
    {
        Assert.AreEqual(new Thickness(2), Convert("#0C1A2B", "#0C1A2B"));
    }

    [TestMethod]
    public void Match_ignores_case_and_surrounding_whitespace()
    {
        Assert.AreEqual(new Thickness(2), Convert("#0c1a2b", "  #0C1A2B "));
    }

    [TestMethod]
    public void Different_hex_returns_no_ring()
    {
        Assert.AreEqual(new Thickness(0), Convert("#0C1A2B", "#1A1F2B"));
    }

    [TestMethod]
    public void Null_values_return_no_ring()
    {
        Assert.AreEqual(new Thickness(0), Convert(null, "#0C1A2B"));
        Assert.AreEqual(new Thickness(0), Convert("#0C1A2B", null));
    }
}
