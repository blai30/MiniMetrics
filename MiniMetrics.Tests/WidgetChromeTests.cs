using MiniMetrics.Models;
using MiniMetrics.Services;
using MiniMetrics.Views;

namespace MiniMetrics.Tests;

[TestClass]
public class WidgetChromeTests
{
    private sealed class RecordingTarget : IWidgetChromeTarget
    {
        public bool? Locked;
        public bool? AlwaysOnTop;
        public bool? SnapEnabled;

        public void SetLocked(bool locked) => Locked = locked;
        public void SetAlwaysOnTop(bool onTop) => AlwaysOnTop = onTop;
        public void SetSnapEnabled(bool enabled) => SnapEnabled = enabled;
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "dm-tests", Path.GetRandomFileName(), "settings.json");

    private static (WidgetChrome chrome, SettingsController controller, RecordingTarget first, RecordingTarget second)
        NewChrome()
    {
        var controller = new SettingsController(new(), new(TempPath()), new FakeSaveScheduler());
        var first = new RecordingTarget();
        var second = new RecordingTarget();
        var chrome = new WidgetChrome(controller, [first, second]);
        return (chrome, controller, first, second);
    }

    [TestMethod]
    public void ToggleLocked_persists_and_fans_to_every_target()
    {
        var (chrome, controller, first, second) = NewChrome();

        bool locked = chrome.ToggleLocked();

        Assert.IsTrue(locked);
        Assert.IsTrue(controller.Current.Locked);
        Assert.AreEqual(true, first.Locked);
        Assert.AreEqual(true, second.Locked);
    }

    [TestMethod]
    public void ToggleAlwaysOnTop_persists_and_fans_to_every_target()
    {
        var (chrome, controller, first, second) = NewChrome();

        bool onTop = chrome.ToggleAlwaysOnTop();

        Assert.IsTrue(onTop);
        Assert.IsTrue(controller.Current.AlwaysOnTop);
        Assert.AreEqual(true, first.AlwaysOnTop);
        Assert.AreEqual(true, second.AlwaysOnTop);
    }

    [TestMethod]
    public void ToggleSnap_persists_and_fans_to_every_target()
    {
        // SnapToEdges defaults to true, so the first toggle turns it off.
        var (chrome, controller, first, second) = NewChrome();

        bool snap = chrome.ToggleSnap();

        Assert.IsFalse(snap);
        Assert.IsFalse(controller.Current.SnapToEdges);
        Assert.AreEqual(false, first.SnapEnabled);
        Assert.AreEqual(false, second.SnapEnabled);
    }

    [TestMethod]
    public void Toggle_returns_new_value_on_each_call()
    {
        var (chrome, _, first, _) = NewChrome();

        Assert.IsTrue(chrome.ToggleLocked());
        Assert.IsFalse(chrome.ToggleLocked());
        Assert.AreEqual(false, first.Locked);
    }
}
