using MiniMetrics.Lib;

namespace MiniMetrics.Tests;

[TestClass]
public class FontCatalogTests
{
    [TestMethod]
    public void Pins_inter_first()
    {
        var result = FontCatalog.Arrange(["Arial", "Verdana"]);
        Assert.AreEqual("Inter", result[0]);
    }

    [TestMethod]
    public void Sorts_the_remainder_case_insensitively()
    {
        var result = FontCatalog.Arrange(["Verdana", "arial", "Cascadia Code"]);
        CollectionAssert.AreEqual(new[] { "Inter", "arial", "Cascadia Code", "Verdana" }, result.ToArray());
    }

    [TestMethod]
    public void Removes_a_duplicate_inter_from_the_system_list()
    {
        var result = FontCatalog.Arrange(["Arial", "Inter", "Verdana"]);
        Assert.AreEqual(1, result.Count(name => name == "Inter"));
        Assert.AreEqual("Inter", result[0]);
    }

    [TestMethod]
    public void Empty_input_yields_just_inter()
    {
        var result = FontCatalog.Arrange([]);
        CollectionAssert.AreEqual(new[] { "Inter" }, result.ToArray());
    }
}
