using System;
using DesktopMetrics.Models;

namespace DesktopMetrics.Services;

public sealed class MockSensorSource : ISensorSource
{
    private readonly bool _includeGpu;
    private int _tick;

    public MockSensorSource(bool includeGpu = true) => _includeGpu = includeGpu;

    public MetricsSnapshot Read()
    {
        _tick++;
        double wave(double mid, double amplitude, int phase)
            => mid + amplitude * Math.Sin((_tick + phase) / 5.0);

        var cpu = new CpuMetrics(Clamp(wave(34, 20, 0)), null);
        var memory = new MemoryMetrics(12_026_124_800UL, 34_359_738_368UL);

        GpuMetrics? gpu = _includeGpu
            ? new GpuMetrics(Clamp(wave(78, 15, 2)), 71, 6_871_947_674UL, 12_884_901_888UL, 185)
            : null;

        return new MetricsSnapshot(cpu, memory, gpu);
    }

    private static double Clamp(double value) => Math.Clamp(value, 0, 100);
}
