# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

CoreTempLinux is a Linux-only GTK4 desktop app (C# / .NET 10) that displays live CPU
temperature, per-core frequency and load, and other hwmon sensors, with a configurable
temperature alert (visual banner + desktop notification + looping audio). It is the Linux
counterpart to the Windows "Core Temp" utility.

The UI text and code comments are in Spanish; keep that convention when editing.

## Commands

```bash
dotnet build            # build (targets net10.0, linux-x64, framework-dependent)
dotnet run              # build and launch the GTK window
dotnet publish -c Release
```

There is no test project, linter config, or CI. `RuntimeIdentifier` is pinned to
`linux-x64` and `SelfContained=false`, so a .NET 10 runtime must be installed to run.

## Architecture

Data flows one direction on a 1-second GLib timer: **sensor readers → `SensorMonitor` →
`Snapshot` → `MainWindow`**.

- **`Sensors/HwmonReader`** — the generic layer. Scans `/sys/class/hwmon/*`, reading
  `temp*_input`, `power*_input`, `fan*_input`, `freq*_input`, `in*_input` (voltage),
  converting raw kernel values by a per-kind divisor into `SensorReading` records. Every
  filesystem access is wrapped in try/catch and degrades to empty/null — sensors are
  optional and vary by machine.
- **`Sensors/SensorMonitor`** — orchestrator. Calls the readers each `Collect()`, picks the
  primary CPU temperature (prefers a `Tctl`/`Tdie`/`Package` label from the known CPU chips
  `k10temp`/`coretemp`/`zenpower`, else the hottest core), tracks session min/max, and
  splits non-CPU sensors into `Snapshot.ExtraSensors`. This is the single source of truth
  the UI consumes.
- **`Sensors/CpuFrequency`** reads `/sys/devices/system/cpu/cpuN/cpufreq/scaling_cur_freq`
  (NaN per core if unreadable). **`Sensors/CpuLoad`** computes per-core % from deltas
  between two `/proc/stat` samples — **the first `ReadPercent()` call returns all zeros**
  because there is no prior sample. **`Sensors/CpuInfo`** reads model/core count from
  `/proc/cpuinfo` once at startup.
- **`Ui/MainWindow`** builds all widgets once in the constructor, then only mutates their
  values in `Refresh()`. Core rows are fixed at `_monitor.CoreCount`; the "otros sensores"
  box is torn down and rebuilt each tick because that set can change.
- **`Ui/AudioAlert`** shells out to `paplay`/`pw-play`/`ffplay` on a freedesktop sound file;
  it refuses to overlap playback, so calling `Play()` every second yields a continuous tone.

### Alert episode model (`MainWindow.EvaluateAlert`)

Alerts work in *episodes*, not per-tick. Crossing the threshold starts one episode
(notification fires once, sound loops); the Silence button mutes only the current episode;
dropping below the threshold ends the episode and re-arms silencing so the next crossing
alerts again. `_alerting` = episode active; `_silenced` = user muted this episode.

## Conventions

- Sensor readers must never throw — any `/sys` or `/proc` read failure degrades gracefully
  (empty list, `NaN`, or `null`), since available sensors differ across hardware.
- GLib timer callbacks are stored in a field (`_tick`) to prevent GC collection while
  registered — do the same for any new persistent callbacks.
- Values passed to GTK markup go through `Escape()`; raw kernel units are divided to
  human units inside `HwmonReader.Add`, not in the UI.
