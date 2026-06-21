# SwiftlyBhopTimer Command Reference

Run commands from chat using the `!command` format. Commands with the `st_` prefix are the preferred SwiftlyBhopTimer aliases.

## Player Commands

| Command | Aliases | Description |
| --- | --- | --- |
| `!stver` | `!st_ver` | Prints the plugin version. |
| `!help` | `!sthelp`, `!st_help` | Prints available commands. |
| `!timer` | `!st_timer` | Toggles your timer on or off. |
| `!options` | `!settings`, `!stoptions`, `!st_options` | Opens the personal options menu for HUD, visibility, FOV, and sounds. |
| `!hud` | `!st_hud` | Toggles your HUD visibility. |
| `!hidelegs` | `!st_hidelegs` | Toggles your local leg visibility. |
| `!hide` | `!st_hide` | Toggles visibility of other players for you. |
| `!hidefps` | `!st_hidefps` | Toggles first-person arms/viewmodel visibility. |
| `!fov <85-130>` | `!st_fov <85-130>` | Sets your player FOV. |
| `!sounds` | `!st_sounds` | Toggles timer sounds for you. |
| `!pause` | `!stpause`, `!st_pause` | Pauses or resumes your current timer run and freezes movement while paused. |
| `!stop` | `!ststop`, `!st_stop` | Stops and resets your current timer run without saving a record. |
| `!cp` | `sbt_cp` | Saves position, angle, and velocity to the current checkpoint slot. Saving resets the timer and discards replay recording. |
| `!tp` | `sbt_tp` | Teleports to the current checkpoint slot. Teleporting resets the timer and discards replay recording. |
| `sbt_nextcp` | `sbt_prevcp`, `sbt_clearcp` | Bind-friendly checkpoint slot selection and clear commands. |
| `!ssp` | `sbt_ssp`, `!ssp clear` | Saves your current position as a personal start position. Normal `!r` restarts prefer this position. |
| `!noclip` | `!stnoclip`, `!st_noclip` | Toggles plugin-controlled noclip. Enabling it force-stops the timer and blocks new starts until disabled. Intended for servers with console noclip disabled. |
| `!r` | `!st_r` | Returns you to the current bonus respawn while running a bonus, otherwise to the map respawn/start position. |
| `!b <1-99>` | `!bonus <1-99>`, `!st_b <1-99>`, `!st_bonus <1-99>` | Returns you to the configured bonus respawn/start position. |
| `!b1` ... `!b99` | `!st_b1` ... `!st_b99` | Shortcut commands for each bonus stage. |

## Map Vote Commands

| Command | Aliases | Description |
| --- | --- | --- |
| `!rtv` | `!rockthevote`, `!st_rtv` | Votes to start a map vote early. |
| `!nominate [map]` | `!st_nominate [map]` | Nominates a map for the next vote. Without a map, opens a nomination menu. |
| `!maps` | `!st_maps` | Opens the map list. Players nominate; admins can change map from the menu. |
| `!tier [map]` | `!sttier`, `!st_tier` | Prints the configured map difficulty tier. Without a map, prints the current map tier. |
| `!timeleft` | `!st_timeleft` | Prints the current plugin-controlled map time remaining. |
| `!nextmap` | `!st_nextmap` | Prints the selected next map, if a vote has selected one. |

## Record Commands

| Command | Aliases | Description |
| --- | --- | --- |
| `!top [map]` | `!mtop [map]`, `!st_top [map]`, `!st_mtop [map]` | Prints top records for the current map or a specified map. |
| `!rank [map]` | `!st_rank [map]` | Prints your rank on the current map or a specified map. |
| `!sr [map]` | `!st_sr [map]` | Prints the server record for the current map or a specified map. |
| `!btop <1-99>` | `!topbonus <1-99>`, `!st_btop <1-99>`, `!st_topbonus <1-99>` | Prints top records for a bonus stage. |
| `!stage` | `!st_stage` | Prints your current stage number and stage time. |
| `!replay [b1]` | `!streplay [b1]`, `!st_replay [b1]` | Opens a center menu to select Top 1-5 / PB replay playback. Add `b1` or `bonus 1` to browse bonus replays. |
| `!pbreplay [b1]` | `!stpbreplay [b1]`, `!st_pbreplay [b1]` | Spawns your PB replay bot for the main map or a bonus. |

## Admin Commands

Admin access is granted by the SwiftlyS2 permission `swiftlybhoptimer.admin`. A group with `swiftlybhoptimer.*` also grants access through SwiftlyS2 wildcard permissions.

| Command | Aliases | Description |
| --- | --- | --- |
| `!admin` | `!stadmin`, `!st_admin` | Opens the admin menu for deleting times, setting zones, applying cfg, and related actions. |
| `!map [map]` | `!changemap`, `!st_changemap` | Opens the configured map selector, or changes to a configured map immediately when a map name is provided. |
| `!mapvote` | `!st_mapvote` | Starts a map vote immediately. |
| `!changemap <map>` | `!st_changemap <map>` | Changes to a configured map immediately. Without a map, opens the map selector. |
| `!extendmap` | `!st_extendmap` | Extends the current map by the configured extend time. |
| `!maptier <map> <0-10>` | `!stmaptier`, `!st_maptier` | Saves the configured difficulty tier for a map. Use `0` for unset, or `1`-`10` for Tier 1 (Novice) through Tier 10 (Master/TAS). Use `!maptier <0-10>` to set the current map. |
| `!addmap <map> <workshopId> [0-10]` | `!staddmap`, `!st_addmap` | Adds a workshop map to the map chooser, or updates an existing map. Tier is optional and defaults to `0`. |
| `!st_cfg` | `!stcfg` | Regenerates and applies the SwiftlyBhopTimer cfg. |
| `!st_chat` | `!stchat` | Reloads chat prefix/color configuration. |
| `!st_replaybot` | `!streplaybot` | Forces replay bot creation. |
| `!st_collision [on/off]` | `!stcollision [on/off]` | Toggles player collision disabling. |
| `!st_damage [on/off]` | `!stdamage [on/off]` | Toggles player damage disabling. |
| `!st_deltime <rank> [map]` | `!stdeltime`, `!st_delrecord`, `!stdelrecord` | Deletes the best time at the specified `!top` rank. |
| `!st_setstart1` | `!stsetstart1` | Saves your current position as start zone corner 1. |
| `!st_setstart2` | `!stsetstart2` | Saves your current position as start zone corner 2. |
| `!st_setend1` | `!stsetend1` | Saves your current position as end zone corner 1. |
| `!st_setend2` | `!stsetend2` | Saves your current position as end zone corner 2. |
| `!st_setrespawn` | `!stsetrespawn` | Saves your current position as the respawn position. |
| `!st_setbonusstart1 <1-99>` | `!stsetbonusstart1 <1-99>` | Saves your current position as bonus start zone corner 1. |
| `!st_setbonusstart2 <1-99>` | `!stsetbonusstart2 <1-99>` | Saves your current position as bonus start zone corner 2. |
| `!st_setbonusend1 <1-99>` | `!stsetbonusend1 <1-99>` | Saves your current position as bonus end zone corner 1. |
| `!st_setbonusend2 <1-99>` | `!stsetbonusend2 <1-99>` | Saves your current position as bonus end zone corner 2. |
| `!st_setbonusrespawn <1-99>` | `!stsetbonusrespawn <1-99>` | Saves your current position as a bonus respawn position. |

## Debug/Operations Commands

| Command | Aliases | Description |
| --- | --- | --- |
| `!st_debugtouch` | `!stdebugtouch` | Toggles trigger touch debug output. |
| `!st_map [map]` | `!stmap [map]` | Prints current map information. With `map`, overrides the plugin-side map name. |
| `!st_where` | `!stwhere` | Prints your position, zone state, and timer state. |
| `!st_beam` | `!stbeam` | Schedules start/end zone beam redraw. |

## Examples

```text
!top
!rank
!sr bhop_eazy
!r
!b1
!btop 1
!replay b1
!pbreplay b1
!pause
!stop
!noclip
!st_fov 110
!st_deltime 1
!st_deltime 3 bhop_eazy
!st_setrespawn
!st_setbonusstart1 1
!st_setbonusend2 1
```
