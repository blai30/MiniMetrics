using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MiniMetrics.Lib;
using MiniMetrics.Models;

namespace MiniMetrics.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly Dictionary<string, bool> _visibility = new();

    public ObservableCollection<MetricRowViewModel> Rows { get; } = new();

    // Named accessors onto the four fixed metrics so the two-column layout can bind each slot
    // directly. Rows stays the source of truth; these reflect whatever it currently holds.
    public MetricRowViewModel? Cpu => Rows.FirstOrDefault(r => r.Key == "cpu");
    public MetricRowViewModel? Ram => Rows.FirstOrDefault(r => r.Key == "ram");
    public MetricRowViewModel? Gpu => Rows.FirstOrDefault(r => r.Key == "gpu");
    public MetricRowViewModel? Vram => Rows.FirstOrDefault(r => r.Key == "vram");

    // Drives the right column and the divider: both collapse when no NVIDIA GPU is present.
    public bool HasGpu => Gpu is not null;

    [ObservableProperty]
    private IBrush _cardBackground = Brushes.Transparent;

    // Recomputes the card's solid background color from a base color and opacity.
    public void ApplyAppearance(string backgroundColor, int opacity)
    {
        string color = AppearanceColor.Derive(backgroundColor, opacity);
        CardBackground = new SolidColorBrush(Color.Parse(color));
    }

    // Reconciles the bound row collection against a freshly built row list,
    // updating existing rows in place so the UI animates smoothly and bindings stay alive.
    public void ApplySnapshot(MetricsSnapshot snapshot)
    {
        IReadOnlyList<MetricRow> built = RowBuilder.Build(snapshot);
        bool membershipChanged = false;

        // Remove rows that no longer exist (a device released or absent drops its rows).
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

        // The accessor properties read from Rows, so they only need to re-notify when the set of
        // rows actually changes identity: a row appearing or disappearing.
        if (membershipChanged)
        {
            OnPropertyChanged(nameof(Cpu));
            OnPropertyChanged(nameof(Ram));
            OnPropertyChanged(nameof(Gpu));
            OnPropertyChanged(nameof(Vram));
            OnPropertyChanged(nameof(HasGpu));
        }
    }

    // Loads the initial visibility map from persisted settings. Applies to any rows already present.
    public void LoadVisibility(IDictionary<string, bool> map)
    {
        foreach (var pair in map)
        {
            _visibility[pair.Key] = pair.Value;
        }

        foreach (var row in Rows)
        {
            ApplyVisibility(row);
        }
    }

    // Toggles a single metric's visibility and updates the affected row element immediately.
    public void SetVisibility(string key, bool visible)
    {
        _visibility[key] = visible;

        // The key identifies a single metric; find whichever row owns it and reapply.
        string owner = key.Split('.')[0];
        var row = Rows.FirstOrDefault(r => r.Key == owner);
        if (row is not null)
        {
            ApplyVisibility(row);
        }
    }

    // Maps the seven per-metric visibility keys onto a row's element-level flags. Compute cards
    // (CPU, GPU) toggle individual elements so hiding one does not reflow the rest; memory cards
    // (RAM, VRAM) are a single metric, so the whole card collapses via IsVisible.
    private void ApplyVisibility(MetricRowViewModel row)
    {
        bool Visible(string key) => _visibility.GetValueOrDefault(key, true);

        switch (row.Key)
        {
            case "cpu":
                row.UsageVisible = Visible("cpu.usage");
                row.TempVisible = Visible("cpu.temp");
                row.PowerVisible = Visible("cpu.power");
                break;
            case "gpu":
                row.UsageVisible = Visible("gpu.usage");
                row.TempVisible = Visible("gpu.temp");
                row.PowerVisible = Visible("gpu.power");
                break;
            case "ram":
                row.IsVisible = Visible("ram.usage");
                break;
            case "vram":
                row.IsVisible = Visible("vram.usage");
                break;
        }
    }
}
