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
    private void LoadMap(string mapName)
    {
        var normalizedMapName = NormalizeMapName(mapName);
        if (!IsUsableMapName(normalizedMapName))
        {
            Core.Logger.LogWarning(
                "Ignoring invalid map load name '{MapName}'. Keeping current map '{CurrentMap}'.",
                mapName,
                _currentMapName);
            return;
        }

        var sameMapReload = string.Equals(_currentMapName, normalizedMapName, StringComparison.OrdinalIgnoreCase);
        if (sameMapReload)
        {
            _roundFlowConfigService?.ApplyFull(includeTimeLimit: false);
            ExecuteServerCommand("sbt_movement_compat_burst");
            ScheduleDelayedMapExec("map_reload_same", includeTimeLimit: false);
            if (!HasUsableActiveMapData())
            {
                ReloadActiveMapData(preserveExisting: true);
            }

            ScheduleZoneRender("map_reload_same");
            Core.Logger.LogInformation(
                "Ignored same-map load notification: {Map}; keeping active map data: start={Start}; end={End}; respawn={Respawn}",
                _currentMapName,
                _activeMap.StartTriggerName,
                _activeMap.EndTriggerName,
                _activeMap.RespawnPosition?.ToString() ?? "none");
            return;
        }

        _currentMapName = normalizedMapName;
        ResetAllPlayerModesToStandard("map_load");
        _mapChooserService?.BeginMap(_currentMapName, DateTime.UtcNow);
        ApplyMapChooserTimeLimitToHelper();
        ExecuteServerCommand("sbt_movement_compat_burst");
        _replayBotAddRetryTicks = 0;
        _replayBotAddAttempts = 0;
        _lastServerReplayBotSlots.Clear();
        RemoveAllAdditionalReplayStates("map_change");
        var loadedMapData = ReloadActiveMapData(preserveExisting: false);
        if (!loadedMapData && !HasUsableActiveMapData())
        {
            TryRestoreKnownGoodActiveMap(normalizedMapName);
        }

        if (loadedMapData || string.Equals(_activeMap.MapName, normalizedMapName, StringComparison.OrdinalIgnoreCase))
        {
            _timerStateStore.Clear();
            _zoneSetupPreviews.Clear();
            _replayService?.ClearRuntime();
            _zoneRenderService?.Clear();
            _zoneRenderService?.ClearPreviews();
        }
        else
        {
            Core.Logger.LogWarning(
                "Skipped state clear for map notification '{Map}' because no usable MapData was loaded and active map is still '{ActiveMap}'.",
                normalizedMapName,
                _activeMap.MapName);
        }

        _roundFlowConfigService?.ApplyFull(includeTimeLimit: false);
        ScheduleDelayedMapExec("map_load", includeTimeLimit: false);
        ScheduleZoneRender("map_load");

        Core.Logger.LogInformation(
            "Map loaded: {Map}; start={Start}; end={End}; respawn={Respawn}",
            _currentMapName,
            _activeMap.StartTriggerName,
            _activeMap.EndTriggerName,
            _activeMap.RespawnPosition?.ToString() ?? "none");
    }

    private static string NormalizeMapName(string? mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return "";
        }

        var normalized = mapName.Trim().Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < normalized.Length - 1)
        {
            normalized = normalized[(lastSlash + 1)..];
        }

        return Path.GetFileNameWithoutExtension(normalized);
    }

    private static bool IsUsableMapName(string mapName)
    {
        return !string.IsNullOrWhiteSpace(mapName) &&
               !string.Equals(mapName, "unknown", StringComparison.OrdinalIgnoreCase);
    }

    private bool ReloadActiveMapData(bool preserveExisting = true)
    {
        if (_mapDataService is null)
        {
            Core.Logger.LogWarning("MapData reload skipped because MapDataService is not initialized.");
            return false;
        }

        try
        {
            var mapInfo = _mapDataService.LoadAsync(_currentMapName).GetAwaiter().GetResult();
            if (mapInfo is null)
            {
                if (TryRestoreKnownGoodActiveMap(_currentMapName))
                {
                    Core.Logger.LogWarning(
                        "MapData not found for '{Map}'. Restored last known good active map data: start={Start}; end={End}; respawn={Respawn}",
                        _currentMapName,
                        _activeMap.StartZone?.ToString() ?? "none",
                        _activeMap.EndZone?.ToString() ?? "none",
                        _activeMap.RespawnPosition?.ToString() ?? "none");
                    return false;
                }

                if (preserveExisting && CanPreserveCurrentActiveMap())
                {
                    Core.Logger.LogWarning(
                        "MapData not found for '{Map}'. Keeping existing active map data: start={Start}; end={End}; respawn={Respawn}",
                        _currentMapName,
                        _activeMap.StartZone?.ToString() ?? "none",
                        _activeMap.EndZone?.ToString() ?? "none",
                        _activeMap.RespawnPosition?.ToString() ?? "none");
                    return false;
                }

                KeepExistingActiveMapData("MapData not found");
                return false;
            }

            _activeMap = ActiveMapInfo.FromMapInfo(_currentMapName, mapInfo);
            RememberKnownGoodActiveMap(_activeMap);
            return true;
        }
        catch (Exception ex)
        {
            if (TryRestoreKnownGoodActiveMap(_currentMapName))
            {
                Core.Logger.LogWarning(
                    ex,
                    "Failed to reload MapData for '{Map}'. Restored last known good active map data.",
                    _currentMapName);
                return false;
            }

            if (preserveExisting && CanPreserveCurrentActiveMap())
            {
                Core.Logger.LogWarning(
                    ex,
                    "Failed to reload MapData for '{Map}'. Keeping existing active map data.",
                    _currentMapName);
                return false;
            }

            KeepExistingActiveMapData($"Failed to reload MapData: {ex.Message}");
            return false;
        }
    }

    private bool CanPreserveCurrentActiveMap()
    {
        return IsUsableMapName(_activeMap.MapName) &&
               string.Equals(_activeMap.MapName, _currentMapName, StringComparison.OrdinalIgnoreCase);
    }

    private void RememberKnownGoodActiveMap(ActiveMapInfo activeMap)
    {
        if (!HasConfiguredMapData(activeMap))
        {
            return;
        }

        _knownGoodActiveMaps[activeMap.MapName] = activeMap;
        _lastKnownGoodActiveMap = activeMap;
    }

    private bool TryRestoreKnownGoodActiveMap(string mapName)
    {
        if (IsUsableMapName(mapName) && _knownGoodActiveMaps.TryGetValue(mapName, out var knownGood))
        {
            _activeMap = knownGood;
            return true;
        }

        if (_lastKnownGoodActiveMap is not null &&
            (!IsUsableMapName(mapName) || string.Equals(_lastKnownGoodActiveMap.MapName, mapName, StringComparison.OrdinalIgnoreCase)))
        {
            _currentMapName = _lastKnownGoodActiveMap.MapName;
            _activeMap = _lastKnownGoodActiveMap;
            return true;
        }

        return false;
    }

    private static bool HasConfiguredMapData(ActiveMapInfo activeMap)
    {
        return activeMap.StartZone is not null ||
               activeMap.EndZone is not null ||
               activeMap.RespawnPosition is not null ||
               activeMap.Bonuses.Values.Any(bonus =>
                   bonus.StartZone is not null ||
                   bonus.EndZone is not null ||
                   bonus.RespawnPosition is not null);
    }

    private void KeepExistingActiveMapData(string reason)
    {
        if (HasConfiguredMapData(_activeMap))
        {
            Core.Logger.LogWarning(
                "{Reason} for '{Map}'. Keeping existing active map data: activeMap={ActiveMap}; start={Start}; end={End}; respawn={Respawn}",
                reason,
                _currentMapName,
                _activeMap.MapName,
                _activeMap.StartZone?.ToString() ?? "none",
                _activeMap.EndZone?.ToString() ?? "none",
                _activeMap.RespawnPosition?.ToString() ?? "none");
            return;
        }

        if (TryRestoreKnownGoodActiveMap(_currentMapName))
        {
            Core.Logger.LogWarning(
                "{Reason} for '{Map}'. Restored last known good active map data: activeMap={ActiveMap}; start={Start}; end={End}; respawn={Respawn}",
                reason,
                _currentMapName,
                _activeMap.MapName,
                _activeMap.StartZone?.ToString() ?? "none",
                _activeMap.EndZone?.ToString() ?? "none",
                _activeMap.RespawnPosition?.ToString() ?? "none");
            return;
        }

        Core.Logger.LogWarning(
            "{Reason} for '{Map}', and no existing active map data is available. Keeping current empty active map instead of resetting.",
            reason,
            _currentMapName);
    }

    private void RecoverMapFromEngineStatusIfNeeded()
    {
        if (HasUsableActiveMapData())
        {
            return;
        }

        if (TryRestoreKnownGoodActiveMap(_currentMapName))
        {
            _mapRecoveryWarningLogged = false;
            Core.Logger.LogWarning(
                "Recovered active map data from last known good cache. CurrentMap={CurrentMap}; start={Start}; end={End}; respawn={Respawn}",
                _currentMapName,
                _activeMap.StartZone?.ToString() ?? "none",
                _activeMap.EndZone?.ToString() ?? "none",
                _activeMap.RespawnPosition?.ToString() ?? "none");
            ScheduleZoneRender("map_recovered_cache", delayTicks: 1);
            return;
        }

        var recoveredMapName = TryGetCurrentMapFromStatus();
        var recoveredMap = recoveredMapName ?? "";
        if (!IsUsableMapName(recoveredMap))
        {
            if (!_mapRecoveryWarningLogged)
            {
                _mapRecoveryWarningLogged = true;
                Core.Logger.LogWarning(
                    "Map recovery skipped. CurrentMap={CurrentMap}; ActiveMap={ActiveMap}; no usable map name found from status.",
                    _currentMapName,
                    _activeMap.MapName);
            }

            return;
        }

        _mapRecoveryWarningLogged = false;
        Core.Logger.LogWarning(
            "Recovering active map data from server status. CurrentMap={CurrentMap}; RecoveredMap={RecoveredMap}; ActiveMap={ActiveMap}",
            _currentMapName,
            recoveredMap,
            _activeMap.MapName);

        LoadMap(recoveredMap);

        if (!HasUsableActiveMapData())
        {
            ReloadActiveMapData(preserveExisting: true);
        }
    }

    private bool HasUsableActiveMapData()
    {
        return IsUsableMapName(_currentMapName) &&
               CanPreserveCurrentActiveMap() &&
               HasConfiguredMapData(_activeMap);
    }

    private string? TryGetCurrentMapFromStatus()
    {
        try
        {
            var outputLines = new List<string>();
            Core.Engine.ExecuteCommandWithBuffer("status", line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    outputLines.Add(line);
                }
            });

            foreach (var line in outputLines)
            {
                var match = StatusMapPattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                return NormalizeMapName(match.Groups["map"].Value);
            }
        }
        catch (Exception ex)
        {
            if (!_mapRecoveryWarningLogged)
            {
                _mapRecoveryWarningLogged = true;
                Core.Logger.LogWarning(ex, "Failed to recover current map from server status.");
            }
        }

        return null;
    }

    private void SeedBundledMapData(SwiftlyBhopTimerPaths paths)
    {
        Directory.CreateDirectory(paths.MapDataDirectory);
        CleanupMisplacedSeededMapData(paths.MapDataDirectory);

        var fileCopied = 0;
        if (!Directory.Exists(paths.BundledMapDataDirectory))
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Bundled MapData not found: {paths.BundledMapDataDirectory}");
        }
        else
        {
            foreach (var sourcePath in Directory.EnumerateFiles(paths.BundledMapDataDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = NormalizeMapDataRelativePath(Path.GetRelativePath(paths.BundledMapDataDirectory, sourcePath));
                var destinationPath = Path.Combine(paths.MapDataDirectory, relativePath);

                if (File.Exists(destinationPath) && !ShouldRefreshSeededMapData(relativePath))
                {
                    continue;
                }

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                File.Copy(sourcePath, destinationPath, overwrite: true);
                fileCopied++;
            }
        }

        var resourceCopied = 0;
        var assembly = typeof(SwiftlyBhopTimer).Assembly;
        foreach (var resourceName in assembly.GetManifestResourceNames().Where(name => name.StartsWith("SwiftlyBhopTimer/MapData/", StringComparison.Ordinal)))
        {
            var relativePath = NormalizeMapDataRelativePath(resourceName["SwiftlyBhopTimer/MapData/".Length..]);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var destinationPath = Path.Combine(paths.MapDataDirectory, relativePath);
            if (File.Exists(destinationPath) && !ShouldRefreshSeededMapData(relativePath))
            {
                continue;
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            using var source = assembly.GetManifestResourceStream(resourceName);
            if (source is null)
            {
                continue;
            }

            using var destination = File.Create(destinationPath);
            source.CopyTo(destination);
            resourceCopied++;
        }

        Console.WriteLine($"[SwiftlyBhopTimer] Seeded MapData files: folder={fileCopied}, embedded={resourceCopied}; Source={paths.BundledMapDataDirectory}; Target={paths.MapDataDirectory}");
    }

    private static string NormalizeMapDataRelativePath(string relativePath)
    {
        return relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
    }

    private static void CleanupMisplacedSeededMapData(string mapDataDirectory)
    {
        if (!Directory.Exists(mapDataDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(mapDataDirectory))
        {
            var fileName = Path.GetFileName(path);
            if (!fileName.Contains('\\'))
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch
            {
                // Best effort cleanup for Linux files accidentally created with '\' in the file name.
            }
        }
    }

    private static bool ShouldRefreshSeededMapData(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return normalized.StartsWith("MapExecs/", StringComparison.OrdinalIgnoreCase);
    }

    private void RecoverMapDataIfDue()
    {
        if (HasUsableActiveMapData())
        {
            _mapRecoveryTicks = 0;
            return;
        }

        _mapRecoveryTicks++;
        if (_mapRecoveryTicks < MapRecoveryCheckTicks)
        {
            return;
        }

        _mapRecoveryTicks = 0;
        RecoverMapFromEngineStatusIfNeeded();
    }

    private void ScheduleDelayedMapExec(string reason, bool includeTimeLimit)
    {
        _delayedMapExecTicks = DelayedMapExecInitialDelayTicks;
        _delayedMapExecPassesRemaining = DelayedMapExecPasses;
        _delayedMapExecRoundResetPending = true;
        _delayedMapExecIncludeTimeLimit = includeTimeLimit;

        Core.Logger.LogInformation(
            "Scheduled delayed MapExec application. Reason={Reason}; Map={Map}; DelayTicks={DelayTicks}; Passes={Passes}; IncludeTimeLimit={IncludeTimeLimit}",
            reason,
            _currentMapName,
            DelayedMapExecInitialDelayTicks,
            DelayedMapExecPasses,
            includeTimeLimit);
    }

    private void ProcessDelayedMapExec()
    {
        if (_delayedMapExecPassesRemaining <= 0)
        {
            return;
        }

        if (_delayedMapExecTicks > 0)
        {
            _delayedMapExecTicks--;
            return;
        }

        _delayedMapExecPassesRemaining--;
        _delayedMapExecTicks = DelayedMapExecIntervalTicks;

        var executedPath = _mapExecService?.ExecuteForMap(_currentMapName);
        _roundFlowConfigService?.ApplyFull(includeTimeLimit: _delayedMapExecIncludeTimeLimit);
        ApplyBhopHelperDefaults("delayed_map_exec");

        if (executedPath is null)
        {
            if (_delayedMapExecPassesRemaining <= 0)
            {
                _delayedMapExecRoundResetPending = false;
            }

            Core.Logger.LogInformation(
                "No delayed MapExec cfg matched. Map={Map}; RemainingPasses={RemainingPasses}",
                _currentMapName,
                _delayedMapExecPassesRemaining);
            return;
        }

        Core.Logger.LogInformation(
            "Delayed MapExec applied. Map={Map}; Cfg={Cfg}; RemainingPasses={RemainingPasses}",
            _currentMapName,
            executedPath,
            _delayedMapExecPassesRemaining);

        if (!_delayedMapExecRoundResetPending)
        {
            return;
        }

        _delayedMapExecRoundResetPending = false;
        ExecuteServerCommand("mp_restartgame 1");
        SchedulePostRoundResetConfig(includeTimeLimit: _delayedMapExecIncludeTimeLimit);
        Core.Logger.LogInformation(
            "Round reset requested after delayed MapExec. Map={Map}; Cfg={Cfg}",
            _currentMapName,
            executedPath);
    }

    private void SchedulePostRoundResetConfig(bool includeTimeLimit)
    {
        _postRoundResetConfigTicks = PostRoundResetConfigInitialDelayTicks;
        _postRoundResetConfigPassesRemaining = PostRoundResetConfigPasses;
        _postRoundResetConfigIncludeTimeLimit = includeTimeLimit;

        Core.Logger.LogInformation(
            "Scheduled post-round-reset config enforcement. Map={Map}; DelayTicks={DelayTicks}; Passes={Passes}; IncludeTimeLimit={IncludeTimeLimit}",
            _currentMapName,
            PostRoundResetConfigInitialDelayTicks,
            PostRoundResetConfigPasses,
            includeTimeLimit);
    }

    private void ProcessPostRoundResetConfig()
    {
        if (_postRoundResetConfigPassesRemaining <= 0)
        {
            return;
        }

        if (_postRoundResetConfigTicks > 0)
        {
            _postRoundResetConfigTicks--;
            return;
        }

        _postRoundResetConfigPassesRemaining--;
        _postRoundResetConfigTicks = PostRoundResetConfigIntervalTicks;

        _roundFlowConfigService?.ApplyFull(includeTimeLimit: _postRoundResetConfigIncludeTimeLimit);
        ApplyBhopHelperDefaults("post_round_reset_config");

        Core.Logger.LogInformation(
            "Post-round-reset config enforced. Map={Map}; RemainingPasses={RemainingPasses}",
            _currentMapName,
            _postRoundResetConfigPassesRemaining);
    }

    private void ProcessDelayedZoneRendering()
    {
        if (!_zoneRenderPending)
        {
            return;
        }

        _zoneRenderTicks++;
        if (_zoneRenderTicks < _zoneRenderDelayTicks)
        {
            return;
        }

        _zoneRenderPending = false;
        try
        {
            _zoneRenderService?.DrawZones(_activeMap);
            Console.WriteLine($"[SwiftlyBhopTimer] Zone beam redraw reason: {_zoneRenderReason}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Zone rendering failed: {ex}");
        }
    }

    private void ProcessZoneRenderHealthCheck()
    {
        if (_zoneRenderPending)
        {
            return;
        }

        _zoneRenderHealthTicks++;
        if (_zoneRenderHealthTicks < ZoneRenderHealthCheckTicks)
        {
            return;
        }

        _zoneRenderHealthTicks = 0;
        if (_zoneRenderService?.HasInvalidBeams() == true)
        {
            ScheduleZoneRender("invalid_beam");
        }
    }

    private void ProcessZoneSetupPreviews()
    {
        foreach (var player in Core.PlayerManager.GetAllValidPlayers())
        {
            if (!IsPlayablePlayer(player) || !_zoneSetupPreviews.TryGetValue(player.Slot, out var preview))
            {
                continue;
            }

            preview.Ticks++;
            if (preview.Ticks < ZoneSetupPreviewUpdateIntervalTicks)
            {
                continue;
            }

            preview.Ticks = 0;
            var pawn = player.PlayerPawn ?? player.Pawn;
            if (!EntityReflection.TryGetPosition(pawn, out var currentPosition))
            {
                continue;
            }

            var previewZone = ZoneBounds.FromPoints(preview.Anchor, currentPosition);
            _zoneRenderService?.DrawPreviewZone(player.Slot, previewZone, preview.StartZone);
        }
    }

    private void ScheduleZoneRender(string reason, int delayTicks = ZoneRenderDelayTicks)
    {
        _zoneRenderPending = true;
        _zoneRenderTicks = 0;
        _zoneRenderDelayTicks = Math.Max(1, delayTicks);
        _zoneRenderHealthTicks = 0;
        _zoneRenderReason = reason;
    }

    private void UpdateZoneSetupPreview(IPlayer player, string propertyName, Vector3Value position)
    {
        if (propertyName.EndsWith("RespawnPos", StringComparison.OrdinalIgnoreCase))
        {
            _zoneSetupPreviews.Remove(player.Slot);
            _zoneRenderService?.ClearPreview(player.Slot);
            return;
        }

        var startZone = propertyName.Contains("Start", StringComparison.OrdinalIgnoreCase);
        if (_zoneSetupPreviews.TryGetValue(player.Slot, out var current) &&
            current.StartZone == startZone &&
            !string.Equals(current.PropertyName, propertyName, StringComparison.OrdinalIgnoreCase))
        {
            _zoneSetupPreviews.Remove(player.Slot);
            _zoneRenderService?.ClearPreview(player.Slot);
            return;
        }

        _zoneSetupPreviews[player.Slot] = new ZoneSetupPreview(position, startZone, propertyName);
    }

    private Vector3Value? GetRestartPosition()
    {
        EnsureActiveMapDataAvailable("restart");

        if (_activeMap.RespawnPosition is not null)
        {
            return _activeMap.RespawnPosition.Value;
        }

        if (_activeMap.StartZone is null)
        {
            return null;
        }

        return new Vector3Value(
            (_activeMap.StartZone.MinX + _activeMap.StartZone.MaxX) / 2.0f,
            (_activeMap.StartZone.MinY + _activeMap.StartZone.MaxY) / 2.0f,
            _activeMap.StartZone.MinZ);
    }

    private Vector3Value? GetConfiguredRestartPosition()
    {
        EnsureActiveMapDataAvailable("restart");
        return _activeMap.RespawnPosition;
    }

    private Vector3Value? GetStartZoneRestartFallbackPosition()
    {
        EnsureActiveMapDataAvailable("restart");

        if (_activeMap.StartZone is null)
        {
            return null;
        }

        return new Vector3Value(
            (_activeMap.StartZone.MinX + _activeMap.StartZone.MaxX) / 2.0f,
            (_activeMap.StartZone.MinY + _activeMap.StartZone.MaxY) / 2.0f,
            _activeMap.StartZone.MinZ);
    }

    private Vector3Value? GetBonusRestartPosition(int bonusNumber)
    {
        EnsureActiveMapDataAvailable("bonus restart");

        if (!_activeMap.TryGetBonus(bonusNumber, out var bonus))
        {
            return null;
        }

        if (bonus.RespawnPosition is not null)
        {
            return bonus.RespawnPosition.Value;
        }

        if (bonus.StartZone is null)
        {
            return null;
        }

        return new Vector3Value(
            (bonus.StartZone.MinX + bonus.StartZone.MaxX) / 2.0f,
            (bonus.StartZone.MinY + bonus.StartZone.MaxY) / 2.0f,
            bonus.StartZone.MinZ);
    }

    private void EnsureActiveMapDataAvailable(string reason)
    {
        if (HasUsableActiveMapData())
        {
            return;
        }

        Core.Logger.LogWarning(
            "Active map data missing during {Reason}. Attempting recovery. CurrentMap={CurrentMap}; ActiveMap={ActiveMap}",
            reason,
            _currentMapName,
            _activeMap.MapName);

        RecoverMapFromEngineStatusIfNeeded();
    }

    private sealed class ZoneSetupPreview
    {
        public ZoneSetupPreview(Vector3Value anchor, bool startZone, string propertyName)
        {
            Anchor = anchor;
            StartZone = startZone;
            PropertyName = propertyName;
        }

        public Vector3Value Anchor { get; }
        public bool StartZone { get; }
        public string PropertyName { get; }
        public int Ticks { get; set; }
    }
}
