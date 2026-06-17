using System.Collections.Generic;
using System.Linq;

namespace MiniMetrics.Lib;

// One configurable metric: its persisted visibility key, the card it renders on, the label shown in
// settings, and whether reading it needs the elevated kernel driver.
public sealed record MetricEntry(string Key, string Card, string Label, bool RequiresElevation);

// The single definition of every configurable metric. Visibility seeding, the settings toggles, the
// legacy-key migration, and the elevation check all derive from this list instead of restating the
// keys. Adding a configurable metric here is enough for all of them; only the widget rendering
// (RowBuilder, MetricRowViewModel, the row template) stays per-metric.
public static class MetricRegistry
{
    public static IReadOnlyList<MetricEntry> All { get; } = new MetricEntry[]
    {
        new("cpu.usage",  "cpu",  "Usage",       false),
        new("cpu.temp",   "cpu",  "Temperature", true),
        new("cpu.power",  "cpu",  "Power",       true),
        new("ram.usage",  "ram",  "Usage",       false),
        new("gpu.usage",  "gpu",  "Usage",       false),
        new("gpu.temp",   "gpu",  "Temperature", false),
        new("gpu.power",  "gpu",  "Power",       false),
        new("vram.usage", "vram", "Usage",       false),
    };

    // The distinct card keys, in declaration order. These double as the legacy whole-card keys that
    // predate per-metric visibility.
    public static IReadOnlyList<string> Cards { get; } =
        All.Select(entry => entry.Card).Distinct().ToList();

    // The metrics that belong to a card, in declaration order.
    public static IEnumerable<MetricEntry> ForCard(string card) =>
        All.Where(entry => entry.Card == card);
}
