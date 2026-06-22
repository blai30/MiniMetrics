using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace MiniMetrics.Tests;

public static class Capture
{
    // Flushes pending layout/render jobs, captures the shown window to a PNG under the captures
    // directory, and returns the frame so a test can assert on it. Returns null if no frame was
    // produced (which a smoke test should treat as a failure).
    public static WriteableBitmap? Window(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        if (frame is not null)
            frame.Save(Path.Combine(CapturePaths.OutputDirectory(), name + ".png"));

        return frame;
    }
}
