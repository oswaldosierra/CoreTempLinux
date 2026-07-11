# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

CoreTempLinux is a Linux-only GTK4 desktop app (C# / .NET 10) that displays live CPU
temperature, per-core frequency and load, and other hwmon sensors, with a configurable
temperature alert (visual banner + desktop notification + looping audio) and a
system-tray icon showing the temperature as a number. Its UI mirrors the Windows "Core
Temp" utility: a CPU info panel, a large colour-coded temperature readout, and session
Min/Max. Note that on AMD/Linux `k10temp` exposes only a single `Tctl` value (no per-core
temperatures and no Tj.Max), so those Core Temp elements appear only on Intel `coretemp`.

The UI text and code comments are in Spanish; keep that convention when editing.

## Commands

```bash
dotnet build            # build (targets net10.0, linux-x64, framework-dependent)
dotnet run              # build and launch the GTK window
dotnet publish -c Release
dotnet test tests/CoreTempLinux.Tests   # run the xUnit unit tests
```

There is no linter config or CI. `RuntimeIdentifier` is pinned to `linux-x64` and
`SelfContained=false`, so a .NET 10 runtime must be installed to run.

Unit tests live in `tests/CoreTempLinux.Tests` (xUnit). They cover the GTK-free logic
(`AlertStateMachine`, `HwmonReader`, `SensorMonitor`, `CpuLoad`, `CpuFrequency`, `CpuInfo`,
`LinuxFileSystem`, `ConsoleAppLogger`, `TempScale`, `TrayIconRenderer`) by injecting a
`FakeFileSystem` and stub readers, so no `/sys` or `/proc` access is needed. The main `.csproj` excludes `tests/**` from its own
compilation (`<Compile Remove>`), and the test project references it via `ProjectReference`.

## Architecture

Data flows one direction on a 1-second GLib timer: **sensor readers → `SensorMonitor` →
`Snapshot` → `MainWindow`**.

Dependencies are wired by constructor injection from the **composition root in
`Program.cs`** — it is the only place that `new`s concrete types. Components depend on
interfaces (`IFileSystem`, `IHwmonReader`, `ICpuFrequencyReader`, `ICpuLoadReader`,
`ISensorMonitor`, `IAudioAlert`, `INotifier`, `ITrayIcon`, `IAppLogger`), so any of them
can be swapped or faked without touching the rest.

- **`Diagnostics/IAppLogger`** + `ConsoleAppLogger` — the logging seam. Failures are
  logged, never swallowed silently. Min level defaults to `Info`; set `CORETEMP_LOG=debug`
  to see per-read degradations.
- **`Sensors/IFileSystem`** + `LinuxFileSystem` — the single choke point for all `/sys`
  and `/proc` reads. It never throws: *expected* failures (missing sensor, no permission,
  device I/O error) log at Debug; *unexpected* ones log at Warning once per path (to avoid
  flooding the per-second loop). Every sensor reader depends on this, not on `File`/`Directory`.

- **`Sensors/HwmonReader`** (`IHwmonReader`) — the generic layer. Scans `/sys/class/hwmon/*`,
  reading `temp*_input`, `power*_input`, `fan*_input`, `freq*_input`, `in*_input` (voltage),
  converting raw kernel values by a per-kind divisor into `SensorReading` records. All disk
  access goes through the injected `IFileSystem`, so it needs no try/catch of its own —
  missing sensors degrade to empty/null there.
- **`Sensors/SensorMonitor`** — orchestrator. Calls the readers each `Collect()`, picks the
  primary CPU temperature (prefers a `Tctl`/`Tdie`/`Package` label from the known CPU chips
  `k10temp`/`coretemp`/`zenpower`, else the hottest core), tracks session min/max, and
  splits non-CPU sensors into `Snapshot.ExtraSensors`. It also tracks per-sensor session
  Min/Max (`Snapshot.CoreTempStats`, one entry per CPU temp label) and the package frequency
  (`Snapshot.PackageFreqMhz`, the mean of readable per-core freqs). This is the single source
  of truth the UI consumes.
- **`Sensors/CpuFrequency`** reads `/sys/devices/system/cpu/cpuN/cpufreq/scaling_cur_freq`
  (NaN per core if unreadable). **`Sensors/CpuLoad`** computes per-core % from deltas
  between two `/proc/stat` samples — **the first `ReadPercent()` call returns all zeros**
  because there is no prior sample. **`Sensors/CpuInfo`** reads model, vendor,
  family/model/stepping, physical vs logical cores, socket count and max frequency (from
  cpufreq) once at startup — the data behind the Core Temp-style CPU info panel.
- **`Ui/MainWindow`** builds all widgets once in the constructor, then only mutates their
  values in `Refresh()` (which wraps `RefreshCore()` in try/catch so a transient error can't
  kill the GLib timer). Its collaborators (`ISensorMonitor`, `IAudioAlert`, `INotifier`,
  `AlertStateMachine`, `IAppLogger`) all arrive by constructor. Core rows are fixed at
  `_monitor.CoreCount`; the "otros sensores" box is torn down and rebuilt each tick because
  that set can change.
- **`Ui/AudioAlert`** (`IAudioAlert`) shells out to `paplay`/`pw-play`/`ffplay` on a
  freedesktop sound file; it refuses to overlap playback, so calling `Play()` every second
  yields a continuous tone. **`Ui/GtkNotifier`** (`INotifier`) wraps `Gio.Notification`.
- **`Ui/DBusTrayIcon`** (`ITrayIcon`) shows the CPU temperature as a number in the system
  tray, at the look of Core Temp. It implements the freedesktop `StatusNotifierItem` spec
  over D-Bus with **Tmds.DBus** (GTK4 dropped `StatusIcon`; AppIndicator is GTK3-only). The
  icon bitmap is drawn by **`Ui/TrayIconRenderer`** — a dependency-free 5×7 bitmap font that
  emits an ARGB32 pixmap, so it is deterministic and unit-tested. Registration is async and
  never throws: with no session bus or no `StatusNotifierWatcher` it logs a Warning and no-ops
  (`NullTrayIcon`). On **GNOME the tray needs the "AppIndicator and KStatusNotifierItem
  Support" extension**; KDE and others host it natively.
- **`Ui/TempScale`** is the single source of truth for temperature colour: `Classify(temp,
  crit)` → `TempLevel`, then `CssClass()` for the window and `Rgb()` for the tray icon, so
  both always agree. If the critical (Tj.Max) is known it grades by proximity to it, else by
  absolute thresholds (common on AMD/Linux, where `k10temp` exposes neither per-core temps
  nor a crit).

### Alert episode model (`Alerts/AlertStateMachine`)

Alerts work in *episodes*, not per-tick. The episode logic lives in the GTK-free
`AlertStateMachine`: `Evaluate(temp, threshold)` returns an `AlertPhase`
(`Idle`/`Started`/`Active`/`Ended`) and `Silence()` mutes the current episode.
`MainWindow.EvaluateAlert` only translates that phase to banner + sound + notification.
Crossing the threshold starts one episode (notification fires once, sound loops); Silence
mutes only the current episode; dropping below the threshold ends it and re-arms silencing
so the next crossing alerts again.

## Conventions

- Sensor readers must never throw — any `/sys` or `/proc` read failure degrades gracefully
  (empty list, `NaN`, or `null`), since available sensors differ across hardware. Do the
  reads through `IFileSystem` rather than adding try/catch in the reader; that is where the
  degradation and logging live.
- GLib timer callbacks are stored in a field (`_tick`) to prevent GC collection while
  registered — do the same for any new persistent callbacks.
- Values passed to GTK markup go through `Escape()`; raw kernel units are divided to
  human units inside `HwmonReader.Add`, not in the UI.
