using System.Data;

namespace SwiftlyBhopTimer.Services;

internal enum SqlDialectKind
{
    MySql,
    PostgreSql,
    SQLite
}

internal sealed class SqlDialect
{
    private SqlDialect(SqlDialectKind kind)
    {
        Kind = kind;
    }

    public SqlDialectKind Kind { get; }

    public static SqlDialect FromConnection(IDbConnection connection)
    {
        var typeName = connection.GetType().FullName ?? connection.GetType().Name;
        if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return new SqlDialect(SqlDialectKind.PostgreSql);
        }

        if (typeName.Contains("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            return new SqlDialect(SqlDialectKind.SQLite);
        }

        return new SqlDialect(SqlDialectKind.MySql);
    }

    public string Parameter(string name)
    {
        return Kind == SqlDialectKind.PostgreSql ? $":{name}" : $"@{name}";
    }

    public string CreateMetaTableSql => Kind switch
    {
        SqlDialectKind.PostgreSql => """
            CREATE TABLE IF NOT EXISTS st_meta (
                meta_key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """,
        SqlDialectKind.SQLite => """
            CREATE TABLE IF NOT EXISTS st_meta (
                meta_key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """,
        _ => """
            CREATE TABLE IF NOT EXISTS st_meta (
                meta_key VARCHAR(191) PRIMARY KEY,
                value TEXT NOT NULL
            ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
            """
    };

    public string CreateTimesTableSql => Kind switch
    {
        SqlDialectKind.PostgreSql => """
            CREATE TABLE IF NOT EXISTS st_times (
                id BIGSERIAL PRIMARY KEY,
                map_name TEXT NOT NULL,
                steam_id TEXT NOT NULL,
                player_name TEXT NULL,
                timer_ticks INTEGER NOT NULL,
                run_mode TEXT NOT NULL DEFAULT 'standard',
                created_at_utc TIMESTAMPTZ NOT NULL
            );
            """,
        SqlDialectKind.SQLite => """
            CREATE TABLE IF NOT EXISTS st_times (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                map_name TEXT NOT NULL,
                steam_id TEXT NOT NULL,
                player_name TEXT NULL,
                timer_ticks INTEGER NOT NULL,
                run_mode TEXT NOT NULL DEFAULT 'standard',
                created_at_utc TEXT NOT NULL
            );
            """,
        _ => """
            CREATE TABLE IF NOT EXISTS st_times (
                id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                map_name VARCHAR(191) NOT NULL,
                steam_id VARCHAR(64) NOT NULL,
                player_name VARCHAR(191) NULL,
                timer_ticks INT NOT NULL,
                run_mode VARCHAR(32) NOT NULL DEFAULT 'standard',
                created_at_utc DATETIME(6) NOT NULL,
                INDEX idx_st_times_map_ticks (map_name, timer_ticks),
                INDEX idx_st_times_map_steam (map_name, steam_id),
                INDEX idx_st_times_map_mode_ticks (map_name, run_mode, timer_ticks),
                INDEX idx_st_times_map_mode_steam (map_name, run_mode, steam_id)
            ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
            """
    };

    public string CreateSettingsTableSql => Kind switch
    {
        SqlDialectKind.PostgreSql => """
            CREATE TABLE IF NOT EXISTS st_settings (
                steam_id TEXT PRIMARY KEY,
                player_name TEXT NULL,
                hide_timer_hud BOOLEAN NOT NULL DEFAULT FALSE,
                hide_legs BOOLEAN NOT NULL DEFAULT FALSE,
                hide_players BOOLEAN NOT NULL DEFAULT FALSE,
                hide_fps_viewmodel BOOLEAN NOT NULL DEFAULT FALSE,
                player_fov INTEGER NULL,
                timer_mode TEXT NOT NULL DEFAULT 'standard',
                sounds_enabled BOOLEAN NOT NULL DEFAULT TRUE,
                updated_at_utc TIMESTAMPTZ NOT NULL
            );
            """,
        SqlDialectKind.SQLite => """
            CREATE TABLE IF NOT EXISTS st_settings (
                steam_id TEXT PRIMARY KEY,
                player_name TEXT NULL,
                hide_timer_hud INTEGER NOT NULL DEFAULT 0,
                hide_legs INTEGER NOT NULL DEFAULT 0,
                hide_players INTEGER NOT NULL DEFAULT 0,
                hide_fps_viewmodel INTEGER NOT NULL DEFAULT 0,
                player_fov INTEGER NULL,
                timer_mode TEXT NOT NULL DEFAULT 'standard',
                sounds_enabled INTEGER NOT NULL DEFAULT 1,
                updated_at_utc TEXT NOT NULL
            );
            """,
        _ => """
            CREATE TABLE IF NOT EXISTS st_settings (
                steam_id VARCHAR(64) PRIMARY KEY,
                player_name VARCHAR(191) NULL,
                hide_timer_hud TINYINT(1) NOT NULL DEFAULT 0,
                hide_legs TINYINT(1) NOT NULL DEFAULT 0,
                hide_players TINYINT(1) NOT NULL DEFAULT 0,
                hide_fps_viewmodel TINYINT(1) NOT NULL DEFAULT 0,
                player_fov INT NULL,
                timer_mode VARCHAR(32) NOT NULL DEFAULT 'standard',
                sounds_enabled TINYINT(1) NOT NULL DEFAULT 1,
                updated_at_utc DATETIME(6) NOT NULL
            ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
            """
    };

    public string CreateReplaysTableSql => Kind switch
    {
        SqlDialectKind.PostgreSql => """
            CREATE TABLE IF NOT EXISTS st_replays (
                map_name TEXT PRIMARY KEY,
                steam_id TEXT NOT NULL,
                player_name TEXT NULL,
                timer_ticks INTEGER NOT NULL,
                frame_count INTEGER NOT NULL,
                frames_json TEXT NOT NULL,
                updated_at_utc TIMESTAMPTZ NOT NULL
            );
            """,
        SqlDialectKind.SQLite => """
            CREATE TABLE IF NOT EXISTS st_replays (
                map_name TEXT PRIMARY KEY,
                steam_id TEXT NOT NULL,
                player_name TEXT NULL,
                timer_ticks INTEGER NOT NULL,
                frame_count INTEGER NOT NULL,
                frames_json TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL
            );
            """,
        _ => """
            CREATE TABLE IF NOT EXISTS st_replays (
                map_name VARCHAR(191) PRIMARY KEY,
                steam_id VARCHAR(64) NOT NULL,
                player_name VARCHAR(191) NULL,
                timer_ticks INT NOT NULL,
                frame_count INT NOT NULL,
                frames_json LONGTEXT NOT NULL,
                updated_at_utc DATETIME(6) NOT NULL
            ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
            """
    };

    public string CreatePersonalBestReplaysTableSql => Kind switch
    {
        SqlDialectKind.PostgreSql => """
            CREATE TABLE IF NOT EXISTS st_pb_replays (
                map_name TEXT NOT NULL,
                steam_id TEXT NOT NULL,
                player_name TEXT NULL,
                timer_ticks INTEGER NOT NULL,
                frame_count INTEGER NOT NULL,
                frames_json TEXT NOT NULL,
                updated_at_utc TIMESTAMPTZ NOT NULL,
                PRIMARY KEY (map_name, steam_id)
            );
            """,
        SqlDialectKind.SQLite => """
            CREATE TABLE IF NOT EXISTS st_pb_replays (
                map_name TEXT NOT NULL,
                steam_id TEXT NOT NULL,
                player_name TEXT NULL,
                timer_ticks INTEGER NOT NULL,
                frame_count INTEGER NOT NULL,
                frames_json TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL,
                PRIMARY KEY (map_name, steam_id)
            );
            """,
        _ => """
            CREATE TABLE IF NOT EXISTS st_pb_replays (
                map_name VARCHAR(191) NOT NULL,
                steam_id VARCHAR(64) NOT NULL,
                player_name VARCHAR(191) NULL,
                timer_ticks INT NOT NULL,
                frame_count INT NOT NULL,
                frames_json LONGTEXT NOT NULL,
                updated_at_utc DATETIME(6) NOT NULL,
                PRIMARY KEY (map_name, steam_id)
            ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
            """
    };

    public string UpsertMetaSql => Kind switch
    {
        SqlDialectKind.PostgreSql => """
            INSERT INTO st_meta (meta_key, value)
            VALUES (:key, :value)
            ON CONFLICT(meta_key) DO UPDATE SET value = excluded.value;
            """,
        SqlDialectKind.SQLite => """
            INSERT INTO st_meta (meta_key, value)
            VALUES (@key, @value)
            ON CONFLICT(meta_key) DO UPDATE SET value = excluded.value;
            """,
        _ => """
            INSERT INTO st_meta (meta_key, value)
            VALUES (@key, @value)
            ON DUPLICATE KEY UPDATE value = VALUES(value);
            """
    };

    public string UpsertSettingsSql => Kind switch
    {
        SqlDialectKind.PostgreSql => """
            INSERT INTO st_settings (
                steam_id,
                player_name,
                hide_timer_hud,
                hide_legs,
                hide_players,
                hide_fps_viewmodel,
                player_fov,
                timer_mode,
                sounds_enabled,
                updated_at_utc
            )
            VALUES (
                :steamId,
                :playerName,
                :hideTimerHud,
                :hideLegs,
                :hidePlayers,
                :hideFpsViewModel,
                :playerFov,
                :timerMode,
                :soundsEnabled,
                :updatedAtUtc
            )
            ON CONFLICT(steam_id) DO UPDATE SET
                player_name = excluded.player_name,
                hide_timer_hud = excluded.hide_timer_hud,
                hide_legs = excluded.hide_legs,
                hide_players = excluded.hide_players,
                hide_fps_viewmodel = excluded.hide_fps_viewmodel,
                player_fov = excluded.player_fov,
                timer_mode = excluded.timer_mode,
                sounds_enabled = excluded.sounds_enabled,
                updated_at_utc = excluded.updated_at_utc;
            """,
        SqlDialectKind.SQLite => """
            INSERT INTO st_settings (
                steam_id,
                player_name,
                hide_timer_hud,
                hide_legs,
                hide_players,
                hide_fps_viewmodel,
                player_fov,
                timer_mode,
                sounds_enabled,
                updated_at_utc
            )
            VALUES (
                @steamId,
                @playerName,
                @hideTimerHud,
                @hideLegs,
                @hidePlayers,
                @hideFpsViewModel,
                @playerFov,
                @timerMode,
                @soundsEnabled,
                @updatedAtUtc
            )
            ON CONFLICT(steam_id) DO UPDATE SET
                player_name = excluded.player_name,
                hide_timer_hud = excluded.hide_timer_hud,
                hide_legs = excluded.hide_legs,
                hide_players = excluded.hide_players,
                hide_fps_viewmodel = excluded.hide_fps_viewmodel,
                player_fov = excluded.player_fov,
                timer_mode = excluded.timer_mode,
                sounds_enabled = excluded.sounds_enabled,
                updated_at_utc = excluded.updated_at_utc;
            """,
        _ => """
            INSERT INTO st_settings (
                steam_id,
                player_name,
                hide_timer_hud,
                hide_legs,
                hide_players,
                hide_fps_viewmodel,
                player_fov,
                timer_mode,
                sounds_enabled,
                updated_at_utc
            )
            VALUES (
                @steamId,
                @playerName,
                @hideTimerHud,
                @hideLegs,
                @hidePlayers,
                @hideFpsViewModel,
                @playerFov,
                @timerMode,
                @soundsEnabled,
                @updatedAtUtc
            )
            ON DUPLICATE KEY UPDATE
                player_name = VALUES(player_name),
                hide_timer_hud = VALUES(hide_timer_hud),
                hide_legs = VALUES(hide_legs),
                hide_players = VALUES(hide_players),
                hide_fps_viewmodel = VALUES(hide_fps_viewmodel),
                player_fov = VALUES(player_fov),
                timer_mode = VALUES(timer_mode),
                sounds_enabled = VALUES(sounds_enabled),
                updated_at_utc = VALUES(updated_at_utc);
            """
    };

    public string UpsertReplaySql => Kind switch
    {
        SqlDialectKind.PostgreSql => """
            INSERT INTO st_replays (
                map_name,
                steam_id,
                player_name,
                timer_ticks,
                frame_count,
                frames_json,
                updated_at_utc
            )
            VALUES (
                :mapName,
                :steamId,
                :playerName,
                :timerTicks,
                :frameCount,
                :framesJson,
                :updatedAtUtc
            )
            ON CONFLICT(map_name) DO UPDATE SET
                steam_id = excluded.steam_id,
                player_name = excluded.player_name,
                timer_ticks = excluded.timer_ticks,
                frame_count = excluded.frame_count,
                frames_json = excluded.frames_json,
                updated_at_utc = excluded.updated_at_utc;
            """,
        SqlDialectKind.SQLite => """
            INSERT INTO st_replays (
                map_name,
                steam_id,
                player_name,
                timer_ticks,
                frame_count,
                frames_json,
                updated_at_utc
            )
            VALUES (
                @mapName,
                @steamId,
                @playerName,
                @timerTicks,
                @frameCount,
                @framesJson,
                @updatedAtUtc
            )
            ON CONFLICT(map_name) DO UPDATE SET
                steam_id = excluded.steam_id,
                player_name = excluded.player_name,
                timer_ticks = excluded.timer_ticks,
                frame_count = excluded.frame_count,
                frames_json = excluded.frames_json,
                updated_at_utc = excluded.updated_at_utc;
            """,
        _ => """
            INSERT INTO st_replays (
                map_name,
                steam_id,
                player_name,
                timer_ticks,
                frame_count,
                frames_json,
                updated_at_utc
            )
            VALUES (
                @mapName,
                @steamId,
                @playerName,
                @timerTicks,
                @frameCount,
                @framesJson,
                @updatedAtUtc
            )
            ON DUPLICATE KEY UPDATE
                steam_id = VALUES(steam_id),
                player_name = VALUES(player_name),
                timer_ticks = VALUES(timer_ticks),
                frame_count = VALUES(frame_count),
                frames_json = VALUES(frames_json),
                updated_at_utc = VALUES(updated_at_utc);
            """
    };

    public string UpsertPersonalBestReplaySql => Kind switch
    {
        SqlDialectKind.PostgreSql => """
            INSERT INTO st_pb_replays (
                map_name,
                steam_id,
                player_name,
                timer_ticks,
                frame_count,
                frames_json,
                updated_at_utc
            )
            VALUES (
                :mapName,
                :steamId,
                :playerName,
                :timerTicks,
                :frameCount,
                :framesJson,
                :updatedAtUtc
            )
            ON CONFLICT(map_name, steam_id) DO UPDATE SET
                player_name = excluded.player_name,
                timer_ticks = excluded.timer_ticks,
                frame_count = excluded.frame_count,
                frames_json = excluded.frames_json,
                updated_at_utc = excluded.updated_at_utc;
            """,
        SqlDialectKind.SQLite => """
            INSERT INTO st_pb_replays (
                map_name,
                steam_id,
                player_name,
                timer_ticks,
                frame_count,
                frames_json,
                updated_at_utc
            )
            VALUES (
                @mapName,
                @steamId,
                @playerName,
                @timerTicks,
                @frameCount,
                @framesJson,
                @updatedAtUtc
            )
            ON CONFLICT(map_name, steam_id) DO UPDATE SET
                player_name = excluded.player_name,
                timer_ticks = excluded.timer_ticks,
                frame_count = excluded.frame_count,
                frames_json = excluded.frames_json,
                updated_at_utc = excluded.updated_at_utc;
            """,
        _ => """
            INSERT INTO st_pb_replays (
                map_name,
                steam_id,
                player_name,
                timer_ticks,
                frame_count,
                frames_json,
                updated_at_utc
            )
            VALUES (
                @mapName,
                @steamId,
                @playerName,
                @timerTicks,
                @frameCount,
                @framesJson,
                @updatedAtUtc
            )
            ON DUPLICATE KEY UPDATE
                player_name = VALUES(player_name),
                timer_ticks = VALUES(timer_ticks),
                frame_count = VALUES(frame_count),
                frames_json = VALUES(frames_json),
                updated_at_utc = VALUES(updated_at_utc);
            """
    };

    public IEnumerable<string> CreateSupplementalIndexSql()
    {
        if (Kind == SqlDialectKind.MySql)
        {
            yield break;
        }

        yield return "CREATE INDEX IF NOT EXISTS idx_st_times_map_ticks ON st_times (map_name, timer_ticks);";
        yield return "CREATE INDEX IF NOT EXISTS idx_st_times_map_steam ON st_times (map_name, steam_id);";
        yield return "CREATE INDEX IF NOT EXISTS idx_st_times_map_mode_ticks ON st_times (map_name, run_mode, timer_ticks);";
        yield return "CREATE INDEX IF NOT EXISTS idx_st_times_map_mode_steam ON st_times (map_name, run_mode, steam_id);";
    }
}
