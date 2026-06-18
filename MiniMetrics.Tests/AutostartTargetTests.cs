using MiniMetrics.Lib;

namespace MiniMetrics.Tests;

[TestClass]
public class AutostartTargetTests
{
    [TestMethod]
    public void Installed_uses_the_stable_root_stub()
    {
        string result = AutostartTarget.Resolve(
            installed: true,
            rootStubPath: @"C:\Users\me\AppData\Local\MiniMetrics\MiniMetrics.exe",
            processPath: @"C:\Users\me\AppData\Local\MiniMetrics\current\MiniMetrics.exe");

        Assert.AreEqual(@"C:\Users\me\AppData\Local\MiniMetrics\MiniMetrics.exe", result);
    }

    [TestMethod]
    public void Portable_uses_the_running_exe()
    {
        string result = AutostartTarget.Resolve(
            installed: false,
            rootStubPath: null,
            processPath: @"D:\Portable\MiniMetrics.exe");

        Assert.AreEqual(@"D:\Portable\MiniMetrics.exe", result);
    }

    [TestMethod]
    public void Installed_without_a_stub_falls_back_to_the_running_exe()
    {
        string result = AutostartTarget.Resolve(
            installed: true,
            rootStubPath: null,
            processPath: @"D:\Portable\MiniMetrics.exe");

        Assert.AreEqual(@"D:\Portable\MiniMetrics.exe", result);
    }
}
