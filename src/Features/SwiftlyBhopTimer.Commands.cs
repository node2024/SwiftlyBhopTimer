using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyBhopTimer.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private void RegisterCommands()
    {
        RegisterPlayerCommand("stver", OnVersionCommand, "Prints SwiftlyBhopTimer version.");
        RegisterPlayerCommand("help", OnHelpCommand, "Prints available SwiftlyBhopTimer commands.");
        RegisterPlayerCommand("sthelp", OnHelpCommand, "Prints available SwiftlyBhopTimer commands.");
        RegisterPlayerCommand("timer", OnTimerCommand, "Toggles timer blocking for the player.");
        RegisterPlayerCommand("hud", OnHudCommand, "Toggles timer HUD for the player.");
        RegisterPlayerCommand("hidelegs", OnHideLegsCommand, "Toggles local legs hiding for the player.");
        RegisterPlayerCommand("hide", OnHidePlayersCommand, "Toggles hiding other players locally.");
        RegisterPlayerCommand("hidefps", OnHideFpsCommand, "Toggles local first-person viewmodel hiding.");
        RegisterPlayerCommand("fov", OnFovCommand, "Sets player FOV.");
        RegisterPlayerCommand("mode", OnModeCommand, TimerRunModes.ClassicEnabled
            ? "Shows or changes bhop mode. Usage: !mode standard|classic."
            : "Shows bhop mode. Classic is temporarily disabled.");
        RegisterPlayerCommand("sounds", OnSoundsCommand, "Toggles sounds for the player.");
        RegisterPlayerCommand("options", OnOptionsCommand, "Opens personal options menu.");
        RegisterPlayerCommand("settings", OnOptionsCommand, "Alias for options.");
        RegisterPlayerCommand("admin", OnAdminMenuCommand, "Opens admin menu.");
        RegisterPlayerCommand("rtv", OnRtvCommand, "Votes to start a map vote.");
        RegisterPlayerCommand("rockthevote", OnRtvCommand, "Alias for rtv.");
        RegisterPlayerCommand("nominate", OnNominateCommand, "Nominates a map for the next vote.");
        RegisterPlayerCommand("maps", OnMapsCommand, "Opens map nomination or admin map selection.");
        RegisterPlayerCommand("tier", OnTierCommand, "Prints the configured map tier.");
        RegisterPlayerCommand("timeleft", OnTimeleftCommand, "Prints current map time left.");
        RegisterPlayerCommand("nextmap", OnNextMapCommand, "Prints the selected next map.");
        RegisterPlayerCommand("map", OnChangeMapCommand, "Opens the admin map selector or changes to a configured map. Admin only.");
        RegisterPlayerCommand("mapvote", OnMapVoteCommand, "Starts a map vote. Admin only.");
        RegisterPlayerCommand("changemap", OnChangeMapCommand, "Changes map. Admin only.");
        RegisterPlayerCommand("extendmap", OnExtendMapCommand, "Extends current map. Admin only.");
        RegisterPlayerCommand("maptier", OnMapTierCommand, "Sets a configured map tier. Admin only.");
        RegisterPlayerCommand("addmap", OnAddMapCommand, "Adds or updates a configured workshop map. Admin only.");
        RegisterPlayerCommand("pause", OnPauseCommand, "Pauses or resumes the current timer run.");
        RegisterPlayerCommand("stop", OnStopCommand, "Stops and resets the current timer run.");
        RegisterPlayerCommand("cp", OnCheckpointCommand, "Saves the current checkpoint slot.");
        RegisterPlayerCommand("tp", OnCheckpointTeleportCommand, "Teleports to the current checkpoint slot.");
        RegisterPlayerCommand("ssp", OnSetStartPositionCommand, "Sets a personal start position for !r.");
        RegisterPlayerCommand("spec", OnSpecCommand, "Moves you to spectator and watches a replay bot when available.");
        RegisterPlayerCommand("noclip", OnNoclipCommand, "Toggles noclip and blocks timer starts while active.");
        RegisterPlayerCommand("stpause", OnPauseCommand, "Alias for pause.");
        RegisterPlayerCommand("ststop", OnStopCommand, "Alias for stop.");
        RegisterPlayerCommand("sbt_cp", OnCheckpointCommand, "Bind-friendly checkpoint save command.");
        RegisterPlayerCommand("sbt_tp", OnCheckpointTeleportCommand, "Bind-friendly checkpoint teleport command.");
        RegisterPlayerCommand("sbt_ssp", OnSetStartPositionCommand, "Bind-friendly personal start position command.");
        RegisterPlayerCommand("sbt_nextcp", OnNextCheckpointCommand, "Bind-friendly checkpoint slot next command.");
        RegisterPlayerCommand("sbt_prevcp", OnPreviousCheckpointCommand, "Bind-friendly checkpoint slot previous command.");
        RegisterPlayerCommand("sbt_clearcp", OnClearCheckpointCommand, "Bind-friendly checkpoint clear command.");
        RegisterPlayerCommand("stspec", OnSpecCommand, "Alias for spec.");
        RegisterPlayerCommand("stnoclip", OnNoclipCommand, "Alias for noclip.");
        RegisterPlayerCommand("stoptions", OnOptionsCommand, "Alias for options.");
        RegisterPlayerCommand("stsettings", OnOptionsCommand, "Alias for options.");
        RegisterPlayerCommand("stadmin", OnAdminMenuCommand, "Alias for admin menu.");
        RegisterPlayerCommand("top", OnTopCommand, "Prints map top records from JSON storage.");
        RegisterPlayerCommand("mtop", OnTopCommand, "Alias for top records.");
        RegisterPlayerCommand("rank", OnRankCommand, "Prints the sender rank on the current map.");
        RegisterPlayerCommand("sr", OnServerRecordCommand, "Prints the current map server record.");
        RegisterPlayerCommand("stdeltime", OnDeleteTimeCommand, "Deletes a ranked time. Admin only.");
        RegisterPlayerCommand("stdelrecord", OnDeleteTimeCommand, "Deletes a ranked time. Admin only.");
        RegisterPlayerCommand("stage", OnStageCommand, "Prints the sender current stage.");
        RegisterPlayerCommand("r", OnRestartCommand, "Teleports to the map respawn position.");
        RegisterPlayerCommand("b", OnBonusRestartCommand, "Teleports to a bonus respawn position. Usage: !b <1-99>.");
        RegisterPlayerCommand("bonus", OnBonusRestartCommand, "Teleports to a bonus respawn position. Usage: !bonus <1-99>.");
        RegisterPlayerCommand("btop", OnBonusTopCommand, "Prints bonus top records. Usage: !btop <1-99>.");
        RegisterPlayerCommand("topbonus", OnBonusTopCommand, "Prints bonus top records. Usage: !topbonus <1-99>.");
        RegisterPlayerCommand("stdebugtouch", OnDebugTouchCommand, "Toggles trigger touch debug output.");
        RegisterPlayerCommand("stmap", OnMapCommand, "Prints or overrides the current map name.");
        RegisterPlayerCommand("stwhere", OnWhereCommand, "Prints current position and zone state.");
        RegisterPlayerCommand("stbeam", OnBeamCommand, "Redraws start and end zone beams.");
        RegisterPlayerCommand("stcfg", OnConfigCommand, "Regenerates and reapplies SwiftlyBhopTimer cfg.");
        RegisterPlayerCommand("stchat", OnChatConfigCommand, "Reloads SwiftlyBhopTimer chat formatting config.");
        RegisterPlayerCommand("stadsreload", OnAdvertisingConfigCommand, "Reloads SwiftlyBhopTimer advertising config. Admin only.");
        RegisterPlayerCommand("replay", OnReplayCommand, "Opens the replay selector.");
        RegisterPlayerCommand("streplay", OnReplayCommand, "Opens the replay selector.");
        RegisterPlayerCommand("streplaybot", OnReplayBotCommand, "Forces replay bot creation.");
        RegisterPlayerCommand("pbreplay", OnPersonalBestReplayBotCommand, "Creates a bot for your personal best replay.");
        RegisterPlayerCommand("stpbreplay", OnPersonalBestReplayBotCommand, "Alias for personal best replay bot.");
        RegisterPlayerCommand("stcollision", OnCollisionCommand, "Toggles player collision disabling.");
        RegisterPlayerCommand("stdamage", OnDamageCommand, "Toggles player damage disabling.");
        RegisterPlayerCommand("stsetstart1", context => OnSetMapPointCommand(context, "MapStartC1", "start corner 1"), "Sets start zone corner 1 to your position.");
        RegisterPlayerCommand("stsetstart2", context => OnSetMapPointCommand(context, "MapStartC2", "start corner 2"), "Sets start zone corner 2 to your position.");
        RegisterPlayerCommand("stsetend1", context => OnSetMapPointCommand(context, "MapEndC1", "end corner 1"), "Sets end zone corner 1 to your position.");
        RegisterPlayerCommand("stsetend2", context => OnSetMapPointCommand(context, "MapEndC2", "end corner 2"), "Sets end zone corner 2 to your position.");
        RegisterPlayerCommand("stsetrespawn", context => OnSetMapPointCommand(context, "RespawnPos", "respawn position"), "Sets respawn position to your position.");
        RegisterPlayerCommand("stsetbonusstart1", context => OnSetBonusMapPointCommand(context, "StartC1", "bonus start corner 1"), "Sets bonus start zone corner 1.");
        RegisterPlayerCommand("stsetbonusstart2", context => OnSetBonusMapPointCommand(context, "StartC2", "bonus start corner 2"), "Sets bonus start zone corner 2.");
        RegisterPlayerCommand("stsetbonusend1", context => OnSetBonusMapPointCommand(context, "EndC1", "bonus end corner 1"), "Sets bonus end zone corner 1.");
        RegisterPlayerCommand("stsetbonusend2", context => OnSetBonusMapPointCommand(context, "EndC2", "bonus end corner 2"), "Sets bonus end zone corner 2.");
        RegisterPlayerCommand("stsetbonusrespawn", context => OnSetBonusMapPointCommand(context, "RespawnPos", "bonus respawn position"), "Sets bonus respawn position.");

        RegisterPlayerCommand("st_ver", OnVersionCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_help", OnHelpCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_timer", OnTimerCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_hud", OnHudCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_hidelegs", OnHideLegsCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_hide", OnHidePlayersCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_hidefps", OnHideFpsCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_fov", OnFovCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_mode", OnModeCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("stmode", OnModeCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_sounds", OnSoundsCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_options", OnOptionsCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_settings", OnOptionsCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_admin", OnAdminMenuCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_rtv", OnRtvCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_rockthevote", OnRtvCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_nominate", OnNominateCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_maps", OnMapsCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_tier", OnTierCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_timeleft", OnTimeleftCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_nextmap", OnNextMapCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_mapvote", OnMapVoteCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_changemap", OnChangeMapCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_extendmap", OnExtendMapCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_maptier", OnMapTierCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_addmap", OnAddMapCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_pause", OnPauseCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_stop", OnStopCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_spec", OnSpecCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_noclip", OnNoclipCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_top", OnTopCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_mtop", OnTopCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_rank", OnRankCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_sr", OnServerRecordCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_deltime", OnDeleteTimeCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_delrecord", OnDeleteTimeCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_stage", OnStageCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_r", OnRestartCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_b", OnBonusRestartCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_bonus", OnBonusRestartCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_btop", OnBonusTopCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_topbonus", OnBonusTopCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_debugtouch", OnDebugTouchCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_map", OnMapCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_where", OnWhereCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_beam", OnBeamCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_cfg", OnConfigCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_chat", OnChatConfigCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_adsreload", OnAdvertisingConfigCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("stads", OnAdvertisingConfigCommand, "Alias for advertising reload command.");
        RegisterPlayerCommand("st_replay", OnReplayCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_replaybot", OnReplayBotCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_pbreplay", OnPersonalBestReplayBotCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_collision", OnCollisionCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_damage", OnDamageCommand, "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("sttier", OnTierCommand, "Alias for tier command.");
        RegisterPlayerCommand("stmaptier", OnMapTierCommand, "Alias for map tier command.");
        RegisterPlayerCommand("staddmap", OnAddMapCommand, "Alias for add map command.");
        RegisterPlayerCommand("st_setstart1", context => OnSetMapPointCommand(context, "MapStartC1", "start corner 1"), "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_setstart2", context => OnSetMapPointCommand(context, "MapStartC2", "start corner 2"), "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_setend1", context => OnSetMapPointCommand(context, "MapEndC1", "end corner 1"), "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_setend2", context => OnSetMapPointCommand(context, "MapEndC2", "end corner 2"), "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_setrespawn", context => OnSetMapPointCommand(context, "RespawnPos", "respawn position"), "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_setbonusstart1", context => OnSetBonusMapPointCommand(context, "StartC1", "bonus start corner 1"), "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_setbonusstart2", context => OnSetBonusMapPointCommand(context, "StartC2", "bonus start corner 2"), "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_setbonusend1", context => OnSetBonusMapPointCommand(context, "EndC1", "bonus end corner 1"), "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_setbonusend2", context => OnSetBonusMapPointCommand(context, "EndC2", "bonus end corner 2"), "SwiftlyBhopTimer alias.");
        RegisterPlayerCommand("st_setbonusrespawn", context => OnSetBonusMapPointCommand(context, "RespawnPos", "bonus respawn position"), "SwiftlyBhopTimer alias.");

        for (var bonusNumber = 1; bonusNumber <= 99; bonusNumber++)
        {
            var capturedBonusNumber = bonusNumber;
            RegisterPlayerCommand($"b{capturedBonusNumber}", context => OnBonusRestartCommand(context, capturedBonusNumber), $"Teleports to bonus {capturedBonusNumber}.");
            RegisterPlayerCommand($"st_b{capturedBonusNumber}", context => OnBonusRestartCommand(context, capturedBonusNumber), $"Teleports to bonus {capturedBonusNumber}.");
        }

        RegisterSharpTimerCompatibilityCommands();
    }

    private void RegisterSharpTimerCompatibilityCommands()
    {
        foreach (var commandName in SharpTimerCompatibilityCommands)
        {
            var capturedCommandName = commandName;
            RegisterPlayerCommand(capturedCommandName, context => OnSharpTimerCompatibilityCommand(capturedCommandName, context), "SharpTimer compatibility command.");
        }
    }

    private void OnSharpTimerCompatibilityCommand(string commandName, ICommandContext context)
    {
        switch (commandName.ToLowerInvariant())
        {
            case "sharptimer_remove_collision":
                if (_playerProtectionService is not null)
                {
                    _playerProtectionService.DisableCollision = ParseSharpTimerBoolean(context, fallback: true);
                }

                break;

            case "sharptimer_remove_damage":
                if (_playerProtectionService is not null)
                {
                    _playerProtectionService.DisableDamage = ParseSharpTimerBoolean(context, fallback: true);
                }

                break;

            case "sharptimer_max_start_speed_enabled":
                _startZoneSpeedLimitEnabled = ParseSharpTimerBoolean(context, fallback: true);
                break;

            case "sharptimer_max_start_speed":
                if (TryParseSharpTimerFloat(context, out var startSpeedLimit) && startSpeedLimit > 0.0f)
                {
                    _startZoneSpeedLimit = startSpeedLimit;
                }

                break;

            case "sharptimer_respawn_enabled":
                _compatRespawnCommandEnabled = ParseSharpTimerBoolean(context, fallback: true);
                break;

            case "sharptimer_top_enabled":
                _compatTopCommandEnabled = ParseSharpTimerBoolean(context, fallback: true);
                break;

            case "sharptimer_rank_enabled":
                _compatRankCommandEnabled = ParseSharpTimerBoolean(context, fallback: true);
                break;

            case "sharptimer_trigger_push_fix":
            case "sharptimer_disable_telehop":
            case "sharptimer_max_bhop_block_time":
            case "sharptimer_kill_pointservercommand_entities":
                ForwardSharpTimerCompatibilityCommandToHelper(commandName, context);
                break;
        }

        Core.Logger.LogDebug("Accepted SharpTimer compatibility command: {Command}", commandName);
    }

    private void ForwardSharpTimerCompatibilityCommandToHelper(string commandName, ICommandContext context)
    {
        var args = string.Join(' ', context.Args
            .Where(arg => !string.IsNullOrWhiteSpace(arg))
            .Select(arg => arg.Replace("\"", string.Empty, StringComparison.Ordinal).Replace(";", string.Empty, StringComparison.Ordinal)));
        var command = string.IsNullOrWhiteSpace(args)
            ? commandName
            : $"{commandName} {args}";

        ExecuteServerCommand(command);
    }

    private static bool ParseSharpTimerBoolean(ICommandContext context, bool fallback)
    {
        if (context.Args.Length == 0 || string.IsNullOrWhiteSpace(context.Args[0]))
        {
            return fallback;
        }

        var value = context.Args[0].Trim();
        if (bool.TryParse(value, out var parsedBool))
        {
            return parsedBool;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
        {
            return parsedInt != 0;
        }

        return value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("enabled", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseSharpTimerFloat(ICommandContext context, out float value)
    {
        value = 0.0f;
        return context.Args.Length > 0 &&
               !string.IsNullOrWhiteSpace(context.Args[0]) &&
               float.TryParse(context.Args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void OnVersionCommand(ICommandContext context)
    {
        Reply(context, $"[SwiftlyBhopTimer] v{PluginVersion}");
    }

    private void OnHelpCommand(ICommandContext context)
    {
        Reply(context, "Commands: {green}!top{default}, {green}!rank{default}, {green}!sr{default}, {green}!stage{default}, {green}!tier{default}, {green}!mode{default}, {green}!r{default}, {green}!b1{default}, {green}!btop 1{default}, {green}!pause{default}, {green}!stop{default}, {green}!spec{default}, {green}!noclip{default}, {green}!options{default}, {green}!hud{default}, {green}!hide{default}, {green}!hidelegs{default}, {green}!hidefps{default}, {green}!fov{default}, {green}!sounds{default}, {green}!stmap{default}, {green}!stbeam{default}, {green}!stcfg{default}");
    }

    private void OnTimerCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        if (state is null)
        {
            return;
        }

        if (context.Sender is not null)
        {
            _replayService?.DiscardRecording(context.Sender);
            ResetTimerStateWithStopSound(context.Sender, state);
        }
        else
        {
            ResetTimerState(state);
        }

        state.IsTimerBlocked = !state.IsTimerBlocked;

        Reply(context, $"[SwiftlyBhopTimer] Timer: {(state.IsTimerBlocked ? "Disabled" : "Enabled")}");
    }

    private void OnPauseCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        var player = context.Sender;
        if (state is null || player is null)
        {
            return;
        }

        if (!IsAnyTimerRunning(state))
        {
            Reply(context, $"{label("Pause")} {label("|")} {gray("no active timer")}");
            return;
        }

        state.IsTimerPaused = !state.IsTimerPaused;
        if (state.IsTimerPaused && !StartPauseFreeze(player, state))
        {
            state.IsTimerPaused = false;
            Reply(context, $"{label("Pause")} {label("|")} {red("could not read player position")}");
            return;
        }

        if (!state.IsTimerPaused)
        {
            StopPauseFreeze(state);
        }

        var time = TimeFormatter.FormatTicks(GetActiveTimerTicks(state));
        Reply(context, state.IsTimerPaused
            ? $"{label("Pause")} {label("|")} {gold(time)}"
            : $"{label("Resume")} {label("|")} {green(time)}");
    }

    private void OnStopCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        if (state is null)
        {
            return;
        }

        var stoppedTicks = GetActiveTimerTicks(state);
        if (context.Sender is not null)
        {
            _replayService?.DiscardRecording(context.Sender);
            ResetTimerStateWithStopSound(context.Sender, state);
        }
        else
        {
            ResetTimerState(state);
        }

        Reply(context, $"{label("Stop")} {label("|")} {red("reset")} {gray(TimeFormatter.FormatTicks(stoppedTicks))}");
    }

    private void OnSpecCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        var player = context.Sender;
        if (state is null || player is null)
        {
            return;
        }

        if (context.Args.Length == 0)
        {
            ShowSpectateMenu(context);
            return;
        }

        var targetName = ResolveSpecTargetName(context);
        MovePlayerToSpectator(player, state);
        if (!string.IsNullOrWhiteSpace(targetName))
        {
            QueueSpectateTarget(player, state, targetName);
            Reply(context, $"{label("Spec")} {label("|")} {green("spectator")} {gray($"target: {targetName}")}");
            return;
        }

        Reply(context, $"{label("Spec")} {label("|")} {green("spectator")}");
    }

    private void OnNoclipCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        var player = context.Sender;
        if (state is null || player is null)
        {
            return;
        }

        if (_noclipService is null)
        {
            Reply(context, "[SwiftlyBhopTimer] Noclip service is not initialized.");
            return;
        }

        var enable = !state.IsNoclipEnabled;
        state.NoclipSyncGraceTicks = 8;
        var applied = ApplyNoclipMovementState(player, enable);

        if (enable)
        {
            if (!applied)
            {
                Reply(context, $"{label("Noclip")} {label("|")} {red("unavailable")} {gray("SwiftlyS2 did not expose writable movement state")}");
                return;
            }

            EnableNoclipTimerBlock(player, state);
        }
        else
        {
            DisableNoclipMovementState(player, state);
            ResetTimerStateWithStopSound(player, state);
        }

        var status = state.IsNoclipEnabled ? green("ON") : red("OFF");
        var note = applied ? "" : $" {gray("movement state may already be restored")}";
        Reply(context, $"{label("Noclip")} {label("|")} {status}{note}");
    }

    private void OnHudCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        if (state is null)
        {
            return;
        }

        state.HideTimerHud = !state.HideTimerHud;
        state.HudTicks = 0;
        if (state.HideTimerHud && context.Sender is not null)
        {
            ClearHud(context.Sender);
        }

        SavePlayerSettings(state);
        Reply(context, $"[SwiftlyBhopTimer] HUD: {(state.HideTimerHud ? "Hidden" : "Shown")}");
    }

    private void OnHideLegsCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        if (state is null || context.Sender is null)
        {
            return;
        }

        state.HideLegs = !state.HideLegs;
        _playerVisualService?.ApplySelfVisuals(context.Sender, state);
        SavePlayerSettings(state);
        Reply(context, $"{label("Hide legs")} {label("|")} {(state.HideLegs ? green("ON") : red("OFF"))}");
    }

    private void OnHidePlayersCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        if (state is null || context.Sender is null)
        {
            return;
        }

        state.HidePlayers = !state.HidePlayers;
        _playerVisualService?.ApplyPlayerHiding(context.Sender, state, GetHideTargetPlayers());
        SavePlayerSettings(state);
        Reply(context, $"{label("Hide players")} {label("|")} {(state.HidePlayers ? green("ON") : red("OFF"))}");
    }

    private void OnHideFpsCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        if (state is null || context.Sender is null)
        {
            return;
        }

        state.HideFpsViewModel = !state.HideFpsViewModel;
        ApplyHideFpsWithRespawnRefresh(context.Sender, state);
        SavePlayerSettings(state);
        Reply(context, $"{label("Hide FPS")} {label("|")} {(state.HideFpsViewModel ? green("ON") : red("OFF"))}");
    }

    private void OnFovCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        if (state is null || context.Sender is null)
        {
            return;
        }

        if (context.Args.Length == 0 || string.IsNullOrWhiteSpace(context.Args[0]))
        {
            Reply(context, $"{label("FOV")} {label("|")} {gold(state.PlayerFov?.ToString(CultureInfo.InvariantCulture) ?? "default")} {gray($"({MinPlayerFov}-{MaxPlayerFov})")}");
            return;
        }

        if (!int.TryParse(context.Args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var requestedFov))
        {
            Reply(context, $"{label("FOV")} {label("|")} {red("Invalid value")} {gray($"Usage: !fov {MinPlayerFov}-{MaxPlayerFov}")}");
            return;
        }

        var fov = Math.Clamp(requestedFov, MinPlayerFov, MaxPlayerFov);
        state.PlayerFov = fov;
        ApplyPlayerFov(context.Sender, state);
        SavePlayerSettings(state);

        var clampMessage = fov == requestedFov ? "" : $" {gray($"clamped from {requestedFov}")}";
        Reply(context, $"{label("FOV")} {label("|")} {green(fov.ToString(CultureInfo.InvariantCulture))}{clampMessage}");
    }

    private void OnModeCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        var player = context.Sender;
        if (state is null || player is null)
        {
            return;
        }

        if (context.Args.Length == 0 || string.IsNullOrWhiteSpace(context.Args[0]))
        {
            var usage = TimerRunModes.ClassicEnabled ? "Usage: !mode standard|classic" : "Classic disabled for this test";
            Reply(context, $"{label("Mode")} {label("|")} {gold(TimerRunModes.ToDisplayName(state.TimerMode))} {gray(usage)}");
            return;
        }

        if (!TimerRunModes.TryParse(context.Args[0], out var requestedMode))
        {
            var usage = TimerRunModes.ClassicEnabled ? "Use standard or classic" : "Use standard";
            Reply(context, $"{label("Mode")} {label("|")} {red("invalid mode")} {gray(usage)}");
            return;
        }

        if (!TimerRunModes.IsEnabled(requestedMode))
        {
            state.TimerMode = TimerRunMode.Standard;
            ApplyPlayerModeIfOnTimerTeam(player, state);
            Reply(context, $"{label("Mode")} {label("|")} {gold("Classic")} {red("disabled")} {gray("using Standard for this test")}");
            return;
        }

        if (state.TimerMode == requestedMode)
        {
            ApplyPlayerModeIfOnTimerTeam(player, state);
            Reply(context, $"{label("Mode")} {label("|")} {gold(TimerRunModes.ToDisplayName(state.TimerMode))}");
            return;
        }

        SetPlayerTimerMode(player, state, requestedMode);

        Reply(context, $"{label("Mode")} {label("|")} {green(TimerRunModes.ToDisplayName(state.TimerMode))} {gray("timer reset")}");
    }

    private bool SetPlayerTimerMode(IPlayer player, PlayerTimerState state, TimerRunMode requestedMode)
    {
        requestedMode = TimerRunModes.Normalize(requestedMode);
        if (state.TimerMode == requestedMode)
        {
            return false;
        }

        _replayService?.DiscardRecording(player);
        ResetTimerStateWithStopSound(player, state);
        state.TimerMode = requestedMode;
        state.LastHudStatsRefreshUtc = DateTime.MinValue;
        state.CachedHudRecordMapName = "";
        ApplyPlayerModeIfOnTimerTeam(player, state);
        SavePlayerSettings(state);
        return true;
    }

    private void OnSoundsCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        if (state is null)
        {
            return;
        }

        state.SoundsEnabled = !state.SoundsEnabled;
        SavePlayerSettings(state);
        Reply(context, $"[SwiftlyBhopTimer] Sounds: {(state.SoundsEnabled ? "ON" : "OFF")}");
    }

    private void OnOptionsCommand(ICommandContext context)
    {
        ShowOptionsMenu(context);
    }

    private void OnAdminMenuCommand(ICommandContext context)
    {
        ShowAdminMenu(context);
    }

    private void OnReplayCommand(ICommandContext context)
    {
        ShowReplayMenu(context);
    }

    private void OnReplayBotCommand(ICommandContext context)
    {
        if (context.Sender is not null && !IsAdmin(context.Sender))
        {
            Reply(context, "[SwiftlyBhopTimer] You do not have permission to force replay bot creation.");
            return;
        }

        var expectedModes = ServerReplayModes
            .Where(mode => _replayService?.HasReplay(_currentMapName, mode) == true)
            .ToArray();
        var before = GetServerReplayBots();
        if (expectedModes.Length > 0 && expectedModes.All(mode => GetServerReplayBot(mode) is not null))
        {
            Reply(context, $"{label("Replay bot")} {label("|")} {green($"{before.Count} active")} {gray("already exists")}");
            return;
        }

        var availableModes = expectedModes.Select(TimerRunModes.ToDisplayName).ToArray();
        ForceReplayBotAdd();
        var after = GetServerReplayBots();
        if (availableModes.Length > 0 && expectedModes.All(mode => GetServerReplayBot(mode) is not null))
        {
            Reply(context, $"{label("Replay bot")} {label("|")} {green($"{after.Count} active")} {gray("created")}");
            return;
        }

        Reply(context, $"{label("Replay bot")} {label("|")} {gold("add requested")} {gray($"SR modes: {(availableModes.Length == 0 ? "none" : string.Join("/", availableModes))}. Waiting for helper...")}");
        ScheduleAfterTicks(32, () =>
        {
            var delayed = GetServerReplayBots();
            Reply(context, delayed.Count == 0
                ? $"{label("Replay bot")} {label("|")} {red("not visible")} {gray("Check helper logs for CreateBot signature/null/quota messages.")}"
                : $"{label("Replay bot")} {label("|")} {green($"{delayed.Count} active")} {gray("created")}");
        });
    }

    private void OnPersonalBestReplayBotCommand(ICommandContext context)
    {
        if (_replayService is null)
        {
            Reply(context, "[SwiftlyBhopTimer] Replay service is not initialized.");
            return;
        }

        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        var steamId = player.SteamID.ToString();
        var mode = GetPlayerTimerMode(player);
        if (!TryResolveReplayTarget(context, out var replayTarget))
        {
            Reply(context, $"{label("PB replay")} {label("|")} {red("Usage")} {gray("!pbreplay or !pbreplay b1")}");
            return;
        }

        var replay = _replayService.LoadPersonalBestReplay(replayTarget.RecordMapName, mode, steamId);
        var serverReplay = _replayService.LoadReplay(replayTarget.RecordMapName, mode);
        var useServerReplay = replay is null &&
                              serverReplay is not null &&
                              string.Equals(serverReplay.SteamId, steamId, StringComparison.Ordinal);
        if ((replay is null || replay.Frames.Count == 0) && !useServerReplay)
        {
            Reply(context, $"{label("PB replay")} {label("|")} {red("not found")} {gray($"A new PB must be recorded for {replayTarget.Label} before a PB bot can be spawned.")}");
            return;
        }

        useServerReplay = useServerReplay || IsSameReplay(replay!, serverReplay);
        var replayToPlay = useServerReplay ? serverReplay! : replay!;
        var bonusNumber = replayTarget.BonusNumber;
        ActivateAdditionalReplay(
            player,
            replayToPlay,
            bonusNumber.HasValue ? $"bonus:{bonusNumber.Value}:pb" : "pb",
            bonusNumber.HasValue ? $"[ Bonus #{bonusNumber.Value} PB Replay ]" : "[ PB Replay ]",
            bonusNumber.HasValue
                ? $"Bonus #{bonusNumber.Value} PB replay {TimeFormatter.FormatTicks(replayToPlay.TimerTicks)}"
                : $"PB replay {TimeFormatter.FormatTicks(replayToPlay.TimerTicks)}",
            mode,
            useServerReplay,
            kickAfterFirstLoop: true,
            allowParallelCopy: true);
    }

    private void OnTopCommand(ICommandContext context)
    {
        if (!_compatTopCommandEnabled)
        {
            Reply(context, $"{label("Top")} {label("|")} {red("disabled")}");
            return;
        }

        if (_recordStore is null)
        {
            Reply(context, "[SwiftlyBhopTimer] Record store is not initialized.");
            return;
        }

        var mapName = GetMapNameArgument(context);
        var mode = GetContextTimerMode(context);
        var records = _recordStore.GetTopRecordsAsync(mapName, 10, mode).GetAwaiter().GetResult();

        if (records.Count == 0)
        {
            Reply(context, $"{label("Top")} {green("records")} {label("|")} {gold(TimerRunModes.ToDisplayName(mode))} {label("|")} {lightBlue(mapName)} {gray("has no records yet")}");
            return;
        }

        Reply(context, $"{label("Top")} {green("records")} {label("|")} {gold(TimerRunModes.ToDisplayName(mode))} {label("|")} {lightBlue(mapName)}");
        foreach (var (record, index) in records.Select((record, index) => (record, index)))
        {
            var name = string.IsNullOrWhiteSpace(record.PlayerName) ? record.SteamId : record.PlayerName;
            Reply(context, $"{topPlacement(index + 1)} {gold(TimeFormatter.FormatTicks(record.TimerTicks))} {gray(name)}");
        }
    }

    private void OnDeleteTimeCommand(ICommandContext context)
    {
        if (_recordStore is null)
        {
            Reply(context, "[SwiftlyBhopTimer] Record store is not initialized.");
            return;
        }

        var player = context.Sender;
        if (player is null || !IsAdmin(player))
        {
            Reply(context, "[SwiftlyBhopTimer] You do not have permission to delete times.");
            return;
        }

        if (context.Args.Length == 0 ||
            !int.TryParse(context.Args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) ||
            rank <= 0)
        {
            Reply(context, $"{label("Delete time")} {label("|")} {red("Usage")} {gray("!st_deltime <rank> [map]")}");
            return;
        }

        var mapName = GetMapNameArgument(context, startIndex: 1);
        var mode = GetPlayerTimerMode(player);
        var deleted = _recordStore.DeleteBestRecordByRankAsync(mapName, rank, mode).GetAwaiter().GetResult();
        if (deleted is null)
        {
            Reply(context, $"{label("Delete time")} {label("|")} {gold(TimerRunModes.ToDisplayName(mode))} {label("|")} {lightBlue(mapName)} {red($"rank #{rank} not found")}");
            return;
        }

        var name = string.IsNullOrWhiteSpace(deleted.PlayerName) ? deleted.SteamId : deleted.PlayerName;
        var replayDeleted = deleted.Rank == 1 && (_replayService?.DeleteReplayIfMatches(deleted.MapName, deleted.Mode, deleted.SteamId, deleted.TimerTicks) ?? false);
        var replayPart = replayDeleted ? $" {label("|")} {gray("SR replay removed")}" : "";

        Reply(context, $"{label("Delete time")} {label("|")} {gold(TimerRunModes.ToDisplayName(deleted.Mode))} {label("|")} {lightBlue(deleted.MapName)} {topPlacement(deleted.Rank)} {white(TimeFormatter.FormatTicks(deleted.TimerTicks))} {gray(name)} {green("deleted")}{replayPart}");
    }

    private void OnRankCommand(ICommandContext context)
    {
        if (!_compatRankCommandEnabled)
        {
            Reply(context, $"{label("Rank")} {label("|")} {red("disabled")}");
            return;
        }

        if (_recordStore is null)
        {
            Reply(context, "[SwiftlyBhopTimer] Record store is not initialized.");
            return;
        }

        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        var mapName = GetMapNameArgument(context);
        var mode = GetPlayerTimerMode(player);
        var rank = _recordStore.GetRankAsync(mapName, player.SteamID.ToString(), mode).GetAwaiter().GetResult();

        Reply(context, rank is null
            ? $"{label("Rank")} {label("|")} {gold(TimerRunModes.ToDisplayName(mode))} {label("|")} {lightBlue(mapName)} {gray("no record yet")}"
            : $"{label("Rank")} {label("|")} {gold(TimerRunModes.ToDisplayName(mode))} {label("|")} {lightBlue(mapName)} {gold($"#{rank.Placement}")}{gray($"/{rank.Total}")} {lightBlue(TimeFormatter.FormatTicks(rank.TimerTicks))}");
    }

    private void OnServerRecordCommand(ICommandContext context)
    {
        if (_recordStore is null)
        {
            Reply(context, "[SwiftlyBhopTimer] Record store is not initialized.");
            return;
        }

        var mapName = GetMapNameArgument(context);
        var mode = GetContextTimerMode(context);
        var record = _recordStore.GetTopRecordsAsync(mapName, 1, mode).GetAwaiter().GetResult().FirstOrDefault();

        Reply(context, record is null
            ? $"{label("SR")} {label("|")} {gold(TimerRunModes.ToDisplayName(mode))} {label("|")} {lightBlue(mapName)} {gray("no record yet")}"
            : $"{label("SR")} {label("|")} {gold(TimerRunModes.ToDisplayName(mode))} {label("|")} {lightBlue(mapName)} {white(TimeFormatter.FormatTicks(record.TimerTicks))} {gray(record.PlayerName ?? record.SteamId)}");
    }

    private void OnStageCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        if (state is null)
        {
            return;
        }

        if (state.CurrentMapStage <= 0)
        {
            Reply(context, $"{label("Stage")} {label("|")} {gray("not started")}");
            return;
        }

        var ticks = state.CurrentStageTicks ?? (state.IsTimerRunning ? state.TimerTicks : 0);
        Reply(context, $"{label("Stage")} {label("|")} {gold($"#{state.CurrentMapStage}")} {lightBlue(TimeFormatter.FormatTicks(ticks))}");
    }

    private void OnDebugTouchCommand(ICommandContext context)
    {
        var state = GetSenderStateOrReply(context);
        if (state is null)
        {
            return;
        }

        state.DebugTouches = !state.DebugTouches;
        Reply(context, $"[SwiftlyBhopTimer] Touch debug: {(state.DebugTouches ? "ON" : "OFF")}");
    }

    private void OnMapCommand(ICommandContext context)
    {
        if (context.Args.Length == 0 || string.IsNullOrWhiteSpace(context.Args[0]))
        {
            Reply(context, BuildMapStatusMessage());
            return;
        }

        LoadMap(context.Args[0]);
        Reply(context, BuildMapStatusMessage());
    }

    private void OnWhereCommand(ICommandContext context)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        var pawn = player.PlayerPawn ?? player.Pawn;
        if (!EntityReflection.TryGetPosition(pawn, out var position))
        {
            Reply(context, $"[SwiftlyBhopTimer] Could not read player position. PawnType={pawn?.GetType().FullName ?? "null"}");
            return;
        }

        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        var bonusStart = string.Join(",", _activeMap.Bonuses.Values.Where(bonus => bonus.IsInsideStartZone(position)).Select(bonus => bonus.Number));
        var bonusEnd = string.Join(",", _activeMap.Bonuses.Values.Where(bonus => bonus.IsInsideEndZone(position)).Select(bonus => bonus.Number));
        Reply(context, $"[SwiftlyBhopTimer] Pos={position}; start={_activeMap.IsInsideStartZone(position)}; end={_activeMap.IsInsideEndZone(position)}; bonusStart={FormatBonusList(bonusStart)}; bonusEnd={FormatBonusList(bonusEnd)}; running={state.IsTimerRunning}; bonus={state.IsBonusTimerRunning}:{state.CurrentBonusNumber}; paused={state.IsTimerPaused}");
    }

    private void OnBeamCommand(ICommandContext context)
    {
        ScheduleZoneRender("command", delayTicks: 1);
        Reply(context, "[SwiftlyBhopTimer] Zone beam redraw scheduled.");
    }

    private void OnConfigCommand(ICommandContext context)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        if (!IsAdmin(player))
        {
            Reply(context, "[SwiftlyBhopTimer] You do not have permission to regenerate server cfg.");
            return;
        }

        if (_roundFlowConfigService is null)
        {
            Reply(context, "[SwiftlyBhopTimer] Config service is not initialized.");
            return;
        }

        try
        {
            _roundFlowConfigService.WriteConfig();
            _roundFlowConfigService.ApplyFull();
            Reply(context, $"[SwiftlyBhopTimer] Cfg regenerated and applied: {_roundFlowConfigService.ConfigPath}");
        }
        catch (Exception ex)
        {
            Reply(context, $"[SwiftlyBhopTimer] Failed to regenerate cfg: {ex.Message}");
        }
    }

    private void OnChatConfigCommand(ICommandContext context)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "This command can only be used by players.");
            return;
        }

        if (!IsAdmin(player))
        {
            Reply(context, "You do not have permission to reload chat config.");
            return;
        }

        if (_chatFormatService is null)
        {
            Reply(context, "Chat config service is not initialized.");
            return;
        }

        _chatFormatService.Reload();
        Reply(context, $"Chat config reloaded: {_chatFormatService.ConfigPath}");
    }

    private void OnAdvertisingConfigCommand(ICommandContext context)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "This command can only be used by players.");
            return;
        }

        if (!IsAdmin(player))
        {
            Reply(context, "You do not have permission to reload advertising config.");
            return;
        }

        if (_advertisingService is null)
        {
            Reply(context, "Advertising service is not initialized.");
            return;
        }

        var count = _advertisingService.Reload();
        Reply(context, $"Advertising config reloaded: {count} messages; {_advertisingService.ConfigPath}");
    }

    private void OnCollisionCommand(ICommandContext context)
    {
        if (!CanUseAdminRuntimeCommand(context, "change collision settings"))
        {
            return;
        }

        if (_playerProtectionService is null)
        {
            Reply(context, "[SwiftlyBhopTimer] Protection service is not initialized.");
            return;
        }

        _playerProtectionService.DisableCollision = ParseOptionalBoolean(context, !_playerProtectionService.DisableCollision);
        Reply(context, $"[SwiftlyBhopTimer] Collision disable: {(_playerProtectionService.DisableCollision ? "ON" : "OFF")}");
    }

    private void OnDamageCommand(ICommandContext context)
    {
        if (!CanUseAdminRuntimeCommand(context, "change damage settings"))
        {
            return;
        }

        if (_playerProtectionService is null)
        {
            Reply(context, "[SwiftlyBhopTimer] Protection service is not initialized.");
            return;
        }

        _playerProtectionService.DisableDamage = ParseOptionalBoolean(context, !_playerProtectionService.DisableDamage);
        Reply(context, $"[SwiftlyBhopTimer] Damage disable: {(_playerProtectionService.DisableDamage ? "ON" : "OFF")}");
    }

    private bool CanUseAdminRuntimeCommand(ICommandContext context, string action)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return false;
        }

        if (!IsAdmin(player))
        {
            Reply(context, $"[SwiftlyBhopTimer] You do not have permission to {action}.");
            return false;
        }

        return true;
    }

    private static bool ParseOptionalBoolean(ICommandContext context, bool fallback)
    {
        if (context.Args.Length == 0 || string.IsNullOrWhiteSpace(context.Args[0]))
        {
            return fallback;
        }

        var value = context.Args[0].Trim();
        return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private void OnRestartCommand(ICommandContext context)
    {
        if (!_compatRespawnCommandEnabled)
        {
            Reply(context, $"{label("Restart")} {label("|")} {red("disabled")}");
            return;
        }

        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        if (state.RestartCommandCooldownTicks > 0)
        {
            return;
        }

        var restartingBonusNumber = state.IsBonusTimerRunning && state.CurrentBonusNumber is >= 1 and <= 99
            ? state.CurrentBonusNumber
            : 0;
        if (restartingBonusNumber > 0)
        {
            var bonusDestination = GetBonusRestartPosition(restartingBonusNumber);
            if (bonusDestination is null)
            {
                Reply(context, $"{label("Bonus")} {gold($"#{restartingBonusNumber}")} {label("|")} {red("RespawnPos or start zone is not configured")}");
                return;
            }

            QueueRestartRespawn(player, state, bonusDestination.Value, applyCommandCooldown: true);
            Reply(context, $"{label("Bonus")} {gold($"#{restartingBonusNumber}")} {label("|")} {green("restarted")}");
            return;
        }

        if (TryGetPersonalStartPosition(state, out var personalStart))
        {
            QueueRestartRespawnToCheckpoint(player, state, personalStart, applyCommandCooldown: true);
            Reply(context, $"{label("Restart")} {label("|")} {green("personal start")}");
            return;
        }

        if (GetConfiguredRestartPosition() is { } configuredDestination)
        {
            QueueRestartRespawn(player, state, configuredDestination, applyCommandCooldown: true);
            Reply(context, "[SwiftlyBhopTimer] Restarted.");
            return;
        }

        QueueRestartRespawnToMapStart(player, state, GetStartZoneRestartFallbackPosition(), applyCommandCooldown: true);
        Reply(context, "[SwiftlyBhopTimer] Restarted.");
    }

    private void OnBonusRestartCommand(ICommandContext context)
    {
        if (!TryGetBonusNumberArgument(context, 0, out var bonusNumber))
        {
            Reply(context, $"{label("Bonus")} {label("|")} {red("Usage")} {gray("!b1 or !b <1-99>")}");
            return;
        }

        OnBonusRestartCommand(context, bonusNumber);
    }

    private void OnBonusRestartCommand(ICommandContext context, int bonusNumber)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        var destination = GetBonusRestartPosition(bonusNumber);
        if (destination is null)
        {
            Reply(context, $"{label("Bonus")} {gold($"#{bonusNumber}")} {label("|")} {red("RespawnPos or start zone is not configured")}");
            return;
        }

        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        if (state.RestartCommandCooldownTicks > 0)
        {
            return;
        }

        QueueRestartRespawn(player, state, destination.Value, applyCommandCooldown: true);
        Reply(context, $"{label("Bonus")} {gold($"#{bonusNumber}")} {label("|")} {green("restarted")}");
    }

    private void OnBonusTopCommand(ICommandContext context)
    {
        if (_recordStore is null)
        {
            Reply(context, "[SwiftlyBhopTimer] Record store is not initialized.");
            return;
        }

        if (!TryGetBonusNumberArgument(context, 0, out var bonusNumber))
        {
            Reply(context, $"{label("Bonus top")} {label("|")} {red("Usage")} {gray("!btop <1-99>")}");
            return;
        }

        var recordMapName = GetBonusRecordMapName(bonusNumber);
        var mode = GetContextTimerMode(context);
        var records = _recordStore.GetTopRecordsAsync(recordMapName, 10, mode).GetAwaiter().GetResult();
        if (records.Count == 0)
        {
            Reply(context, $"{label("Bonus top")} {gold($"#{bonusNumber}")} {label("|")} {gold(TimerRunModes.ToDisplayName(mode))} {label("|")} {lightBlue(recordMapName)} {gray("has no records yet")}");
            return;
        }

        Reply(context, $"{label("Bonus top")} {gold($"#{bonusNumber}")} {label("|")} {gold(TimerRunModes.ToDisplayName(mode))} {label("|")} {lightBlue(recordMapName)}");
        foreach (var (record, index) in records.Select((record, index) => (record, index)))
        {
            var name = string.IsNullOrWhiteSpace(record.PlayerName) ? record.SteamId : record.PlayerName;
            Reply(context, $"{topPlacement(index + 1)} {gold(TimeFormatter.FormatTicks(record.TimerTicks))} {gray(name)}");
        }
    }

    private void OnSetMapPointCommand(ICommandContext context, string propertyName, string label)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        if (!IsAdmin(player))
        {
            Reply(context, "[SwiftlyBhopTimer] You do not have permission to edit map zones.");
            return;
        }

        SaveMapPointFromPlayer(player, propertyName, label, message => Reply(context, message));
    }

    private void OnSetBonusMapPointCommand(ICommandContext context, string propertyName, string labelText)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        if (!IsAdmin(player))
        {
            Reply(context, "[SwiftlyBhopTimer] You do not have permission to edit bonus zones.");
            return;
        }

        if (!TryGetBonusNumberArgument(context, 0, out var bonusNumber))
        {
            Reply(context, $"{label("Bonus setup")} {label("|")} {red("Usage")} {gray("!st_setbonusstart1 <1-99>")}");
            return;
        }

        SaveBonusMapPointFromPlayer(player, bonusNumber, propertyName, labelText, message => Reply(context, message));
    }

    private void SaveMapPointFromPlayer(IPlayer player, string propertyName, string label, Action<string> reply)
    {
        if (_mapDataService is null)
        {
            reply("[SwiftlyBhopTimer] MapData service is not initialized.");
            return;
        }

        var pawn = player.PlayerPawn ?? player.Pawn;
        if (!EntityReflection.TryGetPosition(pawn, out var position))
        {
            reply($"[SwiftlyBhopTimer] Could not read player position. PawnType={pawn?.GetType().FullName ?? "null"}");
            return;
        }

        try
        {
            var savedPath = _mapDataService
                .SaveMapSettingAsync(_currentMapName, propertyName, position.ToString())
                .GetAwaiter()
                .GetResult();

            ReloadActiveMapData();
            UpdateZoneSetupPreview(player, propertyName, position);
            ScheduleZoneRender("map_setting");
            reply($"[SwiftlyBhopTimer] Saved {label}: {position}; file={savedPath}");
        }
        catch (Exception ex)
        {
            reply($"[SwiftlyBhopTimer] Failed to save {label}: {ex.Message}");
        }
    }

    private void SaveBonusMapPointFromPlayer(IPlayer player, int bonusNumber, string propertyName, string label, Action<string> reply)
    {
        if (_mapDataService is null)
        {
            reply("[SwiftlyBhopTimer] MapData service is not initialized.");
            return;
        }

        var pawn = player.PlayerPawn ?? player.Pawn;
        if (!EntityReflection.TryGetPosition(pawn, out var position))
        {
            reply($"[SwiftlyBhopTimer] Could not read player position. PawnType={pawn?.GetType().FullName ?? "null"}");
            return;
        }

        try
        {
            var savedPath = _mapDataService
                .SaveBonusMapSettingAsync(_currentMapName, bonusNumber, propertyName, position.ToString())
                .GetAwaiter()
                .GetResult();

            ReloadActiveMapData();
            UpdateZoneSetupPreview(player, $"Bonuses.{bonusNumber}.{propertyName}", position);
            ScheduleZoneRender("bonus_map_setting");
            reply($"[SwiftlyBhopTimer] Saved Bonus #{bonusNumber} {label}: {position}; file={savedPath}");
        }
        catch (Exception ex)
        {
            reply($"[SwiftlyBhopTimer] Failed to save Bonus #{bonusNumber} {label}: {ex.Message}");
        }
    }

    private static bool TryGetBonusNumberArgument(ICommandContext context, int index, out int bonusNumber)
    {
        bonusNumber = 0;
        return context.Args.Length > index &&
               int.TryParse(context.Args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out bonusNumber) &&
               bonusNumber is >= 1 and <= 99;
    }

    private PlayerTimerState? GetSenderStateOrReply(ICommandContext context)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return null;
        }

        return _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
    }

    private bool IsAdmin(IPlayer player)
    {
        return AdminPermissions.Any(permission => Core.Permission.PlayerHasPermission(player.SteamID, permission));
    }

    private string GetMapNameArgument(ICommandContext context)
    {
        return GetMapNameArgument(context, startIndex: 0);
    }

    private string GetMapNameArgument(ICommandContext context, int startIndex)
    {
        if (context.Args.Length > startIndex && !string.IsNullOrWhiteSpace(context.Args[startIndex]))
        {
            return context.Args[startIndex];
        }

        return _currentMapName != "unknown"
            ? _currentMapName
            : _mapDataService?.FindFirstKnownMapName() ?? "unknown";
    }

    private string BuildMapStatusMessage()
    {
        var startZone = _activeMap.StartZone?.ToString() ?? "none";
        var endZone = _activeMap.EndZone?.ToString() ?? "none";
        var respawn = _activeMap.RespawnPosition?.ToString() ?? "none";
        var bonuses = _activeMap.Bonuses.Count == 0
            ? "none"
            : string.Join(",", _activeMap.Bonuses.Keys.OrderBy(number => number).Select(number => $"#{number}"));
        var diagnostics = _mapDataService?.GetDiagnostics() ?? "MapDataService not initialized";

        return $"[SwiftlyBhopTimer] Current map: {_currentMapName}; startTrigger={_activeMap.StartTriggerName}; endTrigger={_activeMap.EndTriggerName}; startZone={startZone}; endZone={endZone}; respawn={respawn}; bonuses={bonuses}; MapData={diagnostics}";
    }

    private static string FormatBonusList(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "none" : value;
    }
}
