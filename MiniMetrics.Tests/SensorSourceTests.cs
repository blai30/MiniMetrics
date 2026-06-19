using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class SensorSourceTests
{
    [TestMethod]
    public void Released_device_returns_a_null_section()
    {
        var source = new MockSensorSource();
        source.SetActiveDevices(false, true, true);

        var snapshot = source.Read();

        Assert.IsNull(snapshot.Cpu);
        Assert.IsNotNull(snapshot.Memory);
        Assert.IsNotNull(snapshot.Gpu);
    }

    [TestMethod]
    public void Re_enabling_a_device_restores_its_section()
    {
        var source = new MockSensorSource();
        source.SetActiveDevices(false, false, false);
        source.SetActiveDevices(true, true, true);

        var snapshot = source.Read();

        Assert.IsNotNull(snapshot.Cpu);
        Assert.IsNotNull(snapshot.Memory);
        Assert.IsNotNull(snapshot.Gpu);
    }
}
