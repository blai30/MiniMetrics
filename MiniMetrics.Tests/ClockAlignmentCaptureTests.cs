using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;
using MiniMetrics.Views;

namespace MiniMetrics.Tests;

// Visual record for issue #25: a right-aligned auto-width clock rendered on a wide time and then a
// narrow one. The text sits flush to the same right edge in both frames.
[TestClass]
public class ClockAlignmentCaptureTests
{
    [TestMethod]
    public Task Right_aligned_auto_width_clock_across_a_width_change()
        => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly)
            .Dispatch(() =>
            {
                Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;

                var viewModel = new DateTimeWidgetViewModel();
                viewModel.SetLocale(CultureInfo.GetCultureInfo("en-US"));
                viewModel.SetTimeZone(TimeZoneInfo.Utc);
                viewModel.SetAlignment(ClockAlignment.Right);
                viewModel.SetWidthMode(ClockWidthMode.Auto);
                viewModel.ApplyAppearance("#0F121D", 100);
                viewModel.Tick(new(2026, 6, 16, 12, 26, 42, TimeSpan.Zero));

                var window = new DateTimeWindow { DataContext = viewModel };
                window.Show();
                window.Position = new PixelPoint(1000, 100);

                Assert.IsNotNull(Capture.Window(window, "clock-align-1-wide"));

                viewModel.Tick(new(2026, 6, 16, 1, 26, 42, TimeSpan.Zero));
                Assert.IsNotNull(Capture.Window(window, "clock-align-2-narrow"));
            }, CancellationToken.None);
}
