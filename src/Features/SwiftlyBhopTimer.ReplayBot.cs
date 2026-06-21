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
    private static readonly TimerRunMode[] ServerReplayModes =
        TimerRunModes.ClassicEnabled
            ? [TimerRunMode.Standard, TimerRunMode.Classic]
            : [TimerRunMode.Standard];

    private void ProcessReplayBotPlayback()
    {
        if (_replayService is null || !IsUsableMapName(_currentMapName))
        {
            return;
        }

        RemoveDisabledModeReplayBots();

        foreach (var mode in ServerReplayModes)
        {
            var serverRecordBot = GetServerReplayBot(mode);
            if (serverRecordBot is null)
            {
                continue;
            }

            try
            {
                if (!_lastServerReplayBotSlots.TryGetValue(mode, out var lastSlot) || lastSlot != serverRecordBot.Slot)
                {
                    _lastServerReplayBotSlots[mode] = serverRecordBot.Slot;
                    _replayService.ResetServerReplayPlayback(_currentMapName, mode);
                }

                if (!serverRecordBot.IsAlive)
                {
                    serverRecordBot.Respawn();
                }
                else
                {
                    _replayService.PlaybackServerReplayTick(
                        serverRecordBot,
                        _currentMapName,
                        mode,
                        ReplayService.GetServerReplayPlaybackKey(_currentMapName, mode),
                        loop: true,
                        forceIndexTimeline: true);
                    _replayPlaybackWarningLogged = false;
                }
            }
            catch (Exception ex)
            {
                if (!_replayPlaybackWarningLogged)
                {
                    Console.WriteLine($"[SwiftlyBhopTimer] {TimerRunModes.ToDisplayName(mode)} SR replay playback failed: {ex.Message}");
                    _replayPlaybackWarningLogged = true;
                }
            }
        }

        if (_activePersonalBestReplayBots.Count == 0)
        {
            return;
        }

        var usedAdditionalReplayBotSlots = new HashSet<int>();
        foreach (var state in _activePersonalBestReplayBots.Values.ToArray())
        {
            var replayBot = GetPersonalBestReplayBot(state, usedAdditionalReplayBotSlots);
            if (replayBot is null)
            {
                continue;
            }

            usedAdditionalReplayBotSlots.Add(replayBot.Slot);
            try
            {
                if (!replayBot.IsAlive)
                {
                    replayBot.Respawn();
                    continue;
                }

                if (state.UseServerReplay)
                {
                    _replayService.PlaybackServerReplayTick(replayBot, state.MapName, state.Mode, state.PlaybackKey, loop: false, forceIndexTimeline: true);
                }
                else
                {
                    _replayService.PlaybackPersonalBestTick(replayBot, state.MapName, state.Mode, state.SteamId, state.PlaybackKey, loop: false);
                }

                if (state.KickAfterFirstLoop &&
                    _replayService.GetReplayStatus(state.PlaybackKey) is { FrameIndex: var frameIndex, FrameCount: var frameCount } &&
                    frameCount > 0 &&
                    frameIndex >= frameCount)
                {
                    RemoveAdditionalReplayState(state, "replay_finished");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SwiftlyBhopTimer] Additional replay playback failed for {state.BotName}: {ex.Message}");
            }
        }
    }

    private void RemoveDisabledModeReplayBots()
    {
        if (TimerRunModes.ClassicEnabled)
        {
            return;
        }

        foreach (var bot in GetServerReplayBots(TimerRunMode.Classic))
        {
            ScheduleReplayBotKick("classic_disabled", bot.Name);
        }
    }

    private void ProcessReplayBotConvarRefresh()
    {
        if (_replayBotConvarRefreshTicks > 0)
        {
            _replayBotConvarRefreshTicks--;
            return;
        }

        var shouldRefresh = _activePersonalBestReplayBots.Count > 0 || GetServerReplayBot() is not null;
        if (!shouldRefresh)
        {
            return;
        }

        _replayBotConvarRefreshTicks = ReplayBotConvarRefreshTicks;
        ScheduleReplayBotConvarsApply("maintain");
    }

    private void EnsureReplayBot()
    {
        if (!IsUsableMapName(_currentMapName) || _replayService is null)
        {
            return;
        }

        var missingModes = ServerReplayModes
            .Where(mode => GetServerReplayBot(mode) is null && _replayService.HasReplay(_currentMapName, mode))
            .ToArray();
        if (missingModes.Length == 0)
        {
            return;
        }

        if (_replayBotAddRetryTicks > 0)
        {
            _replayBotAddRetryTicks--;
            return;
        }

        _replayBotAddAttempts++;
        _replayBotAddRetryTicks = ReplayBotAddRetryTicks;

        var delayTicks = 1;
        foreach (var mode in missingModes)
        {
            _replayService.ResetServerReplayPlayback(_currentMapName, mode);
            ScheduleReplayBotAdd($"auto_{TimerRunModes.ToStorageValue(mode)}", GetServerReplayBotDisplayName(mode), delayTicks: delayTicks);
            delayTicks += 6;
        }

        Console.WriteLine($"[SwiftlyBhopTimer] Replay bot add requested. Modes={string.Join(",", missingModes.Select(TimerRunModes.ToDisplayName))}; Attempt={_replayBotAddAttempts}");
    }

    private void ForceReplayBotAdd()
    {
        _replayBotAddAttempts = 0;
        _replayBotAddRetryTicks = 0;

        var modes = ServerReplayModes
            .Where(mode => _replayService?.HasReplay(_currentMapName, mode) == true)
            .ToArray();
        if (modes.Length == 0)
        {
            modes = [TimerRunMode.Standard];
        }

        var delayTicks = 1;
        foreach (var mode in modes)
        {
            _replayService?.ResetServerReplayPlayback(_currentMapName, mode);
            ScheduleReplayBotAdd($"forced_{TimerRunModes.ToStorageValue(mode)}", GetServerReplayBotDisplayName(mode), delayTicks: delayTicks);
            delayTicks += 6;
        }

        Console.WriteLine("[SwiftlyBhopTimer] Replay bot add force-requested.");
    }

    private void RefreshServerReplayBotAfterRecordUpdate(TimerRunMode mode)
    {
        var existingNames = GetServerReplayBots(mode)
            .Select(bot => bot.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var existingName in existingNames)
        {
            ScheduleReplayBotKick("sr_update", existingName);
        }

        _replayBotAddAttempts = 0;
        _replayBotAddRetryTicks = Math.Max(_replayBotAddRetryTicks, 16);
        _lastServerReplayBotSlots.Remove(mode);
        _replayService?.ResetServerReplayPlayback(_currentMapName, mode);
        ScheduleReplayBotAdd($"sr_update_{TimerRunModes.ToStorageValue(mode)}", GetServerReplayBotDisplayName(mode), delayTicks: 4);
    }

    private void ScheduleReplayBotAdd(string reason, string displayName, bool oneShot = false, int delayTicks = 1)
    {
        var commandName = oneShot ? "sbt_replaybot_add_once" : "sbt_replaybot_add";
        var command = string.IsNullOrWhiteSpace(displayName)
            ? commandName
            : $"{commandName} \"{EscapeServerCommandArgument(displayName)}\"";

        ScheduleAfterTicks(delayTicks, () =>
        {
            ExecuteServerCommand("sbt_replaybot_convars_apply");
            Core.Logger.LogInformation("Replay bot command ({Reason}) executing helper server command: {Command}", reason, command);
            ExecuteServerCommand(command);
            ExecuteServerCommand("sbt_replaybot_optimize");
        });
    }

    private void ScheduleReplayBotConvarsApply(string reason)
    {
        Core.Scheduler.NextTick(() =>
        {
            Core.Logger.LogInformation("Replay bot command ({Reason}) executing helper server command: sbt_replaybot_convars_apply", reason);
            ExecuteServerCommand("sbt_replaybot_convars_apply");
            ExecuteServerCommand("sbt_replaybot_optimize");
        });
    }

    private void ScheduleReplayBotComplete(string reason, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        var command = $"sbt_replaybot_complete \"{EscapeServerCommandArgument(displayName)}\"";
        Core.Scheduler.NextTick(() =>
        {
            Core.Logger.LogInformation("Replay bot command ({Reason}) executing helper server command: {Command}", reason, command);
            ExecuteServerCommand(command);
        });
    }

    private void ScheduleAfterTicks(int delayTicks, Action action)
    {
        if (delayTicks <= 0)
        {
            action();
            return;
        }

        Core.Scheduler.NextTick(() => ScheduleAfterTicks(delayTicks - 1, action));
    }

    private void RemoveAdditionalReplayState(PersonalBestReplayBotState state, string reason)
    {
        _replayService?.RemoveReplayPlayback(state.PlaybackKey);
        _activePersonalBestReplayBots.Remove(state.PlaybackKey);
        ScheduleReplayBotComplete(reason, state.BotName);
    }

    private void RemoveAllAdditionalReplayStates(string reason)
    {
        foreach (var state in _activePersonalBestReplayBots.Values.ToArray())
        {
            RemoveAdditionalReplayState(state, reason);
        }
    }

    private void ScheduleReplayBotKick(string reason, string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        var command = $"sbt_replaybot_kick \"{EscapeServerCommandArgument(displayName)}\"";
        Core.Scheduler.NextTick(() =>
        {
            Core.Logger.LogInformation("Replay bot command ({Reason}) executing helper server command: {Command}", reason, command);
            ExecuteServerCommand(command);
        });
    }

    private string GetServerReplayBotDisplayName()
    {
        return GetServerReplayBotDisplayName(TimerRunMode.Standard);
    }

    private string GetServerReplayBotDisplayName(TimerRunMode mode)
    {
        var replay = _replayService?.LoadReplay(_currentMapName, mode);
        return FormatReplayBotDisplayName(GetServerReplayBotTag(mode), replay);
    }

    private static string GetServerReplayBotTag(TimerRunMode mode)
    {
        return mode == TimerRunMode.Classic ? "SR-CL" : "SR-ST";
    }

    private static bool TryGetServerReplayBotMode(string? name, out TimerRunMode mode)
    {
        mode = TimerRunMode.Standard;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.Equals("SBT_SR", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("SwiftlyBhopTimerReplay", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("[SR]", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("[SR Replay]", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("[SR-ST]", StringComparison.OrdinalIgnoreCase))
        {
            mode = TimerRunMode.Standard;
            return true;
        }

        if (name.StartsWith("[SR-CL]", StringComparison.OrdinalIgnoreCase))
        {
            mode = TimerRunMode.Classic;
            return true;
        }

        return false;
    }

    private static string FormatReplayBotDisplayName(string tag, ReplayData? replay)
    {
        if (replay is null)
        {
            return $"[{tag}] Replay";
        }

        var playerName = SanitizeReplayBotNamePart(string.IsNullOrWhiteSpace(replay.PlayerName) ? replay.SteamId : replay.PlayerName);
        var time = TimeFormatter.FormatTicks(replay.TimerTicks);
        return FitReplayBotName($"[{tag}] {playerName} {time}");
    }

    private static string SanitizeReplayBotNamePart(string value)
    {
        var sanitized = Regex.Replace(value, @"[\r\n\t""\\]+", " ").Trim();
        sanitized = Regex.Replace(sanitized, @"\s+", " ");
        return sanitized.Length <= 32 ? sanitized : sanitized[..32].TrimEnd();
    }

    private static string FitReplayBotName(string value)
    {
        const int maxLength = 63;
        return value.Length <= maxLength ? value : value[..maxLength].TrimEnd();
    }

    private static string EscapeServerCommandArgument(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private void ExecuteServerCommand(string command)
    {
        try
        {
            Core.Engine.ExecuteCommand(command);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to execute server command '{command}': {ex.Message}");
        }
    }

    private void ExecuteServerCommandWithBuffer(string command)
    {
        try
        {
            Core.Engine.ExecuteCommandWithBuffer(command, output =>
            {
                if (!string.IsNullOrWhiteSpace(output))
                {
                    Core.Logger.LogInformation("Server command output for '{Command}': {Output}", command, output.Trim());
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to execute buffered server command '{command}': {ex.Message}");
        }
    }

    private IPlayer? GetReplayBot()
    {
        return GetServerReplayBot();
    }

    private IPlayer? GetServerReplayBot()
    {
        return GetServerReplayBots().FirstOrDefault();
    }

    private IPlayer? GetServerReplayBot(TimerRunMode mode)
    {
        return GetServerReplayBots(mode).FirstOrDefault();
    }

    private IReadOnlyList<IPlayer> GetServerReplayBots()
    {
        return Core.PlayerManager.GetBots()
            .Where(player =>
                player is { IsValid: true, IsFakeClient: true } &&
                TryGetServerReplayBotMode(player.Name, out _))
            .ToList();
    }

    private IReadOnlyList<IPlayer> GetServerReplayBots(TimerRunMode mode)
    {
        return Core.PlayerManager.GetBots()
            .Where(player =>
                player is { IsValid: true, IsFakeClient: true } &&
                TryGetServerReplayBotMode(player.Name, out var botMode) &&
                botMode == mode)
            .ToList();
    }

    private IPlayer? GetPersonalBestReplayBot()
    {
        return Core.PlayerManager.GetBots()
            .FirstOrDefault(player =>
                player is { IsValid: true, IsFakeClient: true } &&
                IsAdditionalReplayBotName(player.Name));
    }

    private IPlayer? GetPersonalBestReplayBot(string botName)
    {
        return Core.PlayerManager.GetBots()
            .FirstOrDefault(player =>
                player is { IsValid: true, IsFakeClient: true } &&
                string.Equals(player.Name, botName, StringComparison.OrdinalIgnoreCase));
    }

    private IPlayer? GetPersonalBestReplayBot(PersonalBestReplayBotState state, ISet<int> usedSlots)
    {
        var exact = Core.PlayerManager.GetBots()
            .FirstOrDefault(player =>
                player is { IsValid: true, IsFakeClient: true } &&
                !usedSlots.Contains(player.Slot) &&
                string.Equals(player.Name, state.BotName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var tag = GetReplayBotNameTag(state.BotName);
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        return Core.PlayerManager.GetBots()
            .FirstOrDefault(player =>
                player is { IsValid: true, IsFakeClient: true } &&
                !usedSlots.Contains(player.Slot) &&
                player.Name.StartsWith(tag, StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<IPlayer> GetPersonalBestReplayBots()
    {
        return Core.PlayerManager.GetBots()
            .Where(player =>
                player is { IsValid: true, IsFakeClient: true } &&
                IsAdditionalReplayBotName(player.Name))
            .ToList();
    }

    private static bool IsAdditionalReplayBotName(string name)
    {
        return name.StartsWith("SBT_PB_", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("SBT_TOP_", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("[PB]", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("[B", StringComparison.OrdinalIgnoreCase) ||
               IsRankReplayBotName(name) ||
               name.StartsWith("[PB Replay]", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("[Top Replay]", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRankReplayBotName(string name)
    {
        if (name.Length < 4 || name[0] != '[')
        {
            return false;
        }

        var closingBracketIndex = name.IndexOf(']', StringComparison.Ordinal);
        if (closingBracketIndex <= 1 || closingBracketIndex > 4)
        {
            return false;
        }

        for (var index = 1; index < closingBracketIndex; index++)
        {
            if (!char.IsDigit(name[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetReplayBotNameTag(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name[0] != '[')
        {
            return "";
        }

        var closingBracketIndex = name.IndexOf(']', StringComparison.Ordinal);
        return closingBracketIndex > 0 ? name[..(closingBracketIndex + 1)] : "";
    }
}
