using System;
using System.Collections.Generic;
using System.Linq;

namespace MiniMetrics.Lib;

// One configurable metric: its persisted visibility key, the card it renders on, the label shown in
// settings, and whether reading it needs the elevated kernel driver.
public sealed record MetricEntry(string Key, string Card, string Label, bool RequiresElevation)
{
    // The element suffix after the "card." prefix ("cpu.usage" -> "usage"), used to map a metric onto a
    // row's element-level visibility flag. Computed once because the registry is built at startup.
    public string Element { get; } = Key[(Card.Length + 1)..];
}

// The single definition of every configurable metric. Visibility seeding, the settings toggles, the
// legacy-key migration, and the elevation check all derive from this list instead of restating the
// keys. Adding a configurable metric here is enough for all of them; only the widget rendering
// (RowBuilder, MetricRowViewModel, the row template) stays per-metric.
public static class MetricRegistry
{
    public static IReadOnlyList<MetricEntry> All { get; } = new MetricEntry[]
    {
        new("cpu.usage", "cpu", "Usage", false),
        new("cpu.temp", "cpu", "Temperature", true),
        new("cpu.power", "cpu", "Power", true),
        new("ram.usage", "ram", "Usage", false),
        new("gpu.usage", "gpu", "Usage", false),
        new("gpu.temp", "gpu", "Temperature", false),
        new("gpu.power", "gpu", "Power", false),
        new("vram.usage", "vram", "Usage", false)
    };

    // The distinct card keys, in declaration order. These double as the legacy whole-card keys that
    // predate per-metric visibility.
    public static IReadOnlyList<string> Cards { get; } =
        All.Select(entry => entry.Card).Distinct().ToList();

    // Metrics grouped by card, materialized once so the per-frame render path can look a card's metrics
    // up without re-scanning or allocating.
    private static readonly Dictionary<string, MetricEntry[]> MetricsByCard =
        All.GroupBy(entry => entry.Card).ToDictionary(group => group.Key, group => group.ToArray());

    // The metrics that belong to a card, in declaration order.
    public static IReadOnlyList<MetricEntry> ForCard(string card) =>
        MetricsByCard.TryGetValue(card, out var metrics) ? metrics : Array.Empty<MetricEntry>();

    // True when at least one of the card's metrics is visible. Absent keys default to visible, matching
    // the render path: a fresh install with an empty visibility map shows every metric.
    public static bool AnyVisible(string card, IReadOnlyDictionary<string, bool> visibility) =>
        ForCard(card).Any(entry => visibility.GetValueOrDefault(entry.Key, true));

    // Elevation is required only for an elevation-flagged metric the user has explicitly turned on.
    // Absent keys default to false, so a fresh install (empty visibility map) never requires elevation
    // and never prompts at first launch.
    public static bool RequiresElevation(IReadOnlyDictionary<string, bool> visibility) =>
        All.Where(entry => entry.RequiresElevation)
            .Any(entry => visibility.GetValueOrDefault(entry.Key, false));
}
