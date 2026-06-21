using System.Globalization;
using MiniMetrics.Converters;

namespace MiniMetrics.Tests;

[TestClass]
public class IndexEqualsConverterTests
{
    private static readonly IndexEqualsConverter Converter = new();

    private static object? Convert(object? value, string parameter) =>
        Converter.Convert(value, typeof(bool), parameter, CultureInfo.InvariantCulture);

    [TestMethod]
    public void True_when_index_matches_parameter()
    {
        Assert.AreEqual(true, Convert(2, "2"));
    }

    [TestMethod]
    public void False_when_index_differs()
    {
        Assert.AreEqual(false, Convert(1, "2"));
    }

    [TestMethod]
    public void False_for_non_int_value()
    {
        Assert.AreEqual(false, Convert(null, "0"));
    }
}
