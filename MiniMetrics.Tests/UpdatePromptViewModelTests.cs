using MiniMetrics.ViewModels;

namespace MiniMetrics.Tests;

[TestClass]
public class UpdatePromptViewModelTests
{
    [TestMethod]
    public void ForAvailable_is_actionable_and_carries_version_and_url()
    {
        var viewModel = UpdatePromptViewModel.ForAvailable("1.3.0", "1.2.0", "https://example/r");

        Assert.IsTrue(viewModel.IsActionable);
        Assert.IsFalse(viewModel.IsInformational);
        Assert.AreEqual("1.3.0", viewModel.Version);
        Assert.AreEqual("https://example/r", viewModel.Url);
        StringAssert.Contains(viewModel.Body, "1.3.0");
        StringAssert.Contains(viewModel.Body, "1.2.0");
    }

    [TestMethod]
    public void ForUpToDate_is_informational()
    {
        var viewModel = UpdatePromptViewModel.ForUpToDate("1.2.0");

        Assert.IsFalse(viewModel.IsActionable);
        Assert.IsTrue(viewModel.IsInformational);
        StringAssert.Contains(viewModel.Body, "1.2.0");
    }

    [TestMethod]
    public void ForFailed_is_informational()
    {
        var viewModel = UpdatePromptViewModel.ForFailed();

        Assert.IsFalse(viewModel.IsActionable);
        Assert.IsTrue(viewModel.IsInformational);
    }

    [TestMethod]
    public void ForInstallReady_is_actionable_and_installable_without_a_url()
    {
        var viewModel = UpdatePromptViewModel.ForInstallReady("1.3.0", "1.2.0");

        Assert.IsTrue(viewModel.IsActionable);
        Assert.IsTrue(viewModel.CanInstall);
        Assert.IsNull(viewModel.Url);
        Assert.AreEqual("1.3.0", viewModel.Version);
        StringAssert.Contains(viewModel.Body, "1.3.0");
        StringAssert.Contains(viewModel.Body, "1.2.0");
    }

    [TestMethod]
    public void ForAvailable_is_actionable_but_not_installable()
    {
        var viewModel = UpdatePromptViewModel.ForAvailable("1.3.0", "1.2.0", "https://example/r");

        Assert.IsTrue(viewModel.IsActionable);
        Assert.IsFalse(viewModel.CanInstall);
    }
}
