using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using MiniMetrics.Views;

namespace MiniMetrics.Tests;

[TestClass]
public class StableWidthTextBlockTests
{
    private static T Measure<T>(string text) where T : TextBlock, new()
    {
        var block = new T
        {
            Text = text,
            FontSize = 80,
            FontFamily = new("Inter"),
            FontWeight = FontWeight.Medium,
        };
        block.Measure(Size.Infinity);
        return block;
    }

    private static Task Run(Action body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TestAppBuilder).Assembly)
            .Dispatch(body, CancellationToken.None);

    [TestMethod]
    public Task Width_depends_on_the_shape_of_the_text_not_on_which_digits_show()
        => Run(() =>
        {
            // "1" is much narrower than "8" in Inter, so these two differ under proportional figures.
            double ones = Measure<StableWidthTextBlock>("11:11:11 PM").DesiredSize.Width;
            double eights = Measure<StableWidthTextBlock>("88:88:88 PM").DesiredSize.Width;

            Assert.AreEqual(eights, ones, 1e-9);
        });

    [TestMethod]
    public Task A_digit_only_change_keeps_the_width_where_tabular_figures_differ()
        => Run(() =>
        {
            // Inter's tabular "4" is narrower than its other tabular digits, so reserving the tabular
            // width of the literal text would still move by a pixel here.
            static double Width(string text)
            {
                var block = new StableWidthTextBlock
                {
                    Text = text,
                    FontSize = 80,
                    FontFamily = new(MiniMetrics.Lib.WidgetStyleProfile.BundledInter),
                    FontWeight = FontWeight.Medium,
                };
                block.Measure(Size.Infinity);
                return block.DesiredSize.Width;
            }

            Assert.AreEqual(Width("1:00:43 PM"), Width("1:00:44 PM"), 1e-9);
        });

    [TestMethod]
    public Task Changing_the_font_size_measures_again()
        => Run(() =>
        {
            var block = Measure<StableWidthTextBlock>("12:34:56 PM");
            double small = block.DesiredSize.Width;

            block.FontSize = 160;
            block.Measure(Size.Infinity);

            Assert.IsTrue(block.DesiredSize.Width > small * 1.5);
        });

    [TestMethod]
    public Task A_plain_TextBlock_is_the_one_that_moves()
        => Run(() =>
        {
            double ones = Measure<TextBlock>("11:11:11 PM").DesiredSize.Width;
            double eights = Measure<TextBlock>("88:88:88 PM").DesiredSize.Width;

            Assert.AreNotEqual(eights, ones, "the fixture no longer proves anything if Inter goes monospaced");
            Assert.IsTrue(ones < eights);
        });

    [TestMethod]
    public Task Reserving_the_width_does_not_turn_on_tabular_figures_for_rendering()
        => Run(() =>
        {
            var block = Measure<StableWidthTextBlock>("11:11:11 PM");

            // The control only measures with the feature. Enabling it here would change the glyphs
            // the reader actually sees, which is the look this control exists to avoid.
            Assert.IsNull(block.FontFeatures);
        });

    [TestMethod]
    public Task Text_that_holds_no_digits_measures_exactly_as_it_always_did()
        => Run(() =>
        {
            const string text = "Tuesday, June";

            Assert.AreEqual(
                Measure<TextBlock>(text).DesiredSize.Width,
                Measure<StableWidthTextBlock>(text).DesiredSize.Width,
                1e-9);
        });

    [TestMethod]
    public Task Empty_text_measures_as_empty()
        => Run(() =>
        {
            Assert.AreEqual(
                Measure<TextBlock>("").DesiredSize.Width,
                Measure<StableWidthTextBlock>("").DesiredSize.Width,
                1e-9);
        });
}
