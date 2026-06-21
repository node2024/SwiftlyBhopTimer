using System.Globalization;
using System.Reflection;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Players;
using SwiftlyBhopTimer.Services;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private const int SpectateTargetRetryTicks = 192;
    private const int SpectateTargetCycleFallbackDelayTicks = 8;
    private const int SpectateTargetCycleFallbackIntervalTicks = 2;
    private const int SpectateTargetHelperRetryIntervalTicks = 16;
    private const int SpectatorTeamNumber = 1;
    private const int CounterTerroristTeamNumber = 3;
    private const int ObserverModeInEye = 4;

    private string? ResolveSpecTargetName(ICommandContext context)
    {
        var requested = context.Args.Length > 0 ? context.Args[0].Trim() : "";
        if (requested.Equals("pb", StringComparison.OrdinalIgnoreCase) ||
            requested.Equals("pbreplay", StringComparison.OrdinalIgnoreCase))
        {
            var steamId = context.Sender?.SteamID.ToString();
            if (!string.IsNullOrWhiteSpace(steamId))
            {
                var state = _activePersonalBestReplayBots.Values.FirstOrDefault(state =>
                    string.Equals(state.SteamId, steamId, StringComparison.Ordinal) &&
                    state.HudLabel.Contains("PB", StringComparison.OrdinalIgnoreCase));
                if (state is not null)
                {
                    return state.BotName;
                }
            }

            return GetPersonalBestReplayBot()?.Name;
        }

        if (requested.Equals("sr", StringComparison.OrdinalIgnoreCase) ||
            requested.Equals("replay", StringComparison.OrdinalIgnoreCase))
        {
            var mode = context.Sender is null ? TimerRunMode.Standard : GetPlayerTimerMode(context.Sender);
            return GetServerReplayBot(mode)?.Name ?? GetServerReplayBotDisplayName(mode);
        }

        if (requested.Equals("sr-st", StringComparison.OrdinalIgnoreCase) ||
            requested.Equals("srstd", StringComparison.OrdinalIgnoreCase) ||
            requested.Equals("standard", StringComparison.OrdinalIgnoreCase))
        {
            return GetServerReplayBot(TimerRunMode.Standard)?.Name ?? GetServerReplayBotDisplayName(TimerRunMode.Standard);
        }

        if (requested.Equals("sr-cl", StringComparison.OrdinalIgnoreCase) ||
            requested.Equals("srclassic", StringComparison.OrdinalIgnoreCase) ||
            requested.Equals("classic", StringComparison.OrdinalIgnoreCase))
        {
            if (!TimerRunModes.ClassicEnabled)
            {
                return GetServerReplayBot(TimerRunMode.Standard)?.Name ?? GetServerReplayBotDisplayName(TimerRunMode.Standard);
            }

            return GetServerReplayBot(TimerRunMode.Classic)?.Name ?? GetServerReplayBotDisplayName(TimerRunMode.Classic);
        }

        if (!string.IsNullOrWhiteSpace(requested))
        {
            return string.Join(' ', context.Args).Trim();
        }

        var defaultMode = context.Sender is null ? TimerRunMode.Standard : GetPlayerTimerMode(context.Sender);
        return GetServerReplayBot(defaultMode)?.Name ??
               (_replayService?.HasReplay(_currentMapName, defaultMode) == true ? GetServerReplayBotDisplayName(defaultMode) : null);
    }

    private void MovePlayerToSpectator(IPlayer player, PlayerTimerState state)
    {
        _replayService?.DiscardRecording(player);
        ResetTimerStateWithStopSound(player, state);
        state.IsTimerBlocked = false;
        state.IsTimerPaused = false;
        state.IsNoclipEnabled = false;
        state.NoclipSyncGraceTicks = 0;
        state.NoclipReapplyTicks = 0;
        state.PendingRestartCheckpoint = null;
        state.PendingRestartPosition = null;
        state.PendingRestartTeleportTicks = 0;
        state.StartSpeedLimitPauseTicks = 0;
        StopPauseFreeze(state);
        _noclipService?.Apply(player, enabled: false);
        ApplyNoclipHelper(player, enabled: false);

        if (!TryChangeTeam(player, SpectatorTeamNumber))
        {
            ClientCommandInvoker.TryExecute(player, "jointeam 1");
            ClientCommandInvoker.TryExecute(player, "spectate");
        }
    }

    private void QueueSpectateTarget(IPlayer player, PlayerTimerState state, string targetName)
    {
        state.PendingSpectateTargetName = targetName;
        state.ActiveReplayTargetName = targetName;
        var target = FindSpectateTargetByName(targetName);
        state.PendingSpectateTargetSlot = target?.Slot;
        state.ActiveSpectateTargetSlot = target?.Slot;
        state.PendingSpectateTargetTicks = SpectateTargetRetryTicks;
        Core.Scheduler.NextTick(() => TryApplySpectateTarget(player, state));
    }

    private void ProcessPendingSpectateTarget(IPlayer player, PlayerTimerState state)
    {
        if (state.PendingSpectateTargetTicks <= 0 || string.IsNullOrWhiteSpace(state.PendingSpectateTargetName))
        {
            if (state.PendingSpectateTargetTicks <= 0)
            {
                state.PendingSpectateTargetName = null;
                state.PendingSpectateTargetSlot = null;
            }

            return;
        }

        state.PendingSpectateTargetTicks--;
        if (TryApplySpectateTarget(player, state))
        {
            state.PendingSpectateTargetTicks = 0;
            state.PendingSpectateTargetName = null;
            state.PendingSpectateTargetSlot = null;
        }
    }

    private bool TryApplySpectateTarget(IPlayer viewer, PlayerTimerState state)
    {
        if (string.IsNullOrWhiteSpace(state.PendingSpectateTargetName))
        {
            return false;
        }

        var target = FindSpectateTargetByName(state.PendingSpectateTargetName);
        if (target is null || !target.IsAlive)
        {
            return false;
        }

        state.PendingSpectateTargetSlot = target.Slot;

        if (viewer.IsAlive && TryGetTeamNumber(viewer) is not SpectatorTeamNumber)
        {
            MovePlayerToSpectator(viewer, state);
            return false;
        }

        var elapsedTicks = GetSpectateTargetElapsedTicks(state);
        ApplySpectateTargetHelperIfNeeded(viewer, target, elapsedTicks);
        if (elapsedTicks < SpectateTargetCycleFallbackDelayTicks)
        {
            ApplySpectateTargetCommands(viewer, target);
        }
        else
        {
            ClientCommandInvoker.TryExecute(viewer, $"spec_mode {ObserverModeInEye}");
        }

        var isTargetObserved = IsSamePlayer(TryGetObservedPlayer(viewer), target);
        if (isTargetObserved)
        {
            state.ActiveReplayTargetName = target.Name;
            state.ActiveSpectateTargetSlot = target.Slot;
            return true;
        }

        ApplySpectateCycleFallbackIfNeeded(viewer, elapsedTicks);
        state.ActiveReplayTargetName = target.Name;
        state.ActiveSpectateTargetSlot = target.Slot;
        return false;
    }

    private void ApplySpectateTargetHelperIfNeeded(IPlayer viewer, IPlayer target, int elapsedTicks)
    {
        if (elapsedTicks < SpectateTargetCycleFallbackDelayTicks ||
            elapsedTicks % SpectateTargetHelperRetryIntervalTicks == 0)
        {
            _movementBridgeService?.SetSpectateTarget(viewer, target);
        }
    }

    private static void ApplySpectateTargetCommands(IPlayer viewer, IPlayer target)
    {
        var escapedName = EscapeClientCommandArgument(target.Name);
        var entityIndex = GetSpectateEntityIndex(target);

        ClientCommandInvoker.TryExecute(viewer, $"spec_mode {ObserverModeInEye}");
        ClientCommandInvoker.TryExecute(viewer, $"spec_player_by_name \"{escapedName}\"");
        ClientCommandInvoker.TryExecute(viewer, $"spec_player \"{escapedName}\"");

        if (target.UserID > 0)
        {
            ClientCommandInvoker.TryExecute(viewer, $"spec_player_by_userid {target.UserID}");
            ClientCommandInvoker.TryExecute(viewer, $"spec_player #{target.UserID}");
            ClientCommandInvoker.TryExecute(viewer, $"spec_player {target.UserID}");
        }

        if (target.SteamID > 0)
        {
            ClientCommandInvoker.TryExecute(viewer, $"spec_player_by_accountid {target.SteamID}");
            ClientCommandInvoker.TryExecute(viewer, $"spec_player_by_xuid {target.SteamID}");
        }

        if (entityIndex > 0)
        {
            ClientCommandInvoker.TryExecute(viewer, $"spec_player {entityIndex}");
        }

        if (target.Slot >= 0)
        {
            ClientCommandInvoker.TryExecute(viewer, $"spec_player {target.Slot}");
        }
    }

    private static void ApplySpectateCycleFallbackIfNeeded(IPlayer viewer, int elapsedTicks)
    {
        if (elapsedTicks < SpectateTargetCycleFallbackDelayTicks ||
            elapsedTicks % SpectateTargetCycleFallbackIntervalTicks != 0)
        {
            return;
        }

        ClientCommandInvoker.TryExecute(viewer, "spec_next");
        ClientCommandInvoker.TryExecute(viewer, $"spec_mode {ObserverModeInEye}");
    }

    private static int GetSpectateTargetElapsedTicks(PlayerTimerState state)
    {
        return Math.Max(0, SpectateTargetRetryTicks - state.PendingSpectateTargetTicks);
    }

    private static int GetSpectateEntityIndex(IPlayer target)
    {
        foreach (var candidate in new object?[] { target.PlayerPawn, target.Pawn, target.Controller, target.ServerSideClient, target })
        {
            if (candidate is not null &&
                EntityReflection.TryGetEntityIndex(candidate, out var entityIndex) &&
                entityIndex > 0)
            {
                return entityIndex;
            }
        }

        return target.Slot >= 0 ? target.Slot + 1 : 0;
    }

    private IPlayer? FindSpectateTargetByName(string targetName)
    {
        return FindReplayBotByName(targetName, allowPrefixFallback: true) ?? FindHumanPlayerByName(targetName);
    }

    private IPlayer? FindReplayBotByName(string targetName, bool allowPrefixFallback = false)
    {
        var exactMatch = Core.PlayerManager.GetBots()
            .FirstOrDefault(player =>
                player is { IsValid: true, IsFakeClient: true } &&
                string.Equals(player.Name, targetName, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null || !allowPrefixFallback)
        {
            return exactMatch;
        }

        var tag = GetReplayBotNameTag(targetName);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return Core.PlayerManager.GetBots()
                .FirstOrDefault(player =>
                    player is { IsValid: true, IsFakeClient: true } &&
                    player.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase));
        }

        return Core.PlayerManager.GetBots()
            .FirstOrDefault(player =>
                player is { IsValid: true, IsFakeClient: true } &&
                player.Name.StartsWith(tag, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryChangeTeam(IPlayer player, int teamNumber)
    {
        foreach (var target in new object?[] { player, player.Controller, player.ServerSideClient })
        {
            if (target is null)
            {
                continue;
            }

            if (TryInvokeTeamMethod(target, teamNumber))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryInvokeTeamMethod(object target, int teamNumber)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var methodName in new[] { "ChangeTeam", "SwitchTeam", "JoinTeam", "SetTeam" })
        {
            foreach (var method in target.GetType().GetMethods(flags).Where(method => method.Name == methodName))
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 1)
                {
                    continue;
                }

                if (TryConvertTeamArgument(teamNumber, parameters[0].ParameterType, out var argument) &&
                    TryInvokeMethod(target, method, argument))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryConvertTeamArgument(int teamNumber, Type parameterType, out object? argument)
    {
        argument = null;
        try
        {
            if (parameterType.IsEnum)
            {
                argument = Enum.ToObject(parameterType, teamNumber);
                return true;
            }

            argument = Convert.ChangeType(teamNumber, parameterType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryInvokeMethod(object target, MethodInfo method, object? argument)
    {
        try
        {
            method.Invoke(target, [argument]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string EscapeClientCommandArgument(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
