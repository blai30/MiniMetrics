using Avalonia;
using Avalonia.Headless;
using MiniMetrics;

[assembly: AvaloniaTestApplication(typeof(MiniMetrics.Tests.TestAppBuilder))]

namespace MiniMetrics.Tests;

// Boots the real App headless with Skia so captured frames contain real pixels. App.Initialize loads
// App.axaml (FluentTheme, the Settings.* zinc brushes, the neutral accent), while App's heavy startup
// stays dormant because the headless lifetime is not a classic desktop lifetime.
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new() { UseHeadlessDrawing = false })
        .WithInterFont();
}
