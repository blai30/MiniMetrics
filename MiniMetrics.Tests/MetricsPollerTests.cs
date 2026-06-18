using System;
using System.Threading;
using System.Threading.Tasks;
using MiniMetrics.Models;
using MiniMetrics.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MiniMetrics.Tests;

[TestClass]
public class MetricsPollerTests
{
    // On shutdown the owner disposes the poller and then disposes the sensor source. If Dispose returns
    // while a read is still running on the loop thread, the source (LibreHardwareMonitor plus its kernel
    // driver) is closed mid-read and faults natively, which is the Windows "Application Error" crash.
    // Dispose must wait for an in-flight read to finish before returning.
    [TestMethod]
    public void Dispose_waits_for_an_in_flight_read_to_finish()
    {
        var readEntered = new ManualResetEventSlim(false);
        var releaseRead = new ManualResetEventSlim(false);

        var source = new BlockingSensorSource(() =>
        {
            readEntered.Set();
            releaseRead.Wait();
        });

        var poller = new MetricsPoller(source, TimeSpan.FromMilliseconds(10));
        poller.Start();

        Assert.IsTrue(readEntered.Wait(TimeSpan.FromSeconds(5)), "the poll loop never entered Read");

        // Dispose on a background thread so the test can observe whether it returns early.
        Task dispose = Task.Run(() => poller.Dispose());

        Assert.IsFalse(
            dispose.Wait(TimeSpan.FromMilliseconds(200)),
            "Dispose returned while a read was still in flight");

        releaseRead.Set();

        Assert.IsTrue(
            dispose.Wait(TimeSpan.FromSeconds(5)),
            "Dispose did not return after the read finished");
    }

    private sealed class BlockingSensorSource : ISensorSource
    {
        private readonly Action _onRead;

        public BlockingSensorSource(Action onRead) => _onRead = onRead;

        public void SetActiveDevices(bool cpu, bool memory, bool gpu)
        {
        }

        public MetricsSnapshot Read()
        {
            _onRead();
            return new MetricsSnapshot(null, null, null);
        }
    }
}
