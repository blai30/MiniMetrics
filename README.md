![MiniMetrics clock and metric widgets on the desktop](docs/screenshots/widgets-strip.png)

# MiniMetrics

A mini widget display for performance metrics that sits on your desktop. MiniMetrics keeps live CPU and GPU readouts and a clock floating on your screen, always within view.

![Latest release](https://img.shields.io/github/v/release/blai30/MiniMetrics)
![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078D6)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)

## Requirements

- Windows 10 or 11, 64-bit (x64)
- The framework-dependent builds require the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). The self-contained builds need nothing extra.
- For full hardware sensor access (for example CPU temperature and power), the app needs administrator rights and the [PawnIO](https://pawnio.eu/) driver. It asks for elevation only when you enable those metrics, points you to the driver if it is missing, and can start silently elevated at logon. See [Administrator rights and startup](docs/elevation-and-startup.md) for details.

## Features

- Floating CPU and GPU widgets with live readouts such as temperature, usage, power, and RAM/VRAM, powered by [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
- Temperature color coding with thresholds, including a critical level for the GPU
- Date and time widget with timezone selection and a timezone offset shown on hover
- Per-metric visibility toggles for CPU, RAM, GPU, and VRAM, so you show only what you care about
- Customizable background color and opacity, with an always-on-top display
- Edge snapping: widgets snap flush to screen edges and to each other
- Optional run at Windows startup
- System tray controls to show and hide widgets
- Remembers each widget's position and visibility between sessions
- Low footprint, with periodic memory trimming

## Download

Grab the newest build from the [latest release](https://github.com/blai30/MiniMetrics/releases/latest). The installer is the easiest path and updates itself in place; the portable downloads remain for anyone who prefers a loose exe.

| Download (suffix) | Pick this if |
| --- | --- |
| `-win-x64-sc-Setup.exe` | Recommended. Installs MiniMetrics, updates itself in place, and uninstalls cleanly. Self-contained, no .NET install needed. |
| `-win-x64-fd-Setup.exe` | Same installer, smaller, but requires the .NET 10 Desktop Runtime already installed. |
| `-selfcontained.exe` | Portable. One file, no .NET install needed (largest download). |
| `-selfcontained.zip` | Portable. Same, but a smaller download. Unzip the folder and run `MiniMetrics.exe`. |
| `-framework-dependent.exe` | Portable. You already have the .NET 10 Desktop Runtime. One lean file. |
| `-framework-dependent.zip` | Portable. Same, smallest download. Unzip and run. |
| `-debug-symbols.zip` | Only needed for diagnosing crash reports. |

The builds and the installer are unsigned, so Windows SmartScreen may warn on first launch. Choose "More info" then "Run anyway".

## Tech stack

- [Avalonia](https://avaloniaui.net/) 12 for the cross-platform UI toolkit
- C# on .NET 10
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) for MVVM
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) for hardware sensors

## Screenshots

CPU and GPU widgets:

| CPU | GPU |
| --- | --- |
| ![CPU widget](docs/screenshots/cpu-metrics.webp) | ![GPU widget](docs/screenshots/gpu-metrics.webp) |

Clock widget:

![Clock widget](docs/screenshots/clock-widget.webp)

Settings, with background color, opacity, time zone, and per-metric visibility:

![Settings window](docs/screenshots/settings-window.webp)
