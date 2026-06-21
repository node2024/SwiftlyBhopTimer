using SwiftlyBhopTimer.Services;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private void ShowReplayMenu(ICommandContext context)
    {
        if (_replayService is null || _recordStore is null)
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

        if (!TryResolveReplayTarget(context, out var replayTarget))
        {
            Reply(context, $"{label("Replay")} {label("|")} {red("Usage")} {gray("!replay or !replay b1")}");
            return;
        }

        var mode = GetPlayerTimerMode(player);
        var replayOptions = BuildReplayMenuOptions(player, replayTarget);
        if (replayOptions.Count == 0)
        {
            Reply(context, $"{label("Replay")} {label("|")} {red("no replay data")} {gray($"Top/PB replay data is not available for {replayTarget.Label}.")}");
            return;
        }

        try
        {
            var configuration = new MenuConfiguration
            {
                Title = $"{replayTarget.Title} {TimerRunModes.ToDisplayName(mode)}",
                MaxVisibleItems = 6,
                HideFooter = false,
                HideComment = false,
                FreezePlayer = false,
                AutoCloseAfter = 20
            };
            var menu = Core.MenusAPI.CreateMenu(
                configuration,
                new MenuKeybindOverrides(),
                null!,
                MenuOptionScrollStyle.CenterFixed,
                MenuOptionTextStyle.TruncateEnd);

            foreach (var replayOption in replayOptions)
            {
                AddReplayMenuOption(menu, replayOption);
            }

            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open replay menu: {ex.Message}");
            Reply(context, $"{label("Replay")} {label("|")} {red("menu unavailable")} {gray("Check server console for SwiftlyS2 menu API errors.")}");
        }
    }

    private void ShowSpectateMenu(ICommandContext context)
    {
        var player = context.Sender;
        if (player is null)
        {
            Reply(context, "[SwiftlyBhopTimer] This command can only be used by players.");
            return;
        }

        var options = BuildSpectateMenuOptions(player);
        if (options.Count == 0)
        {
            Reply(context, $"{label("Spec")} {label("|")} {red("no targets")} {gray("No players or replay bots are available.")}");
            return;
        }

        try
        {
            var configuration = new MenuConfiguration
            {
                Title = "Spectate",
                MaxVisibleItems = 8,
                HideFooter = false,
                HideComment = false,
                FreezePlayer = false,
                AutoCloseAfter = 20
            };
            var menu = Core.MenusAPI.CreateMenu(
                configuration,
                new MenuKeybindOverrides(),
                null!,
                MenuOptionScrollStyle.CenterFixed,
                MenuOptionTextStyle.TruncateEnd);

            foreach (var spectateOption in options)
            {
                AddSpectateMenuOption(menu, spectateOption);
            }

            Core.MenusAPI.OpenMenuForPlayer(player, menu);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to open spectate menu: {ex.Message}");
            Reply(context, $"{label("Spec")} {label("|")} {red("menu unavailable")} {gray("Check server console for SwiftlyS2 menu API errors.")}");
        }
    }

    private List<ReplayMenuEntry> BuildReplayMenuOptions(IPlayer player, ReplayTarget replayTarget)
    {
        var options = new List<ReplayMenuEntry>();
        if (_replayService is null || _recordStore is null)
        {
            return options;
        }

        var recordMapName = replayTarget.RecordMapName;
        var mode = GetPlayerTimerMode(player);
        var serverReplay = _replayService.LoadReplay(recordMapName, mode);
        var topRecords = _recordStore.GetTopRecordsAsync(recordMapName, 5, mode).GetAwaiter().GetResult();
        for (var index = 0; index < topRecords.Count; index++)
        {
            var rank = index + 1;
            var record = topRecords[index];
            ReplayData? replay;
            var useServerReplay = rank == 1 && serverReplay is not null && serverReplay.Frames.Count > 0;
            if (useServerReplay)
            {
                replay = serverReplay;
            }
            else
            {
                replay = _replayService.LoadPersonalBestReplay(recordMapName, mode, record.SteamId);
            }

            if (replay is null || replay.Frames.Count == 0)
            {
                continue;
            }

            var playerName = string.IsNullOrWhiteSpace(record.PlayerName) ? replay.PlayerName : record.PlayerName!;
            playerName = string.IsNullOrWhiteSpace(playerName) ? record.SteamId : playerName;
            var isBonus = replayTarget.BonusNumber.HasValue;
            var bonusNumber = replayTarget.BonusNumber.GetValueOrDefault();
            options.Add(new ReplayMenuEntry(
                isBonus
                    ? $"B{bonusNumber} Top #{rank}  {TimeFormatter.FormatTicks(replay.TimerTicks)}"
                    : $"Top #{rank}  {TimeFormatter.FormatTicks(replay.TimerTicks)}",
                $"by {playerName}",
                isBonus ? $"bonus:{bonusNumber}:top:{rank}" : $"top:{rank}",
                isBonus ? $"[ Bonus #{bonusNumber} Top #{rank} Replay ]" : $"[ Top #{rank} Replay ]",
                replay,
                mode,
                useServerReplay));
        }

        var steamId = player.SteamID.ToString();
        var personalBestReplay = _replayService.LoadPersonalBestReplay(recordMapName, mode, steamId);
        var personalBestUsesServerReplay = personalBestReplay is null &&
                                           serverReplay is not null &&
                                           string.Equals(serverReplay.SteamId, steamId, StringComparison.Ordinal);
        if ((personalBestReplay is not null && personalBestReplay.Frames.Count > 0) || personalBestUsesServerReplay)
        {
            var useServerReplay = personalBestUsesServerReplay || IsSameReplay(personalBestReplay!, serverReplay);
            var replay = useServerReplay ? serverReplay! : personalBestReplay!;
            var isBonus = replayTarget.BonusNumber.HasValue;
            var bonusNumber = replayTarget.BonusNumber.GetValueOrDefault();
            options.Add(new ReplayMenuEntry(
                isBonus
                    ? $"B{bonusNumber} PB  {TimeFormatter.FormatTicks(replay.TimerTicks)}"
                    : $"PB  {TimeFormatter.FormatTicks(replay.TimerTicks)}",
                "your personal best",
                isBonus ? $"bonus:{bonusNumber}:pb" : "pb",
                isBonus ? $"[ Bonus #{bonusNumber} PB Replay ]" : "[ PB Replay ]",
                replay,
                mode,
                useServerReplay));
        }

        return options;
    }

    private List<SpectateMenuEntry> BuildSpectateMenuOptions(IPlayer player)
    {
        var options = new List<SpectateMenuEntry>();
        foreach (var target in Core.PlayerManager.GetAllValidPlayers()
                     .Where(target => IsPlayablePlayer(target) && !IsSamePlayer(player, target))
                     .OrderBy(target => target.Name, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(new SpectateMenuEntry(
                $"Watch {FitReplayMenuText(target.Name)}",
                "current player",
                target.Name));
        }

        foreach (var bot in Core.PlayerManager.GetBots()
                     .Where(bot => bot is { IsValid: true, IsFakeClient: true })
                     .OrderBy(bot => bot.Name, StringComparer.OrdinalIgnoreCase))
        {
            options.Add(new SpectateMenuEntry(
                $"Watch {FitReplayMenuText(bot.Name)}",
                "current bot",
                bot.Name));
        }

        return options;
    }

    private void AddReplayMenuOption(IMenuAPI menu, ReplayMenuEntry replayOption)
    {
        var option = new ButtonMenuOption(replayOption.Text, 120, 1000)
        {
            Comment = replayOption.Comment,
            CloseAfterClick = true
        };

        option.Click += (_, args) =>
        {
            ActivateAdditionalReplay(
                args.Player,
                replayOption.Replay,
                replayOption.Kind,
                replayOption.HudLabel,
                replayOption.Text,
                replayOption.Mode,
                replayOption.UseServerReplay,
                kickAfterFirstLoop: true,
                allowParallelCopy: true);
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void AddSpectateMenuOption(IMenuAPI menu, SpectateMenuEntry spectateOption)
    {
        var option = new ButtonMenuOption(spectateOption.Text, 120, 1000)
        {
            Comment = spectateOption.Comment,
            CloseAfterClick = true
        };

        option.Click += (_, args) =>
        {
            var state = _timerStateStore.GetOrCreate(args.Player.Slot, args.Player.SteamID.ToString(), args.Player.Name);
            MovePlayerToSpectator(args.Player, state);
            QueueSpectateTarget(args.Player, state, spectateOption.TargetName);
            SendChat(args.Player, $"{label("Spec")} {label("|")} {green("spectator")} {gray($"target: {spectateOption.TargetName}")}");
            return ValueTask.CompletedTask;
        };

        menu.AddOption(option);
    }

    private void ActivateAdditionalReplay(
        IPlayer player,
        ReplayData replay,
        string kind,
        string hudLabel,
        string chatLabel,
        TimerRunMode mode,
        bool useServerReplay = false,
        bool kickAfterFirstLoop = false,
        bool allowParallelCopy = false)
    {
        if (_replayService is null)
        {
            return;
        }

        var serial = ++_additionalReplayBotSerial;
        var playbackKey = allowParallelCopy
            ? $"{kind}:menu:{serial}:{replay.MapName}:{replay.SteamId}:{replay.TimerTicks}"
            : $"{kind}:{replay.MapName}:{replay.SteamId}:{replay.TimerTicks}";
        if (!allowParallelCopy && _activePersonalBestReplayBots.TryGetValue(playbackKey, out var existingState))
        {
            RemoveAdditionalReplayState(existingState, "replay_replaced");
        }
        else if (_activePersonalBestReplayBots.Count >= MaxAdditionalReplayBots)
        {
            var oldestState = _activePersonalBestReplayBots.First();
            RemoveAdditionalReplayState(oldestState.Value, "replay_quota_prune");
        }

        var botName = GetAdditionalReplayBotDisplayName(kind, replay, serial);
        foreach (var duplicateState in _activePersonalBestReplayBots.Values
            .Where(state => string.Equals(state.BotName, botName, StringComparison.OrdinalIgnoreCase))
            .ToArray())
        {
            RemoveAdditionalReplayState(duplicateState, "replay_duplicate_prune");
        }

        _activePersonalBestReplayBots[playbackKey] = new PersonalBestReplayBotState(
            replay.SteamId,
            replay.MapName,
            botName,
            playbackKey,
            hudLabel,
            replay,
            mode,
            kickAfterFirstLoop,
            useServerReplay);
        _replayService.ResetReplayPlayback(playbackKey);

        ScheduleReplayBotAdd(kind, botName, kickAfterFirstLoop);
        var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
        QueueSpectateTarget(player, state, botName);
        SendChat(player, $"{label("Replay")} {label("|")} {green("bot add requested")} {gray(chatLabel)}");
    }

    private static string GetAdditionalReplayBotDisplayName(string kind, ReplayData replay, int serial)
    {
        if (TryParseBonusReplayKind(kind, out var bonusNumber, out var bonusKind, out var bonusRank))
        {
            var tag = bonusKind.Equals("top", StringComparison.OrdinalIgnoreCase)
                ? $"B{bonusNumber}#{bonusRank}"
                : $"B{bonusNumber}PB";
            return FormatReplayBotDisplayName(tag, replay);
        }

        if (kind.StartsWith("top:", StringComparison.OrdinalIgnoreCase))
        {
            var rank = kind["top:".Length..];
            return FormatReplayBotDisplayName(rank, replay);
        }

        return FormatReplayBotDisplayName("PB", replay);
    }

    private bool TryResolveReplayTarget(ICommandContext context, out ReplayTarget target)
    {
        target = new ReplayTarget(_currentMapName, "Replay", _currentMapName, null);
        if (context.Args.Length == 0)
        {
            return true;
        }

        var token = context.Args[0].Trim();
        if (token.Equals("main", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("map", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryParseBonusReplayArgument(context.Args, out var bonusNumber))
        {
            target = new ReplayTarget(
                GetBonusRecordMapName(bonusNumber),
                $"Replay Bonus #{bonusNumber}",
                $"Bonus #{bonusNumber}",
                bonusNumber);
            return true;
        }

        return false;
    }

    private static bool TryParseBonusReplayArgument(IReadOnlyList<string> args, out int bonusNumber)
    {
        bonusNumber = 0;
        if (args.Count == 0)
        {
            return false;
        }

        var token = args[0].Trim();
        if (token.Equals("b", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("bn", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("bonus", StringComparison.OrdinalIgnoreCase))
        {
            return args.Count > 1 && TryParseBonusNumber(args[1], out bonusNumber);
        }

        if (token.StartsWith("bonus", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseBonusNumber(token["bonus".Length..], out bonusNumber);
        }

        if (token.StartsWith("bn", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseBonusNumber(token["bn".Length..], out bonusNumber);
        }

        if (token.StartsWith("b", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseBonusNumber(token[1..], out bonusNumber);
        }

        return TryParseBonusNumber(token, out bonusNumber);
    }

    private static bool TryParseBonusNumber(string value, out int bonusNumber)
    {
        value = value.Trim().TrimStart('#', '_', '-');
        return int.TryParse(value, out bonusNumber) && bonusNumber is >= 1 and <= 99;
    }

    private static bool TryParseBonusReplayKind(string kind, out int bonusNumber, out string bonusKind, out int rank)
    {
        bonusNumber = 0;
        bonusKind = "";
        rank = 0;
        var parts = kind.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 ||
            !parts[0].Equals("bonus", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[1], out bonusNumber) ||
            bonusNumber is < 1 or > 99)
        {
            return false;
        }

        bonusKind = parts[2];
        if (bonusKind.Equals("pb", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return bonusKind.Equals("top", StringComparison.OrdinalIgnoreCase) &&
               parts.Length > 3 &&
               int.TryParse(parts[3], out rank) &&
               rank > 0;
    }

    private static string FitReplayMenuText(string value)
    {
        const int maxLength = 48;
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        return value.Length <= maxLength ? value : value[..maxLength].TrimEnd();
    }

    private static bool IsSameReplay(ReplayData replay, ReplayData? other)
    {
        return other is not null &&
               string.Equals(replay.MapName, other.MapName, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(replay.SteamId, other.SteamId, StringComparison.Ordinal) &&
               replay.TimerTicks == other.TimerTicks;
    }
}

public sealed record ReplayMenuEntry(string Text, string Comment, string Kind, string HudLabel, ReplayData Replay, TimerRunMode Mode, bool UseServerReplay);

public sealed record SpectateMenuEntry(string Text, string Comment, string TargetName);

public sealed record ReplayTarget(string RecordMapName, string Title, string Label, int? BonusNumber);
