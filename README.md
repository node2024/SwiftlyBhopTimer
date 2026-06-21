# SwiftlyBhopTimer

SwiftlyS2 bhop-focused timer workspace.

- [日本語 README](README.ja.md)

This project is intentionally separated from the legacy CounterStrikeSharp
project. It does not reference `CounterStrikeSharp.API` and targets SwiftlyS2's
C# API instead.

This plugin is a test implementation intended as a SwiftlyS2-based successor to
[girlglock/SharpTimer](https://github.com/girlglock/SharpTimer). SharpTimer is
used as a reference for expected timer behavior and feature direction, while
this project focuses on a bhop-oriented SwiftlyS2 implementation.

## Requirements

- Counter-Strike 2 dedicated server
- SwiftlyS2 `1.3.5` or newer
- SwiftlyS2 C# / managed plugin runtime environment
- The main plugin targets `net10.0`.
- MetaMod:Source and an OS-matching `SwiftlyBhopTimer MetaMod Helper` build are
  required.

## Bhop Modes

- `Standard`: the default mode based on CS2 movement. It applies timer-oriented
  compatibility and protection behavior while keeping movement feel changes as
  small as practical.
- `Classic`: an experimental mode aimed at reproducing the Bhop style of CSGO /
  CS:S servers, especially the feel of 100 tick bhop strafing and speed
  preservation. It is not a simple faster-speed mode; it softens air-strafe
  speed loss and nudges movement toward the CSGO / CS:S input feel.

Classic does not have a single fixed speed-gain percentage. The actual gain
depends on current velocity, view angle, input direction, ground state, and map
gimmicks. The current helper-side implementation references CSS-style 100 tick
air acceleration and only corrects movement within a range that avoids
unnecessarily breaking CS2's current velocity.

## MetaMod Helper

SwiftlyBhopTimer is split into two layers: the SwiftlyS2 C# plugin and the
`SwiftlyBhopTimer MetaMod Helper`.

The helper is a required native MetaMod:Source plugin. The SwiftlyS2 plugin owns
timer logic, database access, HUD, chat commands, MapData, and record storage.
The helper owns CS2 engine-adjacent behavior that is harder to control reliably
from SwiftlyS2/C#.

This split exists because some behavior is either not exposed cleanly through
SwiftlyS2 or is easily affected by server mode, workshop cfgs, and engine command
restrictions. The helper handles replay bot creation/removal/quota management,
round flow and map time support, HTML HUD flashing mitigation, native `!r`
teleport, `map_start` spawn fallback, noclip MoveType control, hidefps/viewmodel
control, trigger_push/telehop/bhop block/RNGFix compatibility, and Classic-mode
movement assistance.

This keeps the timer plugin pleasant to develop in SwiftlyS2/C#, while keeping
low-level CS2 behavior in MetaMod where it is more reliable. If the timer is
ever ported away from SwiftlyS2, the helper can still be reused by calling its
`sbt_*` server commands from another plugin layer.

## Current State

This is an independent SwiftlyS2 bhop timer implementation.
It includes:

- SwiftlyS2 `net10.0` project
- split SwiftlyS2 plugin and MetaMod Helper architecture
- plugin lifecycle entry point
- raw command registration for timer commands
- map load, player join/disconnect, tick, and entity touch event subscriptions
- start/end trigger timer flow for the active map
- bonus start/end/respawn timer flow with separate bonus records
- coordinate fallback using legacy `MapStartC1/MapStartC2` and `MapEndC1/MapEndC2`
- map JSON DTOs for `cfg/SwiftlyBhopTimer/MapData`
- database-backed settings, records, and replay storage
- timer-safe cfg/ConVar application for round flow, teams, bots, protection,
  and map voting
- MetaMod Helper support for replay bots, noclip, restart teleport,
  movement/gimmick compatibility, and native engine workarounds

## Implemented Features

Timer and records:

- Start/end zone timer flow
- Trigger-based and coordinate-based start/end detection
- Mode-specific records for Standard and Classic
- PB, SR, rank, and top-record commands
- Finish chat messages with rank, PB delta, and SR delta
- New SR global chat notification
- SwiftlyS2 database-backed settings, times, and replay storage

Maps and zones:

- MapData loading, bundled MapData seeding, and map setting commands
- Coordinate fallback using `MapStartC1/MapStartC2` and `MapEndC1/MapEndC2`
- Start/end zone beam rendering
- Admin setup for start, end, and respawn positions
- Bonus stage start/end/respawn setup
- `!b1`-style bonus teleport commands and bonus top records
- Map tiers, map adding, MapChooser, RTV, and map extend

Player controls and practice:

- Restart command with respawn-position teleport
- Pause and stop/reset commands
- Practice `!cp` / `!tp` commands with bind-friendly `sbt_cp`, `sbt_tp`, and `sbt_nextcp`
- Personal start position with `!ssp` / `sbt_ssp`
- Noclip command with timer reset and timer-start blocking while enabled
- Start zone speed limit
- `!hidelegs`, `!hide`, `!hidefps`, and `!fov`
- Player settings persistence for HUD, visuals, FOV, and sounds

HUD, chat, and menus:

- CenterHTML HUD
- HUD flash mitigation
- Spectator HUD data lookup
- Chat prefix and color formatting configuration
- Personal options/settings menu
- Admin menu

Replay Bot and Helper:

- Persistent Standard and Classic SR replay bots
- `!replay` menu for top-record and PB replay bots
- `!spec` menu for spectating players and bots
- MetaMod Helper replay bot creation, removal, and quota management
- Helper-backed native restart teleport, map_start fallback, noclip, and hidefps support
- Helper-backed trigger_push, telehop, bhop block, and RNGFix compatibility
- Helper-backed Classic mode movement assistance

Server protection and cfg:

- Collision disable and damage disable settings
- Weapon drop suppression and pistol restore after team changes
- Server cfg generation for timer-friendly round flow
- Timer-safe cfg/ConVar application without jump/gravity/air-accel overrides
- Linux and Windows publish outputs

## Build

```powershell
dotnet restore
dotnet build
```

Publish both Linux and Windows packages:

```powershell
.\scripts\publish.ps1
```

Publish one target manually:

```powershell
dotnet publish .\SwiftlyBhopTimer.csproj -c Release -r linux-x64 --self-contained false
dotnet publish .\SwiftlyBhopTimer.csproj -c Release -r win-x64 --self-contained false
```

Published files are written to:

- `build/SwiftlyBhopTimer_linux`
- `build/SwiftlyBhopTimer_windows`

## Recommended Server Plugins

For closer surf/bhop movement behavior and fewer CS2 movement edge cases, it is
recommended to install:

- Either [SharpTimer/STFixes-metamod](https://github.com/SharpTimer/STFixes-metamod)
  or [Source2ZE/MovementUnlocker](https://github.com/Source2ZE/MovementUnlocker)
- [Interesting-exe/CS2Fixes-RampbugFix](https://github.com/Interesting-exe/CS2Fixes-RampbugFix/)

`STFixes-metamod` already contains surf/bhop-oriented movement fixes and a
movement unlocker option, so avoid enabling duplicate movement unlock behavior
from multiple plugins at the same time.

## Commands

- [Command reference (English)](COMMANDS.en.md)
- [コマンド表 (日本語)](COMMANDS.ja.md)

## Map Chooser

Map voting is configured in `cfg/SwiftlyBhopTimer/SwiftlyBhopTimer.MapChooser.json`.
Set `VoteStartBeforeEndMinutes` to choose how many minutes before the map end
the automatic vote starts. The vote options include configured maps and an
extend option while `MaxExtends` has not been reached.

Map chooser chat output has its own color settings under `ChatColors`, so vote
messages can be styled without changing normal timer, rank, or finish messages.

```json
{
  "VoteStartBeforeEndMinutes": 5.0,
  "ExtendMinutes": 15.0,
  "MaxExtends": 2,
  "ChatColors": {
    "Label": "{lightblue}",
    "Value": "{green}",
    "Accent": "{gold}",
    "Extend": "{gold}",
    "Muted": "{gray}",
    "Error": "{red}"
  }
}
```

## Development Layout

- `src/SwiftlyBhopTimerPlugin.cs`: plugin lifecycle, shared state, service initialization, tick loop.
- `src/Features/SwiftlyBhopTimer.Commands.cs`: command registration and chat command handlers.
- `src/Features/SwiftlyBhopTimer.Events.cs`: SwiftlyS2 and game event hooks.
- `src/Features/SwiftlyBhopTimer.MapData.cs`: map loading, recovery, zone rendering, setup preview.
- `src/Features/SwiftlyBhopTimer.Movement.cs`: restart teleport, FOV, start-zone speed limiting, pause movement freeze.
- `src/Features/SwiftlyBhopTimer.Hud.cs`: CenterHTML HUD rendering and HUD flash mitigation.
- `src/Features/SwiftlyBhopTimer.ReplayBot.cs`: experimental replay bot creation and playback loop.
- `src/Features/SwiftlyBhopTimer.TimerFlow.cs`: start/end/stage/bonus timer flow and finish-record messages.
- `src/Features/SwiftlyBhopTimer.Infrastructure.cs`: chat formatting helpers, DB diagnostics, settings save helpers.
- `src/Services`: reusable storage, map, rendering, visual, protection, replay, and formatting services.

New gameplay features should usually start in the matching `src/Features` file.
Reusable IO, DB, parsing, or engine helper code should go under `src/Services`.

## Notes

The command handlers use an optional map name argument.

```text
!top bhop_beginnerfriendly
!rank bhop_beginnerfriendly
!sr bhop_beginnerfriendly
```

Use `!st_debugtouch` in-game to print resolved trigger names while testing maps.
Use `!st_map <mapname>` if the plugin was hot-reloaded and did not receive a map
load event.

## References / Credits

SwiftlyBhopTimer is an independent implementation, but the following plugins,
projects, and documents were referenced while designing and validating behavior.
Rights and licenses remain with each respective project.

- [swiftly-solution/swiftlys2](https://github.com/swiftly-solution/swiftlys2): SwiftlyS2 core and C# plugin API
- [girlglock/SharpTimer](https://github.com/girlglock/SharpTimer): timer behavior, MapData, chat output, replay, and general Bhop Timer feature direction
- [KZGlobalteam/cs2kz-metamod](https://github.com/KZGlobalteam/cs2kz-metamod): MetaMod-side bot creation and native helper implementation approach
- [girlglock/CS2FlashingHtmlHudFix](https://github.com/girlglock/CS2FlashingHtmlHudFix): CS2 HTML HUD flashing mitigation reference
- [jason-e/rngfix](https://github.com/jason-e/rngfix): slope, edge, telehop, triggerjump, and related RNGFix behavior reference
- [shavitush/bhoptimer](https://github.com/shavitush/bhoptimer): CSGO / CS:S-style Bhop Timer and strafe / physics direction reference
- [SharpTimer/STFixes-metamod](https://github.com/SharpTimer/STFixes-metamod): surf/bhop movement-fix reference and recommended server plugin
- [Source2ZE/MovementUnlocker](https://github.com/Source2ZE/MovementUnlocker): movement unlock reference and recommended server plugin
- [Interesting-exe/CS2Fixes-RampbugFix](https://github.com/Interesting-exe/CS2Fixes-RampbugFix/): rampbug mitigation reference and recommended server plugin
