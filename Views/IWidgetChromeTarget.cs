namespace MiniMetrics.Views;

// What a widget host exposes to the chrome flags that apply to every widget at once. WidgetChrome fans a
// toggle across a collection of these, so the fan-out can be tested without a real window.
public interface IWidgetChromeTarget
{
    void SetLocked(bool locked);

    void SetAlwaysOnTop(bool onTop);

    void SetSnapEnabled(bool enabled);
}
