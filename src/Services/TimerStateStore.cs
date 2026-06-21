using SwiftlyBhopTimer.Models;

namespace SwiftlyBhopTimer.Services;

public sealed class TimerStateStore
{
    private readonly Dictionary<int, PlayerTimerState> _states = [];

    public PlayerTimerState GetOrCreate(int slot, string steamId, string playerName)
    {
        if (_states.TryGetValue(slot, out var state))
        {
            if (!string.Equals(state.SteamId, steamId, StringComparison.Ordinal))
            {
                state.SettingsLoaded = false;
            }

            state.SteamId = steamId;
            state.PlayerName = playerName;
            return state;
        }

        state = new PlayerTimerState
        {
            Slot = slot,
            SteamId = steamId,
            PlayerName = playerName,
            SoundsEnabled = true
        };

        _states[slot] = state;
        return state;
    }

    public void Remove(int slot)
    {
        _states.Remove(slot);
    }

    public IEnumerable<PlayerTimerState> All()
    {
        return _states.Values;
    }

    public void Clear()
    {
        _states.Clear();
    }
}

public sealed class PlayerTimerState
{
    public int Slot { get; init; }
    public string SteamId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public bool IsTimerRunning { get; set; }
    public bool IsTimerPaused { get; set; }
    public bool IsTimerBlocked { get; set; }
    public bool IsNoclipEnabled { get; set; }
    public int TimerTicks { get; set; }
    public int? LastFinishedTicks { get; set; }
    public bool IsBonusTimerRunning { get; set; }
    public int CurrentBonusNumber { get; set; }
    public int BonusTimerTicks { get; set; }
    public int? LastFinishedBonusTicks { get; set; }
    public bool WasInsideStartZone { get; set; }
    public bool WasInsideEndZone { get; set; }
    public HashSet<int> WasInsideBonusStartZones { get; } = [];
    public HashSet<int> WasInsideBonusEndZones { get; } = [];
    public bool DebugTouches { get; set; }
    public bool HideTimerHud { get; set; }
    public bool HideLegs { get; set; }
    public bool HidePlayers { get; set; }
    public bool HideFpsViewModel { get; set; }
    public int? PlayerFov { get; set; }
    public float? OriginalViewmodelOffsetX { get; set; }
    public float? OriginalViewmodelOffsetY { get; set; }
    public float? OriginalViewmodelOffsetZ { get; set; }
    public float? OriginalViewmodelFov { get; set; }
    public TimerRunMode TimerMode { get; set; } = TimerRunMode.Standard;
    public bool SoundsEnabled { get; set; }
    public bool SettingsLoaded { get; set; }
    public int HudTicks { get; set; }
    public int CurrentMapStage { get; set; }
    public int? CurrentStageTicks { get; set; }
    public int StartSpeedLimitPauseTicks { get; set; }
    public int RestartCommandCooldownTicks { get; set; }
    public int PendingRestartTeleportTicks { get; set; }
    public int PendingSecondaryWeaponTicks { get; set; }
    public int PendingSpectateTargetTicks { get; set; }
    public int AdminZoneSaveCooldownTicks { get; set; }
    public int PendingAdminBonusSelectionTicks { get; set; }
    public string? ActiveReplayTargetName { get; set; }
    public int? ActiveSpectateTargetSlot { get; set; }
    public int NoclipSyncGraceTicks { get; set; }
    public int NoclipReapplyTicks { get; set; }
    public int TeamSwitchSuppressTicks { get; set; }
    public int ActiveCheckpointSlot { get; set; } = 1;
    public PlayerCheckpoint?[] Checkpoints { get; } = new PlayerCheckpoint?[4];
    public string PersonalStartMapName { get; set; } = "";
    public PlayerCheckpoint? PersonalStartPosition { get; set; }
    public PlayerCheckpoint? PendingRestartCheckpoint { get; set; }
    public Vector3Value? PendingRestartPosition { get; set; }
    public string? PendingSpectateTargetName { get; set; }
    public int? PendingSpectateTargetSlot { get; set; }
    public Vector3Value? PauseFreezePosition { get; set; }
    public DateTime LastHudStatsRefreshUtc { get; set; } = DateTime.MinValue;
    public string CachedHudPb { get; set; } = "--";
    public string CachedHudRank { get; set; } = "[ --- / --- ]";
    public string CachedHudSr { get; set; } = "--";
    public string CachedHudRecordMapName { get; set; } = "";
}
