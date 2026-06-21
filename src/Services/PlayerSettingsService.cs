using System.Data;
using System.Text.Json;
using Dapper;

namespace SwiftlyBhopTimer.Services;

public sealed class PlayerSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Func<IDbConnection> _connectionFactory;
    private readonly string _legacySettingsPath;
    private readonly object _sync = new();

    public PlayerSettingsService(Func<IDbConnection> connectionFactory, string legacySettingsPath)
    {
        _connectionFactory = connectionFactory;
        _legacySettingsPath = legacySettingsPath;
    }

    public void EnsureInitialized()
    {
        lock (_sync)
        {
            using var connection = OpenConnection();
            var dialect = SqlDialect.FromConnection(connection);
            connection.Execute(dialect.CreateMetaTableSql);
            connection.Execute(dialect.CreateSettingsTableSql);
            EnsureSettingsColumns(connection, dialect);
            ImportLegacyJsonSettingsIfNeeded(connection, dialect);
        }
    }

    public void Apply(string steamId, PlayerTimerState state)
    {
        if (string.IsNullOrWhiteSpace(steamId))
        {
            return;
        }

        lock (_sync)
        {
            using var connection = OpenConnection();
            var dialect = SqlDialect.FromConnection(connection);
            var sql = $"""
                SELECT
                    hide_timer_hud AS HideTimerHud,
                    hide_legs AS HideLegs,
                    hide_players AS HidePlayers,
                    hide_fps_viewmodel AS HideFpsViewModel,
                    player_fov AS PlayerFov,
                    timer_mode AS TimerMode,
                    sounds_enabled AS SoundsEnabled
                FROM st_settings
                WHERE steam_id = {dialect.Parameter("steamId")}
                LIMIT 1;
                """;
            var settings = connection.QueryFirstOrDefault<PlayerSettingsRow>(sql, new { steamId });
            if (settings is null)
            {
                return;
            }

            state.HideTimerHud = settings.HideTimerHud;
            state.HideLegs = settings.HideLegs;
            state.HidePlayers = settings.HidePlayers;
            state.HideFpsViewModel = settings.HideFpsViewModel;
            state.PlayerFov = settings.PlayerFov;
            state.TimerMode = TimerRunModes.ParseOrDefault(settings.TimerMode);
            state.SoundsEnabled = settings.SoundsEnabled;
        }
    }

    public void Save(PlayerTimerState state)
    {
        if (string.IsNullOrWhiteSpace(state.SteamId))
        {
            return;
        }

        lock (_sync)
        {
            using var connection = OpenConnection();
            var dialect = SqlDialect.FromConnection(connection);
            connection.Execute(dialect.UpsertSettingsSql, new
            {
                steamId = state.SteamId,
                playerName = state.PlayerName,
                hideTimerHud = state.HideTimerHud,
                hideLegs = state.HideLegs,
                hidePlayers = state.HidePlayers,
                hideFpsViewModel = state.HideFpsViewModel,
                playerFov = state.PlayerFov,
                timerMode = TimerRunModes.ToStorageValue(state.TimerMode),
                soundsEnabled = state.SoundsEnabled,
                updatedAtUtc = DateTime.UtcNow
            });
        }
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

    private static void EnsureSettingsColumns(IDbConnection connection, SqlDialect dialect)
    {
        foreach (var columnDefinition in GetSettingsColumnDefinitions(dialect))
        {
            try
            {
                connection.Execute($"ALTER TABLE st_settings ADD COLUMN {columnDefinition};");
            }
            catch
            {
                // Existing installs may already have the column. The next SELECT/UPSERT
                // will surface any real schema issue with a useful database error.
            }
        }
    }

    private static IReadOnlyList<string> GetSettingsColumnDefinitions(SqlDialect dialect)
    {
        return dialect.Kind switch
        {
            SqlDialectKind.PostgreSql =>
            [
                "player_name TEXT NULL",
                "hide_timer_hud BOOLEAN NOT NULL DEFAULT FALSE",
                "hide_legs BOOLEAN NOT NULL DEFAULT FALSE",
                "hide_players BOOLEAN NOT NULL DEFAULT FALSE",
                "hide_fps_viewmodel BOOLEAN NOT NULL DEFAULT FALSE",
                "player_fov INTEGER NULL",
                "timer_mode TEXT NOT NULL DEFAULT 'standard'",
                "sounds_enabled BOOLEAN NOT NULL DEFAULT TRUE",
                "updated_at_utc TIMESTAMPTZ NULL"
            ],
            SqlDialectKind.SQLite =>
            [
                "player_name TEXT NULL",
                "hide_timer_hud INTEGER NOT NULL DEFAULT 0",
                "hide_legs INTEGER NOT NULL DEFAULT 0",
                "hide_players INTEGER NOT NULL DEFAULT 0",
                "hide_fps_viewmodel INTEGER NOT NULL DEFAULT 0",
                "player_fov INTEGER NULL",
                "timer_mode TEXT NOT NULL DEFAULT 'standard'",
                "sounds_enabled INTEGER NOT NULL DEFAULT 1",
                "updated_at_utc TEXT NULL"
            ],
            _ =>
            [
                "player_name VARCHAR(191) NULL",
                "hide_timer_hud TINYINT(1) NOT NULL DEFAULT 0",
                "hide_legs TINYINT(1) NOT NULL DEFAULT 0",
                "hide_players TINYINT(1) NOT NULL DEFAULT 0",
                "hide_fps_viewmodel TINYINT(1) NOT NULL DEFAULT 0",
                "player_fov INT NULL",
                "timer_mode VARCHAR(32) NOT NULL DEFAULT 'standard'",
                "sounds_enabled TINYINT(1) NOT NULL DEFAULT 1",
                "updated_at_utc DATETIME(6) NULL"
            ]
        };
    }

    private void ImportLegacyJsonSettingsIfNeeded(IDbConnection connection, SqlDialect dialect)
    {
        if (DbRecordStore.GetMetaValue(connection, dialect, "legacy_settings_imported") == "1")
        {
            return;
        }

        if (!File.Exists(_legacySettingsPath))
        {
            DbRecordStore.SetMetaValue(connection, dialect, "legacy_settings_imported", "1");
            return;
        }

        Dictionary<string, LegacyPlayerSettingsRecord>? settings;
        try
        {
            settings = JsonSerializer.Deserialize<Dictionary<string, LegacyPlayerSettingsRecord>>(
                File.ReadAllText(_legacySettingsPath),
                JsonOptions);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to import legacy player settings '{_legacySettingsPath}': {ex.Message}");
            DbRecordStore.SetMetaValue(connection, dialect, "legacy_settings_imported", "1");
            return;
        }

        if (settings is null)
        {
            DbRecordStore.SetMetaValue(connection, dialect, "legacy_settings_imported", "1");
            return;
        }

        using var transaction = connection.BeginTransaction();
        foreach (var (steamId, setting) in settings)
        {
            if (string.IsNullOrWhiteSpace(steamId))
            {
                continue;
            }

            connection.Execute(dialect.UpsertSettingsSql, new
            {
                steamId,
                playerName = (string?)null,
                hideTimerHud = setting.HideTimerHud,
                hideLegs = setting.HideLegs,
                hidePlayers = setting.HidePlayers,
                hideFpsViewModel = setting.HideFpsViewModel,
                playerFov = setting.PlayerFov,
                timerMode = TimerRunModes.StandardKey,
                soundsEnabled = setting.SoundsEnabled,
                updatedAtUtc = DateTime.UtcNow
            }, transaction);
        }

        DbRecordStore.SetMetaValue(connection, dialect, "legacy_settings_imported", "1", transaction);
        transaction.Commit();
    }

    private sealed class PlayerSettingsRow
    {
        public bool HideTimerHud { get; set; }
        public bool HideLegs { get; set; }
        public bool HidePlayers { get; set; }
        public bool HideFpsViewModel { get; set; }
        public int? PlayerFov { get; set; }
        public string? TimerMode { get; set; }
        public bool SoundsEnabled { get; set; }
    }
}

internal sealed class LegacyPlayerSettingsRecord
{
    public bool HideTimerHud { get; set; }
    public bool HideLegs { get; set; }
    public bool HidePlayers { get; set; }
    public bool HideFpsViewModel { get; set; }
    public int? PlayerFov { get; set; }
    public string? TimerMode { get; set; }
    public bool SoundsEnabled { get; set; } = true;
}
