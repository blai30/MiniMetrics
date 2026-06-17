# Administrator rights and startup

MiniMetrics runs as a normal application most of the time. It only asks for administrator rights when you turn on a metric that genuinely needs them: **CPU temperature** and **CPU power**. This page explains why, what to expect, why the approach is the correct one rather than just a convenient one, and how it compares to other tools you may already use.

## Why CPU temperature and power need administrator rights

Different sensors are read in different ways:

- **CPU usage, RAM, GPU usage, GPU temperature, GPU power, and VRAM** come from normal Windows and graphics-vendor interfaces. They need no special permissions.
- **CPU temperature and CPU power** are only available by reading the processor's internal model-specific registers (MSRs). Windows only allows that through a small kernel-level driver, and Windows only lets a program load that driver when it is running with administrator rights.

This is not a MiniMetrics quirk. It is how the underlying [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) library, and every other CPU-sensor tool, has to work.

## What you will see

- **By default, CPU temperature and CPU power are turned off.** A fresh install never asks for administrator rights.
- **When you turn either one on,** Windows shows one User Account Control (UAC) prompt asking to restart MiniMetrics with administrator rights.
  - **Accept** and MiniMetrics restarts elevated and starts showing the readings.
  - **Decline** and the option simply stays off. MiniMetrics keeps running normally, and CPU temperature/power show a muted dash (`—`) instead of a value.
- **If you have not granted rights,** those two readings show the `—` placeholder. Everything else keeps working.

You can leave CPU temperature and power off and use MiniMetrics entirely without administrator rights.

## Why this approach is correct, not just convenient

Restarting the app to gain rights can look like a shortcut. It is actually the only mechanism Windows supports.

Windows does not let a program that is already running raise its own permission level. The single supported way for an app to gain administrator rights is to start a brand-new copy of itself through the UAC prompt and close the old one. So "restart elevated" is not a workaround MiniMetrics invented; it is the documented Windows behavior, and it is what every well-behaved tool does.

The same reasoning explains the two different ways MiniMetrics can start with Windows:

- A normal startup entry (the kind shown in Task Manager's Startup tab) is always launched by Windows without administrator rights. An app that needed elevation from such an entry would silently fail to start.
- A scheduled task set to run with the highest privileges at logon is the one and only way Windows allows an app to start elevated automatically without prompting every single time.

That is why MiniMetrics uses an ordinary startup entry when no privileged metric is enabled, and switches to a highest-privileges scheduled task when CPU temperature or power is on.

## Seamless startup

If you enable **Run at startup** together with CPU temperature or power, MiniMetrics starts automatically and silently elevated at every logon, with no prompt.

This is why the experience feels seamless in normal use: you grant rights once, when you first turn the metric on, and after that the scheduled task starts MiniMetrics elevated at every logon without ever prompting again. The only time you would see the prompt come back is if you launch MiniMetrics by double-clicking the program directly instead of letting it start with Windows. If you run it from startup, as most people do, that one initial grant is the last prompt you see.

## How this compares to HWiNFO and PowerToys

MiniMetrics deliberately follows the same approach as well-established Windows tools:

| Behavior | MiniMetrics | HWiNFO | PowerToys |
| --- | --- | --- | --- |
| Runs as a normal user by default | Yes | Yes | Yes |
| Needs administrator rights for CPU sensors / privileged features | Yes (CPU temp and power) | Yes (full sensor access) | Yes (some modules) |
| Gains rights by restarting elevated through a UAC prompt | Yes | Yes | Yes |
| Starts silently elevated at logon via a highest-privileges scheduled task | Yes | Yes (Auto Start) | Yes (run elevated at startup) |
| Can run with reduced features without administrator rights | Yes | Yes | Yes |

If you already run HWiNFO or PowerToys from startup, MiniMetrics behaves the same way: one grant, then silent.

## Could MiniMetrics avoid running elevated at all?

There is one alternative design worth mentioning, because it is the approach a security purist would reach for first.

Instead of elevating the whole app, MiniMetrics could keep its window running as a normal user forever and install a separate small background service that runs with administrator rights, loads the driver, and hands the readings to the app. HWiNFO offers a variation of this with its sensor service.

MiniMetrics intentionally does not do this. That design roughly triples the moving parts: a second program to install and uninstall, a channel for the two to talk to each other, and the service itself still needs administrator rights to install in the first place. For a lightweight desktop widget, that complexity is not worth it, and it is more than even PowerToys takes on (PowerToys elevates its whole process when a feature needs it, rather than splitting). Keeping the design simple is the deliberate tradeoff.

## What kernel driver this uses

To read CPU package temperature and power, MiniMetrics relies on LibreHardwareMonitor, which in this version uses **PawnIO**, a modern sandboxed kernel driver, rather than the older WinRing0 driver that many monitoring tools historically used. The difference matters:

- WinRing0 exposes broad, unrestricted kernel access to any caller, which made it a popular target for abuse. Microsoft eventually added it to the Windows vulnerable-driver blocklist.
- PawnIO is its security-focused successor. It is a signed kernel driver that only runs small, verified, sandboxed modules for specific hardware-reading tasks. It does not hand out general-purpose kernel read/write access, so it is not the same class of risk, and it is built to coexist with Secure Boot and Memory Integrity.

Two things are worth knowing:

- **MiniMetrics does not install a kernel driver.** It bundles only PawnIO's sandboxed modules and uses them only if the PawnIO driver is already installed on your system (for example, by LibreHardwareMonitor's own installer or PawnIO's setup). If PawnIO is not present, CPU temperature and power simply stay blank. Nothing is forced onto your machine.
- Because the driver is installed and signed independently, an anti-malware or anti-cheat tool sees a known, signed driver (PawnIO), not an unknown driver shipped by this app.

## Secure Boot and Memory Integrity

Secure Boot validates your boot chain and does not block MiniMetrics from running. Memory Integrity (HVCI) and the vulnerable-driver blocklist are stricter, but PawnIO is built to be compatible with them. In the rare case that a hardened configuration blocks the driver, the effect is harmless: CPU temperature and power stay blank, and the rest of MiniMetrics keeps working.

The published builds are also unsigned, so Windows SmartScreen may warn on first launch. That warning is about the executable having no established reputation yet, not about the kernel driver or any malware finding, and it is unrelated to the elevation behavior above.

## Games with kernel-level anti-cheat (MapleStory, Valorant, and similar)

Kernel-level anti-cheats (such as Nexon Game Security used by MapleStory, plus Vanguard, Easy Anti-Cheat, and BattlEye) watch closely for any kernel driver that reads low-level CPU registers. PawnIO is legitimate and signed, which makes it far less likely to be treated as a cheat than WinRing0 was, but behavior varies by anti-cheat, and some are aggressive about blocking any monitoring driver while a protected game is running.

To stay safe:

- If you play games with kernel-level anti-cheat, the simplest choice is to leave CPU temperature and power turned off. With them off, MiniMetrics never touches a kernel driver at all.
- If you do enable them, fully quit MiniMetrics before launching such a game. This is the same precaution commonly recommended even for well-known tools like HWiNFO and MSI Afterburner.
- No tool can guarantee how a particular anti-cheat will react. If protecting a specific game account matters to you, do not enable CPU temperature and power while that game is installed or running.

## A note on security

With CPU temperature and power off (the default), MiniMetrics uses no kernel driver and runs as an ordinary user-mode app. Turning them on relies on the PawnIO driver and runs MiniMetrics elevated, which is a slightly larger attack surface than a normal app. This is inherent to reading CPU temperature and power, and HWiNFO and similar tools share the same property. If you prefer to minimize it, leave these two metrics off; everything else in MiniMetrics works fully without elevation or any driver.
