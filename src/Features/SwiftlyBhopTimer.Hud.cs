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
    private void RenderHud(IPlayer player, PlayerTimerState state)
    {
        if (IsPlayerSpectator(player))
        {
            state.ActiveReplayTargetName = null;
            state.ActiveSpectateTargetSlot = null;
            state.PendingSpectateTargetName = null;
            state.PendingSpectateTargetSlot = null;
            state.PendingSpectateTargetTicks = 0;
            ClearHud(player);
            return;
        }

        if (state.HideTimerHud)
        {
            return;
        }

        state.HudTicks++;
        if (state.HudTicks < HudUpdateIntervalTicks)
        {
            return;
        }

        state.HudTicks = 0;
        RefreshHudStatsIfNeeded(player, state);
        var playerTeam = TryGetTeamNumber(player);
        if (playerTeam is not null &&
            playerTeam is not 1 &&
            string.IsNullOrWhiteSpace(state.ActiveReplayTargetName) &&
            state.ActiveSpectateTargetSlot is null &&
            state.PendingSpectateTargetTicks <= 0 &&
            TryGetObservedPlayer(player) is null)
        {
            state.ActiveReplayTargetName = null;
            state.ActiveSpectateTargetSlot = null;
        }

        var hudOverride = TryGetReplayHudOverride(player, state) ?? TryGetObservedPlayerHudOverride(player, state);
        var pawn = player.PlayerPawn ?? player.Pawn;
        var speed = hudOverride?.Speed ?? (EntityReflection.TryGetVelocity(pawn, out var velocity)
            ? MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y)
            : 0.0f);
        var showBonusHud = state.IsBonusTimerRunning || state.LastFinishedBonusTicks.HasValue;
        var displayTicks = hudOverride?.CurrentTicks ?? (state.IsBonusTimerRunning
            ? state.BonusTimerTicks
            : state.LastFinishedBonusTicks ?? (state.IsTimerRunning
                ? state.TimerTicks
                : state.LastFinishedTicks ?? 0));
        var time = TimeFormatter.FormatTicks(displayTicks);
        var timeColor = hudOverride is not null
            ? "#ffffff"
            : (state.LastFinishedTicks.HasValue || state.LastFinishedBonusTicks.HasValue) && !state.IsTimerRunning && !state.IsBonusTimerRunning
                ? "#66bb6a"
                : state.IsTimerPaused
                    ? "#ffd54f"
                : state.IsTimerRunning || state.IsBonusTimerRunning
                    ? "#ffffff"
                    : "#7f1d1d";
        var modeName = TimerRunModes.ToDisplayName(state.TimerMode);
        var headerLine = hudOverride?.HeaderLine ?? (showBonusHud && state.CurrentBonusNumber > 0
            ? $"[ {modeName} Bonus #{state.CurrentBonusNumber} ]"
            : $"{state.CachedHudRank} {modeName}");
        var detailLine = hudOverride?.DetailLine
            ?? $"<font class='fontSize-s stratum-bold-italic' color='#90a4ae'>PB</font> <font class='fontSize-s stratum-bold-italic' color='#f5f5f5'>{state.CachedHudPb}</font>"
            + $"  <font class='fontSize-s stratum-bold-italic' color='#546e7a'>/</font>"
            + $"  <font class='fontSize-s stratum-bold-italic' color='#90a4ae'>SR</font> <font class='fontSize-s stratum-bold-italic' color='#f5f5f5'>{state.CachedHudSr}</font>";
        const string hudSeparator = "<font class='fontSize-s' color='#37474f'>------------</font>";
        var hud = $"<font class='fontSize-s stratum-bold-italic' color='#78909c'>{headerLine}</font>"
            + $"<br>{detailLine}"
            + $"<br>{hudSeparator}"
            + $"<br><font class='fontSize-xxl stratum-bold-italic' style='font-size:42px;' color='#f5f5f5'>{MathF.Round(speed):0000}</font> <font class='fontSize-m stratum-bold-italic' color='#90a4ae'>u/s</font>"
            + $"<br>{hudSeparator}"
            + $"<br><font class='fontSize-l stratum-bold-italic' style='font-size:23px;' color='{timeColor}'>{time}</font>";

        player.SendCenterHTML(hud, HudHtmlDurationMs);
    }

    private ReplayHudOverride? TryGetReplayHudOverride(IPlayer viewer, PlayerTimerState state)
    {
        if (_replayService is null || !TryGetObservedReplayBot(viewer, state, out var bot, out var additionalReplayState, out var isServerRecord, out var serverRecordMode))
        {
            return null;
        }

        ReplayPlaybackStatus? status;
        string header;
        if (isServerRecord)
        {
            status = _replayService.GetServerReplayStatus(_currentMapName, serverRecordMode);
            header = $"[ SR {TimerRunModes.ToDisplayName(serverRecordMode)} ]";
        }
        else if (additionalReplayState is not null)
        {
            status = _replayService.GetReplayStatus(additionalReplayState.PlaybackKey);
            header = additionalReplayState.HudLabel;
        }
        else
        {
            return null;
        }

        if (status is null)
        {
            var fallbackReplay = isServerRecord ? _replayService.LoadReplay(_currentMapName, serverRecordMode) : additionalReplayState?.Replay;
            if (fallbackReplay is null)
            {
                return null;
            }

            var fallbackDetail = $"<font class='fontSize-s stratum-bold-italic' color='#90a4ae'>{bot.Name}</font>"
                + $" <font class='fontSize-s stratum-bold-italic' color='#f5f5f5'>{TimeFormatter.FormatTicks(fallbackReplay.TimerTicks)}</font>";
            return new ReplayHudOverride(header, fallbackDetail, GetPlayerHorizontalSpeed(bot), 0);
        }

        var playerName = string.IsNullOrWhiteSpace(status.PlayerName) ? bot.Name : status.PlayerName;
        var detail = $"<font class='fontSize-s stratum-bold-italic' color='#90a4ae'>{playerName}</font>"
            + $" <font class='fontSize-s stratum-bold-italic' color='#f5f5f5'>{TimeFormatter.FormatTicks(status.TotalTicks)}</font>";
        return new ReplayHudOverride(header, detail, status.Speed, status.CurrentTicks);
    }

    private ReplayHudOverride? TryGetObservedPlayerHudOverride(IPlayer viewer, PlayerTimerState viewerState)
    {
        var target = TryGetObservedPlayer(viewer);
        if (target is null && viewerState.ActiveSpectateTargetSlot is { } targetSlot)
        {
            target = FindHumanPlayerBySlot(targetSlot);
        }

        if (target is null && !string.IsNullOrWhiteSpace(viewerState.ActiveReplayTargetName))
        {
            target = FindHumanPlayerByName(viewerState.ActiveReplayTargetName);
        }

        if (!IsPlayablePlayer(target) || IsSamePlayer(viewer, target))
        {
            return null;
        }

        var targetState = _timerStateStore.GetOrCreate(target!.Slot, target.SteamID.ToString(), target.Name);
        RefreshHudStatsIfNeeded(target, targetState);
        viewerState.ActiveReplayTargetName = target.Name;
        viewerState.ActiveSpectateTargetSlot = target.Slot;

        var currentTicks = targetState.IsTimerRunning
            ? targetState.TimerTicks
            : targetState.LastFinishedTicks ?? 0;
        var detail = $"<font class='fontSize-s stratum-bold-italic' color='#90a4ae'>{target.Name}</font>"
            + $" <font class='fontSize-s stratum-bold-italic' color='#f5f5f5'>{targetState.CachedHudPb}</font>"
            + $"  <font class='fontSize-s stratum-bold-italic' color='#546e7a'>/</font>"
            + $"  <font class='fontSize-s stratum-bold-italic' color='#90a4ae'>SR</font> <font class='fontSize-s stratum-bold-italic' color='#f5f5f5'>{targetState.CachedHudSr}</font>";

        return new ReplayHudOverride("[ Spectating ]", detail, GetPlayerHorizontalSpeed(target), currentTicks);
    }

    private IPlayer? FindHumanPlayerByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        var exact = Core.PlayerManager.GetAllValidPlayers()
            .FirstOrDefault(player => IsPlayablePlayer(player) &&
                                      string.Equals(player.Name, targetName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        return Core.PlayerManager.GetAllValidPlayers()
            .FirstOrDefault(player => IsPlayablePlayer(player) &&
                                      player.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase));
    }

    private IPlayer? FindHumanPlayerBySlot(int slot)
    {
        return Core.PlayerManager.GetAllValidPlayers()
            .FirstOrDefault(player => IsPlayablePlayer(player) && player.Slot == slot);
    }

    private IPlayer? FindPlayerBySlot(int slot)
    {
        var human = Core.PlayerManager.GetAllValidPlayers()
            .FirstOrDefault(player => player is { IsValid: true } && player.Slot == slot);
        if (human is not null)
        {
            return human;
        }

        return Core.PlayerManager.GetBots()
            .FirstOrDefault(player => player is { IsValid: true } && player.Slot == slot);
    }

    private bool TryGetObservedReplayBot(
        IPlayer viewer,
        PlayerTimerState viewerState,
        out IPlayer bot,
        out PersonalBestReplayBotState? additionalReplayState,
        out bool isServerRecord,
        out TimerRunMode serverRecordMode)
    {
        bot = null!;
        additionalReplayState = null;
        isServerRecord = false;
        serverRecordMode = TimerRunMode.Standard;

        var targetPlayer = TryGetObservedPlayer(viewer);
        if (targetPlayer is not null && targetPlayer.IsFakeClient)
        {
            if (TryGetServerReplayBotMode(targetPlayer.Name, out var observedMode) &&
                IsSamePlayer(targetPlayer, GetServerReplayBot(observedMode)))
            {
                bot = targetPlayer;
                isServerRecord = true;
                serverRecordMode = observedMode;
                return true;
            }

            foreach (var state in _activePersonalBestReplayBots.Values)
            {
                var sameName = string.Equals(state.BotName, targetPlayer.Name, StringComparison.OrdinalIgnoreCase);
                if (!sameName)
                {
                    var stateTag = GetReplayBotNameTag(state.BotName);
                    var targetTag = GetReplayBotNameTag(targetPlayer.Name);
                    if (string.IsNullOrWhiteSpace(stateTag) || string.IsNullOrWhiteSpace(targetTag))
                    {
                        continue;
                    }

                    if (!string.Equals(stateTag, targetTag, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                bot = targetPlayer;
                additionalReplayState = state;
                return true;
            }

            if (string.IsNullOrWhiteSpace(viewerState.ActiveReplayTargetName))
            {
                viewerState.ActiveReplayTargetName = targetPlayer.Name;
            }

            return true;
        }

        if (viewerState.ActiveSpectateTargetSlot is { } targetSlot &&
            FindPlayerBySlot(targetSlot) is { IsFakeClient: true } botFromSlot)
        {
            if (TryGetServerReplayBotMode(botFromSlot.Name, out var observedMode) &&
                IsSamePlayer(botFromSlot, GetServerReplayBot(observedMode)))
            {
                bot = botFromSlot;
                isServerRecord = true;
                serverRecordMode = observedMode;
                return true;
            }

            foreach (var state in _activePersonalBestReplayBots.Values)
            {
                if (string.Equals(botFromSlot.Name, state.BotName, StringComparison.OrdinalIgnoreCase) ||
                    (GetReplayBotNameTag(botFromSlot.Name) is { Length: > 0 } targetTag &&
                     string.Equals(GetReplayBotNameTag(state.BotName), targetTag, StringComparison.OrdinalIgnoreCase)))
                {
                    bot = botFromSlot;
                    additionalReplayState = state;
                    return true;
                }
            }

            bot = botFromSlot;
            additionalReplayState = null;
            return true;
        }

        // If spectator metadata reflection is incomplete (common in some builds), keep a best-effort HUD
        // using the latest explicit spectate target name.
        if (string.IsNullOrWhiteSpace(viewerState.ActiveReplayTargetName))
        {
            return false;
        }

        bot = null!;
        if (TryFindReplayBotByTargetName(viewerState.ActiveReplayTargetName, out var replayBotFromName))
        {
            if (TryGetServerReplayBotMode(replayBotFromName.Name, out var observedMode) &&
                IsSamePlayer(replayBotFromName, GetServerReplayBot(observedMode)))
            {
                bot = replayBotFromName;
                isServerRecord = true;
                serverRecordMode = observedMode;
                return true;
            }

            foreach (var state in _activePersonalBestReplayBots.Values)
            {
                if (string.Equals(replayBotFromName.Name, state.BotName, StringComparison.OrdinalIgnoreCase) ||
                    (GetReplayBotNameTag(replayBotFromName.Name) is { Length: > 0 } targetTag &&
                     string.Equals(GetReplayBotNameTag(state.BotName), targetTag, StringComparison.OrdinalIgnoreCase)))
                {
                    bot = replayBotFromName;
                    additionalReplayState = state;
                    return true;
                }
            }

            bot = replayBotFromName;
            additionalReplayState = null;
            return true;
        }

        var activeTag = GetReplayBotNameTag(viewerState.ActiveReplayTargetName);
        if (TryGetServerReplayBotMode(viewerState.ActiveReplayTargetName, out var activeMode) &&
            GetServerReplayBot(activeMode) is { } explicitServerBot)
        {
            bot = explicitServerBot;
            isServerRecord = true;
            serverRecordMode = activeMode;
            return true;
        }

        foreach (var candidate in _activePersonalBestReplayBots.Values)
        {
            var candidateTag = GetReplayBotNameTag(candidate.BotName);
            if (string.Equals(activeTag, candidateTag, StringComparison.OrdinalIgnoreCase))
            {
                var matchedBot = GetPersonalBestReplayBot(candidate.BotName);
                if (matchedBot is not null)
                {
                    bot = matchedBot;
                    additionalReplayState = candidate;
                    return true;
                }
            }
        }

        if (_activePersonalBestReplayBots.Count == 1)
        {
            var onlyState = _activePersonalBestReplayBots.Values.First();
            var onlyBot = GetPersonalBestReplayBot(onlyState.BotName);
            if (onlyBot is not null)
            {
                bot = onlyBot;
                additionalReplayState = onlyState;
                return true;
            }
        }

        return false;
    }

    private bool TryFindReplayBotByTargetName(string? targetName, out IPlayer bot)
    {
        bot = null!;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return false;
        }

        var foundBot = FindReplayBotByName(targetName, allowPrefixFallback: true);
        if (foundBot is null)
        {
            return false;
        }

        bot = foundBot;
        return true;
    }

    private IPlayer? TryGetObservedPlayer(IPlayer viewer)
    {
        var candidateTargets = new List<object?>();
        var seenTargets = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var pawn in new object?[] { viewer.PlayerPawn, viewer.Pawn })
        {
            if (pawn is null || !seenTargets.Add(pawn))
            {
                continue;
            }

            candidateTargets.Add(pawn);
            candidateTargets.Add(TryGetMemberValue(pawn, "Controller", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        }

        if (viewer.Controller is not null)
        {
            candidateTargets.Add(viewer.Controller);
        }

        candidateTargets.Add(TryGetMemberValue(viewer, "Controller", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        candidateTargets.Add(TryGetMemberValue(viewer, "ServerSideClient", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

        foreach (var candidate in candidateTargets)
        {
            var targetPawn = ResolveObserverTarget(candidate);
            if (targetPawn is not null)
            {
                var player = ResolvePlayerFromObserverTarget(targetPawn);
                if (player is not null)
                {
                    return player;
                }
            }
        }

        return null;
    }

    private IPlayer? ResolvePlayerFromObserverTarget(object target)
    {
        if (target is IPlayer player)
        {
            return player;
        }

        if (EntityReflection.TryGetEntityIndex(target, out var targetEntityIndex))
        {
            foreach (var candidate in Core.PlayerManager.GetAllValidPlayers())
            {
                if (PlayerHasEntityIndex(candidate, targetEntityIndex))
                {
                    return candidate;
                }
            }
        }

        if (TryConvertObserverTargetIndex(target, out var observerIndex) &&
            ResolvePlayerFromObserverIndex(observerIndex) is { } playerFromIndex)
        {
            return playerFromIndex;
        }

        var playerFromPawn = EntityReflection.GetPlayerFromPawn(Core.PlayerManager, target);
        if (playerFromPawn is not null)
        {
            return playerFromPawn;
        }

        foreach (var candidate in Core.PlayerManager.GetAllValidPlayers())
        {
            if (ObserverTargetMatches(target, candidate.PlayerPawn) ||
                ObserverTargetMatches(target, candidate.Pawn) ||
                ObserverTargetMatches(target, candidate.Controller) ||
                ObserverTargetMatches(target, candidate.ServerSideClient))
            {
                return candidate;
            }
        }

        return null;
    }

    private IPlayer? ResolvePlayerFromObserverIndex(int observerIndex)
    {
        if (observerIndex <= 0)
        {
            return null;
        }

        foreach (var entityIndex in GetObserverEntityIndexCandidates(observerIndex))
        {
            foreach (var candidate in GetAllObservedPlayerCandidates())
            {
                if (PlayerHasEntityIndex(candidate, entityIndex))
                {
                    return candidate;
                }
            }
        }

        if (observerIndex < 65)
        {
            return FindPlayerBySlot(observerIndex);
        }

        return null;
    }

    private IEnumerable<IPlayer> GetAllObservedPlayerCandidates()
    {
        var seenSlots = new HashSet<int>();
        foreach (var player in Core.PlayerManager.GetAllValidPlayers())
        {
            if (player is { IsValid: true } && seenSlots.Add(player.Slot))
            {
                yield return player;
            }
        }

        foreach (var bot in Core.PlayerManager.GetBots())
        {
            if (bot is { IsValid: true } && seenSlots.Add(bot.Slot))
            {
                yield return bot;
            }
        }
    }

    private static IEnumerable<int> GetObserverEntityIndexCandidates(int observerIndex)
    {
        yield return observerIndex;

        var entry15 = observerIndex & 0x7fff;
        if (entry15 > 0 && entry15 != observerIndex)
        {
            yield return entry15;
        }

        var entry14 = observerIndex & 0x3fff;
        if (entry14 > 0 && entry14 != observerIndex && entry14 != entry15)
        {
            yield return entry14;
        }
    }

    private static bool PlayerHasEntityIndex(IPlayer player, int entityIndex)
    {
        if (entityIndex <= 0)
        {
            return false;
        }

        foreach (var candidate in new object?[] { player.PlayerPawn, player.Pawn, player.Controller, player.ServerSideClient, player })
        {
            if (candidate is not null &&
                EntityReflection.TryGetEntityIndex(candidate, out var candidateIndex) &&
                candidateIndex == entityIndex)
            {
                return true;
            }
        }

        return player.Slot + 1 == entityIndex;
    }

    private static bool ObserverTargetMatches(object target, object? candidate)
    {
        if (candidate is null)
        {
            return false;
        }

        if (ReferenceEquals(target, candidate))
        {
            return true;
        }

        try
        {
            return target.Equals(candidate) || candidate.Equals(target);
        }
        catch
        {
            return false;
        }
    }

    private static object? ResolveObserverTarget(object? source)
    {
        if (source is null)
        {
            return null;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var directName in new[]
                 {
                     "ObserverTarget",
                     "SpectatorTarget",
                     "SpecTarget",
                     "m_hObserverTarget",
                     "ObserverTargetHandle"
                 })
        {
            var direct = TryGetMemberValue(source, directName, flags);
            var directPawn = UnwrapEntityReference(direct, 0);
            if (directPawn is not null)
            {
                return directPawn;
            }
        }

        foreach (var serviceName in new[] { "ObserverServices", "CObserverServices", "Observer", "SpectatorServices" })
        {
            var observerServices = TryGetMemberValue(source, serviceName, flags);
            if (observerServices is null)
            {
                continue;
            }

            var directTarget = ResolveObserverTargetFromServices(observerServices);
            if (directTarget is not null)
            {
                return directTarget;
            }
        }

        return null;
    }

    private static object? ResolveObserverTargetFromServices(object observerServices)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var memberName in new[]
                 {
                     "ObserverTarget",
                     "ObserverTargetEntity",
                     "ObserverTargetPawn",
                     "m_hObserverTarget",
                     "m_ObserverTarget",
                     "Target",
                     "ObservedEntity",
                     "ObservedPawn"
                 })
        {
            var observerTarget = TryGetMemberValue(observerServices, memberName, flags);
            var targetPawn = UnwrapEntityReference(observerTarget, 0);
            if (targetPawn is not null)
            {
                return targetPawn;
            }
        }

        return null;
    }

    private static object? UnwrapEntityReference(object? value, int depth)
    {
        if (value is null || depth > 4)
        {
            return value;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var memberName in new[]
                 { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn", "EntityHandle", "EntityIndex", "Index", "EntryIndex" })
        {
            var nested = TryGetMemberValue(value, memberName, flags);
            if (nested is null || ReferenceEquals(nested, value))
            {
                continue;
            }

            return UnwrapEntityReference(nested, depth + 1);
        }

        return value;
    }

    private static bool TryConvertObserverTargetIndex(object? value, out int index)
    {
        return TryConvertObserverTargetIndex(value, out index, 0);
    }

    private static bool TryConvertObserverTargetIndex(object? value, out int index, int depth)
    {
        index = 0;
        if (value is null || depth > 5 || value is string)
        {
            return false;
        }

        try
        {
            switch (value)
            {
                case int intValue:
                    index = intValue;
                    return index > 0;
                case uint uintValue:
                    index = uintValue > int.MaxValue ? 0 : (int)uintValue;
                    return index > 0;
                case short shortValue:
                    index = shortValue;
                    return index > 0;
                case ushort ushortValue:
                    index = ushortValue;
                    return index > 0;
                case byte byteValue:
                    index = byteValue;
                    return index > 0;
                case long longValue:
                    index = longValue > int.MaxValue ? 0 : (int)longValue;
                    return index > 0;
                case ulong ulongValue:
                    index = ulongValue > int.MaxValue ? 0 : (int)ulongValue;
                    return index > 0;
            }

            if (value.GetType().IsEnum)
            {
                index = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return index > 0;
            }
        }
        catch
        {
            return false;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var memberName in new[] { "Index", "EntityIndex", "EntryIndex", "Value", "RawValue", "Raw", "Handle", "EntityHandle" })
        {
            var nested = TryGetMemberValue(value, memberName, flags);
            if (nested is null || ReferenceEquals(nested, value))
            {
                continue;
            }

            if (TryConvertObserverTargetIndex(nested, out index, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static int? TryGetTeamNumber(IPlayer player)
    {
        var pawn = player.PlayerPawn ?? player.Pawn;
        if (pawn is null)
        {
            return null;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var memberName in new[] { "TeamNum", "TeamNumRaw", "TeamNumCt", "Team", "team", "TeamNum_t" })
        {
            var rawValue = TryGetMemberValue(pawn, memberName, flags);
            if (rawValue is null)
            {
                continue;
            }

            if (rawValue is byte teamByte)
            {
                return teamByte;
            }

            try
            {
                return Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
            }
            catch
            {
            }
        }

        return null;
    }

    private static float GetPlayerHorizontalSpeed(IPlayer player)
    {
        var pawn = player.PlayerPawn ?? player.Pawn;
        return EntityReflection.TryGetVelocity(pawn, out var velocity)
            ? MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y)
            : 0.0f;
    }

    private static bool IsSamePlayer(IPlayer? left, IPlayer? right)
    {
        return left is not null &&
               right is not null &&
               left.IsValid &&
               right.IsValid &&
               (ReferenceEquals(left, right) || left.Slot == right.Slot || string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static void ClearHud(IPlayer player)
    {
        player.SendCenterHTML("<font style='font-size:1px;' color='#000000'>.</font>", HudClearDurationMs);
    }

    private void RefreshHudStatsIfNeeded(IPlayer player, PlayerTimerState state)
    {
        if (_recordStore is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var recordMapName = GetHudRecordMapName(state);
        var recordCacheKey = $"{recordMapName}|{TimerRunModes.ToStorageValue(state.TimerMode)}";
        if ((now - state.LastHudStatsRefreshUtc).TotalSeconds < 5 &&
            string.Equals(state.CachedHudRecordMapName, recordCacheKey, StringComparison.Ordinal))
        {
            return;
        }

        state.LastHudStatsRefreshUtc = now;
        try
        {
            var steamId = player.SteamID.ToString();
            if (!string.Equals(state.CachedHudRecordMapName, recordCacheKey, StringComparison.Ordinal))
            {
                state.CachedHudRecordMapName = recordCacheKey;
                state.CachedHudPb = "--";
                state.CachedHudRank = "[ --- / --- ]";
                state.CachedHudSr = "--";
            }

            var rank = _recordStore.GetRankAsync(recordMapName, steamId, state.TimerMode).GetAwaiter().GetResult();
            var sr = _recordStore.GetTopRecordsAsync(recordMapName, 1, state.TimerMode).GetAwaiter().GetResult().FirstOrDefault();

            state.CachedHudPb = rank is null ? "--" : TimeFormatter.FormatTicks(rank.TimerTicks);
            state.CachedHudRank = rank is null
                ? "[ --- / --- ]"
                : $"[ <font style='font-size:16px;' color='#f5f5f5'>{rank.Placement}</font> / <font style='font-size:16px;' color='#f5f5f5'>{rank.Total}</font> ]";
            state.CachedHudSr = sr is null ? "--" : TimeFormatter.FormatTicks(sr.TimerTicks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to refresh HUD stats: {ex.Message}");
        }
    }

    private string GetHudRecordMapName(PlayerTimerState state)
    {
        return (state.IsBonusTimerRunning || state.LastFinishedBonusTicks.HasValue) && state.CurrentBonusNumber > 0
            ? GetBonusRecordMapName(state.CurrentBonusNumber)
            : _currentMapName;
    }

    private void ApplyHtmlHudFlashFix()
    {
        try
        {
            var currentTime = TryReadNumber(Core.Engine.GlobalVars, "CurrentTime", "CurTime", "Curtime", "Currenttime");
            if (!currentTime.HasValue)
            {
                LogHtmlHudFlashFixUnavailable("current server time was not found.");
                return;
            }

            ExecuteServerCommand($"sbt_htmlhud_fix {currentTime.Value.ToString("0.###", CultureInfo.InvariantCulture)}");
        }
        catch (Exception ex)
        {
            LogHtmlHudFlashFixUnavailable(ex.Message);
        }
    }

    private void LogHtmlHudFlashFixUnavailable(string reason)
    {
        if (_htmlHudFlashFixUnavailableLogged)
        {
            return;
        }

        _htmlHudFlashFixUnavailableLogged = true;
        Core.Logger.LogWarning("HTML HUD flash fix is unavailable: {Reason}", reason);
    }

    private static double? TryReadNumber(object instance, params string[] propertyNames)
    {
        var type = instance.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var propertyName in propertyNames)
        {
            var value = TryGetMemberValue(instance, propertyName, flags);
            if (value is null)
            {
                continue;
            }

            var number = TryConvertToDouble(value);
            if (number.HasValue)
            {
                return number.Value;
            }
        }

        return null;
    }

    private static double? TryConvertToDouble(object value)
    {
        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            // Some Swiftly schema values are wrapper structs. Read their numeric payload if present.
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var memberName in new[] { "Value", "RawValue", "Raw", "FloatValue", "DoubleValue", "IntValue" })
        {
            var nested = TryGetMemberValue(value, memberName, flags);
            if (nested is null || ReferenceEquals(nested, value))
            {
                continue;
            }

            try
            {
                return Convert.ToDouble(nested, CultureInfo.InvariantCulture);
            }
            catch
            {
            }
        }

        return null;
    }

    private static object? TryGetMemberValue(object instance, string memberName, BindingFlags flags)
    {
        var type = instance.GetType();
        try
        {
            var property = type.GetProperty(memberName, flags);
            if (property is not null && property.CanRead && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(instance);
            }

            return type.GetField(memberName, flags)?.GetValue(instance);
        }
        catch
        {
            return null;
        }
    }
}

public sealed record ReplayHudOverride(string HeaderLine, string DetailLine, float Speed, int CurrentTicks);
