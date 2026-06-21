using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyBhopTimer.Services;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SwiftlyBhopTimer;

[PluginMetadata(Id = "SwiftlyBhopTimer", Version = "0.1.0", Name = "SwiftlyBhopTimer", Author = "SwiftlyBhopTimer contributors", Description = "SwiftlyS2 bhop timer.")]
public sealed partial class SwiftlyBhopTimer : BasePlugin
{
    private const string PluginVersion = "0.1.0";
    private const string DatabaseConnectionName = "SwiftlyBhopTimer";
    private const int HudUpdateIntervalTicks = 1;
    private const int HudHtmlDurationMs = 0;
    private const int HudClearDurationMs = 1;
    private const int ZoneSetupPreviewUpdateIntervalTicks = 8;
    private const int ZoneRenderDelayTicks = 20;
    private const int ZoneRenderHealthCheckTicks = 320;
    private const int MapRecoveryCheckTicks = 128;
    private const int DelayedMapExecInitialDelayTicks = 128;
    private const int DelayedMapExecIntervalTicks = 128;
    private const int DelayedMapExecPasses = 3;
    private const int PostRoundResetConfigInitialDelayTicks = 80;
    private const int PostRoundResetConfigIntervalTicks = 64;
    private const int PostRoundResetConfigPasses = 5;
    private const int ReplayBotAddRetryTicks = 96;
    private const int ReplayBotConvarRefreshTicks = 128;
    private const int ReplayBotQuota = 5;
    private const int ServerReplayBotCount = 2;
    private const int MaxAdditionalReplayBots = ReplayBotQuota - ServerReplayBotCount;
    private const int RestartCommandCooldownTicks = 8;
    private const int AdminZoneSaveCooldownTicks = 8;
    private const int AdminBonusSelectionTimeoutTicks = 1920;
    private const int RestartSpeedLimitPauseTicks = 32;
    private const int RestartTeleportAfterRespawnTicks = 2;
    private const int TeamChangeTeleportAfterSpawnTicks = 2;
    private const int SecondaryWeaponGiveDelayTicks = 6;
    private const string DefaultSecondaryWeapon = "weapon_usp_silencer";
    private const int MinPlayerFov = 85;
    private const int MaxPlayerFov = 130;
    private const float StartZoneSpeedLimit = 320.0f;
    private const float ClassicStartZoneSpeedLimit = 260.0f;
    private static readonly string[] SharpTimerCompatibilityCommands =
    [
        "sharptimer_ad_enabled",
        "sharptimer_ad_timer",
        "sharptimer_autoset_mapinfo_hostname_enabled",
        "sharptimer_chat_prefix",
        "sharptimer_checkpoints_enabled",
        "sharptimer_checkpoints_only_when_timer_stopped",
        "sharptimer_command_spam_cooldown",
        "sharptimer_connect_commands_msg_enabled",
        "sharptimer_connectmsg_enabled",
        "sharptimer_custom_map_cfgs_enabled",
        "sharptimer_debug_enabled",
        "sharptimer_disable_telehop",
        "sharptimer_discordwebhook_enabled",
        "sharptimer_discordwebhook_print_pb",
        "sharptimer_discordwebhook_print_sr",
        "sharptimer_display_rank_tags_chat",
        "sharptimer_display_rank_tags_scoreboard",
        "sharptimer_enable_keys_hud",
        "sharptimer_enable_timer_hud",
        "sharptimer_end_beam_color",
        "sharptimer_end_enabled",
        "sharptimer_fake_trigger_height",
        "sharptimer_force_disable_json",
        "sharptimer_force_knife_speed",
        "sharptimer_forced_player_speed",
        "sharptimer_global_rank_free_points_enabled",
        "sharptimer_global_rank_max_free_rewards",
        "sharptimer_global_rank_min_points_threshold",
        "sharptimer_global_rank_points_enabled",
        "sharptimer_goto_enabled",
        "sharptimer_help_enabled",
        "sharptimer_hide_all_players",
        "sharptimer_hostname",
        "sharptimer_hud_primary_color",
        "sharptimer_hud_secondary_color",
        "sharptimer_hud_tertiary_color",
        "sharptimer_jumpstats_enabled",
        "sharptimer_jumpstats_max_vert",
        "sharptimer_jumpstats_min_distance",
        "sharptimer_jumpstats_movement_unlocker_cap",
        "sharptimer_jumpstats_movement_unlocker_cap_value",
        "sharptimer_kill_pointservercommand_entities",
        "sharptimer_max_bhop_block_time",
        "sharptimer_max_start_speed",
        "sharptimer_max_start_speed_enabled",
        "sharptimer_mysql_enabled",
        "sharptimer_override_beam_colors_enabled",
        "sharptimer_rank_enabled",
        "sharptimer_remote_data_bhop",
        "sharptimer_remote_data_kz",
        "sharptimer_remote_data_surf",
        "sharptimer_remove_checkpoints_restrictions",
        "sharptimer_remove_collision",
        "sharptimer_remove_crouch_fatigue",
        "sharptimer_remove_damage",
        "sharptimer_remove_damage_use_alt",
        "sharptimer_remove_legs",
        "sharptimer_replay_loop_bot_enabled",
        "sharptimer_replay_max_length",
        "sharptimer_replays_enabled",
        "sharptimer_respawn_enabled",
        "sharptimer_start_beam_color",
        "sharptimer_top_enabled",
        "sharptimer_trigger_push_fix",
        "sharptimer_use2Dspeed_enabled",
        "sharptimer_velo_bar_enabled",
        "sharptimer_velo_bar_max_speed",
        "sharptimer_vip_gif_host"
    ];
    private static readonly string[] AdminPermissions = ["swiftlybhoptimer.admin"];
    private static readonly Regex StageTriggerPattern = new(@"^(?:s|stage)([1-9][0-9]?)_start$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BonusStartTriggerPattern = new(@"^(?:(?:b|bonus)(?<bonus>[1-9][0-9]?)_start|timer_bonus(?<timerbonus>[1-9][0-9]?)_startzone)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BonusEndTriggerPattern = new(@"^(?:(?:b|bonus)(?<bonus>[1-9][0-9]?)_end|timer_bonus(?<timerbonus>[1-9][0-9]?)_endzone)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StatusMapPattern = new(@"^\s*map\s*:\s*(?<map>\S+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly List<Guid> _commandRegistrations = [];
    private readonly List<Guid> _gameEventRegistrations = [];
    private readonly Dictionary<int, ZoneSetupPreview> _zoneSetupPreviews = [];
    private readonly Dictionary<string, ActiveMapInfo> _knownGoodActiveMaps = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimerStateStore _timerStateStore = new();
    private DbRecordStore? _recordStore;
    private MapDataService? _mapDataService;
    private MapExecService? _mapExecService;
    private ZoneRenderService? _zoneRenderService;
    private RoundFlowConfigService? _roundFlowConfigService;
    private ChatFormatService? _chatFormatService;
    private AdvertisingService? _advertisingService;
    private TimerSoundService? _timerSoundService;
    private PlayerProtectionService? _playerProtectionService;
    private PlayerVisualService? _playerVisualService;
    private PlayerWeaponService? _playerWeaponService;
    private PlayerSettingsService? _playerSettingsService;
    private ReplayService? _replayService;
    private NoclipService? _noclipService;
    private MovementBridgeService? _movementBridgeService;
    private MapChooserService? _mapChooserService;
    private EventDelegates.OnTick? _onTick;
    private Guid? _clientCommandHookRegistration;
    private string _currentMapName = "unknown";
    private ActiveMapInfo _activeMap = ActiveMapInfo.Default("unknown");
    private ActiveMapInfo? _lastKnownGoodActiveMap;
    private bool _zoneRenderPending;
    private int _zoneRenderTicks;
    private int _zoneRenderDelayTicks = ZoneRenderDelayTicks;
    private int _zoneRenderHealthTicks;
    private int _mapRecoveryTicks;
    private string _zoneRenderReason = "initial";
    private bool _htmlHudFlashFixUnavailableLogged;
    private bool _mapRecoveryWarningLogged;
    private bool _replayPlaybackWarningLogged;
    private int _delayedMapExecTicks;
    private int _delayedMapExecPassesRemaining;
    private bool _delayedMapExecRoundResetPending;
    private bool _delayedMapExecIncludeTimeLimit = true;
    private int _postRoundResetConfigTicks;
    private int _postRoundResetConfigPassesRemaining;
    private bool _postRoundResetConfigIncludeTimeLimit = true;
    private int _replayBotAddRetryTicks;
    private int _replayBotAddAttempts;
    private int _replayBotConvarRefreshTicks;
    private readonly Dictionary<TimerRunMode, int> _lastServerReplayBotSlots = [];
    private int _additionalReplayBotSerial;
    private readonly Dictionary<string, PersonalBestReplayBotState> _activePersonalBestReplayBots = new(StringComparer.Ordinal);
    private bool _startZoneSpeedLimitEnabled = true;
    private float _startZoneSpeedLimit = StartZoneSpeedLimit;
    private bool _compatRespawnCommandEnabled = true;
    private bool _compatTopCommandEnabled = true;
    private bool _compatRankCommandEnabled = true;

    public SwiftlyBhopTimer(ISwiftlyCore core) : base(core)
    {
    }

    public override void Load(bool hotReload)
    {
        Console.WriteLine($"[SwiftlyBhopTimer] Load start. HotReload={hotReload}");

        var paths = SwiftlyBhopTimerPaths.FromCore(Core);

        SeedBundledMapData(paths);

        _recordStore = new DbRecordStore(() => Core.Database.GetConnection(DatabaseConnectionName), paths.PlayerRecordsDirectory);
        _mapDataService = new MapDataService(paths.MapDataDirectory, paths.BundledMapDataDirectory);
        _mapExecService = new MapExecService(Core, paths.MapDataDirectory);
        _zoneRenderService = new ZoneRenderService(Core);
        _roundFlowConfigService = new RoundFlowConfigService(Core, paths.GeneratedConfigPath);
        _chatFormatService = new ChatFormatService(paths.ChatConfigPath);
        _advertisingService = new AdvertisingService(paths.AdvertisingConfigPath);
        _timerSoundService = new TimerSoundService();
        _playerProtectionService = new PlayerProtectionService();
        _playerVisualService = new PlayerVisualService(Core);
        _playerWeaponService = new PlayerWeaponService();
        _playerSettingsService = new PlayerSettingsService(() => Core.Database.GetConnection(DatabaseConnectionName), paths.LegacyPlayerSettingsPath);
        _replayService = new ReplayService(() => Core.Database.GetConnection(DatabaseConnectionName));
        _noclipService = new NoclipService();
        _movementBridgeService = new MovementBridgeService(Core);
        _mapChooserService = new MapChooserService(paths.MapChooserConfigPath, paths.MapChooserMapListPath, paths.MapDataDirectory, paths.BundledMapChooserMapsPath);
        _chatFormatService.EnsureConfigAndLoad();
        _advertisingService.EnsureConfigAndLoad();
        _recordStore.EnsureInitialized();
        _playerSettingsService.EnsureInitialized();
        _replayService.EnsureInitialized();
        _mapChooserService.EnsureInitialized();
        _roundFlowConfigService.WriteConfig();
        Core.Scheduler.NextTick(() => _roundFlowConfigService?.ApplyFull(includeTimeLimit: false));
        Core.Scheduler.NextTick(() => ApplyBhopHelperDefaults("plugin_load"));
        Core.Scheduler.NextTick(RecoverMapFromEngineStatusIfNeeded);

        RegisterCommands();
        _clientCommandHookRegistration = Core.Command.HookClientCommand(OnClientCommand);

        _onTick = OnTick;
        _onClientPutInServer = OnClientPutInServer;
        _onClientDisconnected = OnClientDisconnected;
        _onMapLoad = OnMapLoad;
        _onEntityTakeDamage = OnEntityTakeDamage;
        _onEntityStartTouch = OnEntityStartTouch;
        _onEntityEndTouch = OnEntityEndTouch;

        Core.Event.OnTick += _onTick;
        Core.Event.OnClientPutInServer += _onClientPutInServer;
        Core.Event.OnClientDisconnected += _onClientDisconnected;
        Core.Event.OnMapLoad += _onMapLoad;
        Core.Event.OnEntityTakeDamage += _onEntityTakeDamage;
        Core.Event.OnEntityStartTouch += _onEntityStartTouch;
        Core.Event.OnEntityEndTouch += _onEntityEndTouch;

        RegisterGameEventHooks();

        Core.Logger.LogInformation(
            "SwiftlyBhopTimer {Version} loaded. HotReload={HotReload}; Data={DataDirectory}",
            PluginVersion,
            hotReload,
            paths.DataDirectory);
        Console.WriteLine($"[SwiftlyBhopTimer] Load complete. Commands={_commandRegistrations.Count}; Data={paths.DataDirectory}; Cfg={paths.GeneratedConfigPath}; ChatCfg={paths.ChatConfigPath}; AdsCfg={paths.AdvertisingConfigPath}; DBConnection={DatabaseConnectionName}; MapData={_mapDataService.GetDiagnostics()}");
    }

    public override void Unload()
    {
        if (_onTick is not null)
        {
            Core.Event.OnTick -= _onTick;
        }

        if (_onClientPutInServer is not null)
        {
            Core.Event.OnClientPutInServer -= _onClientPutInServer;
        }

        if (_onClientDisconnected is not null)
        {
            Core.Event.OnClientDisconnected -= _onClientDisconnected;
        }

        if (_onMapLoad is not null)
        {
            Core.Event.OnMapLoad -= _onMapLoad;
        }

        if (_onEntityTakeDamage is not null)
        {
            Core.Event.OnEntityTakeDamage -= _onEntityTakeDamage;
        }

        if (_onEntityStartTouch is not null)
        {
            Core.Event.OnEntityStartTouch -= _onEntityStartTouch;
        }

        if (_onEntityEndTouch is not null)
        {
            Core.Event.OnEntityEndTouch -= _onEntityEndTouch;
        }

        foreach (var registration in _commandRegistrations)
        {
            Core.Command.UnregisterCommand(registration);
        }

        if (_clientCommandHookRegistration.HasValue)
        {
            Core.Command.UnhookClientCommand(_clientCommandHookRegistration.Value);
        }

        foreach (var registration in _gameEventRegistrations)
        {
            Core.GameEvent.Unhook(registration);
        }

        _commandRegistrations.Clear();
        _gameEventRegistrations.Clear();
        _timerStateStore.Clear();
        _zoneSetupPreviews.Clear();
        _zoneRenderService?.Clear();
        _zoneRenderService?.ClearPreviews();
        _roundFlowConfigService = null;
        _chatFormatService = null;
        _advertisingService = null;
        _timerSoundService = null;
        _playerProtectionService = null;
        _playerVisualService = null;
        _playerWeaponService = null;
        _playerSettingsService = null;
        _replayService = null;
        _mapExecService = null;
        _movementBridgeService = null;
        _mapChooserService = null;
        _onTick = null;
        _onClientPutInServer = null;
        _onClientDisconnected = null;
        _onMapLoad = null;
        _onEntityTakeDamage = null;
        _onEntityStartTouch = null;
        _onEntityEndTouch = null;
        _clientCommandHookRegistration = null;

        Core.Logger.LogInformation("SwiftlyBhopTimer unloaded.");
    }

    private static string green(string value) => $"{{green}}{value}{{default}}";

    private static string white(string value) => $"{{white}}{value}{{default}}";

    private static string gold(string value) => $"{{gold}}{value}{{default}}";

    private static string gray(string value) => $"{{gray}}{value}{{default}}";

    private static string blue(string value) => $"{{blue}}{value}{{default}}";

    private static string lightBlue(string value) => $"{{lightblue}}{value}{{default}}";

    private static string red(string value) => $"{{red}}{value}{{default}}";

    private static string magenta(string value) => $"{{magenta}}{value}{{default}}";

    private static string lightGreen(string value) => $"{{lightgreen}}{value}{{default}}";

    private static string label(string value) => gray(value);

    private static string topPlacement(int placement)
    {
        return placement switch
        {
            1 => magenta("#1"),
            2 => lightGreen("#2"),
            3 => lightBlue("#3"),
            _ => gray($"#{placement}")
        };
    }

    private void OnTick()
    {
        _roundFlowConfigService?.ApplyMaintainedIfDue();
        RecoverMapDataIfDue();
        ProcessDelayedMapExec();
        ProcessPostRoundResetConfig();
        ProcessDelayedZoneRendering();
        ProcessZoneRenderHealthCheck();
        ProcessZoneSetupPreviews();
        ProcessReplayBotConvarRefresh();
        EnsureReplayBot();
        ProcessReplayBotPlayback();
        ApplyHtmlHudFlashFix();

        var players = Core.PlayerManager.GetAllValidPlayers().Where(IsPlayablePlayer).ToList();
        ProcessAdvertising(players);
        ProcessMapChooser(players);
        var hideTargets = GetHideTargetPlayers(players);
        foreach (var player in players)
        {
            var state = _timerStateStore.GetOrCreate(player.Slot, player.SteamID.ToString(), player.Name);
            ApplyPlayerSettingsIfNeeded(player, state);
            TickPlayerCooldowns(state);
            SyncExternalNoclipState(player, state);
            if (!state.IsNoclipEnabled)
            {
                ProcessPendingRestartTeleport(player, state);
            }

            ProcessPendingSecondaryWeapon(player, state);
            ProcessPendingSpectateTarget(player, state);
            _playerProtectionService?.ApplyCollision(player);
            _playerVisualService?.ApplySelfVisuals(player, state);
            _playerVisualService?.ApplyPlayerHiding(player, state, hideTargets);
            ApplyPlayerFov(player, state);
            if (state.IsNoclipEnabled)
            {
                RenderHud(player, state);
                continue;
            }

            if (state.IsTimerPaused)
            {
                ApplyPauseFreeze(player, state);
                RenderHud(player, state);
                continue;
            }

            ApplyCoordinateZoneTimer(player, state);

            if (state.IsTimerRunning && !state.IsTimerPaused)
            {
                state.TimerTicks++;
                _replayService?.CaptureFrame(player, state.TimerTicks);
            }
            else if (state.IsBonusTimerRunning && !state.IsTimerPaused)
            {
                state.BonusTimerTicks++;
                _replayService?.CaptureFrame(player, state.BonusTimerTicks);
            }

            RenderHud(player, state);
        }
    }

}

public sealed record PersonalBestReplayBotState(
    string SteamId,
    string MapName,
    string BotName,
    string PlaybackKey,
    string HudLabel,
    ReplayData Replay,
    TimerRunMode Mode,
    bool KickAfterFirstLoop,
    bool UseServerReplay);
