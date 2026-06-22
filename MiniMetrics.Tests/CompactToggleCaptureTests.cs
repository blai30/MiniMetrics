using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using MiniMetrics.Lib;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;
using MiniMetrics.Views;

namespace MiniMetrics.Tests;

// Reproduction harness for the "RAM/VRAM breaks after toggling compact and back" report. Drives one
// metric widget through full -> compact -> full and captures each frame so the regression is visible.
[TestClass]
public class CompactToggleCaptureTests
{
    private static MetricsSnapshot Snapshot() => new(
        new(34.0, null, null),
        new(12_026_124_800UL, 34_359_738_368UL),
        null);

    [TestMethod]
    public Task Cpu_widget_full_compact_full_roundtrip()
        => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly)
            .Dispatch(() =>
            {
                Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;

                var viewModel = new MetricWidgetViewModel("cpu", "ram");
                viewModel.ApplySnapshot(Snapshot());
                viewModel.ApplyAppearance("#0F121D", 100);
                viewModel.ApplyStyle(WidgetStyleProfile.Resolve(null, 150, WidgetFontWeight.Regular));

                var window = new MetricWidgetWindow { DataContext = viewModel };
                window.Show();
                Capture.Window(window, "compact-toggle-1-full");

                viewModel.IsCompact = true;
                Dispatcher.UIThread.RunJobs();
                Capture.Window(window, "compact-toggle-2-compact");

                viewModel.IsCompact = false;
                Dispatcher.UIThread.RunJobs();
                var frame = Capture.Window(window, "compact-toggle-3-back-to-full");

                Assert.IsNotNull(frame);
            }, CancellationToken.None);

    [TestMethod]
    public Task Returning_to_full_restores_the_scaled_size()
        => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly)
            .Dispatch(() =>
            {
                var viewModel = new MetricWidgetViewModel("cpu", "ram");
                viewModel.ApplySnapshot(Snapshot());
                viewModel.ApplyStyle(WidgetStyleProfile.Resolve(null, 150, WidgetFontWeight.Regular));

                var window = new MetricWidgetWindow { DataContext = viewModel };
                window.Show();

                viewModel.IsCompact = true;
                Dispatcher.UIThread.RunJobs();

                viewModel.IsCompact = false;
                Dispatcher.UIThread.RunJobs();

                // The full window must come back at its scaled size (210x176 * 1.5), not the unscaled
                // constant, or the lower (memory) card is clipped.
                Assert.AreEqual(viewModel.ScaledWidth, window.Width, 1e-9);
                Assert.AreEqual(viewModel.ScaledHeight, window.Height, 1e-9);
            }, CancellationToken.None);

    [TestMethod]
    public Task Changing_scale_while_full_resizes_the_window()
        => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly)
            .Dispatch(() =>
            {
                var viewModel = new MetricWidgetViewModel("cpu", "ram");
                viewModel.ApplySnapshot(Snapshot());

                var window = new MetricWidgetWindow { DataContext = viewModel };
                window.Show();

                // Toggle once so any code-driven size override is in effect, then change scale.
                viewModel.IsCompact = true;
                Dispatcher.UIThread.RunJobs();
                viewModel.IsCompact = false;
                Dispatcher.UIThread.RunJobs();

                viewModel.ApplyStyle(WidgetStyleProfile.Resolve(null, 150, WidgetFontWeight.Regular));
                Dispatcher.UIThread.RunJobs();

                Assert.AreEqual(viewModel.ScaledWidth, window.Width, 1e-9);
                Assert.AreEqual(viewModel.ScaledHeight, window.Height, 1e-9);
            }, CancellationToken.None);
}
