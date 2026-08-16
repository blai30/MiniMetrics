using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MiniMetrics.ViewModels;
using MiniMetrics.Views;

namespace MiniMetrics.Tests;

[TestClass]
public class UpdatePromptWindowTests
{
    // The dialog is a fixed width that only sizes to its content vertically, so a button row that
    // outgrows it is silently clipped rather than wrapped. Adding "View release" pushed the installed
    // variant's four buttons past the right edge, which is what this guards against.
    private const double Margin = 24;

    private static readonly UpdatePromptViewModel[] States =
    [
        UpdatePromptViewModel.ForInstallReady("1.1.2", "1.1.1", "https://example.invalid"),
        UpdatePromptViewModel.ForAvailable("1.1.2", "1.1.1", "https://example.invalid"),
        UpdatePromptViewModel.ForUpToDate("1.1.2"),
        UpdatePromptViewModel.ForFailed()
    ];

    [TestMethod]
    public Task No_action_button_is_clipped_in_any_state()
        => HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly)
            .Dispatch(() =>
            {
                Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;

                foreach (var state in States)
                {
                    var window = new UpdatePromptWindow(state);
                    window.Show();
                    Dispatcher.UIThread.RunJobs();

                    var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.IsVisible).ToList();
                    Assert.IsTrue(buttons.Count > 0, $"{state.Heading}: expected at least one button");

                    foreach (var button in buttons)
                    {
                        var edge = button.TranslatePoint(new(button.Bounds.Width, 0), window);
                        Assert.IsNotNull(edge);
                        Assert.IsTrue(
                            edge.Value.X <= window.Width - Margin + 0.5,
                            $"'{button.Content}' reaches {edge.Value.X:F1} in a {window.Width} wide dialog, past the {Margin} margin");
                    }

                    window.Close();
                }
            }, CancellationToken.None);
}
