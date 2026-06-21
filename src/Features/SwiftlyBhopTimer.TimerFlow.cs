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
    private void ApplyCoordinateZoneTimer(IPlayer player, PlayerTimerState state)
    {
        if (state.IsTimerBlocked || state.IsNoclipEnabled)
        {
            return;
        }

        EnsureActiveMapDataAvailable("coordinate timer");

        var pawn = player.PlayerPawn ?? player.Pawn;
        if (!EntityReflection.TryGetPosition(pawn, out var position))
        {
            if (state.DebugTouches)
            {
                SendChat(player, $"[SwiftlyBhopTimer] Position unavailable. PawnType={pawn?.GetType().FullName ?? "null"}");
            }

            return;
        }

        var insideStart = _activeMap.IsInsideStartZone(position);
        var insideEnd = _activeMap.IsInsideEndZone(position);

        if (insideStart)
        {
            LimitStartZoneSpeed(player, pawn, state);
        }

        if (insideStart && !state.WasInsideStartZone && IsAnyTimerRunning(state))
        {
            _replayService?.DiscardRecording(player);
            ResetTimerStateWithStopSound(player, state);
            SendChat(player, $"{gray("Timer")} reset.");
        }

        if (state.WasInsideStartZone && !insideStart && !IsAnyTimerRunning(state))
        {
            StartTimer(player, state);
        }

        if (state.DebugTouches && (insideStart != state.WasInsideStartZone || insideEnd != state.WasInsideEndZone))
        {
            SendChat(player, $"Debug | pos={gray(position.ToString())}; start={insideStart}; end={insideEnd}");
        }

        if (insideEnd && !state.WasInsideEndZone && state.IsTimerRunning && !state.IsTimerPaused)
        {
            StopTimer(player, state);
        }

        state.WasInsideStartZone = insideStart;
        state.WasInsideEndZone = insideEnd;

        ApplyCoordinateBonusZoneTimer(player, pawn, position, state);
    }

    private void StartTimer(IPlayer player, PlayerTimerState state)
    {
        ResetBonusTimerState(state, clearLastFinished: true);
        state.IsTimerRunning = true;
        state.IsTimerPaused = false;
        state.TimerTicks = 0;
        state.LastFinishedTicks = null;
        state.CurrentMapStage = 1;
        state.CurrentStageTicks = 0;
        _replayService?.StartRecording(player);
        _timerSoundService?.PlayStart(player, state.SoundsEnabled);
        SendChat(player, $"{green("Timer started")} {label("|")} {gold(TimerRunModes.ToDisplayName(state.TimerMode))} {label("|")} {label("Stage")} {gold("#1")}");
    }

    private void StopTimer(IPlayer player, PlayerTimerState state)
    {
        var finishedTicks = state.TimerTicks;
        state.IsTimerRunning = false;
        state.IsTimerPaused = false;
        state.TimerTicks = 0;
        state.LastFinishedTicks = finishedTicks;
        state.LastHudStatsRefreshUtc = DateTime.MinValue;
        _timerSoundService?.PlayEnd(player, state.SoundsEnabled);

        var replayFrames = _replayService?.FinishRecording(player) ?? [];
        _ = SaveFinishedRunAsync(player.SteamID.ToString(), player.Name, finishedTicks, player, replayFrames, state.TimerMode);
    }

    private void ApplyCoordinateBonusZoneTimer(IPlayer player, object? pawn, Vector3Value position, PlayerTimerState state)
    {
        foreach (var bonus in _activeMap.Bonuses.Values)
        {
            var insideStart = bonus.IsInsideStartZone(position);
            var insideEnd = bonus.IsInsideEndZone(position);
            var wasInsideStart = state.WasInsideBonusStartZones.Contains(bonus.Number);
            var wasInsideEnd = state.WasInsideBonusEndZones.Contains(bonus.Number);

            if (insideStart)
            {
                LimitStartZoneSpeed(player, pawn, state);
            }

            if (insideStart && !wasInsideStart && IsAnyTimerRunning(state))
            {
                _replayService?.DiscardRecording(player);
                ResetTimerStateWithStopSound(player, state);
                SendChat(player, $"{label("Bonus")} {gold($"#{bonus.Number}")} {label("|")} {gray("reset")}");
            }

            if (wasInsideStart && !insideStart && !IsAnyTimerRunning(state))
            {
                StartBonusTimer(player, state, bonus.Number);
            }

            if (insideEnd && !wasInsideEnd && state.IsBonusTimerRunning && state.CurrentBonusNumber == bonus.Number && !state.IsTimerPaused)
            {
                StopBonusTimer(player, state);
            }

            UpdateBonusInsideSet(state.WasInsideBonusStartZones, bonus.Number, insideStart);
            UpdateBonusInsideSet(state.WasInsideBonusEndZones, bonus.Number, insideEnd);
        }
    }

    private void StartBonusTimer(IPlayer player, PlayerTimerState state, int bonusNumber)
    {
        state.IsTimerRunning = false;
        state.IsTimerPaused = false;
        state.TimerTicks = 0;
        state.LastFinishedTicks = null;
        state.IsBonusTimerRunning = true;
        state.CurrentBonusNumber = bonusNumber;
        state.BonusTimerTicks = 0;
        state.LastFinishedBonusTicks = null;
        state.CurrentMapStage = 0;
        state.CurrentStageTicks = null;
        state.HudTicks = 0;
        _replayService?.StartRecording(player);
        _timerSoundService?.PlayStart(player, state.SoundsEnabled);
        SendChat(player, $"{green("Bonus started")} {label("|")} {gold(TimerRunModes.ToDisplayName(state.TimerMode))} {label("|")} {gold($"#{bonusNumber}")}");
    }

    private void StopBonusTimer(IPlayer player, PlayerTimerState state)
    {
        var bonusNumber = state.CurrentBonusNumber;
        var finishedTicks = state.BonusTimerTicks;
        state.IsBonusTimerRunning = false;
        state.IsTimerPaused = false;
        state.BonusTimerTicks = 0;
        state.LastFinishedBonusTicks = finishedTicks;
        state.HudTicks = 0;
        state.LastHudStatsRefreshUtc = DateTime.MinValue;
        _timerSoundService?.PlayEnd(player, state.SoundsEnabled);

        var replayFrames = _replayService?.FinishRecording(player) ?? [];
        _ = SaveFinishedBonusRunAsync(bonusNumber, player.SteamID.ToString(), player.Name, finishedTicks, player, replayFrames, state.TimerMode);
    }

    private void TryNotifyStage(IPlayer player, PlayerTimerState state, string triggerName)
    {
        if (!state.IsTimerRunning || state.IsTimerPaused || !TryParseStageTrigger(triggerName, out var stage) || stage <= 1)
        {
            return;
        }

        if (state.CurrentMapStage == stage)
        {
            return;
        }

        var stageTicks = state.TimerTicks;
        state.CurrentMapStage = stage;
        state.CurrentStageTicks = stageTicks;
        SendChat(player, $"{label("Stage")} {gold($"#{stage}")} {label("|")} {lightBlue(TimeFormatter.FormatTicks(stageTicks))}");
    }

    private static bool TryParseStageTrigger(string triggerName, out int stage)
    {
        stage = 0;
        if (string.IsNullOrWhiteSpace(triggerName))
        {
            return false;
        }

        if (triggerName.Equals("map_start", StringComparison.OrdinalIgnoreCase))
        {
            stage = 1;
            return true;
        }

        var match = StageTriggerPattern.Match(triggerName);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out stage);
    }

    private async Task SaveFinishedRunAsync(string steamId, string playerName, int timerTicks, IPlayer player, IReadOnlyList<ReplayFrame> replayFrames, TimerRunMode mode)
    {
        await SaveFinishedRunAsync(
            _currentMapName,
            "Finished",
            "[New SR]",
            steamId,
            playerName,
            timerTicks,
            player,
            replayFrames,
            mode,
            saveReplays: true,
            refreshServerReplayBot: true);
    }

    private async Task SaveFinishedBonusRunAsync(int bonusNumber, string steamId, string playerName, int timerTicks, IPlayer player, IReadOnlyList<ReplayFrame> replayFrames, TimerRunMode mode)
    {
        await SaveFinishedRunAsync(
            GetBonusRecordMapName(bonusNumber),
            $"Bonus #{bonusNumber} Finished",
            $"[New Bonus #{bonusNumber} SR]",
            steamId,
            playerName,
            timerTicks,
            player,
            replayFrames,
            mode,
            saveReplays: true,
            refreshServerReplayBot: false);
    }

    private async Task SaveFinishedRunAsync(
        string recordMapName,
        string finishLabel,
        string newSrLabel,
        string steamId,
        string playerName,
        int timerTicks,
        IPlayer player,
        IReadOnlyList<ReplayFrame> replayFrames,
        TimerRunMode mode,
        bool saveReplays,
        bool refreshServerReplayBot)
    {
        if (_recordStore is null)
        {
            await SendChatAsync(player, $"{magenta(finishLabel)} {label("|")} {white(TimeFormatter.FormatTicks(timerTicks))}");
            return;
        }

        var oldRank = await _recordStore.GetRankAsync(recordMapName, steamId, mode);
        var oldServerRecord = (await _recordStore.GetTopRecordsAsync(recordMapName, 1, mode)).FirstOrDefault();
        var placement = await _recordStore.GetPlacementForTimeAsync(recordMapName, steamId, timerTicks, mode);
        await _recordStore.SaveIfPersonalBestAsync(recordMapName, steamId, playerName, timerTicks, mode);
        var newRank = await _recordStore.GetRankAsync(recordMapName, steamId, mode);
        var isPersonalBest = oldRank is null || timerTicks <= oldRank.TimerTicks;
        var rankPart = newRank is not null && isPersonalBest
            ? $"{gold($"#{newRank.Placement}")}{gray($"/{newRank.Total}")}"
            : $"{gold($"#{placement.Placement}")}{gray($"/{placement.Total}")}";
        var pbPart = BuildFinishPbPart(oldRank, timerTicks);
        var srPart = BuildFinishSrPart(oldServerRecord, timerTicks);
        var modePart = gold(TimerRunModes.ToDisplayName(mode));

        await SendChatAsync(player, $"{magenta(finishLabel)} {label("|")} {modePart} {label("|")} {white(TimeFormatter.FormatTicks(timerTicks))} {label("| Rank")} {rankPart} {label("|")} {pbPart} {label("|")} {srPart}");
        if (saveReplays && isPersonalBest)
        {
            _replayService?.SavePersonalBestReplay(recordMapName, mode, steamId, playerName, timerTicks, replayFrames);
        }

        if (oldServerRecord is null || timerTicks < oldServerRecord.TimerTicks)
        {
            if (saveReplays)
            {
                _replayService?.SaveReplay(recordMapName, mode, steamId, playerName, timerTicks, replayFrames);
            }

            if (refreshServerReplayBot)
            {
                RefreshServerReplayBotAfterRecordUpdate(mode);
            }

            var srImprovement = oldServerRecord is null
                ? ""
                : $" {label("|")} {lightBlue($"-{TimeFormatter.FormatDeltaTicks(timerTicks - oldServerRecord.TimerTicks)}")}";
            SendChatAll($"{magenta(newSrLabel)} {modePart} {white(TimeFormatter.FormatTicks(timerTicks))} {label("by")} {green(playerName)}{srImprovement}");
        }
    }

    private static bool IsAnyTimerRunning(PlayerTimerState state)
    {
        return state.IsTimerRunning || state.IsBonusTimerRunning;
    }

    private static int GetActiveTimerTicks(PlayerTimerState state)
    {
        return state.IsBonusTimerRunning
            ? state.BonusTimerTicks
            : state.TimerTicks;
    }

    private static void UpdateBonusInsideSet(HashSet<int> insideSet, int bonusNumber, bool inside)
    {
        if (inside)
        {
            insideSet.Add(bonusNumber);
        }
        else
        {
            insideSet.Remove(bonusNumber);
        }
    }

    private string GetBonusRecordMapName(int bonusNumber)
    {
        return GetBonusRecordMapName(_currentMapName, bonusNumber);
    }

    private static string GetBonusRecordMapName(string mapName, int bonusNumber)
    {
        return $"{mapName}_bonus{bonusNumber}";
    }

    private static string BuildFinishPbPart(PlayerRank? oldRank, int timerTicks)
    {
        if (oldRank is null)
        {
            return $"{green("First PB")}";
        }

        var deltaTicks = timerTicks - oldRank.TimerTicks;
        if (deltaTicks < 0)
        {
            return $"{lightBlue($"PB -{TimeFormatter.FormatDeltaTicks(deltaTicks)}")}";
        }

        if (deltaTicks > 0)
        {
            return $"{red($"PB +{TimeFormatter.FormatDeltaTicks(deltaTicks)}")}";
        }

        return $"{gray("PB +00:00.000")}";
    }

    private static string BuildFinishSrPart(PlayerRecordEntry? serverRecord, int timerTicks)
    {
        if (serverRecord is null)
        {
            return $"{green("First SR")}";
        }

        var deltaTicks = timerTicks - serverRecord.TimerTicks;
        if (deltaTicks < 0)
        {
            return $"{lightBlue($"SR -{TimeFormatter.FormatDeltaTicks(deltaTicks)}")}";
        }

        if (deltaTicks > 0)
        {
            return $"{red($"SR +{TimeFormatter.FormatDeltaTicks(deltaTicks)}")}";
        }

        return $"{gray("SR +00:00.000")}";
    }
}
