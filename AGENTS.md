Include ..\AGENTS.md

# Wonder Automation — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `wonderautomation`
- **Namespace:** `Calloatti.WonderAutomation`
- **ModId:** `Calloatti.WonderAutomation`
- **Framework:** Bindito DI
- **Min Game Version:** 1.0.12.5 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Adds automation support to Wonders. Decorates `Wonder` with `WonderLaunchTerminal` (a second automation pin), plus a UI fragment to launch the wonder via automation network signals on rising edge.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `WonderAutomation.cs` | `WonderLaunchTerminal`, `WonderLaunchTerminalFragment`, `WonderAutomationConfigurator` |

## Version Folders
- `Version-1.0` — targets game 1.0.x.x
- `Version-1.1` — targets game 1.1.x.x
