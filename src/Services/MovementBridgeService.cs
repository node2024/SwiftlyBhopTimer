using System.Globalization;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Players;

namespace SwiftlyBhopTimer.Services;

public sealed class MovementBridgeService
{
    private readonly ISwiftlyCore _core;
    private bool _applyWarningLogged;

    public MovementBridgeService(ISwiftlyCore core)
    {
        _core = core;
    }

    public bool QueueRestartTeleport(IPlayer player, Vector3Value destination, int delayTicks)
    {
        if (player is not { IsValid: true })
        {
            return false;
        }

        return Execute(string.Join(' ',
            "sbt_restart_player",
            player.Slot.ToString(CultureInfo.InvariantCulture),
            Format(destination.X),
            Format(destination.Y),
            Format(destination.Z),
            Math.Max(0, delayTicks).ToString(CultureInfo.InvariantCulture)));
    }

    public bool QueueRestartTeleportToMapStart(IPlayer player, Vector3Value? fallbackDestination, int delayTicks)
    {
        if (player is not { IsValid: true })
        {
            return false;
        }

        var parts = new List<string>
        {
            "sbt_restart_player_map_start",
            player.Slot.ToString(CultureInfo.InvariantCulture),
            Math.Max(0, delayTicks).ToString(CultureInfo.InvariantCulture)
        };

        if (fallbackDestination is { } fallback)
        {
            parts.Add(Format(fallback.X));
            parts.Add(Format(fallback.Y));
            parts.Add(Format(fallback.Z));
        }

        return Execute(string.Join(' ', parts));
    }

    public bool SetBhopMode(IPlayer player, TimerRunMode mode)
    {
        if (player is not { IsValid: true })
        {
            return false;
        }

        return Execute(string.Join(' ',
            "sbt_bhop_mode",
            player.Slot.ToString(CultureInfo.InvariantCulture),
            TimerRunModes.ToStorageValue(mode)));
    }

    public bool SetSpectateTarget(IPlayer viewer, IPlayer target)
    {
        if (viewer is not { IsValid: true } || target is not { IsValid: true })
        {
            return false;
        }

        return Execute(string.Join(' ',
            "sbt_spec_target",
            viewer.Slot.ToString(CultureInfo.InvariantCulture),
            target.Slot.ToString(CultureInfo.InvariantCulture)));
    }

    public bool ForceTeam(IPlayer player, int teamNumber)
    {
        if (player is not { IsValid: true })
        {
            return false;
        }

        return Execute(string.Join(' ',
            "sbt_force_team",
            player.Slot.ToString(CultureInfo.InvariantCulture),
            teamNumber.ToString(CultureInfo.InvariantCulture)));
    }

    private bool Execute(string command)
    {
        try
        {
            _core.Engine.ExecuteCommand(command);
            return true;
        }
        catch (Exception ex)
        {
            if (!_applyWarningLogged)
            {
                _applyWarningLogged = true;
                Console.WriteLine($"[SwiftlyBhopTimer] Failed to execute movement helper command '{command}': {ex.Message}");
            }

            return false;
        }
    }

    private static string Format(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
