using System;
using LibreHardwareMonitor.Hardware;

namespace MiniMetrics.Services;

// Walks the hardware tree and refreshes every device's sensors. Required before reading values.
public sealed class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);

    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();

        // Each Update appends the new reading to ISensor.Values, which LibreHardwareMonitor keeps
        // for a full day by default. The widget only ever reads the latest Value, so that history
        // is dead weight that grows once per second for every sensor. Setting the window to zero
        // turns history tracking off and keeps the footprint flat.
        foreach (ISensor sensor in hardware.Sensors)
        {
            sensor.ValuesTimeWindow = TimeSpan.Zero;
        }

        foreach (IHardware sub in hardware.SubHardware)
        {
            sub.Accept(this);
        }
    }

    public void VisitSensor(ISensor sensor) { }

    public void VisitParameter(IParameter parameter) { }
}
