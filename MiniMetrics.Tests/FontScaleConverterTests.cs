using System.Globalization;
using Avalonia;
using MiniMetrics.Converters;

namespace MiniMetrics.Tests;

[TestClass]
public class FontScaleConverterTests
{
    [TestMethod]
    public void Font_scale_multiplies_base_size_by_factor()
    {
        var converter = new FontScaleConverter();

        var result = converter.Convert(1.5, typeof(double), "42", CultureInfo.InvariantCulture);

        Assert.AreEqual(63.0, (double)result!, 1e-9);
    }

    [TestMethod]
    public void Font_scale_at_unity_returns_base_size()
    {
        var converter = new FontScaleConverter();

        var result = converter.Convert(1.0, typeof(double), "18", CultureInfo.InvariantCulture);

        Assert.AreEqual(18.0, (double)result!, 1e-9);
    }

    [TestMethod]
    public void Thickness_scale_multiplies_every_side()
    {
        var converter = new ThicknessScaleConverter();

        var result = converter.Convert(1.5, typeof(Thickness), "20,14,20,16", CultureInfo.InvariantCulture);

        Assert.AreEqual(new Thickness(30, 21, 30, 24), (Thickness)result!);
    }

    [TestMethod]
    public void Thickness_scale_handles_a_two_value_parameter()
    {
        var converter = new ThicknessScaleConverter();

        var result = converter.Convert(2.0, typeof(Thickness), "18,0", CultureInfo.InvariantCulture);

        Assert.AreEqual(new Thickness(36, 0, 36, 0), (Thickness)result!);
    }
}
