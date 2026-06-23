using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.GameEventDefinitions;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyBhopTimer.Models;
using SwiftlyBhopTimer.Services;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private void QueueRestartRespawn(IPlayer player, PlayerTimerState state, Vector3Value destination, bool applyCommandCooldown)
    {
        _replayService?.DiscardRecording(player);
        ResetTimerStateWithStopSound(player, state);
        state.StartSpeedLimitPauseTicks = RestartSpeedLimitPauseTicks;
        if (applyCommandCooldown)
        {
            state.RestartCommandCooldownTicks = RestartCommandCooldownTicks;
        }

        state.PendingSecondaryWeaponTicks = SecondaryWeaponGiveDelayTicks;
        _movementBridgeService?.QueueRestartTeleport(player, destination, RestartTeleportAfterRespawnTicks);
        player.Respawn();
    }

    private void QueueRestartRespawnToMapStart(IPlayer player, PlayerTimerState state, Vector3Value? fallbackDestination, bool applyCommandCooldown)
    {
        _replayService?.DiscardRecording(player);
        ResetTimerStateWithStopSound(player, state);
        state.StartSpeedLimitPauseTicks = RestartSpeedLimitPauseTicks;
        if (applyCommandCooldown)
        {
            state.RestartCommandCooldownTicks = RestartCommandCooldownTicks;
        }

        state.PendingSecondaryWeaponTicks = SecondaryWeaponGiveDelayTicks;
        var queued = _movementBridgeService?.QueueRestartTeleportToMapStart(player, fallbackDestination, RestartTeleportAfterRespawnTicks) == true;
        if (!queued && fallbackDestination is { } fallback)
        {
            _movementBridgeService?.QueueRestartTeleport(player, fallback, RestartTeleportAfterRespawnTicks);
        }

        player.Respawn();
    }

    private void QueueRestartRespawnToCheckpoint(IPlayer player, PlayerTimerState state, PlayerCheckpoint checkpoint, bool applyCommandCooldown)
    {
        _replayService?.DiscardRecording(player);
        ResetTimerStateWithStopSound(player, state);
        state.StartSpeedLimitPauseTicks = RestartSpeedLimitPauseTicks;
        if (applyCommandCooldown)
        {
            state.RestartCommandCooldownTicks = RestartCommandCooldownTicks;
        }

        state.PendingSecondaryWeaponTicks = SecondaryWeaponGiveDelayTicks;
        state.PendingRestartCheckpoint = checkpoint;
        state.PendingRestartPosition = null;
        state.PendingRestartTeleportTicks = RestartTeleportAfterRespawnTicks;
        player.Respawn();
    }

    private void ResetTimerStateWithStopSound(IPlayer player, PlayerTimerState state)
    {
        if (IsAnyTimerRunning(state))
        {
            _timerSoundService?.PlayStop(player, state.SoundsEnabled);
        }

        ResetTimerState(state);
    }

    private static void ApplyPlayerFov(IPlayer player, PlayerTimerState state)
    {
        if (!state.PlayerFov.HasValue)
        {
            return;
        }

        var controller = player.Controller;
        if (controller is null)
        {
            return;
        }

        var desiredFov = checked((uint)Math.Clamp(state.PlayerFov.Value, MinPlayerFov, MaxPlayerFov));
        if (controller.DesiredFOV == desiredFov)
        {
            return;
        }

        controller.DesiredFOV = desiredFov;
        controller.DesiredFOVUpdated();
    }

    private static void ResetTimerState(PlayerTimerState state)
    {
        state.IsTimerRunning = false;
        state.IsTimerPaused = false;
        state.TimerTicks = 0;
        state.LastFinishedTicks = null;
        state.WasInsideStartZone = false;
        state.WasInsideEndZone = false;
        state.CurrentMapStage = 0;
        state.CurrentStageTicks = null;
        state.CurrentMapCheckpoint = 0;
        state.CurrentCheckpointTicks = null;
        state.PauseFreezePosition = null;
        ResetBonusTimerState(state, clearLastFinished: true);
    }

    private static void ResetBonusTimerState(PlayerTimerState state, bool clearLastFinished)
    {
        state.IsBonusTimerRunning = false;
        state.CurrentBonusNumber = 0;
        state.BonusTimerTicks = 0;
        if (clearLastFinished)
        {
            state.LastFinishedBonusTicks = null;
        }

        state.WasInsideBonusStartZones.Clear();
        state.WasInsideBonusEndZones.Clear();
    }

    private static bool StartPauseFreeze(IPlayer player, PlayerTimerState state)
    {
        var pawn = player.PlayerPawn ?? player.Pawn;
        if (!EntityReflection.TryGetPosition(pawn, out var position))
        {
            return false;
        }

        state.PauseFreezePosition = position;
        player.Teleport(new Vector(position.X, position.Y, position.Z), null, Vector.Zero);
        return true;
    }

    private static void StopPauseFreeze(PlayerTimerState state)
    {
        state.PauseFreezePosition = null;
    }

    private static void ApplyPauseFreeze(IPlayer player, PlayerTimerState state)
    {
        if (state.PauseFreezePosition is null && !StartPauseFreeze(player, state))
        {
            player.Teleport(null, null, Vector.Zero);
            return;
        }

        if (state.PauseFreezePosition is not { } position)
        {
            return;
        }

        player.Teleport(new Vector(position.X, position.Y, position.Z), null, Vector.Zero);
    }

    private static void TeleportPlayer(IPlayer player, Vector3Value position)
    {
        var pawn = player.PlayerPawn ?? player.Pawn;
        var angle = EntityReflection.TryGetEyeAngles(pawn, out var eyeAngles)
            ? new QAngle { X = eyeAngles.X, Y = eyeAngles.Y, Z = eyeAngles.Z }
            : new QAngle();

        player.Teleport(new Vector(position.X, position.Y, position.Z), angle, Vector.Zero);
    }

    private void LimitStartZoneSpeed(IPlayer player, object? pawn, PlayerTimerState state)
    {
        if (!_startZoneSpeedLimitEnabled || state.StartSpeedLimitPauseTicks > 0)
        {
            return;
        }

        if (!EntityReflection.TryGetVelocity(pawn, out var velocity))
        {
            return;
        }

        if ((EntityReflection.TryIsOnGround(pawn, out var onGround) && !onGround) || MathF.Abs(velocity.Z) > 8.0f)
        {
            return;
        }

        var horizontalSpeed = MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
        var configuredLimit = TimerRunModes.Normalize(state.TimerMode) == TimerRunMode.Classic
            ? ClassicStartZoneSpeedLimit
            : _startZoneSpeedLimit;
        var speedLimit = Math.Max(1.0f, configuredLimit);
        if (horizontalSpeed <= speedLimit || horizontalSpeed <= 0.01f)
        {
            return;
        }

        var scale = speedLimit / horizontalSpeed;
        var limitedVelocity = new Vector(velocity.X * scale, velocity.Y * scale, velocity.Z);
        player.Teleport(null, null, limitedVelocity);
    }

    private static void TickPlayerCooldowns(PlayerTimerState state)
    {
        if (state.StartSpeedLimitPauseTicks > 0)
        {
            state.StartSpeedLimitPauseTicks--;
        }

        if (state.RestartCommandCooldownTicks > 0)
        {
            state.RestartCommandCooldownTicks--;
        }

        if (state.TeamSwitchSuppressTicks > 0)
        {
            state.TeamSwitchSuppressTicks--;
        }

        if (state.AdminZoneSaveCooldownTicks > 0)
        {
            state.AdminZoneSaveCooldownTicks--;
        }

        if (state.PendingAdminBonusSelectionTicks > 0)
        {
            state.PendingAdminBonusSelectionTicks--;
        }
    }

    private static void ProcessPendingRestartTeleport(IPlayer player, PlayerTimerState state)
    {
        if (state.PendingRestartCheckpoint is { } checkpoint)
        {
            if (state.PendingRestartTeleportTicks > 0)
            {
                state.PendingRestartTeleportTicks--;
                return;
            }

            if (TryTeleportPlayerToCheckpoint(player, checkpoint))
            {
                state.StartSpeedLimitPauseTicks = RestartSpeedLimitPauseTicks;
                state.PendingSecondaryWeaponTicks = Math.Max(state.PendingSecondaryWeaponTicks, SecondaryWeaponGiveDelayTicks);
            }

            state.PendingRestartCheckpoint = null;
            state.PendingRestartTeleportTicks = 0;
            return;
        }

        if (state.PendingRestartPosition is null)
        {
            return;
        }

        if (state.PendingRestartTeleportTicks > 0)
        {
            state.PendingRestartTeleportTicks--;
            return;
        }

        TeleportPlayer(player, state.PendingRestartPosition.Value);
        state.PendingRestartPosition = null;
        state.PendingRestartTeleportTicks = 0;
        state.StartSpeedLimitPauseTicks = RestartSpeedLimitPauseTicks;
        state.PendingSecondaryWeaponTicks = Math.Max(state.PendingSecondaryWeaponTicks, SecondaryWeaponGiveDelayTicks);
    }

    private static bool TryTeleportPlayerToCheckpoint(IPlayer player, PlayerCheckpoint checkpoint)
    {
        if (!Vector3Value.TryParse(checkpoint.PositionString, out var position))
        {
            return false;
        }

        Vector3Value.TryParse(checkpoint.RotationString, out var angle);
        Vector3Value.TryParse(checkpoint.SpeedString, out var velocity);
        player.Teleport(
            new Vector(position.X, position.Y, position.Z),
            new QAngle { X = angle.X, Y = angle.Y, Z = angle.Z },
            new Vector(velocity.X, velocity.Y, velocity.Z));
        return true;
    }

    private void QueueSecondaryWeaponGive(IPlayer? player)
    {
        if (!IsPlayablePlayer(player))
        {
            return;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        state.PendingSecondaryWeaponTicks = Math.Max(state.PendingSecondaryWeaponTicks, SecondaryWeaponGiveDelayTicks);
    }

    private void ProcessPendingSecondaryWeapon(IPlayer player, PlayerTimerState state)
    {
        if (state.PendingSecondaryWeaponTicks <= 0)
        {
            return;
        }

        state.PendingSecondaryWeaponTicks--;
        if (state.PendingSecondaryWeaponTicks > 0)
        {
            return;
        }

        _playerWeaponService?.RebuildDefaultWeapons(player, DefaultSecondaryWeapon);
    }
}
