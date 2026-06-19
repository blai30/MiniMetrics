using LibreHardwareMonitor.Hardware;
using MiniMetrics.Services;

namespace MiniMetrics.Tests;

[TestClass]
public class UpdateVisitorTests
{
    [TestMethod]
    public void Updates_every_device_and_its_subdevices()
    {
        var sub = new FakeHardware();
        var root = new FakeHardware { SubHardware = [sub] };

        root.Accept(new UpdateVisitor());

        Assert.AreEqual(1, root.UpdateCount);
        Assert.AreEqual(1, sub.UpdateCount);
    }

    [TestMethod]
    public void Disables_sensor_value_history_to_stop_it_accumulating()
    {
        // LibreHardwareMonitor records a value into ISensor.Values on every Update, retaining a
        // default of one day of history. The widget only reads the latest Value, so left alone that
        // history grows unbounded for a day across every sensor. The visitor must switch it off.
        var rootSensor = new FakeSensor();
        var subSensor = new FakeSensor();
        var sub = new FakeHardware { Sensors = [subSensor] };
        var root = new FakeHardware
        {
            Sensors = [rootSensor],
            SubHardware = [sub]
        };

        root.Accept(new UpdateVisitor());

        Assert.AreEqual(TimeSpan.Zero, rootSensor.ValuesTimeWindow);
        Assert.AreEqual(TimeSpan.Zero, subSensor.ValuesTimeWindow);
    }

    private sealed class FakeHardware : IHardware
    {
        public int UpdateCount { get; private set; }
        public ISensor[] Sensors { get; set; } = [];
        public IHardware[] SubHardware { get; set; } = [];

        public void Update() => UpdateCount++;
        public void Accept(IVisitor visitor) => visitor.VisitHardware(this);

        public void Traverse(IVisitor visitor)
        {
        }

        public HardwareType HardwareType => default;
        public Identifier Identifier => null!;
        public string Name { get; set; } = "fake";
        public IHardware Parent => null!;
        public IDictionary<string, string> Properties => new Dictionary<string, string>();
        public string GetReport() => string.Empty;

        // Required by IHardware but never raised by the fake.
#pragma warning disable CS0067
        public event SensorEventHandler? SensorAdded;
        public event SensorEventHandler? SensorRemoved;
#pragma warning restore CS0067
    }

    private sealed class FakeSensor : ISensor
    {
        // Mirrors LibreHardwareMonitor's default so the test starts from the leaking state.
        public TimeSpan ValuesTimeWindow { get; set; } = TimeSpan.FromDays(1);

        public void Accept(IVisitor visitor) => visitor.VisitSensor(this);

        public void Traverse(IVisitor visitor)
        {
        }

        public void ResetMin()
        {
        }

        public void ResetMax()
        {
        }

        public void ClearValues()
        {
        }

        public IControl Control => null!;
        public IHardware Hardware => null!;
        public Identifier Identifier => null!;
        public int Index => 0;
        public bool IsDefaultHidden => false;
        public float? Max => null;
        public float? Min => null;
        public string Name { get; set; } = "fake";
        public IReadOnlyList<IParameter> Parameters => [];
        public SensorType SensorType => default;
        public float? Value => null;
        public IEnumerable<SensorValue> Values => [];
    }
}
