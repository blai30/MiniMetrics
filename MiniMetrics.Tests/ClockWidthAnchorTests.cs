using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;
using MiniMetrics.Views;

namespace MiniMetrics.Tests;

// Issue #25: in Auto width mode the clock window sizes to its content, so it resizes whenever the
// rendered time changes width. The aligned edge has to stay planted, or the whole widget slides.
[TestClass]
public class ClockWidthAnchorTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    // "12:26:42 PM" renders wider than "1:26:42 AM".
    private static readonly DateTimeOffset Wide = new(2026, 6, 16, 12, 26, 42, TimeSpan.Zero);
    private static readonly DateTimeOffset Narrow = new(2026, 6, 16, 1, 26, 42, TimeSpan.Zero);

    private const int Left = 1000;

    private static DateTimeWidgetViewModel NewVm(ClockAlignment alignment, ClockWidthMode mode)
    {
        var viewModel = new DateTimeWidgetViewModel();
        viewModel.SetLocale(EnUs);
        viewModel.SetTimeZone(TimeZoneInfo.Utc);
        viewModel.SetAlignment(alignment);
        viewModel.SetWidthMode(mode);
        return viewModel;
    }

    // Shows a clock at a known position on the wide time, then reformats to the narrow time and
    // reports the window edges (physical pixels) before and after the resize.
    private static (double WideLeft, double WideRight, double NarrowLeft, double NarrowRight) Resize(
        ClockAlignment alignment, ClockWidthMode mode = ClockWidthMode.Auto)
    {
        var viewModel = NewVm(alignment, mode);
        viewModel.Tick(Wide);

        var window = new DateTimeWindow { DataContext = viewModel };
        window.Show();
        window.Position = new PixelPoint(Left, 100);
        Dispatcher.UIThread.RunJobs();

        double wideLeft = window.Position.X;
        double wideRight = wideLeft + window.Bounds.Width * window.RenderScaling;

        viewModel.Tick(Narrow);
        Dispatcher.UIThread.RunJobs();

        double narrowLeft = window.Position.X;
        double narrowRight = narrowLeft + window.Bounds.Width * window.RenderScaling;

        Assert.AreNotEqual(wideRight - wideLeft, narrowRight - narrowLeft,
            "the two times must render at different widths for this test to mean anything");

        return (wideLeft, wideRight, narrowLeft, narrowRight);
    }

    private static Task Run(Action body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly)
            .Dispatch(body, CancellationToken.None);

    [TestMethod]
    public Task Right_aligned_auto_width_keeps_its_right_edge()
        => Run(() =>
        {
            var edges = Resize(ClockAlignment.Right);

            Assert.AreEqual(edges.WideRight, edges.NarrowRight, 1.0);
        });

    [TestMethod]
    public Task Center_aligned_auto_width_keeps_its_center()
        => Run(() =>
        {
            var edges = Resize(ClockAlignment.Center);

            Assert.AreEqual(
                (edges.WideLeft + edges.WideRight) / 2,
                (edges.NarrowLeft + edges.NarrowRight) / 2,
                1.0);
        });

    [TestMethod]
    public Task Left_aligned_auto_width_keeps_its_left_edge()
        => Run(() =>
        {
            var edges = Resize(ClockAlignment.Left);

            Assert.AreEqual(edges.WideLeft, edges.NarrowLeft, 1e-9);
        });

    [TestMethod]
    public Task Fixed_width_never_moves_the_window()
        => Run(() =>
        {
            var viewModel = NewVm(ClockAlignment.Right, ClockWidthMode.Fixed);
            viewModel.Tick(Wide);

            var window = new DateTimeWindow { DataContext = viewModel };
            window.Show();
            window.Position = new PixelPoint(Left, 100);
            Dispatcher.UIThread.RunJobs();

            viewModel.Tick(Narrow);
            Dispatcher.UIThread.RunJobs();

            Assert.AreEqual(Left, window.Position.X);
            Assert.AreEqual(viewModel.ScaledWidth, window.Bounds.Width, 1e-9);
        });

    [TestMethod]
    public Task Auto_width_holds_steady_across_seconds()
        => Run(() =>
        {
            var viewModel = NewVm(ClockAlignment.Right, ClockWidthMode.Auto);
            viewModel.Tick(Wide);

            var window = new DateTimeWindow { DataContext = viewModel };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            double width = window.Bounds.Width;

            // Tabular figures give every digit the same advance, so a tick that only changes which
            // digits are shown must not resize the window at all.
            for (int second = 0; second < 60; second++)
            {
                viewModel.Tick(new(2026, 6, 16, 12, 26, second, TimeSpan.Zero));
                Dispatcher.UIThread.RunJobs();
                Assert.AreEqual(width, window.Bounds.Width, 1e-9, $"width moved at :{second:00}");
            }
        });
}
