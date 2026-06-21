using System.Data;
using System.Text.Json;
using Dapper;
using SwiftlyBhopTimer.Models;

namespace SwiftlyBhopTimer.Services;

public sealed class DbRecordStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<IDbConnection> _connectionFactory;
    private readonly string _legacyPlayerRecordsDirectory;
    private readonly object _sync = new();

    public DbRecordStore(Func<IDbConnection> connectionFactory, string legacyPlayerRecordsDirectory)
    {
        _connectionFactory = connectionFactory;
        _legacyPlayerRecordsDirectory = legacyPlayerRecordsDirectory;
    }

    public void EnsureInitialized()
    {
        lock (_sync)
        {
            using var connection = OpenConnection();
            var dialect = SqlDialect.FromConnection(connection);
            CreateSchema(connection, dialect);
            EnsureTimesColumns(connection, dialect);
            CreateSupplementalIndexes(connection, dialect);
            ImportLegacyJsonRecordsIfNeeded(connection, dialect);
        }
    }

    public Task<IReadOnlyList<PlayerRecordEntry>> GetTopRecordsAsync(string mapName, int limit)
    {
        return GetTopRecordsAsync(mapName, limit, TimerRunMode.Standard);
    }

    public Task<IReadOnlyList<PlayerRecordEntry>> GetTopRecordsAsync(string mapName, int limit, TimerRunMode mode)
    {
        lock (_sync)
        {
            using var connection = OpenConnection();
            var dialect = SqlDialect.FromConnection(connection);
            var runMode = TimerRunModes.ToStorageValue(mode);
            var sql = $"""
                SELECT b.steam_id AS SteamId, b.player_name AS PlayerName, b.timer_ticks AS TimerTicks
                FROM (
                    SELECT
                        t.steam_id,
                        (
                            SELECT t2.player_name
                            FROM st_times t2
                            WHERE t2.map_name = t.map_name
                              AND t2.steam_id = t.steam_id
                              AND t2.run_mode = t.run_mode
                            ORDER BY t2.timer_ticks ASC, t2.id ASC
                            LIMIT 1
                        ) AS player_name,
                        MIN(t.timer_ticks) AS timer_ticks
                    FROM st_times t
                    WHERE t.map_name = {dialect.Parameter("mapName")}
                      AND t.run_mode = {dialect.Parameter("runMode")}
                    GROUP BY t.steam_id, t.map_name, t.run_mode
                ) b
                ORDER BY b.timer_ticks ASC, b.steam_id ASC
                LIMIT {dialect.Parameter("limit")};
                """;

            var records = connection.Query<PlayerRecordRow>(sql, new { mapName, runMode, limit })
                .Select(record => new PlayerRecordEntry(record.SteamId, record.PlayerName, record.TimerTicks))
                .ToArray();

            return Task.FromResult<IReadOnlyList<PlayerRecordEntry>>(records);
        }
    }

    public async Task<PlayerRank?> GetRankAsync(string mapName, string steamId)
    {
        return await GetRankAsync(mapName, steamId, TimerRunMode.Standard);
    }

    public async Task<PlayerRank?> GetRankAsync(string mapName, string steamId, TimerRunMode mode)
    {
        var records = await GetAllBestRecordsAsync(mapName, mode);
        for (var index = 0; index < records.Count; index++)
        {
            if (string.Equals(records[index].SteamId, steamId, StringComparison.Ordinal))
            {
                return new PlayerRank(index + 1, records.Count, records[index].TimerTicks);
            }
        }

        return null;
    }

    public async Task<TimePlacement> GetPlacementForTimeAsync(string mapName, string steamId, int timerTicks)
    {
        return await GetPlacementForTimeAsync(mapName, steamId, timerTicks, TimerRunMode.Standard);
    }

    public async Task<TimePlacement> GetPlacementForTimeAsync(string mapName, string steamId, int timerTicks, TimerRunMode mode)
    {
        var records = await GetAllBestRecordsAsync(mapName, mode);
        var fasterCount = records.Count(record =>
            !string.Equals(record.SteamId, steamId, StringComparison.Ordinal) &&
            record.TimerTicks < timerTicks);
        var total = records.Any(record => string.Equals(record.SteamId, steamId, StringComparison.Ordinal))
            ? records.Count
            : records.Count + 1;

        return new TimePlacement(fasterCount + 1, total);
    }

    public Task SaveIfPersonalBestAsync(string mapName, string steamId, string playerName, int timerTicks)
    {
        return SaveIfPersonalBestAsync(mapName, steamId, playerName, timerTicks, TimerRunMode.Standard);
    }

    public Task SaveIfPersonalBestAsync(string mapName, string steamId, string playerName, int timerTicks, TimerRunMode mode)
    {
        lock (_sync)
        {
            using var connection = OpenConnection();
            var dialect = SqlDialect.FromConnection(connection);
            var runMode = TimerRunModes.ToStorageValue(mode);
            var sql = $"""
                INSERT INTO st_times (map_name, steam_id, player_name, timer_ticks, run_mode, created_at_utc)
                VALUES ({dialect.Parameter("mapName")}, {dialect.Parameter("steamId")}, {dialect.Parameter("playerName")}, {dialect.Parameter("timerTicks")}, {dialect.Parameter("runMode")}, {dialect.Parameter("createdAtUtc")});
                """;
            connection.Execute(sql, new
            {
                mapName,
                steamId,
                playerName,
                timerTicks,
                runMode,
                createdAtUtc = DateTime.UtcNow
            });
        }

        return Task.CompletedTask;
    }

    public Task<DeletedRecord?> DeleteBestRecordByRankAsync(string mapName, int rank)
    {
        return DeleteBestRecordByRankAsync(mapName, rank, TimerRunMode.Standard);
    }

    public Task<DeletedRecord?> DeleteBestRecordByRankAsync(string mapName, int rank, TimerRunMode mode)
    {
        if (rank <= 0)
        {
            return Task.FromResult<DeletedRecord?>(null);
        }

        lock (_sync)
        {
            var records = GetTopRecordsAsync(mapName, int.MaxValue, mode).GetAwaiter().GetResult();
            if (rank > records.Count)
            {
                return Task.FromResult<DeletedRecord?>(null);
            }

            var target = records[rank - 1];
            using var connection = OpenConnection();
            var dialect = SqlDialect.FromConnection(connection);
            var runMode = TimerRunModes.ToStorageValue(mode);
            var idSql = $"""
                SELECT id AS Id
                FROM st_times
                WHERE map_name = {dialect.Parameter("mapName")}
                  AND run_mode = {dialect.Parameter("runMode")}
                  AND steam_id = {dialect.Parameter("steamId")}
                  AND timer_ticks = {dialect.Parameter("timerTicks")}
                ORDER BY id ASC
                LIMIT 1;
                """;
            var row = connection.QueryFirstOrDefault<RecordIdRow>(idSql, new
            {
                mapName,
                runMode,
                steamId = target.SteamId,
                timerTicks = target.TimerTicks
            });
            if (row is null)
            {
                return Task.FromResult<DeletedRecord?>(null);
            }

            var deleteSql = $"DELETE FROM st_times WHERE id = {dialect.Parameter("id")};";
            var affected = connection.Execute(deleteSql, new { row.Id });
            if (affected <= 0)
            {
                return Task.FromResult<DeletedRecord?>(null);
            }

            return Task.FromResult<DeletedRecord?>(new DeletedRecord(
                rank,
                records.Count,
                mapName,
                mode,
                target.SteamId,
                target.PlayerName,
                target.TimerTicks));
        }
    }

    private Task<IReadOnlyList<PlayerRecordEntry>> GetAllBestRecordsAsync(string mapName)
    {
        return GetAllBestRecordsAsync(mapName, TimerRunMode.Standard);
    }

    private Task<IReadOnlyList<PlayerRecordEntry>> GetAllBestRecordsAsync(string mapName, TimerRunMode mode)
    {
        return GetTopRecordsAsync(mapName, int.MaxValue, mode);
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

    private static void CreateSchema(IDbConnection connection, SqlDialect dialect)
    {
        connection.Execute(dialect.CreateMetaTableSql);
        connection.Execute(dialect.CreateTimesTableSql);
    }

    private static void CreateSupplementalIndexes(IDbConnection connection, SqlDialect dialect)
    {
        foreach (var sql in dialect.CreateSupplementalIndexSql())
        {
            connection.Execute(sql);
        }
    }

    private static void EnsureTimesColumns(IDbConnection connection, SqlDialect dialect)
    {
        try
        {
            var definition = dialect.Kind switch
            {
                SqlDialectKind.PostgreSql => "run_mode TEXT NOT NULL DEFAULT 'standard'",
                SqlDialectKind.SQLite => "run_mode TEXT NOT NULL DEFAULT 'standard'",
                _ => "run_mode VARCHAR(32) NOT NULL DEFAULT 'standard'"
            };
            connection.Execute($"ALTER TABLE st_times ADD COLUMN {definition};");
        }
        catch
        {
            // Existing installs may already have the column.
        }
    }

    private void ImportLegacyJsonRecordsIfNeeded(IDbConnection connection, SqlDialect dialect)
    {
        if (GetMetaValue(connection, dialect, "legacy_records_imported") == "1")
        {
            return;
        }

        if (!Directory.Exists(_legacyPlayerRecordsDirectory))
        {
            SetMetaValue(connection, dialect, "legacy_records_imported", "1");
            return;
        }

        using var transaction = connection.BeginTransaction();
        foreach (var filePath in Directory.EnumerateFiles(_legacyPlayerRecordsDirectory, "*.json"))
        {
            var mapName = Path.GetFileNameWithoutExtension(filePath);
            if (string.IsNullOrWhiteSpace(mapName))
            {
                continue;
            }

            Dictionary<string, PlayerRecord>? records;
            try
            {
                records = JsonSerializer.Deserialize<Dictionary<string, PlayerRecord>>(File.ReadAllText(filePath), JsonOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SwiftlyBhopTimer] Failed to import legacy records '{filePath}': {ex.Message}");
                continue;
            }

            if (records is null)
            {
                continue;
            }

            foreach (var (steamId, record) in records)
            {
                if (string.IsNullOrWhiteSpace(steamId) || record.TimerTicks <= 0)
                {
                    continue;
                }

                var sql = $"""
                    INSERT INTO st_times (map_name, steam_id, player_name, timer_ticks, run_mode, created_at_utc)
                    VALUES ({dialect.Parameter("mapName")}, {dialect.Parameter("steamId")}, {dialect.Parameter("playerName")}, {dialect.Parameter("timerTicks")}, {dialect.Parameter("runMode")}, {dialect.Parameter("createdAtUtc")});
                    """;
                connection.Execute(sql, new
                {
                    mapName,
                    steamId,
                    playerName = record.PlayerName ?? "",
                    timerTicks = record.TimerTicks,
                    runMode = TimerRunModes.StandardKey,
                    createdAtUtc = DateTime.UnixEpoch
                }, transaction);
            }
        }

        SetMetaValue(connection, dialect, "legacy_records_imported", "1", transaction);
        transaction.Commit();
    }

    internal static string? GetMetaValue(IDbConnection connection, SqlDialect dialect, string key)
    {
        var sql = $"SELECT value FROM st_meta WHERE meta_key = {dialect.Parameter("key")} LIMIT 1;";

        return connection.QueryFirstOrDefault<string>(sql, new { key });
    }

    internal static void SetMetaValue(IDbConnection connection, SqlDialect dialect, string key, string value, IDbTransaction? transaction = null)
    {
        connection.Execute(dialect.UpsertMetaSql, new { key, value }, transaction);
    }

    private sealed class PlayerRecordRow
    {
        public string SteamId { get; set; } = "";
        public string? PlayerName { get; set; }
        public int TimerTicks { get; set; }
    }

    private sealed class RecordIdRow
    {
        public long Id { get; set; }
    }
}

public sealed record DeletedRecord(int Rank, int Total, string MapName, TimerRunMode Mode, string SteamId, string? PlayerName, int TimerTicks);
