using System.Globalization;
using Avalonia.Media;
using MiniMetrics.Lib;
using MiniMetrics.Models;
using MiniMetrics.ViewModels;

namespace MiniMetrics.Tests;

[TestClass]
public class DateTimeWidgetViewModelTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 6, 16, 14, 26, 42, TimeSpan.Zero);

    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    // A view model seeded to en-US / UTC so assertions are deterministic on any CI locale.
    private static DateTimeWidgetViewModel NewVm()
    {
        var vm = new DateTimeWidgetViewModel();
        vm.SetLocale(EnUs);
        vm.SetTimeZone(TimeZoneInfo.Utc);
        return vm;
    }

    [TestMethod]
    public void Tick_formats_time_and_date_with_locale_defaults()
    {
        var vm = NewVm();

        vm.Tick(Instant);

        Assert.AreEqual("2:26:42 PM", vm.TimeText);
        Assert.AreEqual("Tuesday, June 16, 2026", vm.DateText);
    }

    [TestMethod]
    public void Hovering_swaps_to_the_hover_format_pair()
    {
        var vm = NewVm();
        vm.Tick(Instant);

        vm.IsHovering = true;

        Assert.AreEqual("14:26:42", vm.TimeText);
        Assert.AreEqual("2026-06-16 14:26:42Z", vm.DateText);
    }

    [TestMethod]
    public void Leaving_hover_restores_the_normal_pair()
    {
        var vm = NewVm();
        vm.Tick(Instant);

        vm.IsHovering = true;
        vm.IsHovering = false;

        Assert.AreEqual("2:26:42 PM", vm.TimeText);
        Assert.AreEqual("Tuesday, June 16, 2026", vm.DateText);
    }

    [TestMethod]
    public void SetFormats_applies_a_custom_time_format()
    {
        var vm = NewVm();

        vm.SetFormats("HH:mm", null, null, null);
        vm.Tick(Instant);

        Assert.AreEqual("14:26", vm.TimeText);
    }

    [TestMethod]
    public void SetFormats_falls_back_to_default_for_an_invalid_format()
    {
        var vm = NewVm();

        vm.SetFormats("h", null, null, null);
        vm.Tick(Instant);

        Assert.AreEqual("2:26:42 PM", vm.TimeText);
    }

    [TestMethod]
    public void SetFormats_applies_a_custom_hover_format_pair()
    {
        var vm = NewVm();

        vm.SetFormats(null, null, "HH:mm", null);
        vm.Tick(Instant);
        vm.IsHovering = true;

        Assert.AreEqual("14:26", vm.TimeText);
        // The hover date still uses its default "u" UTC stamp.
        Assert.AreEqual("2026-06-16 14:26:42Z", vm.DateText);
    }

    [TestMethod]
    public void SetLocale_reformats_the_last_instant()
    {
        var vm = NewVm();
        vm.Tick(Instant);

        vm.SetLocale(CultureInfo.GetCultureInfo("fr-FR"));

        Assert.AreEqual("mardi 16 juin 2026", vm.DateText);
    }

    [TestMethod]
    public void SetTimeZone_reformats_the_last_instant()
    {
        var vm = NewVm();
        vm.Tick(Instant);

        vm.SetTimeZone(TimeZoneInfo.CreateCustomTimeZone("m5", TimeSpan.FromHours(-5), "m5", "m5"));

        Assert.AreEqual("9:26:42 AM", vm.TimeText);
    }

    [TestMethod]
    public void ApplyAppearance_sets_a_solid_brush_from_derived_color()
    {
        var vm = new DateTimeWidgetViewModel();

        vm.ApplyAppearance("#0F121D", 100);

        var brush = Assert.IsInstanceOfType<Avalonia.Media.SolidColorBrush>(vm.CardBackground);
        Assert.AreEqual(Avalonia.Media.Color.Parse("#FF0F121D"), brush.Color);
    }

    [TestMethod]
    public void ApplyFont_sets_family_scale_weight_and_scaled_size()
    {
        var viewModel = new DateTimeWidgetViewModel();

        viewModel.ApplyFont(WidgetFontProfile.Resolve("Cascadia Code", 150, WidgetFontWeight.Bold));

        Assert.AreEqual("Cascadia Code", viewModel.FontFamily);
        Assert.AreEqual(1.5, viewModel.FontScale, 1e-9);
        Assert.AreEqual(FontWeight.SemiBold, viewModel.ClockWeight); // Bold preset clock = 600
        Assert.AreEqual(640 * 1.5, viewModel.ScaledWidth, 1e-9);
        Assert.AreEqual(176 * 1.5, viewModel.ScaledHeight, 1e-9);
    }

    [TestMethod]
    public void DateTime_font_defaults_reproduce_todays_look()
    {
        var viewModel = new DateTimeWidgetViewModel();

        Assert.AreEqual(WidgetFontProfile.BundledInter, viewModel.FontFamily);
        Assert.AreEqual(1.0, viewModel.FontScale, 1e-9);
        Assert.AreEqual(FontWeight.Medium, viewModel.ClockWeight);
        Assert.AreEqual(640.0, viewModel.ScaledWidth, 1e-9);
        Assert.AreEqual(176.0, viewModel.ScaledHeight, 1e-9);
    }
}
