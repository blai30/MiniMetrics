using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;
using MiniMetrics.Views;

namespace MiniMetrics.Tests;

[TestClass]
public class SettingsCaptureTests
{
    // Renders the real settings window in dark theme to captures/settings-dark.png and confirms the
    // pill-tab list resolved, which fails if a binding or template in the view is broken.
    [TestMethod]
    public Task Settings_window_dark_renders()
        => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly)
            .Dispatch(() =>
            {
                Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;

                var viewModel = new SettingsViewModel(new(), true, new FakeFontCatalog());
                var window = new SettingsWindow { DataContext = viewModel };
                window.Show();

                var frame = Capture.Window(window, "settings-dark");

                Assert.IsNotNull(frame);
                Assert.IsTrue(frame!.PixelSize.Width >= 640);
                Assert.IsNotNull(window.FindControl<ListBox>("SectionList"));
            }, CancellationToken.None);
}
