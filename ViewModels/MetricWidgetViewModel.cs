using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniMetrics.Lib;
using MiniMetrics.Models;

namespace MiniMetrics.ViewModels;

// One standalone metrics widget: a compute card stacked over its memory card. Constructed with the
// two keys it owns (the CPU widget owns "cpu"+"ram", the GPU widget owns "gpu"+"vram") and ignores
// every other row the builder produces.
public partial class MetricWidgetViewModel(string computeKey, string memoryKey)
    : ObservableObject, IWidgetDisplay
{
    // A read-only view onto the single visibility map (Settings.Visibility); the widget never owns a
    // copy, so what it renders cannot drift from what drives device polling.
    private IReadOnlyDictionary<string, bool> _visibility = new Dictionary<string, bool>();

    public ObservableCollection<MetricRowViewModel> Rows { get; } = [];

    // Named slots the single-column layout binds: the compute card and the memory card. They read
    // from Rows, so they re-notify only when membership changes.
    public MetricRowViewModel? Compute => Rows.FirstOrDefault(r => r.Key == computeKey);
    public MetricRowViewModel? Memory => Rows.FirstOrDefault(r => r.Key == memoryKey);

    // True while this widget has any row to show. The GPU widget reports false when no GPU is
    // present, which the app uses to keep that window hidden.
    public bool HasContent => Rows.Count > 0;

    [ObservableProperty] public partial IBrush CardBackground { get; set; } = Brushes.Transparent;

    // Drives the widget window between its full two-card layout and the single-line compact layout.
    [ObservableProperty] public partial bool IsCompact { get; set; }

    // The widget's natural size at 100% scale; the window binds ScaledWidth/ScaledHeight so it grows
    // and shrinks with the font.
    private const double BaseWidth = 210;
    private const double BaseHeight = 176;

    [ObservableProperty] public partial FontFamily FontFamily { get; set; } = new(WidgetStyleProfile.BundledInter);
    [ObservableProperty] public partial double Scale { get; set; } = 1.0;
    [ObservableProperty] public partial double ScaledWidth { get; set; } = BaseWidth;
    [ObservableProperty] public partial double ScaledHeight { get; set; } = BaseHeight;
    [ObservableProperty] public partial FontWeight StrongWeight { get; set; } = FontWeight.Bold;
    [ObservableProperty] public partial FontWeight UnitWeight { get; set; } = FontWeight.SemiBold;

    // Recomputes the card's solid background color from a base color and opacity.
    public void ApplyAppearance(string backgroundColor, int opacity)
    {
        string color = AppearanceColor.Derive(backgroundColor, opacity);
        CardBackground = new SolidColorBrush(Color.Parse(color));
    }

    // Re-raises accent notifications on every row so the theme-aware converters re-run after a theme
    // change. Called by App when the resolved variant changes.
    public void RefreshThemeColors()
    {
        foreach (var row in Rows) row.NotifyThemeChanged();
    }

    // Applies the resolved style to the window-level bindings and stamps each row so the templates,
    // whose DataContext is a row, can bind the scale and weights too.
    public void ApplyStyle(WidgetStyleProfile profile)
    {
        FontFamily = new(profile.FontFamily);
        Scale = profile.Scale;
        ScaledWidth = BaseWidth * profile.Scale;
        ScaledHeight = BaseHeight * profile.Scale;
        StrongWeight = (FontWeight)profile.StrongWeight;
        UnitWeight = (FontWeight)profile.UnitWeight;

        foreach (var row in Rows) StampStyle(row);
    }

    private void StampStyle(MetricRowViewModel row)
    {
        row.Scale = Scale;
        row.StrongWeight = StrongWeight;
        row.UnitWeight = UnitWeight;
    }

    // Reconciles the bound row collection against the freshly built rows this widget owns, updating
    // existing rows in place so bindings stay alive and the UI animates smoothly.
    public void ApplySnapshot(MetricsSnapshot snapshot)
    {
        var built = RowBuilder.Build(snapshot)
            .Where(r => r.Key == computeKey || r.Key == memoryKey)
            .ToList();
        bool membershipChanged = false;

        // Remove rows that no longer exist (the GPU widget drops both rows when the GPU is gone).
        for (int i = Rows.Count - 1; i >= 0; i--)
            if (built.All(b => b.Key != Rows[i].Key))
            {
                Rows.RemoveAt(i);
                membershipChanged = true;
            }

        // Add or update rows, keeping them in the order produced by the builder.
        for (int i = 0; i < built.Count; i++)
        {
            var row = built[i];
            var existing = Rows.FirstOrDefault(r => r.Key == row.Key);
            if (existing is null)
            {
                existing = new() { Key = row.Key };
                Rows.Insert(i < Rows.Count ? i : Rows.Count, existing);
                StampStyle(existing);
                membershipChanged = true;
            }

            existing.Label = row.Label;
            existing.Value = row.Value;
            existing.Temp = row.Temp;
            existing.TempLevel = row.TempLevel;
            existing.Detail = row.Detail;
            existing.BarPercent = row.BarPercent;
            existing.Color = row.Color;
            ApplyVisibility(existing);
        }

        if (!membershipChanged) return;
        OnPropertyChanged(nameof(Compute));
        OnPropertyChanged(nameof(Memory));
        OnPropertyChanged(nameof(HasContent));
    }

    // Binds the widget to the live visibility map. The widget reads from it directly rather than
    // copying, so a later change to the map is reflected on the next RefreshVisibility or snapshot.
    public void BindVisibility(IReadOnlyDictionary<string, bool> visibility)
    {
        _visibility = visibility;

        foreach (var row in Rows) ApplyVisibility(row);
    }

    // Re-applies the bound visibility to the row owning this key, if this widget owns it. Called after
    // the shared map has changed.
    public void RefreshVisibility(string key)
    {
        string owner = key.Split('.')[0];
        var row = Rows.FirstOrDefault(r => r.Key == owner);
        if (row is not null) ApplyVisibility(row);
    }

    // Maps per-metric visibility keys onto a row's element-level flags. Compute cards (CPU, GPU)
    // toggle individual elements so hiding one does not reflow the rest; memory cards (RAM, VRAM)
    // are a single metric, so the whole card collapses via IsVisible.
    private void ApplyVisibility(MetricRowViewModel row)
    {
        bool Visible(string key) => _visibility.GetValueOrDefault(key, true);

        var metrics = MetricRegistry.ForCard(row.Key);

        // A single-metric card (RAM, VRAM) collapses as a whole; a compute card (CPU, GPU) keeps its
        // slots in place and toggles each element by its metric key suffix.
        if (metrics.Count == 1)
        {
            row.IsVisible = Visible(metrics[0].Key);
            return;
        }

        foreach (var metric in metrics)
        {
            bool visible = Visible(metric.Key);
            switch (metric.Element)
            {
                case "usage":
                    row.UsageVisible = visible;
                    break;
                case "temp":
                    row.TempVisible = visible;
                    break;
                case "power":
                    row.PowerVisible = visible;
                    break;
            }
        }
    }
}
