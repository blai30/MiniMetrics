using System.Globalization;
using Avalonia.Data;
using MiniMetrics.Converters;
using MiniMetrics.Models;

namespace MiniMetrics.Tests;

[TestClass]
public class EnumToBoolConverterTests
{
    private readonly EnumToBoolConverter _converter = new();

    [TestMethod]
    public void Convert_returns_true_when_value_matches_parameter()
    {
        object? result = _converter.Convert(ClockAlignment.Center, typeof(bool), ClockAlignment.Center,
            CultureInfo.InvariantCulture);

        Assert.AreEqual(true, result);
    }

    [TestMethod]
    public void Convert_returns_false_when_value_differs_from_parameter()
    {
        object? result = _converter.Convert(ClockAlignment.Left, typeof(bool), ClockAlignment.Center,
            CultureInfo.InvariantCulture);

        Assert.AreEqual(false, result);
    }

    [TestMethod]
    public void ConvertBack_returns_parameter_when_true()
    {
        object? result = _converter.ConvertBack(true, typeof(ClockAlignment), ClockAlignment.Right,
            CultureInfo.InvariantCulture);

        Assert.AreEqual(ClockAlignment.Right, result);
    }

    [TestMethod]
    public void ConvertBack_returns_DoNothing_when_false()
    {
        object? result = _converter.ConvertBack(false, typeof(ClockAlignment), ClockAlignment.Right,
            CultureInfo.InvariantCulture);

        Assert.AreSame(BindingOperations.DoNothing, result);
    }
}
