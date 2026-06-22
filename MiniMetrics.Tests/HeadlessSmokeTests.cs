using Avalonia.Controls;
using Avalonia.Headless;

namespace MiniMetrics.Tests;

[TestClass]
public class HeadlessSmokeTests
{
    // Proves the Skia-backed headless platform boots on this stack and produces real pixels. The body
    // runs on the session's UI thread via Dispatch; the returned Task fails the test on any exception.
    [TestMethod]
    public Task Headless_skia_renders_a_frame()
        => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly)
            .Dispatch(() =>
            {
                var window = new Window
                {
                    Width = 200,
                    Height = 100,
                    Content = new TextBlock { Text = "headless ok" }
                };
                window.Show();

                var frame = Capture.Window(window, "_smoke");

                Assert.IsNotNull(frame);
                Assert.IsTrue(frame!.PixelSize.Width > 0);
                Assert.IsTrue(frame.PixelSize.Height > 0);
            }, CancellationToken.None);
}
