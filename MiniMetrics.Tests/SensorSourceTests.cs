using MiniMetrics.Services;
using Xunit;

namespace MiniMetrics.Tests;

public class SensorSourceTests
{
    [Fact]
    public void Released_device_returns_a_null_section()
    {
        var source = new MockSensorSource();
        source.SetActiveDevices(cpu: false, memory: true, gpu: true);

        var snapshot = source.Read();

        Assert.Null(snapshot.Cpu);
        Assert.NotNull(snapshot.Memory);
        Assert.NotNull(snapshot.Gpu);
    }

    [Fact]
    public void Re_enabling_a_device_restores_its_section()
    {
        var source = new MockSensorSource();
        source.SetActiveDevices(cpu: false, memory: false, gpu: false);
        source.SetActiveDevices(cpu: true, memory: true, gpu: true);

        var snapshot = source.Read();

        Assert.NotNull(snapshot.Cpu);
        Assert.NotNull(snapshot.Memory);
        Assert.NotNull(snapshot.Gpu);
    }
}
