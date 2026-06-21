using SwiftlyS2.Shared.Players;

namespace SwiftlyBhopTimer.Services;

public sealed class TimerSoundService
{
    private const string StartSound = "sounds/buttons/button9.vsnd";
    private const string EndSound = "sounds/buttons/bell1.vsnd";
    private const string TeleportSound = "sounds/buttons/blip1.vsnd";
    private const string StopSound = "sounds/buttons/button8.vsnd";
    private bool _commandWarningLogged;

    public void PlayStart(IPlayer player, bool playerSoundsEnabled)
    {
        Play(player, playerSoundsEnabled, StartSound);
    }

    public void PlayEnd(IPlayer player, bool playerSoundsEnabled)
    {
        Play(player, playerSoundsEnabled, EndSound);
    }

    public void PlayTeleport(IPlayer player, bool playerSoundsEnabled)
    {
        Play(player, playerSoundsEnabled, TeleportSound);
    }

    public void PlayStop(IPlayer player, bool playerSoundsEnabled)
    {
        Play(player, playerSoundsEnabled, StopSound);
    }

    private void Play(IPlayer player, bool playerSoundsEnabled, string soundPath)
    {
        if (!playerSoundsEnabled)
        {
            return;
        }

        var command = $"play {soundPath}";
        if (ClientCommandInvoker.TryExecute(player, command))
        {
            return;
        }

        if (!_commandWarningLogged)
        {
            _commandWarningLogged = true;
            Console.WriteLine($"[SwiftlyBhopTimer] Could not find a client command method for sound playback. Command='{command}', PlayerType={player.GetType().FullName}");
        }
    }

}
