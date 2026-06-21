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
    private void RegisterPlayerCommand(string commandName, ICommandService.CommandListener handler, string helpText)
    {
        _commandRegistrations.Add(Core.Command.RegisterCommand(commandName, handler, registerRaw: true, permission: "", helpText));
    }

    private void Reply(ICommandContext context, string message)
    {
        if (context.Sender is not null)
        {
            SendChat(context.Sender, message);
            return;
        }

        context.Reply(FormatChat(message));
    }

    private void SendChat(IPlayer player, string message)
    {
        player.SendChat(FormatChat(message));
    }

    private Task SendChatAsync(IPlayer player, string message)
    {
        return player.SendChatAsync(FormatChat(message));
    }

    private void SendChatAll(string message)
    {
        var formatted = FormatChat(message);
        foreach (var player in Core.PlayerManager.GetAllValidPlayers().Where(IsPlayablePlayer))
        {
            player.SendChat(formatted);
        }
    }

    private IReadOnlyList<IPlayer> GetHideTargetPlayers(IEnumerable<IPlayer>? knownPlayablePlayers = null)
    {
        var targets = new List<IPlayer>();
        var seenSlots = new HashSet<int>();

        foreach (var player in knownPlayablePlayers ?? Core.PlayerManager.GetAllValidPlayers().Where(IsPlayablePlayer))
        {
            if (player is { IsValid: true } && seenSlots.Add(player.Slot))
            {
                targets.Add(player);
            }
        }

        foreach (var bot in Core.PlayerManager.GetBots().Where(IsHideableBot))
        {
            if (seenSlots.Add(bot.Slot))
            {
                targets.Add(bot);
            }
        }

        return targets;
    }

    private static bool IsHideableBot(IPlayer? player)
    {
        return player is { IsValid: true, IsFakeClient: true };
    }

    private string FormatChat(string message)
    {
        return _chatFormatService?.Format(message) ?? message;
    }

    private void SavePlayerSettings(PlayerTimerState state)
    {
        try
        {
            state.SettingsLoaded = true;
            _playerSettingsService?.Save(state);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to save player settings for {state.SteamId}: {ex.Message}");
        }
    }

    private void ApplyPlayerSettingsIfNeeded(IPlayer player, PlayerTimerState state)
    {
        if (state.SettingsLoaded || string.IsNullOrWhiteSpace(state.SteamId) || state.SteamId == "0")
        {
            return;
        }

        try
        {
            _playerSettingsService?.Apply(state.SteamId, state);
            state.TimerMode = TimerRunMode.Standard;
            state.SettingsLoaded = true;
            _playerVisualService?.ApplySelfVisuals(player, state);
            if (state.HideFpsViewModel)
            {
                _playerVisualService?.ApplyHideFps(player, state);
            }
            else
            {
                _playerVisualService?.ResetHideFps(player, state);
            }
            ApplyPlayerFov(player, state);
            ApplyPlayerModeIfOnTimerTeam(player, state);
        }
        catch (Exception ex)
        {
            state.SettingsLoaded = true;
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to load player settings for {state.SteamId}: {ex.Message}");
        }
    }

    private void ApplyPlayerMode(IPlayer player, PlayerTimerState state)
    {
        state.TimerMode = TimerRunModes.Normalize(state.TimerMode);
        _movementBridgeService?.SetBhopMode(player, state.TimerMode);
    }

    private void ApplyPlayerModeIfOnTimerTeam(IPlayer player, PlayerTimerState state)
    {
        if (IsPlayerOnTimerTeam(player))
        {
            ApplyPlayerMode(player, state);
        }
    }

    private void ResetPlayerModeToStandard(IPlayer? player)
    {
        if (player is { IsValid: true })
        {
            _movementBridgeService?.SetBhopMode(player, TimerRunMode.Standard);
        }
    }

    private void ResetAllPlayerModesToStandard(string reason)
    {
        foreach (var state in _timerStateStore.All())
        {
            state.TimerMode = TimerRunMode.Standard;
        }

        foreach (var player in Core.PlayerManager.GetAllValidPlayers().Where(IsPlayablePlayer))
        {
            var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
            state.TimerMode = TimerRunMode.Standard;
        }

        for (var slot = 0; slot < 65; slot++)
        {
            ExecuteServerCommand($"sbt_bhop_mode {slot} {TimerRunModes.StandardKey}");
        }

        Core.Logger.LogInformation("Reset all bhop modes to Standard. Reason: {Reason}", reason);
    }

    private static bool IsPlayerOnTimerTeam(IPlayer player)
    {
        var controllerTeam = player.Controller?.TeamNum;
        if (controllerTeam is 2 or 3)
        {
            return true;
        }

        return TryGetTeamNumber(player) is 2 or 3;
    }

    private static bool IsPlayerSpectator(IPlayer player)
    {
        var controllerTeam = player.Controller?.TeamNum;
        if (controllerTeam is 1)
        {
            return true;
        }

        return TryGetTeamNumber(player) is 1;
    }

    private TimerRunMode GetPlayerTimerMode(IPlayer? player)
    {
        if (!IsPlayablePlayer(player))
        {
            return TimerRunMode.Standard;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        state.TimerMode = TimerRunModes.Normalize(state.TimerMode);
        return state.TimerMode;
    }

    private TimerRunMode GetContextTimerMode(ICommandContext context)
    {
        return GetPlayerTimerMode(context.Sender);
    }
}
