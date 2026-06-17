namespace MiniMetrics.ViewModels;

// A widget viewmodel that derives its card background from the shared appearance settings. Lets App
// apply a color/opacity change to every widget through one collection instead of naming each by hand.
public interface IWidgetAppearance
{
    void ApplyAppearance(string backgroundColor, int opacity);
}
