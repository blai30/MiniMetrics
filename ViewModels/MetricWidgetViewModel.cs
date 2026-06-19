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
public partial class MetricWidgetViewModel : ObservableObject, IWidgetAppearance
{
    private readonly string _computeKey;
    private readonly string _memoryKey;

    // A read-only view onto the single visibility map (Settings.Visibility); the widget never owns a
    // copy, so what it renders cannot drift from what drives device polling.
    private IReadOnlyDictionary<string, bool> _visibility = new Dictionary<string, bool>();

    public MetricWidgetViewModel(string computeKey, string memoryKey)
    {
        _computeKey = computeKey;
        _memoryKey = memoryKey;
    }

    public ObservableCollection<MetricRowViewModel> Rows { get; } = new();

    // Named slots the single-column layout binds: the compute card and the memory card. They read
    // from Rows, so they re-notify only when membership changes.
    public MetricRowViewModel? Compute => Rows.FirstOrDefault(r => r.Key == _computeKey);
    public MetricRowViewModel? Memory => Rows.FirstOrDefault(r => r.Key == _memoryKey);

    // True while this widget has any row to show. The GPU widget reports false when no GPU is
    // present, which the app uses to keep that window hidden.
    public bool HasContent => Rows.Count > 0;

    [ObservableProperty]
    private IBrush _cardBackground = Brushes.Transparent;

    // Drives the widget window between its full two-card layout and the single-line compact layout.
    [ObservableProperty]
    private bool _isCompact;

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
        foreach (MetricRowViewModel row in Rows)
        {
            row.NotifyThemeChanged();
        }
    }

    // Reconciles the bound row collection against the freshly built rows this widget owns, updating
    // existing rows in place so bindings stay alive and the UI animates smoothly.
    public void ApplySnapshot(MetricsSnapshot snapshot)
    {
        List<MetricRow> built = RowBuilder.Build(snapshot)
            .Where(r => r.Key == _computeKey || r.Key == _memoryKey)
            .ToList();
        bool membershipChanged = false;

        // Remove rows that no longer exist (the GPU widget drops both rows when the GPU is gone).
        for (int i = Rows.Count - 1; i >= 0; i--)
        {
            if (built.All(b => b.Key != Rows[i].Key))
            {
                Rows.RemoveAt(i);
                membershipChanged = true;
            }
        }

        // Add or update rows, keeping them in the order produced by the builder.
        for (int i = 0; i < built.Count; i++)
        {
            MetricRow row = built[i];
            MetricRowViewModel? existing = Rows.FirstOrDefault(r => r.Key == row.Key);
            if (existing is null)
            {
                existing = new MetricRowViewModel { Key = row.Key };
                Rows.Insert(i < Rows.Count ? i : Rows.Count, existing);
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

        if (membershipChanged)
        {
            OnPropertyChanged(nameof(Compute));
            OnPropertyChanged(nameof(Memory));
            OnPropertyChanged(nameof(HasContent));
        }
    }

    // Binds the widget to the live visibility map. The widget reads from it directly rather than
    // copying, so a later change to the map is reflected on the next RefreshVisibility or snapshot.
    public void BindVisibility(IReadOnlyDictionary<string, bool> visibility)
    {
        _visibility = visibility;

        foreach (var row in Rows)
        {
            ApplyVisibility(row);
        }
    }

    // Re-applies the bound visibility to the row owning this key, if this widget owns it. Called after
    // the shared map has changed.
    public void RefreshVisibility(string key)
    {
        string owner = key.Split('.')[0];
        var row = Rows.FirstOrDefault(r => r.Key == owner);
        if (row is not null)
        {
            ApplyVisibility(row);
        }
    }

    // Maps per-metric visibility keys onto a row's element-level flags. Compute cards (CPU, GPU)
    // toggle individual elements so hiding one does not reflow the rest; memory cards (RAM, VRAM)
    // are a single metric, so the whole card collapses via IsVisible.
    private void ApplyVisibility(MetricRowViewModel row)
    {
        bool Visible(string key) => _visibility.GetValueOrDefault(key, true);

        IReadOnlyList<MetricEntry> metrics = MetricRegistry.ForCard(row.Key);

        // A single-metric card (RAM, VRAM) collapses as a whole; a compute card (CPU, GPU) keeps its
        // slots in place and toggles each element by its metric key suffix.
        if (metrics.Count == 1)
        {
            row.IsVisible = Visible(metrics[0].Key);
            return;
        }

        foreach (MetricEntry metric in metrics)
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
