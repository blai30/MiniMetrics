using System;
using MiniMetrics.Models;

namespace MiniMetrics.Services;

public sealed class MockSensorSource(bool includeGpu = true) : ISensorSource
{
    private int _tick;

    private bool _cpuActive = true;
    private bool _memoryActive = true;
    private bool _gpuActive = true;

    public void SetActiveDevices(bool cpu, bool memory, bool gpu)
    {
        _cpuActive = cpu;
        _memoryActive = memory;
        _gpuActive = gpu;
    }

    public MetricsSnapshot Read()
    {
        _tick++;

        var cpu = _cpuActive ? new CpuMetrics(Clamp(Wave(34, 20, 0)), null, null) : null;
        var memory = _memoryActive ? new MemoryMetrics(12_026_124_800UL, 34_359_738_368UL) : null;

        var gpu = _gpuActive && includeGpu
            ? new GpuMetrics(Clamp(Wave(78, 15, 2)), 71, 6_871_947_674UL, 12_884_901_888UL, 185)
            : null;

        return new MetricsSnapshot(cpu, memory, gpu);

        double Wave(double mid, double amplitude, int phase)
            => mid + amplitude * Math.Sin((_tick + phase) / 5.0);
    }

    private static double Clamp(double value) => Math.Clamp(value, 0, 100);
}
