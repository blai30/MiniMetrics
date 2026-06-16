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
        bool wasEmpty = Rows.Count == 0;
        bool hadGpu = Gpu is not null;

        IReadOnlyList<MetricRow> built = RowBuilder.Build(snapshot);

        // Remove rows that no longer exist (for example GPU and VRAM when no GPU is present).
        for (int i = Rows.Count - 1; i >= 0; i--)
        {
            if (built.All(b => b.Key != Rows[i].Key))
            {
                Rows.RemoveAt(i);
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
            }

            existing.Label = row.Label;
            existing.Value = row.Value;
            existing.Temp = row.Temp;
            existing.TempLevel = row.TempLevel;
            existing.Detail = row.Detail;
            existing.BarPercent = row.BarPercent;
            existing.Color = row.Color;
            existing.IsVisible = _visibility.GetValueOrDefault(row.Key, true);
        }

        // The accessor properties read from Rows, so they only need to re-notify when the set of
        // rows actually changes identity: the first populate, or the GPU appearing/disappearing.
        if (wasEmpty || hadGpu != (Gpu is not null))
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
            row.IsVisible = _visibility.GetValueOrDefault(row.Key, true);
        }
    }

    // Toggles a single metric's visibility and updates the row immediately if present.
    public void SetVisibility(string key, bool visible)
    {
        _visibility[key] = visible;

        var row = Rows.FirstOrDefault(r => r.Key == key);
        if (row is not null)
        {
            row.IsVisible = visible;
        }
    }
}
