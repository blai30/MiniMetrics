# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

This is a living document. As you discover new conventions, gotchas, corrections, or architectural decisions while
working here, add them so future sessions benefit. Keep entries concrete and verified against the code, prune anything
that becomes stale, and avoid restating what the global `~/.claude/CLAUDE.md` already covers.

## What this is

MiniMetrics is a Windows desktop widget app (Avalonia 12, C#, .NET 10): floating, always-on-top CPU/GPU/clock widgets
driven from the system tray. Hardware readings come from LibreHardwareMonitor; CPU temperature and power additionally
need the PawnIO kernel driver and an elevated process. Packaging and in-app updates use Velopack.

## Commands

```powershell
dotnet build                                              # build the whole solution (MiniMetrics.slnx)
dotnet test                                               # run all tests (MSTest)
dotnet test --filter "FullyQualifiedName~MetricRegistryTests"        # one test class
dotnet test --filter "FullyQualifiedName~MetricRegistryTests.RequiresElevation_is_false_for_a_fresh_install"  # one test
dotnet run --project MiniMetrics.csproj                   # run the app locally
```

Release builds (publish, Velopack pack, asset renaming) are produced only by `.github/workflows/release.yml`; do not
hand-build release artifacts unless asked. The version is `0.0.0` for local/dev builds and is overridden by `-p:Version`
from the git tag in the pipeline, so a dev build reads as unreleased rather than a misleading `1.0.0`.

## C# coding style for modern C# versions

- Use null propagation assignment from C# 14.
- Use collection expression from C# 12.

## Architecture

### Layering and the interface-seam pattern

Code is split by responsibility, and platform/IO concerns sit behind interfaces so the logic stays unit-testable:

- `Models/` and `Lib/` are pure C# (no Avalonia, no Win32). `Lib/` holds the real logic: `MetricRegistry`, `RowBuilder`,
  `DeviceActivation`, `ElevationGate`, `EdgeSnap`, `UpdatePolicy`, formatting (`MetricFormatting`, `ClockFormatting`),
  the theming primitives (`ThemePalette`, `ThemeVariantResolver`, `AppearanceColor`), the widget style/scale resolver (`WidgetStyleProfile`), the font picker (`FontCatalog`, `FuzzySearch`), and `AutostartTarget`/`AutostartTaskSecurity`.
- `Services/` defines interfaces (`ISensorSource`, `IElevation`, `IDriverProbe`, `IHardwareTree`, `IStartupOperations`,
  `IReleaseSource`, `ISaveScheduler`, `IUpdateFlow`, `IFontCatalog`) each with a Windows implementation plus a
  `Noop`/`Mock` one. It also holds the coordinators that `App` delegates decisions to: `ElevationCoordinator` (what
  enabling a metric implies given elevation + driver state) and `UninstallCoordinator` (the ordered in-app uninstall).
  `App.axaml.cs` picks the real vs no-op implementation with `OperatingSystem.IsWindows()`, which is also what lets the
  app run on non-Windows for development.
- `ViewModels/`, `Views/`, `Converters/` are the Avalonia MVVM layer (CommunityToolkit.Mvvm, compiled bindings on by
  default).
- Tests use `Fake*` doubles (`FakeHardwareTree`, `FakeReleaseSource`, `FakeSaveScheduler`, `FakeStartupOperations`,
  `FakeFontCatalog`) against those interfaces. When adding a service, add the interface and a fake, and test the pure
  logic rather than the Windows path.

`App.axaml.cs` is the composition root: it wires every service, view model, widget host, tray menu, timer, and the
elevation/update decisions. Read it first to understand how a feature is assembled.

### MetricRegistry is the single source of truth

`Lib/MetricRegistry.cs` lists every configurable metric (`cpu.usage`, `cpu.temp`, `cpu.power`, `ram.usage`, `gpu.usage`,
`gpu.temp`, `gpu.power`, `vram.usage`) with its card, label, and a `RequiresElevation` flag. Visibility seeding, the
settings toggles, legacy-key migration, and the elevation check all derive from this list. Only `cpu.temp` and
`cpu.power` are elevation-flagged. To add a configurable metric, add it here; the only per-metric code that remains is
the widget rendering (`RowBuilder`, `MetricRowViewModel`, the row template).

### Polling pipeline

`MetricsPoller` reads snapshots on a thread-pool loop via `Task.Run` (never on the UI thread; see gotchas) and raises
`SnapshotReady`. `App` marshals that to the UI with `Dispatcher.UIThread.Post`, and the view models reconcile their
`MetricRowViewModel` rows in place rather than rebuilding the collection. `DeviceActivation.Compute` decides which
hardware devices stay active from widget + per-metric visibility, and `ISensorSource.SetActiveDevices` releases any
device whose metrics are all hidden, so nothing hidden is polled or kept resident. `ViewModels/WidgetCoordinator` is the
seam that keeps these in step: a visibility change goes through `WidgetCoordinator.SetMetricVisibility`, which persists,
re-renders the owning widget, and reconciles active devices as one step, and the widget show/hide handlers call
`WidgetCoordinator.ApplyActiveDevices`. Route visibility/activation changes through it rather than calling the three
pieces by hand from `App`.

### Startup sequence (ordering is load-bearing)

`Program.cs` runs steps in a deliberate order: Velopack hooks first (so an install/update/uninstall hook never
relaunches elevated or contends for the mutex), then the elevation gate (`ElevationGate.ShouldRelaunch` relaunches
elevated when a privileged metric is enabled and PawnIO is installed), then the single-instance mutex (`SingleInstance.Acquire`), then Avalonia. Preserve this order when touching startup.

### Elevation and autostart

`app.manifest` requests no elevation; the app relaunches itself elevated on demand. CPU temp/power read model-specific
registers through PawnIO and only work in an elevated process with the driver installed. Autostart has two backends: an
ordinary `HKCU\...\Run` key when unelevated, and a highest-privileges scheduled task (`MiniMetrics\Autostart`) when a
privileged metric is enabled, because a scheduled task is the only way Windows starts an app elevated at logon without
prompting each time. `StartupManager` + `AutostartTarget` + `AutostartTaskSecurity` manage this; tasks are created so
the user's own account can delete them without a prompt. **`docs/elevation-and-startup.md` is the canonical rationale
and user-facing behavior; keep code and that doc in sync.**

### Updates

`UpdateManager.IsInstalled` chooses the `IUpdateFlow` once at startup: `VelopackUpdateFlow` (installed builds update in
place and restart) or `UpdateService` over `GitHubReleaseSource` (portable and dev builds link to the release page;
`UpdateService` implements `IUpdateFlow` directly and its `ApplyAndRestartAsync` throws). `UpdatePolicy.IsDue` gates the
launch-time check by cadence.

### Settings

`SettingsStore` persists JSON under `%APPDATA%\MiniMetrics`. `SettingsController` is the mutation API and debounces
writes through `DispatcherSaveScheduler` (~600 ms); call `Flush()` before relaunching or shutting down. Changes flow
from the settings view model as a `SettingChange` record union (`ViewModels/SettingChange.cs`): each facet is its own
record carrying its payload, so the host pattern-matches on the type instead of reading the value back off the view
model. Adding a setting means adding a record and a match arm, not a new event. Settings also expose per-option
restore-default commands.

### Theming and widget appearance

The app theme is the stock Avalonia Fluent theme (`FluentTheme` in `App.axaml`, from the `Avalonia.Themes.Fluent`
package), not FluentAvalonia. Dialogs and the settings window follow the Windows light/dark theme via Avalonia's
`ActualThemeVariant`, but no longer follow the Windows accent color. The settings window uses a custom shadcn-style look (neutral zinc palette, centered top pill tabs, a centered content column, and flat hairline-divided rows), defined in
`Views/SettingsWindow.axaml` over the `Settings.*` theme brushes in `App.axaml`; the Fluent control accent (`SystemAccentColor` variants in `App.axaml`) is set to neutral grays so switches and sliders render hue-free. The
prompt dialogs (`PawnIoPromptWindow`, `UpdatePromptWindow`, `ConfirmUninstallWindow`) share the zinc look through
`Views/DialogStyles.axaml`: a window gets `Classes="dialog"` plus a `StyleInclude`, title/body text use the
`dialogTitle`/`dialogBody` classes, and the main action uses `Classes="primary"` (solid inverse fill) against ghost
secondary buttons. `Settings.Theme`
(`AppTheme.System`/`Light`/`Dark`) is resolved by `ThemeVariantResolver`, and the card background is stored per variant (`BackgroundColor` for dark, `LightBackgroundColor` for light) so each theme keeps its own color; `ThemePalette` /
`AppearanceColor` derive the card brush and opacity. Widget typeface, weight, and size are one unit:
`WidgetStyleProfile.Resolve` turns `WidgetFontFamily` (null or `"Inter"` maps to the bundled `Avalonia.Fonts.Inter`),
`WidgetScale` (50-200%), and `WidgetFontWeight` into the values widgets bind to. `IWidgetDisplay` pushes appearance (`ApplyAppearance`) and style (`ApplyStyle`) to every widget through one collection. Each widget also has an independent
compact toggle (`CpuCompact` / `GpuCompact` / `DateTimeCompact`); `OverlayWindow`, the shared widget base, owns the
full/compact layout and auto-width lifecycle.

### Widget hosting

Each widget is an `OverlayWindow` wrapped in a `WidgetHost` that owns desktop integration (tool-window styles, z-order,
click-through when locked), position persistence, on-screen recovery, and edge snapping (`EdgeSnap`). The GPU window is
shown reactively once a snapshot confirms a GPU is present.

## Project-specific conventions and gotchas

- **Sensor reads must stay off the UI thread.** Window-drag stutter traced back to the poll loop capturing the UI
  `SynchronizationContext`; `MetricsPoller` uses `Task.Run` and only lightweight `ApplySnapshot` posts reach the UI
  thread. Do not move sensor work onto the dispatcher.
- **LibreHardwareMonitor retains a 1-day value history per sensor.** That is the real memory growth source, not a GC
  problem (GC tuning moved the needle ~2 MB). `UpdateVisitor` sets `sensor.ValuesTimeWindow = TimeSpan.Zero` on every
  update; `Services/MemoryTrimmer.cs` calls native `EmptyWorkingSet` on a timer (once ~10 s after warmup, then every 60
  s, spaced so a trim does not land mid-drag). Keep both.
- **Never enable LibreHardwareMonitor's memory group.** `IsMemoryEnabled` also detects every DIMM and polls its SPD
  thermal sensor over SMBus through PawnIO on each refresh (`MemoryGroup.TryAddDimms`, with a background retry when
  detection fails), sharing the bus with RGB controllers and other hardware tools. `LibreHardwareTree` keeps it off and
  answers RAM reads from `PhysicalMemory.Usage()` (`GlobalMemoryStatusEx`, the same source LHM's "Total Memory" uses).
- **Text over transparent backgrounds.** Subpixel/ClearType antialiasing fringes when compositing over the wallpaper;
  widget windows set `TextOptions.TextRenderingMode="Antialias"` (grayscale). Keep it on overlay windows.
- **Stable number layout.** Numeric rows use `TextElement.FontFeatures="+tnum"` (tabular figures) plus fixed-width slots
  so values do not shift their neighbors as they change.
- **The clock deliberately does not use tabular figures.** They were tried and rejected on looks: Inter's `1` is far
  narrower than its `8`, so `+tnum` pads it out and a time like `11:11:11` renders 73 px wider and visibly loose. The
  clock instead uses `Views/StableWidthTextBlock`, which measures with `+tnum` but draws without it, so each line
  reserves the width its text *could* need while the glyphs keep their natural proportional advances. Without that,
  proportional digits swing the rendered time's width by ~15 px from one second to the next and resize the window on
  every tick in `ClockWidthMode.Auto`. The reserved slack lands on whichever side `TextAlignment` does not pin the text
  to, which is why the compact row pins its two lines to facing edges (the gap between them would otherwise breathe).
- **A content-sized window grows from its right edge.** `SizeToContent.Width` keeps `Position` (the top-left corner)
  fixed, so the clock in `ClockWidthMode.Auto` or compact mode slides sideways whenever its text changes width. That is
  invisible when the text is left-aligned and obvious when it is not, which was issue #25. `DateTimeWindow.OnSizeChanged`
  offsets `Position.X` by `WidthAnchor.AnchoredLeft` so the *aligned* edge is the one that stays planted. The first
  layout (`PreviousSize.Width == 0`) has nothing to anchor against and is skipped.
- **The nested test project is excluded from the main project's globbing** via `DefaultItemExcludes` in
  `MiniMetrics.csproj`; do not remove that line.
- **Timezone/locale-sensitive tests must be deterministic.** Seed explicitly to UTC rather than relying on
  `TimeZoneInfo.Local`, or they fail on UTC CI runners.
- **GPU support is NVIDIA-verified; AMD/Intel are best-effort.** `LibreHardwareTree` detects any of
  `GpuNvidia`/`GpuAmd`/`GpuIntel`, and `HardwareSensorSource` reads NVIDIA sensor names first then falls back to
  AMD/Intel names (`"GPU Hot Spot"`, the generic `"D3D Dedicated Memory"` counters, `"GPU SoC"`/`"GPU PPT"` power
  rails). Only the NVIDIA path is verified against real hardware; the fallbacks are unverified. If you can test on AMD
  or Intel, confirm the actual sensor names and tighten the fallback chains.
- **Crash diagnostics.** `Services/CrashLog.cs` appends unhandled exceptions to `%APPDATA%\MiniMetrics\logs\crash.log`;
  it is wired to `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`, and the `Program.Main` body,
  and is also the catch sink for the `async void` update-check handlers. `SettingsStore.Load` preserves a corrupt
  settings file as `settings.json.bak` before falling back to defaults.

## Working style for this repo

- Non-trivial features follow a spec-first flow: brainstorm, then a design spec under `docs/superpowers/specs/`, then an
  implementation plan under `docs/superpowers/plans/`, then TDD implementation. Those existing docs are a useful record
  of why decisions were made; read the relevant one before changing an established feature (elevation, updates, startup,
  packaging).
- UI work is verified visually in the running app, not just by tests. `.superpowers/capture.ps1` and the brainstorm HTML
  mockups under `.superpowers/` are the screenshot/mockup tooling used for that.
- Favor a lean memory and CPU footprint; it is an explicit product goal and a recurring review point.
