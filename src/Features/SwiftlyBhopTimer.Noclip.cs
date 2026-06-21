using SwiftlyS2.Shared.Players;
using SwiftlyBhopTimer.Services;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private void SyncExternalNoclipState(IPlayer player, PlayerTimerState state)
    {
        if (state.IsNoclipEnabled)
        {
            ReapplyNoclipIfDue(player, state);
        }

        if (state.NoclipSyncGraceTicks > 0)
        {
            state.NoclipSyncGraceTicks--;
            return;
        }

        var actualNoclip = _noclipService?.TryReadEnabled(player);
        if (!actualNoclip.HasValue || actualNoclip.Value == state.IsNoclipEnabled)
        {
            return;
        }

        if (actualNoclip.Value && !state.IsNoclipEnabled)
        {
            _noclipService?.Apply(player, enabled: false);
            ResetTimerStateWithStopSound(player, state);
            SendChat(player, $"{label("Noclip")} {label("|")} {red("blocked")} {gray("use chat command !noclip")}");
            return;
        }

        if (!actualNoclip.Value && state.IsNoclipEnabled)
        {
            return;
        }
    }

    private void ClearNoclipStateAfterPawnReset(IPlayer? player, string reason)
    {
        if (!IsPlayablePlayer(player))
        {
            return;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        if (!state.IsNoclipEnabled)
        {
            return;
        }

        var actualNoclip = _noclipService?.TryReadEnabled(player);
        if (actualNoclip == true)
        {
            return;
        }

        state.IsNoclipEnabled = false;
        state.NoclipSyncGraceTicks = 0;
        state.NoclipReapplyTicks = 0;
        ResetTimerStateWithStopSound(player, state);
        SendChat(player, $"{label("Noclip")} {label("|")} {red("OFF")} {gray(reason)}");
    }

    private void EnableNoclipTimerBlock(IPlayer player, PlayerTimerState state)
    {
        _replayService?.DiscardRecording(player);
        ResetTimerStateWithStopSound(player, state);
        ClearNoclipMovementInterlocks(state);
        state.IsNoclipEnabled = true;
        state.NoclipReapplyTicks = 0;
        ApplyNoclipMovementState(player, enabled: true);
    }

    private void DisableNoclipMovementState(IPlayer player, PlayerTimerState state)
    {
        state.IsNoclipEnabled = false;
        state.NoclipReapplyTicks = 0;
        ClearNoclipMovementInterlocks(state);
        ApplyNoclipHelper(player, enabled: false);
    }

    private static void ClearNoclipMovementInterlocks(PlayerTimerState state)
    {
        state.PendingRestartCheckpoint = null;
        state.PendingRestartPosition = null;
        state.PendingRestartTeleportTicks = 0;
        state.StartSpeedLimitPauseTicks = 0;
        state.PauseFreezePosition = null;
    }

    private void ReapplyNoclipIfDue(IPlayer player, PlayerTimerState state)
    {
        if (state.NoclipReapplyTicks > 0)
        {
            state.NoclipReapplyTicks--;
            return;
        }

        state.NoclipReapplyTicks = 8;
        ApplyNoclipMovementState(player, enabled: true);
    }

    private bool ApplyNoclipMovementState(IPlayer player, bool enabled)
    {
        if (enabled)
        {
            ExecuteServerCommand("sv_noclipspeed 5");
            ExecuteServerCommand("sv_noclipaccelerate 5");
        }

        ApplyNoclipHelper(player, enabled);
        return true;
    }

    private void ApplyNoclipHelper(IPlayer player, bool enabled)
    {
        ExecuteServerCommand($"sbt_noclip {player.Slot} {(enabled ? 1 : 0)}");
    }
}
