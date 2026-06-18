using MiniMetrics.Lib;

namespace MiniMetrics.Tests;

[TestClass]
public class VelopackPathsTests
{
    // AppContext.BaseDirectory always carries a trailing separator; the regression was that this case
    // resolved to "...\current\Update.exe" instead of the real "...\Update.exe" one level up.
    [TestMethod]
    public void ResolveUpdateExe_resolves_install_root_for_trailing_separator_base()
    {
        string result = VelopackPaths.ResolveUpdateExe(@"C:\app\MiniMetrics\current\");

        Assert.AreEqual(@"C:\app\MiniMetrics\Update.exe", result);
    }

    [TestMethod]
    public void ResolveUpdateExe_resolves_install_root_without_trailing_separator()
    {
        string result = VelopackPaths.ResolveUpdateExe(@"C:\app\MiniMetrics\current");

        Assert.AreEqual(@"C:\app\MiniMetrics\Update.exe", result);
    }
}
