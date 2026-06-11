using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DesktopMetrics.Lib;
using DesktopMetrics.Models;

namespace DesktopMetrics.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<MetricRowViewModel> Rows { get; } = new();

    // Reconciles the bound row collection against a freshly built row list,
    // updating existing rows in place so the UI animates smoothly and bindings stay alive.
    public void ApplySnapshot(MetricsSnapshot snapshot)
    {
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
        }
    }
}
