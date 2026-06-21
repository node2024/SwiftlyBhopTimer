using System.Data;
using System.Text.Json;
using Dapper;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;

namespace SwiftlyBhopTimer.Services;

public sealed class ReplayService
{
    private const float PlaybackVelocityTickRate = 64.0f;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<IDbConnection> _connectionFactory;
    private readonly Dictionary<int, List<ReplayFrame>> _activeRecordings = [];
    private readonly Dictionary<string, ReplayData?> _loadedReplays = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReplayData?> _loadedPersonalBestReplays = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> _playbackProgress = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReplayPlaybackStatus> _playbackStatuses = new(StringComparer.OrdinalIgnoreCase);

    public ReplayService(Func<IDbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public void EnsureInitialized()
    {
        using var connection = OpenConnection();
        var dialect = SqlDialect.FromConnection(connection);
        connection.Execute(dialect.CreateReplaysTableSql);
        connection.Execute(dialect.CreatePersonalBestReplaysTableSql);
    }

    public void ClearRuntime()
    {
        _activeRecordings.Clear();
        _loadedReplays.Clear();
        _loadedPersonalBestReplays.Clear();
        _playbackProgress.Clear();
        _playbackStatuses.Clear();
    }

    public void StartRecording(IPlayer player)
    {
        _activeRecordings[player.Slot] = [];
    }

    public void DiscardRecording(IPlayer player)
    {
        DiscardRecording(player.Slot);
    }

    public void DiscardRecording(int slot)
    {
        _activeRecordings.Remove(slot);
    }

    public IReadOnlyList<ReplayFrame> FinishRecording(IPlayer player)
    {
        if (!_activeRecordings.TryGetValue(player.Slot, out var frames))
        {
            return [];
        }

        _activeRecordings.Remove(player.Slot);
        return frames.ToArray();
    }

    public void CaptureFrame(IPlayer player, int timerTicks)
    {
        if (!_activeRecordings.TryGetValue(player.Slot, out var frames))
        {
            return;
        }

        var pawn = player.PlayerPawn ?? player.Pawn;
        if (!EntityReflection.TryGetPosition(pawn, out var position))
        {
            return;
        }

        EntityReflection.TryGetEyeAngles(pawn, out var angle);
        EntityReflection.TryGetVelocity(pawn, out var velocity);
        frames.Add(new ReplayFrame(timerTicks, position, angle, velocity));
    }

    public void SaveReplay(string mapName, string steamId, string playerName, int timerTicks, IReadOnlyList<ReplayFrame> frames)
    {
        SaveReplay(mapName, TimerRunMode.Standard, steamId, playerName, timerTicks, frames);
    }

    public void SaveReplay(string mapName, TimerRunMode mode, string steamId, string playerName, int timerTicks, IReadOnlyList<ReplayFrame> frames)
    {
        if (string.IsNullOrWhiteSpace(mapName) || frames.Count == 0)
        {
            return;
        }

        var storageMapName = GetReplayStorageMapName(mapName, mode);
        var framesJson = JsonSerializer.Serialize(frames, JsonOptions);
        using var connection = OpenConnection();
        var dialect = SqlDialect.FromConnection(connection);
        connection.Execute(dialect.UpsertReplaySql, new
        {
            mapName = storageMapName,
            steamId,
            playerName,
            timerTicks,
            frameCount = frames.Count,
            framesJson,
            updatedAtUtc = DateTime.UtcNow
        });

        _loadedReplays[storageMapName] = new ReplayData(mapName, steamId, playerName, timerTicks, frames.ToArray());
        _playbackProgress[ServerReplayPlaybackKey(storageMapName)] = 0f;
        _playbackStatuses.Remove(ServerReplayPlaybackKey(storageMapName));
    }

    public void SavePersonalBestReplay(string mapName, string steamId, string playerName, int timerTicks, IReadOnlyList<ReplayFrame> frames)
    {
        SavePersonalBestReplay(mapName, TimerRunMode.Standard, steamId, playerName, timerTicks, frames);
    }

    public void SavePersonalBestReplay(string mapName, TimerRunMode mode, string steamId, string playerName, int timerTicks, IReadOnlyList<ReplayFrame> frames)
    {
        if (string.IsNullOrWhiteSpace(mapName) || string.IsNullOrWhiteSpace(steamId) || frames.Count == 0)
        {
            return;
        }

        var storageMapName = GetReplayStorageMapName(mapName, mode);
        var framesJson = JsonSerializer.Serialize(frames, JsonOptions);
        using var connection = OpenConnection();
        var dialect = SqlDialect.FromConnection(connection);
        connection.Execute(dialect.UpsertPersonalBestReplaySql, new
        {
            mapName = storageMapName,
            steamId,
            playerName,
            timerTicks,
            frameCount = frames.Count,
            framesJson,
            updatedAtUtc = DateTime.UtcNow
        });

        var key = PersonalBestCacheKey(storageMapName, steamId);
        _loadedPersonalBestReplays[key] = new ReplayData(mapName, steamId, playerName, timerTicks, frames.ToArray());
        _playbackProgress[PersonalBestReplayPlaybackKey(storageMapName, steamId)] = 0f;
        _playbackStatuses.Remove(PersonalBestReplayPlaybackKey(storageMapName, steamId));
    }

    public ReplayData? LoadReplay(string mapName)
    {
        return LoadReplay(mapName, TimerRunMode.Standard);
    }

    public ReplayData? LoadReplay(string mapName, TimerRunMode mode)
    {
        var storageMapName = GetReplayStorageMapName(mapName, mode);
        if (_loadedReplays.TryGetValue(storageMapName, out var cached))
        {
            return cached;
        }

        using var connection = OpenConnection();
        var dialect = SqlDialect.FromConnection(connection);
        var sql = $"""
            SELECT
                map_name AS MapName,
                steam_id AS SteamId,
                player_name AS PlayerName,
                timer_ticks AS TimerTicks,
                frames_json AS FramesJson
            FROM st_replays
            WHERE map_name = {dialect.Parameter("mapName")}
            LIMIT 1;
            """;
        var row = connection.QueryFirstOrDefault<ReplayRow>(sql, new { mapName = storageMapName });
        if (row is null || string.IsNullOrWhiteSpace(row.FramesJson))
        {
            _loadedReplays[storageMapName] = null;
            _playbackProgress[ServerReplayPlaybackKey(storageMapName)] = 0f;
            _playbackStatuses.Remove(ServerReplayPlaybackKey(storageMapName));
            return null;
        }

        var frames = JsonSerializer.Deserialize<ReplayFrame[]>(row.FramesJson, JsonOptions) ?? [];
        var replay = new ReplayData(mapName, row.SteamId, row.PlayerName ?? "", row.TimerTicks, frames);
        _loadedReplays[storageMapName] = replay;
        _playbackProgress[ServerReplayPlaybackKey(storageMapName)] = 0f;
        _playbackStatuses.Remove(ServerReplayPlaybackKey(storageMapName));
        return replay;
    }

    public ReplayData? LoadPersonalBestReplay(string mapName, string steamId)
    {
        return LoadPersonalBestReplay(mapName, TimerRunMode.Standard, steamId);
    }

    public ReplayData? LoadPersonalBestReplay(string mapName, TimerRunMode mode, string steamId)
    {
        var storageMapName = GetReplayStorageMapName(mapName, mode);
        var key = PersonalBestCacheKey(storageMapName, steamId);
        if (_loadedPersonalBestReplays.TryGetValue(key, out var cached))
        {
            return cached;
        }

        using var connection = OpenConnection();
        var dialect = SqlDialect.FromConnection(connection);
        var sql = $"""
            SELECT
                map_name AS MapName,
                steam_id AS SteamId,
                player_name AS PlayerName,
                timer_ticks AS TimerTicks,
                frames_json AS FramesJson
            FROM st_pb_replays
            WHERE map_name = {dialect.Parameter("mapName")}
              AND steam_id = {dialect.Parameter("steamId")}
            LIMIT 1;
            """;
        var row = connection.QueryFirstOrDefault<ReplayRow>(sql, new { mapName = storageMapName, steamId });
        if (row is null || string.IsNullOrWhiteSpace(row.FramesJson))
        {
            _loadedPersonalBestReplays[key] = null;
            _playbackProgress[PersonalBestReplayPlaybackKey(storageMapName, steamId)] = 0f;
            _playbackStatuses.Remove(PersonalBestReplayPlaybackKey(storageMapName, steamId));
            return null;
        }

        var frames = JsonSerializer.Deserialize<ReplayFrame[]>(row.FramesJson, JsonOptions) ?? [];
        var replay = new ReplayData(mapName, row.SteamId, row.PlayerName ?? "", row.TimerTicks, frames);
        _loadedPersonalBestReplays[key] = replay;
        _playbackProgress[PersonalBestReplayPlaybackKey(storageMapName, steamId)] = 0f;
        _playbackStatuses.Remove(PersonalBestReplayPlaybackKey(storageMapName, steamId));
        return replay;
    }

    public bool DeleteReplayIfMatches(string mapName, string steamId, int timerTicks)
    {
        return DeleteReplayIfMatches(mapName, TimerRunMode.Standard, steamId, timerTicks);
    }

    public bool DeleteReplayIfMatches(string mapName, TimerRunMode mode, string steamId, int timerTicks)
    {
        var storageMapName = GetReplayStorageMapName(mapName, mode);
        using var connection = OpenConnection();
        var dialect = SqlDialect.FromConnection(connection);
        var sql = $"""
            DELETE FROM st_replays
            WHERE map_name = {dialect.Parameter("mapName")}
              AND steam_id = {dialect.Parameter("steamId")}
              AND timer_ticks = {dialect.Parameter("timerTicks")};
            """;
        var affected = connection.Execute(sql, new { mapName = storageMapName, steamId, timerTicks });
        if (affected <= 0)
        {
            return false;
        }

        _loadedReplays.Remove(storageMapName);
        _playbackProgress[ServerReplayPlaybackKey(storageMapName)] = 0f;
        _playbackStatuses.Remove(ServerReplayPlaybackKey(storageMapName));

        return true;
    }

    public bool PlaybackTick(IPlayer bot, string mapName)
    {
        return PlaybackServerReplayTick(bot, mapName, ServerReplayPlaybackKey(mapName), loop: true, forceIndexTimeline: true);
    }

    public bool PlaybackServerReplayTick(IPlayer bot, string mapName, string playbackKey, bool loop, bool forceIndexTimeline = false)
    {
        return PlaybackServerReplayTick(bot, mapName, TimerRunMode.Standard, playbackKey, loop, forceIndexTimeline);
    }

    public bool PlaybackServerReplayTick(IPlayer bot, string mapName, TimerRunMode mode, string playbackKey, bool loop, bool forceIndexTimeline = false)
    {
        var replay = LoadReplay(mapName, mode);
        return PlaybackTick(bot, replay, playbackKey, loop, forceIndexTimeline);
    }

    public bool PlaybackReplayTick(IPlayer bot, ReplayData replay, string playbackKey)
    {
        return PlaybackReplayTick(bot, replay, playbackKey, loop: false, forceIndexTimeline: true);
    }

    public bool PlaybackReplayTick(IPlayer bot, ReplayData replay, string playbackKey, bool loop, bool forceIndexTimeline = true)
    {
        return PlaybackTick(bot, replay, playbackKey, loop, forceIndexTimeline);
    }

    public bool PlaybackPersonalBestTick(IPlayer bot, string mapName, string steamId)
    {
        return PlaybackPersonalBestTick(bot, mapName, steamId, PersonalBestReplayPlaybackKey(mapName, steamId), loop: true, forceIndexTimeline: true);
    }

    public bool PlaybackPersonalBestTick(IPlayer bot, string mapName, string steamId, string playbackKey, bool loop, bool forceIndexTimeline = true)
    {
        return PlaybackPersonalBestTick(bot, mapName, TimerRunMode.Standard, steamId, playbackKey, loop, forceIndexTimeline);
    }

    public bool PlaybackPersonalBestTick(IPlayer bot, string mapName, TimerRunMode mode, string steamId, string playbackKey, bool loop, bool forceIndexTimeline = true)
    {
        var replay = LoadPersonalBestReplay(mapName, mode, steamId);
        return PlaybackTick(bot, replay, playbackKey, loop, forceIndexTimeline);
    }

    public bool HasReplay(string mapName)
    {
        return HasReplay(mapName, TimerRunMode.Standard);
    }

    public bool HasReplay(string mapName, TimerRunMode mode)
    {
        var replay = LoadReplay(mapName, mode);
        return replay is not null && replay.Frames.Count > 0;
    }

    public bool HasPersonalBestReplay(string mapName, string steamId)
    {
        return HasPersonalBestReplay(mapName, TimerRunMode.Standard, steamId);
    }

    public bool HasPersonalBestReplay(string mapName, TimerRunMode mode, string steamId)
    {
        var replay = LoadPersonalBestReplay(mapName, mode, steamId);
        return replay is not null && replay.Frames.Count > 0;
    }

    public ReplayPlaybackStatus? GetServerReplayStatus(string mapName)
    {
        return _playbackStatuses.TryGetValue(ServerReplayPlaybackKey(mapName), out var status) ? status : null;
    }

    public ReplayPlaybackStatus? GetServerReplayStatus(string mapName, TimerRunMode mode)
    {
        return _playbackStatuses.TryGetValue(GetServerReplayPlaybackKey(mapName, mode), out var status) ? status : null;
    }

    public ReplayPlaybackStatus? GetReplayStatus(string playbackKey)
    {
        return _playbackStatuses.TryGetValue(playbackKey, out var status) ? status : null;
    }

    public void ResetReplayPlayback(string playbackKey)
    {
        _playbackProgress[playbackKey] = 0f;
        _playbackStatuses.Remove(playbackKey);
    }

    public void ResetServerReplayPlayback(string mapName)
    {
        ResetReplayPlayback(ServerReplayPlaybackKey(mapName));
    }

    public void ResetServerReplayPlayback(string mapName, TimerRunMode mode)
    {
        ResetReplayPlayback(GetServerReplayPlaybackKey(mapName, mode));
    }

    public void RemoveReplayPlayback(string playbackKey)
    {
        _playbackProgress.Remove(playbackKey);
        _playbackStatuses.Remove(playbackKey);
    }

    public ReplayPlaybackStatus? GetPersonalBestReplayStatus(string mapName, string steamId)
    {
        return _playbackStatuses.TryGetValue(PersonalBestReplayPlaybackKey(mapName, steamId), out var status) ? status : null;
    }

    private bool PlaybackTick(IPlayer bot, ReplayData? replay, string playbackKey, bool loop, bool forceIndexTimeline)
    {
        if (replay is null || replay.Frames.Count == 0)
        {
            _playbackStatuses.Remove(playbackKey);
            return false;
        }

        _playbackProgress.TryGetValue(playbackKey, out var playbackProgress);
        var useIndexTimeline = forceIndexTimeline || ShouldUseIndexTimeline(replay);
        var totalProgress = GetPlaybackTotalProgress(replay, useIndexTimeline);
        if (playbackProgress > totalProgress)
        {
            if (!loop)
            {
                var endedCurrentTicks = CalculateDisplayedTicks(replay, playbackProgress, totalProgress);
                _playbackStatuses[playbackKey] = new ReplayPlaybackStatus(
                    replay.MapName,
                    replay.SteamId,
                    replay.PlayerName,
                    replay.TimerTicks,
                    endedCurrentTicks,
                    0.0f,
                    replay.Frames.Count,
                    replay.Frames.Count);
                return false;
            }

            playbackProgress = 0f;
        }

        var normalizedProgress = totalProgress <= 0 ? 0.0f : Clamp(playbackProgress / totalProgress, 0.0f, 1.0f);
        var sample = useIndexTimeline
            ? BuildIndexTimelineSample(replay.Frames, normalizedProgress, totalProgress)
            : BuildTickTimelineSample(replay.Frames, ReplayTickForProgress(replay.Frames, normalizedProgress, totalProgress));
        var nextPlaybackProgress = playbackProgress + 1.0f;
        var completedLoop = nextPlaybackProgress > totalProgress;
        _playbackProgress[playbackKey] = completedLoop
            ? loop ? 0.0f : totalProgress
            : nextPlaybackProgress;
        var speed = MathF.Sqrt(sample.Velocity.X * sample.Velocity.X + sample.Velocity.Y * sample.Velocity.Y);
        var statusCurrentTicks = CalculateDisplayedTicks(replay, playbackProgress, totalProgress);
        var frameIndex = completedLoop ? replay.Frames.Count : Math.Min(sample.FrameIndex + 1, replay.Frames.Count);
        _playbackStatuses[playbackKey] = new ReplayPlaybackStatus(
            replay.MapName,
            replay.SteamId,
            replay.PlayerName,
            replay.TimerTicks,
            statusCurrentTicks,
            speed,
            frameIndex,
            replay.Frames.Count);

        if (!loop && completedLoop)
        {
            return false;
        }

        bot.Teleport(
            new Vector(sample.Position.X, sample.Position.Y, sample.Position.Z),
            new QAngle { X = sample.Angle.X, Y = sample.Angle.Y, Z = sample.Angle.Z },
            new Vector(sample.Velocity.X, sample.Velocity.Y, sample.Velocity.Z));
        return true;
    }

    private static int GetPlaybackTotalProgress(ReplayData replay, bool useIndexTimeline)
    {
        if (replay.Frames.Count < 2)
        {
            return 1;
        }

        if (useIndexTimeline)
        {
            return replay.TimerTicks > 0
                ? Math.Max(replay.TimerTicks, 1)
                : Math.Max(replay.Frames.Count - 1, 1);
        }

        var frameSpan = replay.Frames[^1].Tick - replay.Frames[0].Tick;
        if (frameSpan <= 0)
        {
            return Math.Max(replay.Frames.Count - 1, 1);
        }

        if (replay.TimerTicks > 0)
        {
            return Math.Max(replay.TimerTicks, 1);
        }

        return Math.Max(frameSpan, 1);
    }

    private static int CalculateDisplayedTicks(ReplayData replay, float playbackProgress, int totalProgress)
    {
        if (totalProgress <= 0)
        {
            return 0;
        }

        if (replay.TimerTicks <= 0)
        {
            return (int)Clamp(playbackProgress, 0.0f, MathF.Max(replay.Frames.Count - 1, 0.0f));
        }

        var ratio = Clamp(playbackProgress / totalProgress, 0.0f, 1.0f);
        return Math.Min((int)MathF.Round(replay.TimerTicks * ratio), replay.TimerTicks);
    }

    private static bool ShouldUseIndexTimeline(ReplayData replay)
    {
        if (replay.TimerTicks <= 0 || replay.Frames.Count < 2)
        {
            return true;
        }

        var firstTick = replay.Frames[0].Tick;
        var lastTick = replay.Frames[^1].Tick;
        if (lastTick <= firstTick)
        {
            return true;
        }

        var tickSpan = lastTick - firstTick;
        var tickCoverage = tickSpan / (float)replay.TimerTicks;
        // If tick span is far from the stored timer duration, index timeline is
        // more stable and avoids long-distance stalls or overspeed in sampled movement.
        return tickCoverage < 0.80f || tickCoverage > 1.25f;
    }

    private static ReplayPlaybackSample BuildIndexTimelineSample(IReadOnlyList<ReplayFrame> frames, float normalizedProgress, int totalTicks)
    {
        if (frames.Count == 1 || totalTicks <= 0)
        {
            return ReplayPlaybackSample.FromFrame(frames[0], 0);
        }

        var exactIndex = Clamp(normalizedProgress, 0.0f, 1.0f) * (frames.Count - 1);
        var frameIndex = Math.Min((int)MathF.Floor(exactIndex), frames.Count - 1);
        if (frameIndex >= frames.Count - 1)
        {
            return ReplayPlaybackSample.FromFrame(frames[^1], frames.Count - 1);
        }

        var amount = exactIndex - frameIndex;
        var frameTickSpan = totalTicks / MathF.Max(frames.Count - 1, 1.0f);
        return Interpolate(frames[frameIndex], frames[frameIndex + 1], amount, frameIndex, velocityTickSpan: frameTickSpan);
    }

    private static ReplayPlaybackSample BuildTickTimelineSample(IReadOnlyList<ReplayFrame> frames, int playbackTick)
    {
        var frameIndex = FindFrameIndexForPlaybackTick(frames, playbackTick);
        if (frameIndex >= frames.Count - 1)
        {
            return ReplayPlaybackSample.FromFrame(frames[frameIndex], frameIndex);
        }

        var current = frames[frameIndex];
        var next = frames[frameIndex + 1];
        var tickSpan = next.Tick - current.Tick;
        if (tickSpan <= 0 || playbackTick <= current.Tick)
        {
            return ReplayPlaybackSample.FromFrame(current, frameIndex);
        }

        var amount = Clamp((playbackTick - current.Tick) / (float)tickSpan, 0.0f, 1.0f);
        return Interpolate(current, next, amount, frameIndex, Math.Max(tickSpan, 1));
    }

    private static int ReplayTickForProgress(IReadOnlyList<ReplayFrame> frames, float normalizedProgress, int totalProgress)
    {
        if (frames.Count == 0)
        {
            return 0;
        }

        if (frames.Count == 1 || normalizedProgress <= 0.0f)
        {
            return frames[0].Tick;
        }

        if (totalProgress <= 1 || normalizedProgress >= 1.0f)
        {
            return frames[^1].Tick;
        }

        var firstTick = frames[0].Tick;
        var lastTick = frames[^1].Tick;
        var span = Math.Max(lastTick - firstTick, 1);
        var ratio = normalizedProgress * (totalProgress - 1) / totalProgress;
        return firstTick + (int)MathF.Floor(span * Clamp(ratio, 0.0f, 1.0f));
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static ReplayPlaybackSample Interpolate(ReplayFrame current, ReplayFrame next, float amount, int frameIndex, float velocityTickSpan)
    {
        return new ReplayPlaybackSample(
            Lerp(current.Position, next.Position, amount),
            LerpAngles(current.Angle, next.Angle, amount),
            CalculatePlaybackVelocity(current.Position, next.Position, velocityTickSpan),
            frameIndex);
    }

    private static Vector3Value CalculatePlaybackVelocity(Vector3Value current, Vector3Value next, float velocityTickSpan)
    {
        var tickSpan = MathF.Max(velocityTickSpan, 0.01f);
        var scale = PlaybackVelocityTickRate / tickSpan;
        return new Vector3Value(
            (next.X - current.X) * scale,
            (next.Y - current.Y) * scale,
            (next.Z - current.Z) * scale);
    }

    private static Vector3Value Lerp(Vector3Value current, Vector3Value next, float amount)
    {
        return new Vector3Value(
            current.X + ((next.X - current.X) * amount),
            current.Y + ((next.Y - current.Y) * amount),
            current.Z + ((next.Z - current.Z) * amount));
    }

    private static Vector3Value LerpAngles(Vector3Value current, Vector3Value next, float amount)
    {
        return new Vector3Value(
            LerpAngle(current.X, next.X, amount),
            LerpAngle(current.Y, next.Y, amount),
            LerpAngle(current.Z, next.Z, amount));
    }

    private static float LerpAngle(float current, float next, float amount)
    {
        var delta = ((next - current + 540.0f) % 360.0f) - 180.0f;
        return current + (delta * amount);
    }

    private static int FindFrameIndexForPlaybackTick(IReadOnlyList<ReplayFrame> frames, int playbackTick)
    {
        var low = 0;
        var high = frames.Count - 1;
        var best = 0;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            if (frames[middle].Tick <= playbackTick)
            {
                best = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return best;
    }

    private static string ServerReplayPlaybackKey(string mapName)
    {
        return $"sr:{mapName}";
    }

    public static string GetServerReplayPlaybackKey(string mapName, TimerRunMode mode)
    {
        return ServerReplayPlaybackKey(GetReplayStorageMapName(mapName, mode));
    }

    public static string GetReplayStorageMapName(string mapName, TimerRunMode mode)
    {
        return mode == TimerRunMode.Standard
            ? mapName
            : $"{mapName}__mode_{TimerRunModes.ToStorageValue(mode)}";
    }

    private static string PersonalBestCacheKey(string mapName, string steamId)
    {
        return $"{mapName}\n{steamId}";
    }

    private static string PersonalBestReplayPlaybackKey(string mapName, string steamId)
    {
        return $"pb:{mapName}:{steamId}";
    }

    private IDbConnection OpenConnection()
    {
        var connection = _connectionFactory();
        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        return connection;
    }

    private sealed class ReplayRow
    {
        public string MapName { get; set; } = "";
        public string SteamId { get; set; } = "";
        public string? PlayerName { get; set; }
        public int TimerTicks { get; set; }
        public string FramesJson { get; set; } = "";
    }
}

public sealed record ReplayData(
    string MapName,
    string SteamId,
    string PlayerName,
    int TimerTicks,
    IReadOnlyList<ReplayFrame> Frames);

public sealed record ReplayPlaybackStatus(
    string MapName,
    string SteamId,
    string PlayerName,
    int TotalTicks,
    int CurrentTicks,
    float Speed,
    int FrameIndex,
    int FrameCount);

public readonly record struct ReplayFrame(
    int Tick,
    Vector3Value Position,
    Vector3Value Angle,
    Vector3Value Velocity);

internal readonly record struct ReplayPlaybackSample(
    Vector3Value Position,
    Vector3Value Angle,
    Vector3Value Velocity,
    int FrameIndex)
{
    public static ReplayPlaybackSample FromFrame(ReplayFrame frame, int frameIndex)
    {
        return new ReplayPlaybackSample(frame.Position, frame.Angle, frame.Velocity, frameIndex);
    }
}
