using System.Text.Json;
using SwiftlyBhopTimer.Models;

namespace SwiftlyBhopTimer.Services;

public sealed class JsonRecordStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _playerRecordsDirectory;

    public JsonRecordStore(string playerRecordsDirectory)
    {
        _playerRecordsDirectory = playerRecordsDirectory;
    }

    public async Task<IReadOnlyList<PlayerRecordEntry>> GetTopRecordsAsync(string mapName, int limit)
    {
        var records = await LoadMapRecordsAsync(mapName);

        return records
            .OrderBy(record => record.Value.TimerTicks)
            .Take(limit)
            .Select(record => new PlayerRecordEntry(record.Key, record.Value.PlayerName, record.Value.TimerTicks))
            .ToArray();
    }

    public async Task<PlayerRank?> GetRankAsync(string mapName, string steamId)
    {
        var records = await LoadMapRecordsAsync(mapName);
        var ordered = records
            .OrderBy(record => record.Value.TimerTicks)
            .ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            if (ordered[index].Key == steamId)
            {
                return new PlayerRank(index + 1, ordered.Length, ordered[index].Value.TimerTicks);
            }
        }

        return null;
    }

    public async Task<TimePlacement> GetPlacementForTimeAsync(string mapName, string steamId, int timerTicks)
    {
        var records = await LoadMapRecordsAsync(mapName);
        var fasterCount = records.Count(record =>
            !string.Equals(record.Key, steamId, StringComparison.Ordinal) &&
            record.Value.TimerTicks < timerTicks);
        var total = records.ContainsKey(steamId) ? records.Count : records.Count + 1;

        return new TimePlacement(fasterCount + 1, total);
    }

    public async Task SaveIfPersonalBestAsync(string mapName, string steamId, string playerName, int timerTicks)
    {
        Directory.CreateDirectory(_playerRecordsDirectory);

        var records = await LoadMapRecordsAsync(mapName);
        if (records.TryGetValue(steamId, out var existingRecord) && existingRecord.TimerTicks <= timerTicks)
        {
            return;
        }

        records[steamId] = new PlayerRecord
        {
            PlayerName = playerName,
            TimerTicks = timerTicks
        };

        await using var stream = File.Create(GetMapRecordPath(mapName));
        await JsonSerializer.SerializeAsync(stream, records, JsonOptions);
    }

    private async Task<Dictionary<string, PlayerRecord>> LoadMapRecordsAsync(string mapName)
    {
        var path = GetMapRecordPath(mapName);
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, PlayerRecord>>(stream, JsonOptions) ?? [];
    }

    private string GetMapRecordPath(string mapName)
    {
        return Path.Combine(_playerRecordsDirectory, $"{mapName}.json");
    }
}

public sealed record PlayerRecordEntry(string SteamId, string? PlayerName, int TimerTicks);

public sealed record PlayerRank(int Placement, int Total, int TimerTicks);

public sealed record TimePlacement(int Placement, int Total);
