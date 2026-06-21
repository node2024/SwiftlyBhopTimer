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
using System.Globalization;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private const int MaxCheckpointSlots = 4;

    private void OnCheckpointCommand(ICommandContext context)
    {
        var player = context.Sender;
        var state = GetSenderStateOrReply(context);
        if (player is null || state is null)
        {
            return;
        }

        SaveCheckpoint(player, state, message => Reply(context, message));
    }

    private void OnCheckpointTeleportCommand(ICommandContext context)
    {
        var player = context.Sender;
        var state = GetSenderStateOrReply(context);
        if (player is null || state is null)
        {
            return;
        }

        TeleportToCheckpoint(player, state, message => Reply(context, message));
    }

    private void OnNextCheckpointCommand(ICommandContext context)
    {
        var player = context.Sender;
        var state = GetSenderStateOrReply(context);
        if (player is null || state is null)
        {
            return;
        }

        SelectCheckpointSlot(player, state, step: 1, message => Reply(context, message));
    }

    private void OnPreviousCheckpointCommand(ICommandContext context)
    {
        var player = context.Sender;
        var state = GetSenderStateOrReply(context);
        if (player is null || state is null)
        {
            return;
        }

        SelectCheckpointSlot(player, state, step: -1, message => Reply(context, message));
    }

    private void OnClearCheckpointCommand(ICommandContext context)
    {
        var player = context.Sender;
        var state = GetSenderStateOrReply(context);
        if (player is null || state is null)
        {
            return;
        }

        ClearCheckpoint(player, state, message => Reply(context, message));
    }

    private void OnSetStartPositionCommand(ICommandContext context)
    {
        var player = context.Sender;
        var state = GetSenderStateOrReply(context);
        if (player is null || state is null)
        {
            return;
        }

        if (context.Args.Length > 0 && context.Args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            ClearPersonalStartPosition(state, message => Reply(context, message));
            return;
        }

        SavePersonalStartPosition(player, state, message => Reply(context, message));
    }

    private bool TryHandleCheckpointRawCommand(int playerId, string commandLine)
    {
        var parts = commandLine.TrimStart().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var command = parts[0].Trim();
        if (!IsCheckpointRawCommand(command))
        {
            return false;
        }

        var player = Core.PlayerManager.GetPlayer(playerId);
        if (!IsPlayablePlayer(player))
        {
            return true;
        }

        var state = _timerStateStore.GetOrCreate(player!.Slot, player.SteamID.ToString(), player.Name);
        switch (command.ToLowerInvariant())
        {
            case "sbt_cp":
                SaveCheckpoint(player, state, message => SendChat(player, message));
                break;
            case "sbt_tp":
                TeleportToCheckpoint(player, state, message => SendChat(player, message));
                break;
            case "sbt_ssp":
                SavePersonalStartPosition(player, state, message => SendChat(player, message));
                break;
            case "sbt_nextcp":
                SelectCheckpointSlot(player, state, step: 1, message => SendChat(player, message));
                break;
            case "sbt_prevcp":
                SelectCheckpointSlot(player, state, step: -1, message => SendChat(player, message));
                break;
            case "sbt_clearcp":
                ClearCheckpoint(player, state, message => SendChat(player, message));
                break;
        }

        return true;
    }

    private static bool IsCheckpointRawCommand(string command)
    {
        return command.Equals("sbt_cp", StringComparison.OrdinalIgnoreCase) ||
               command.Equals("sbt_tp", StringComparison.OrdinalIgnoreCase) ||
               command.Equals("sbt_ssp", StringComparison.OrdinalIgnoreCase) ||
               command.Equals("sbt_nextcp", StringComparison.OrdinalIgnoreCase) ||
               command.Equals("sbt_prevcp", StringComparison.OrdinalIgnoreCase) ||
               command.Equals("sbt_clearcp", StringComparison.OrdinalIgnoreCase);
    }

    private void SaveCheckpoint(IPlayer player, PlayerTimerState state, Action<string> reply)
    {
        if (!player.IsAlive)
        {
            reply($"{label("CP")} {label("|")} {red("player must be alive")}");
            return;
        }

        var pawn = player.PlayerPawn ?? player.Pawn;
        if (!EntityReflection.TryGetPosition(pawn, out var position))
        {
            reply($"{label("CP")} {label("|")} {red("could not read player position")}");
            return;
        }

        EntityReflection.TryGetEyeAngles(pawn, out var angle);
        EntityReflection.TryGetVelocity(pawn, out var velocity);

        _replayService?.DiscardRecording(player);
        ResetTimerStateWithStopSound(player, state);
        state.StartSpeedLimitPauseTicks = RestartSpeedLimitPauseTicks;

        var slot = NormalizeCheckpointSlot(state.ActiveCheckpointSlot);
        state.ActiveCheckpointSlot = slot;
        state.Checkpoints[slot - 1] = new PlayerCheckpoint
        {
            PositionString = FormatCheckpointVector(position),
            RotationString = FormatCheckpointVector(angle),
            SpeedString = FormatCheckpointVector(velocity)
        };

        reply($"{label("CP")} {gold($"#{slot}")} {label("|")} {green("saved")} {gray($"{FormatHorizontalSpeed(velocity)}u")}");
    }

    private void TeleportToCheckpoint(IPlayer player, PlayerTimerState state, Action<string> reply)
    {
        if (!player.IsAlive)
        {
            reply($"{label("TP")} {label("|")} {red("player must be alive")}");
            return;
        }

        var slot = NormalizeCheckpointSlot(state.ActiveCheckpointSlot);
        state.ActiveCheckpointSlot = slot;
        var checkpoint = state.Checkpoints[slot - 1];
        if (checkpoint is null ||
            !Vector3Value.TryParse(checkpoint.PositionString, out var position))
        {
            reply($"{label("TP")} {gold($"#{slot}")} {label("|")} {red("empty")}");
            return;
        }

        Vector3Value.TryParse(checkpoint.RotationString, out var angle);
        Vector3Value.TryParse(checkpoint.SpeedString, out var velocity);

        _replayService?.DiscardRecording(player);
        ResetTimerStateWithStopSound(player, state);
        state.StartSpeedLimitPauseTicks = RestartSpeedLimitPauseTicks;

        player.Teleport(
            new Vector(position.X, position.Y, position.Z),
            new QAngle { X = angle.X, Y = angle.Y, Z = angle.Z },
            new Vector(velocity.X, velocity.Y, velocity.Z));
        _timerSoundService?.PlayTeleport(player, state.SoundsEnabled);

        reply($"{label("TP")} {gold($"#{slot}")} {label("|")} {green("teleported")}");
    }

    private void SavePersonalStartPosition(IPlayer player, PlayerTimerState state, Action<string> reply)
    {
        if (!player.IsAlive)
        {
            reply($"{label("SSP")} {label("|")} {red("player must be alive")}");
            return;
        }

        var pawn = player.PlayerPawn ?? player.Pawn;
        if (!EntityReflection.TryGetPosition(pawn, out var position))
        {
            reply($"{label("SSP")} {label("|")} {red("could not read player position")}");
            return;
        }

        EntityReflection.TryGetEyeAngles(pawn, out var angle);

        _replayService?.DiscardRecording(player);
        ResetTimerStateWithStopSound(player, state);
        state.StartSpeedLimitPauseTicks = RestartSpeedLimitPauseTicks;
        state.PersonalStartMapName = _currentMapName;
        state.PersonalStartPosition = new PlayerCheckpoint
        {
            PositionString = FormatCheckpointVector(position),
            RotationString = FormatCheckpointVector(angle),
            SpeedString = FormatCheckpointVector(default)
        };

        reply($"{label("SSP")} {label("|")} {green("personal start saved")} {gray(_currentMapName)}");
    }

    private void ClearPersonalStartPosition(PlayerTimerState state, Action<string> reply)
    {
        state.PersonalStartMapName = "";
        state.PersonalStartPosition = null;
        reply($"{label("SSP")} {label("|")} {red("cleared")}");
    }

    private bool TryGetPersonalStartPosition(PlayerTimerState state, out PlayerCheckpoint checkpoint)
    {
        checkpoint = null!;
        if (state.PersonalStartPosition is null ||
            !state.PersonalStartMapName.Equals(_currentMapName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        checkpoint = state.PersonalStartPosition;
        return true;
    }

    private void SelectCheckpointSlot(IPlayer player, PlayerTimerState state, int step, Action<string> reply)
    {
        state.ActiveCheckpointSlot = NormalizeCheckpointSlot(state.ActiveCheckpointSlot + step);
        var status = state.Checkpoints[state.ActiveCheckpointSlot - 1] is null ? gray("empty") : green("saved");
        reply($"{label("CP")} {label("|")} {gold($"#{state.ActiveCheckpointSlot}")} {status}");
    }

    private void ClearCheckpoint(IPlayer player, PlayerTimerState state, Action<string> reply)
    {
        var slot = NormalizeCheckpointSlot(state.ActiveCheckpointSlot);
        state.ActiveCheckpointSlot = slot;
        state.Checkpoints[slot - 1] = null;
        reply($"{label("CP")} {gold($"#{slot}")} {label("|")} {red("cleared")}");
    }

    private static int NormalizeCheckpointSlot(int slot)
    {
        return ((slot - 1) % MaxCheckpointSlots + MaxCheckpointSlots) % MaxCheckpointSlots + 1;
    }

    private static string FormatCheckpointVector(Vector3Value value)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{value.X:0.###} {value.Y:0.###} {value.Z:0.###}");
    }

    private static string FormatHorizontalSpeed(Vector3Value velocity)
    {
        var speed = MathF.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y);
        return speed.ToString("0", CultureInfo.InvariantCulture);
    }
}
