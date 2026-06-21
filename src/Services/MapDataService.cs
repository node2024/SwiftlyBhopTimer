using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using SwiftlyBhopTimer.Models;

namespace SwiftlyBhopTimer.Services;

public sealed class MapDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string[] _mapDataDirectories;
    private readonly Dictionary<string, MapInfo> _loadedMapInfoCache = new(StringComparer.OrdinalIgnoreCase);

    public MapDataService(params string[] mapDataDirectories)
    {
        _mapDataDirectories = mapDataDirectories
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<MapInfo?> LoadAsync(string mapName)
    {
        foreach (var directory in _mapDataDirectories)
        {
            var path = Path.Combine(directory, $"{mapName}.json");
            if (!File.Exists(path))
            {
                continue;
            }

            await using var stream = File.OpenRead(path);
            var mapInfo = await JsonSerializer.DeserializeAsync<MapInfo>(stream, JsonOptions);
            if (mapInfo is not null)
            {
                _loadedMapInfoCache[mapName] = mapInfo;
                return mapInfo;
            }
        }

        return _loadedMapInfoCache.GetValueOrDefault(mapName);
    }

    public async Task<ActiveMapInfo> LoadActiveMapInfoAsync(string mapName)
    {
        return ActiveMapInfo.FromMapInfo(mapName, await LoadAsync(mapName));
    }

    public async Task<string> SaveMapSettingAsync(string mapName, string propertyName, string value)
    {
        var (path, mapJson) = await LoadWritableMapJsonAsync(mapName);

        mapJson[propertyName] = value;
        await File.WriteAllTextAsync(path, mapJson.ToJsonString(JsonOptions) + Environment.NewLine);
        return path;
    }

    public async Task<string> SaveBonusMapSettingAsync(string mapName, int bonusNumber, string propertyName, string value)
    {
        if (bonusNumber is < 1 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(bonusNumber), bonusNumber, "Bonus number must be between 1 and 99.");
        }

        var (path, mapJson) = await LoadWritableMapJsonAsync(mapName);
        var bonuses = mapJson["Bonuses"] as JsonObject;
        if (bonuses is null)
        {
            bonuses = [];
            mapJson["Bonuses"] = bonuses;
        }

        var bonusKey = bonusNumber.ToString(CultureInfo.InvariantCulture);
        var bonusJson = bonuses[bonusKey] as JsonObject;
        if (bonusJson is null)
        {
            bonusJson = [];
            bonuses[bonusKey] = bonusJson;
        }

        bonusJson[propertyName] = value;
        await File.WriteAllTextAsync(path, mapJson.ToJsonString(JsonOptions) + Environment.NewLine);
        return path;
    }

    private async Task<(string Path, JsonObject MapJson)> LoadWritableMapJsonAsync(string mapName)
    {
        var path = GetWritableMapDataPath(mapName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (!File.Exists(path))
        {
            return (path, []);
        }

        var content = await File.ReadAllTextAsync(path);
        return (path, JsonNode.Parse(content)?.AsObject() ?? []);
    }

    public string GetWritableMapDataPath(string mapName)
    {
        var directory = _mapDataDirectories.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("No writable MapData directory is configured.");
        }

        return Path.Combine(directory, $"{mapName}.json");
    }

    public string? FindFirstKnownMapName()
    {
        foreach (var directory in _mapDataDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var name = Directory.EnumerateFiles(directory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return null;
    }

    public string GetDiagnostics()
    {
        return string.Join(" | ", _mapDataDirectories.Select(directory =>
        {
            var count = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.json").Count()
                : 0;

            return $"{directory} ({count})";
        }));
    }
}
