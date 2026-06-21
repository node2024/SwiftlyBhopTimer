using SwiftlyS2.Shared.Players;

namespace SwiftlyBhopTimer;

public sealed partial class SwiftlyBhopTimer
{
    private void ProcessAdvertising(IReadOnlyList<IPlayer> players)
    {
        if (_advertisingService is null)
        {
            return;
        }

        var messages = _advertisingService.CollectDueMessages(DateTime.UtcNow, players.Count > 0);
        foreach (var message in messages)
        {
            SendChatAll(message);
        }
    }
}
