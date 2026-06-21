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
    private void RegisterGameEventHooks()
    {
        _gameEventRegistrations.Add(Core.GameEvent.HookPost<EventServerSpawn>(OnServerSpawn));
        _gameEventRegistrations.Add(Core.GameEvent.HookPost<EventGameNewmap>(OnGameNewmap));
        _gameEventRegistrations.Add(Core.GameEvent.HookPost<EventPlayerTeam>(OnPlayerTeam));
        _gameEventRegistrations.Add(Core.GameEvent.HookPost<EventPlayerSpawn>(OnPlayerSpawn));
        _gameEventRegistrations.Add(Core.GameEvent.HookPost<EventPlayerSpawned>(OnPlayerSpawned));
        _gameEventRegistrations.Add(Core.GameEvent.HookPost<EventRoundStart>(OnRoundStart));
    }

    private EventDelegates.OnClientPutInServer? _onClientPutInServer;

    private void OnClientPutInServer(IOnClientPutInServerEvent @event)
    {
        var player = Core.PlayerManager.GetPlayer(@event.PlayerId);
        if (!IsPlayablePlayer(player))
        {
            return;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        _playerVisualService?.ResetHideFps(player);
        ApplyPlayerSettingsIfNeeded(player, state);
        SendChat(player, $"[SwiftlyBhopTimer] Loaded. Map: {_currentMapName}");
        ScheduleZoneRender("client_put_in_server");
    }

    private EventDelegates.OnClientDisconnected? _onClientDisconnected;

    private void OnClientDisconnected(IOnClientDisconnectedEvent @event)
    {
        ExecuteServerCommand($"sbt_noclip {@event.PlayerId} 0");
        ExecuteServerCommand($"sbt_bhop_mode {@event.PlayerId} {TimerRunModes.StandardKey}");
        _playerVisualService?.ResetHideFpsSlot(@event.PlayerId);
        _timerStateStore.Remove(@event.PlayerId);
        _replayService?.DiscardRecording(@event.PlayerId);
        _zoneSetupPreviews.Remove(@event.PlayerId);
        _zoneRenderService?.ClearPreview(@event.PlayerId);
    }

    private EventDelegates.OnMapLoad? _onMapLoad;

    private void OnMapLoad(IOnMapLoadEvent @event)
    {
        LoadMap(@event.MapName);
    }

    private HookResult OnServerSpawn(EventServerSpawn @event)
    {
        LoadMap(@event.MapName);
        return HookResult.Continue;
    }

    private HookResult OnGameNewmap(EventGameNewmap @event)
    {
        LoadMap(@event.MapName);
        return HookResult.Continue;
    }

    private HookResult OnPlayerTeam(EventPlayerTeam @event)
    {
        if (!@event.Disconnect)
        {
            if (@event.Team is 2 or 3)
            {
                ClearNoclipStateAfterPawnReset(@event.UserIdPlayer, "team change");
                ResetAfterTeamChange(@event.UserIdPlayer, @event.Team);
                ScheduleTeamChangeRecovery(@event.UserIdPlayer, @event.Team);
            }
            else
            {
                ResetPlayerModeToStandard(@event.UserIdPlayer);
                ClearSpectatorHud(@event.UserIdPlayer);
            }

            ScheduleZoneRender("player_team");
        }

        return HookResult.Continue;
    }

    private void ClearSpectatorHud(IPlayer? player)
    {
        if (!IsPlayablePlayer(player))
        {
            return;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        state.ActiveReplayTargetName = null;
        state.ActiveSpectateTargetSlot = null;
        state.PendingSpectateTargetName = null;
        state.PendingSpectateTargetSlot = null;
        state.PendingSpectateTargetTicks = 0;
        ClearHud(player);
    }

    private void ResetAfterTeamChange(IPlayer? player, byte team)
    {
        if (team is not (2 or 3) || !IsPlayablePlayer(player))
        {
            return;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        state.ActiveReplayTargetName = null;
        state.ActiveSpectateTargetSlot = null;
        state.PendingSpectateTargetName = null;
        state.PendingSpectateTargetSlot = null;
        state.PendingSpectateTargetTicks = 0;
        if (state.TeamSwitchSuppressTicks > 0)
        {
            return;
        }

        _replayService?.DiscardRecording(player);
        ResetTimerStateWithStopSound(player, state);
        state.IsNoclipEnabled = false;
        state.NoclipSyncGraceTicks = 0;
        state.NoclipReapplyTicks = 0;
        state.StartSpeedLimitPauseTicks = RestartSpeedLimitPauseTicks;
        state.PendingRestartCheckpoint = null;
        state.PendingRestartPosition = null;
        state.PendingRestartTeleportTicks = 0;
        state.PendingSecondaryWeaponTicks = SecondaryWeaponGiveDelayTicks;
        _noclipService?.Apply(player, enabled: false);
        ApplyNoclipHelper(player, enabled: false);
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event)
    {
        ClearMovementStateAfterPawnReset(@event.UserIdPlayer, "spawn reset");
        ReapplyHideFpsAfterPawnReset(@event.UserIdPlayer);
        ReapplyPlayerModeAfterPawnReset(@event.UserIdPlayer);
        QueueSecondaryWeaponGive(@event.UserIdPlayer);
        ScheduleZoneRender("player_spawn");
        return HookResult.Continue;
    }

    private HookResult OnPlayerSpawned(EventPlayerSpawned @event)
    {
        ClearMovementStateAfterPawnReset(@event.UserIdPlayer, "spawn reset");
        ReapplyHideFpsAfterPawnReset(@event.UserIdPlayer);
        ReapplyPlayerModeAfterPawnReset(@event.UserIdPlayer);
        QueueSecondaryWeaponGive(@event.UserIdPlayer);
        ScheduleZoneRender("player_spawned");
        return HookResult.Continue;
    }

    private void ReapplyHideFpsAfterPawnReset(IPlayer? player)
    {
        if (!IsPlayablePlayer(player))
        {
            return;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        if (state.HideFpsViewModel)
        {
            _playerVisualService?.ApplyHideFps(player, state, refreshTicks: 0);
        }
        else
        {
            _playerVisualService?.ResetHideFps(player, state);
        }
    }

    private void ReapplyPlayerModeAfterPawnReset(IPlayer? player)
    {
        if (!IsPlayablePlayer(player))
        {
            return;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        ApplyPlayerModeIfOnTimerTeam(player, state);
    }

    private void ClearMovementStateAfterPawnReset(IPlayer? player, string reason)
    {
        if (!IsPlayablePlayer(player))
        {
            return;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        state.IsTimerPaused = false;
        state.PauseFreezePosition = null;
        state.IsNoclipEnabled = false;
        state.NoclipSyncGraceTicks = 0;
        state.NoclipReapplyTicks = 0;
        _noclipService?.Apply(player, enabled: false);
        ApplyNoclipHelper(player, enabled: false);
    }

    private EventDelegates.OnEntityTakeDamage? _onEntityTakeDamage;

    private void OnEntityTakeDamage(IOnEntityTakeDamageEvent @event)
    {
        var result = _playerProtectionService?.HandleDamage(@event.Entity) ?? HookResult.Continue;
        if (result != HookResult.Continue)
        {
            @event.Result = result;
        }
    }

    private HookResult OnClientCommand(int playerId, string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return HookResult.Continue;
        }

        if (TryHandleJoinTeamCommand(playerId, commandLine))
        {
            return HookResult.Stop;
        }

        if (TryHandleMapShortcutChat(playerId, commandLine))
        {
            return HookResult.Stop;
        }

        if (TryHandleAdminBonusSelectionChat(playerId, commandLine))
        {
            return HookResult.Stop;
        }

        if (TryHandleCheckpointRawCommand(playerId, commandLine))
        {
            return HookResult.Stop;
        }

        var parts = commandLine.TrimStart().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0];
        if (string.Equals(command, "drop", StringComparison.OrdinalIgnoreCase))
        {
            return HookResult.Stop;
        }

        return HookResult.Continue;
    }

    private bool TryHandleJoinTeamCommand(int playerId, string commandLine)
    {
        var parts = commandLine.TrimStart().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !parts[0].Equals("jointeam", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var team) || team is not (2 or 3))
        {
            return false;
        }

        var player = Core.PlayerManager.GetPlayer(playerId);
        if (!IsPlayablePlayer(player))
        {
            return false;
        }

        _ = TryChangeTeam(player!, team);
        _movementBridgeService?.ForceTeam(player!, team);
        Core.Scheduler.NextTick(() => FinalizeForcedJoinTeam(player!, (byte)team));
        return true;
    }

    private void FinalizeForcedJoinTeam(IPlayer player, byte team)
    {
        if (!IsPlayablePlayer(player) || team is not (2 or 3))
        {
            return;
        }

        ResetAfterTeamChange(player, team);
        ScheduleTeamChangeRecovery(player, team);
        ScheduleZoneRender("force_jointeam");
    }

    private bool TryHandleMapShortcutChat(int playerId, string commandLine)
    {
        if (!TryParseChatText(commandLine, out var text))
        {
            return false;
        }

        text = text.Trim();
        if (text.Length < 4 || text[0] is not ('!' or '/'))
        {
            return false;
        }

        var commandText = text[1..].TrimStart();
        var parts = commandText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].Equals("map", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var player = Core.PlayerManager.GetPlayer(playerId);
        if (!IsPlayablePlayer(player))
        {
            return true;
        }

        if (!IsAdmin(player!))
        {
            SendChat(player!, "[SwiftlyBhopTimer] You do not have permission to change map.");
            return true;
        }

        var query = parts.Length > 1 ? parts[1].Trim() : "";
        ChangeMapFromAdmin(player!, query);
        return true;
    }

    private bool TryHandleAdminBonusSelectionChat(int playerId, string commandLine)
    {
        if (!TryParseChatText(commandLine, out var text))
        {
            return false;
        }

        var player = Core.PlayerManager.GetPlayer(playerId);
        if (!IsPlayablePlayer(player))
        {
            return false;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        if (state.PendingAdminBonusSelectionTicks <= 0)
        {
            return false;
        }

        if (!IsAdmin(player))
        {
            state.PendingAdminBonusSelectionTicks = 0;
            return false;
        }

        text = text.Trim();
        if (text.Equals("cancel", StringComparison.OrdinalIgnoreCase))
        {
            state.PendingAdminBonusSelectionTicks = 0;
            SendChat(player, $"{label("Bonus setup")} {label("|")} {gray("cancelled")}");
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bonusNumber) ||
            bonusNumber is < 1 or > 99)
        {
            SendChat(player, $"{label("Bonus setup")} {label("|")} {red("enter a number from 1 to 99")} {gray("or type cancel")}");
            return true;
        }

        state.PendingAdminBonusSelectionTicks = 0;
        Core.Scheduler.NextTick(() => ShowAdminBonusZonesMenu(player, bonusNumber));
        return true;
    }

    private static bool TryParseChatText(string commandLine, out string text)
    {
        text = "";
        var trimmed = commandLine.TrimStart();
        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 ||
            (!parts[0].Equals("say", StringComparison.OrdinalIgnoreCase) &&
             !parts[0].Equals("say_team", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (parts.Length < 2)
        {
            return true;
        }

        text = parts[1].Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            text = text[1..^1];
        }

        return true;
    }

    private void ScheduleTeamChangeRecovery(IPlayer? player, byte team)
    {
        if (team is not (2 or 3) || !IsPlayablePlayer(player))
        {
            return;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        state.TeamSwitchSuppressTicks = Math.Max(state.TeamSwitchSuppressTicks, 8);

        Core.Scheduler.NextTick(() =>
        {
            if (!IsPlayablePlayer(player))
            {
                return;
            }

            var currentState = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
            ClearMovementStateAfterPawnReset(player, "team recovery");
            _noclipService?.Apply(player, enabled: false);

            try
            {
                if (!player.IsAlive)
                {
                    player.Respawn();
                }
            }
            catch (Exception ex)
            {
                Core.Logger.LogWarning(ex, "Team change recovery respawn failed for player {Player}.", player.Name);
            }

            currentState.PendingRestartCheckpoint = null;
            if (GetConfiguredRestartPosition() is { } destination)
            {
                currentState.PendingRestartPosition = destination;
                currentState.PendingRestartTeleportTicks = TeamChangeTeleportAfterSpawnTicks;
            }
            else
            {
                _movementBridgeService?.QueueRestartTeleportToMapStart(player, GetStartZoneRestartFallbackPosition(), TeamChangeTeleportAfterSpawnTicks);
            }

            currentState.StartSpeedLimitPauseTicks = RestartSpeedLimitPauseTicks;
            currentState.PendingSecondaryWeaponTicks = Math.Max(currentState.PendingSecondaryWeaponTicks, SecondaryWeaponGiveDelayTicks);
            ApplyPlayerModeIfOnTimerTeam(player, currentState);
            Core.Scheduler.NextTick(() =>
            {
                if (!IsPlayablePlayer(player))
                {
                    return;
                }

                var settledState = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
                ApplyPlayerModeIfOnTimerTeam(player, settledState);
            });
        });
    }

    private HookResult OnRoundStart(EventRoundStart @event)
    {
        _roundFlowConfigService?.ApplyFull(includeTimeLimit: false);
        ApplyBhopHelperDefaults("round_start");
        foreach (var mode in ServerReplayModes)
        {
            _replayService?.ResetServerReplayPlayback(_currentMapName, mode);
        }

        ScheduleZoneRender("round_start");
        return HookResult.Continue;
    }

    private EventDelegates.OnEntityStartTouch? _onEntityStartTouch;

    private void OnEntityStartTouch(IOnEntityStartTouchEvent @event)
    {
        if (!TryResolveTouch(@event.Entity, @event.OtherEntity, out var player, out var triggerName))
        {
            return;
        }

        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        if (state.IsTimerBlocked || state.IsNoclipEnabled)
        {
            return;
        }

        if (state.IsTimerPaused)
        {
            return;
        }

        if (_activeMap.IsStartTrigger(triggerName) && IsAnyTimerRunning(state))
        {
            _replayService?.DiscardRecording(player);
            ResetTimerStateWithStopSound(player, state);
            SendChat(player, $"{gray("Timer")} reset.");
        }

        if (TryParseBonusStartTrigger(triggerName, out var startBonusNumber) && IsAnyTimerRunning(state))
        {
            _replayService?.DiscardRecording(player);
            ResetTimerStateWithStopSound(player, state);
            SendChat(player, $"{label("Bonus")} {gold($"#{startBonusNumber}")} {label("|")} {gray("reset")}");
        }

        if (_activeMap.IsEndTrigger(triggerName) && state.IsTimerRunning && !state.IsTimerPaused)
        {
            StopTimer(player, state);
        }

        if (TryParseBonusEndTrigger(triggerName, out var endBonusNumber) &&
            state.IsBonusTimerRunning &&
            state.CurrentBonusNumber == endBonusNumber &&
            !state.IsTimerPaused)
        {
            StopBonusTimer(player, state);
        }

        TryNotifyStage(player, state, triggerName);

        if (state.DebugTouches)
        {
            SendChat(player, $"Debug | StartTouch {gray(triggerName)}");
        }
    }

    private EventDelegates.OnEntityEndTouch? _onEntityEndTouch;

    private void OnEntityEndTouch(IOnEntityEndTouchEvent @event)
    {
        if (!TryResolveTouch(@event.Entity, @event.OtherEntity, out var player, out var triggerName))
        {
            return;
        }

        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        if (state.IsTimerBlocked || state.IsNoclipEnabled)
        {
            return;
        }

        if (state.IsTimerPaused)
        {
            return;
        }

        if (_activeMap.IsStartTrigger(triggerName) && !IsAnyTimerRunning(state))
        {
            StartTimer(player, state);
        }

        if (TryParseBonusStartTrigger(triggerName, out var bonusNumber) && !IsAnyTimerRunning(state))
        {
            StartBonusTimer(player, state, bonusNumber);
        }

        if (state.DebugTouches)
        {
            SendChat(player, $"Debug | EndTouch {gray(triggerName)}");
        }
    }

    private bool TryResolveTouch(object entity, object otherEntity, out IPlayer player, out string triggerName)
    {
        player = null!;
        triggerName = "";

        var firstDesignerName = EntityReflection.GetDesignerName(entity);
        var secondDesignerName = EntityReflection.GetDesignerName(otherEntity);

        object? playerEntity = null;
        object? triggerEntity = null;

        if (string.Equals(firstDesignerName, "player", StringComparison.OrdinalIgnoreCase))
        {
            playerEntity = entity;
            triggerEntity = otherEntity;
        }
        else if (string.Equals(secondDesignerName, "player", StringComparison.OrdinalIgnoreCase))
        {
            playerEntity = otherEntity;
            triggerEntity = entity;
        }

        if (playerEntity is null || triggerEntity is null)
        {
            return false;
        }

        player = EntityReflection.GetPlayerFromPawn(Core.PlayerManager, playerEntity)!;
        if (!IsPlayablePlayer(player))
        {
            return false;
        }

        triggerName = EntityReflection.GetEntityName(triggerEntity);
        return !string.IsNullOrWhiteSpace(triggerName);
    }

    private bool TryParseBonusStartTrigger(string triggerName, out int bonusNumber)
    {
        if (_activeMap.TryGetBonusStartTrigger(triggerName, out bonusNumber))
        {
            return true;
        }

        return TryParseBonusTrigger(BonusStartTriggerPattern, triggerName, out bonusNumber);
    }

    private bool TryParseBonusEndTrigger(string triggerName, out int bonusNumber)
    {
        if (_activeMap.TryGetBonusEndTrigger(triggerName, out bonusNumber))
        {
            return true;
        }

        return TryParseBonusTrigger(BonusEndTriggerPattern, triggerName, out bonusNumber);
    }

    private static bool TryParseBonusTrigger(Regex pattern, string triggerName, out int bonusNumber)
    {
        bonusNumber = 0;
        if (string.IsNullOrWhiteSpace(triggerName))
        {
            return false;
        }

        var match = pattern.Match(triggerName);
        if (!match.Success)
        {
            return false;
        }

        var value = match.Groups["bonus"].Success
            ? match.Groups["bonus"].Value
            : match.Groups["timerbonus"].Value;
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out bonusNumber) &&
               bonusNumber is >= 1 and <= 99;
    }

    private static bool IsPlayablePlayer(IPlayer? player)
    {
        return player is { IsValid: true, IsFakeClient: false };
    }
}
