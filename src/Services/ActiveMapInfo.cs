using SwiftlyBhopTimer.Models;

namespace SwiftlyBhopTimer.Services;

public sealed class ActiveMapInfo
{
    private const string DefaultStartTriggerName = "trigger_startzone";
    private const string DefaultEndTriggerName = "trigger_endzone";

    private ActiveMapInfo(string mapName, string startTriggerName, string endTriggerName)
        : this(mapName, startTriggerName, endTriggerName, null, null, null, new Dictionary<int, ActiveBonusInfo>())
    {
    }

    private ActiveMapInfo(
        string mapName,
        string startTriggerName,
        string endTriggerName,
        ZoneBounds? startZone,
        ZoneBounds? endZone,
        Vector3Value? respawnPosition,
        IReadOnlyDictionary<int, ActiveBonusInfo> bonuses)
    {
        MapName = mapName;
        StartTriggerName = string.IsNullOrWhiteSpace(startTriggerName) ? DefaultStartTriggerName : startTriggerName;
        EndTriggerName = string.IsNullOrWhiteSpace(endTriggerName) ? DefaultEndTriggerName : endTriggerName;
        StartZone = startZone;
        EndZone = endZone;
        RespawnPosition = respawnPosition;
        Bonuses = bonuses;
    }

    public string MapName { get; }
    public string StartTriggerName { get; }
    public string EndTriggerName { get; }
    public ZoneBounds? StartZone { get; }
    public ZoneBounds? EndZone { get; }
    public Vector3Value? RespawnPosition { get; }
    public IReadOnlyDictionary<int, ActiveBonusInfo> Bonuses { get; }

    public static ActiveMapInfo FromMapInfo(string mapName, MapInfo? mapInfo)
    {
        return new ActiveMapInfo(
            mapName,
            mapInfo?.MapStartTrigger ?? DefaultStartTriggerName,
            mapInfo?.MapEndTrigger ?? DefaultEndTriggerName,
            ZoneBounds.FromCorners(mapInfo?.MapStartC1, mapInfo?.MapStartC2),
            ZoneBounds.FromCorners(mapInfo?.MapEndC1, mapInfo?.MapEndC2),
            Vector3Value.TryParse(mapInfo?.RespawnPos, out var respawnPosition) ? respawnPosition : null,
            BuildBonuses(mapInfo));
    }

    public static ActiveMapInfo Default(string mapName)
    {
        return new ActiveMapInfo(mapName, DefaultStartTriggerName, DefaultEndTriggerName);
    }

    public bool IsStartTrigger(string triggerName)
    {
        return string.Equals(triggerName, StartTriggerName, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsEndTrigger(string triggerName)
    {
        return string.Equals(triggerName, EndTriggerName, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsInsideStartZone(Vector3Value position)
    {
        return StartZone?.Contains(position) == true;
    }

    public bool IsInsideEndZone(Vector3Value position)
    {
        return EndZone?.Contains(position) == true;
    }

    public bool TryGetBonus(int bonusNumber, out ActiveBonusInfo bonus)
    {
        return Bonuses.TryGetValue(bonusNumber, out bonus!);
    }

    public bool TryGetBonusStartTrigger(string triggerName, out int bonusNumber)
    {
        foreach (var bonus in Bonuses.Values)
        {
            if (bonus.IsStartTrigger(triggerName))
            {
                bonusNumber = bonus.Number;
                return true;
            }
        }

        bonusNumber = 0;
        return false;
    }

    public bool TryGetBonusEndTrigger(string triggerName, out int bonusNumber)
    {
        foreach (var bonus in Bonuses.Values)
        {
            if (bonus.IsEndTrigger(triggerName))
            {
                bonusNumber = bonus.Number;
                return true;
            }
        }

        bonusNumber = 0;
        return false;
    }

    private static IReadOnlyDictionary<int, ActiveBonusInfo> BuildBonuses(MapInfo? mapInfo)
    {
        if (mapInfo?.Bonuses is null || mapInfo.Bonuses.Count == 0)
        {
            return new Dictionary<int, ActiveBonusInfo>();
        }

        var bonuses = new Dictionary<int, ActiveBonusInfo>();
        foreach (var (key, info) in mapInfo.Bonuses)
        {
            if (!int.TryParse(key, out var number) || number is < 1 or > 99)
            {
                continue;
            }

            bonuses[number] = ActiveBonusInfo.FromMapInfo(number, info);
        }

        return bonuses;
    }
}

public sealed class ActiveBonusInfo
{
    private ActiveBonusInfo(
        int number,
        string? startTriggerName,
        string? endTriggerName,
        ZoneBounds? startZone,
        ZoneBounds? endZone,
        Vector3Value? respawnPosition)
    {
        Number = number;
        StartTriggerName = string.IsNullOrWhiteSpace(startTriggerName) ? null : startTriggerName;
        EndTriggerName = string.IsNullOrWhiteSpace(endTriggerName) ? null : endTriggerName;
        StartZone = startZone;
        EndZone = endZone;
        RespawnPosition = respawnPosition;
    }

    public int Number { get; }
    public string? StartTriggerName { get; }
    public string? EndTriggerName { get; }
    public ZoneBounds? StartZone { get; }
    public ZoneBounds? EndZone { get; }
    public Vector3Value? RespawnPosition { get; }

    public static ActiveBonusInfo FromMapInfo(int number, BonusMapInfo? mapInfo)
    {
        return new ActiveBonusInfo(
            number,
            mapInfo?.StartTrigger,
            mapInfo?.EndTrigger,
            ZoneBounds.FromCorners(mapInfo?.StartC1, mapInfo?.StartC2),
            ZoneBounds.FromCorners(mapInfo?.EndC1, mapInfo?.EndC2),
            Vector3Value.TryParse(mapInfo?.RespawnPos, out var respawnPosition) ? respawnPosition : null);
    }

    public bool IsStartTrigger(string triggerName)
    {
        return !string.IsNullOrWhiteSpace(StartTriggerName) &&
               string.Equals(triggerName, StartTriggerName, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsEndTrigger(string triggerName)
    {
        return !string.IsNullOrWhiteSpace(EndTriggerName) &&
               string.Equals(triggerName, EndTriggerName, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsInsideStartZone(Vector3Value position)
    {
        return StartZone?.Contains(position) == true;
    }

    public bool IsInsideEndZone(Vector3Value position)
    {
        return EndZone?.Contains(position) == true;
    }
}
