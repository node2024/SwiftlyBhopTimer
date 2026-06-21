using SwiftlyBhopTimer.Services;
using SwiftlyS2.Shared.Players;
using Microsoft.Extensions.Logging;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private void ApplyHideFpsWithRespawnRefresh(IPlayer player, PlayerTimerState state)
    {
        _replayService?.DiscardRecording(player);
        ResetTimerStateWithStopSound(player, state);
        state.StartSpeedLimitPauseTicks = RestartSpeedLimitPauseTicks;
        state.PendingRestartCheckpoint = null;
        state.PendingRestartPosition = null;
        state.PendingRestartTeleportTicks = 0;
        state.PendingSecondaryWeaponTicks = SecondaryWeaponGiveDelayTicks;

        _playerVisualService?.ApplyHideFps(player, state, refreshTicks: 0);

        try
        {
            player.Respawn();
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "Hide FPS refresh respawn failed for player {Player}.", player.Name);
        }
    }
}
