using System.Linq;
using MiniMetrics.Views;

namespace MiniMetrics.Tests;

[TestClass]
public class TrayMenuControllerTests
{
    [TestMethod]
    public void Removal_indices_are_empty_when_item_is_absent()
    {
        var indices = TrayMenuController.UpdateItemRemovalIndices(-1, hasTrailingSeparator: true);
        Assert.AreEqual(0, indices.Count);
    }

    [TestMethod]
    public void Removal_indices_remove_only_the_item_without_a_trailing_separator()
    {
        var indices = TrayMenuController.UpdateItemRemovalIndices(0, hasTrailingSeparator: false);
        CollectionAssert.AreEqual(new[] { 0 }, indices.ToArray());
    }

    [TestMethod]
    public void Removal_indices_remove_the_item_then_the_collapsed_separator()
    {
        // After removing the item at index 0, the separator that followed it slides down into index 0,
        // so the second removal targets the same index.
        var indices = TrayMenuController.UpdateItemRemovalIndices(0, hasTrailingSeparator: true);
        CollectionAssert.AreEqual(new[] { 0, 0 }, indices.ToArray());
    }
}
