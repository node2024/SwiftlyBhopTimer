using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftlyBhopTimer.Services;

public sealed class MapChooserService
{
    public const int MaxTier = 10;

    private static readonly string[] TierNames =
    [
        "Novice",
        "Novice+",
        "Advanced Beginner",
        "Competent",
        "Competent+",
        "Proficient",
        "Proficient+",
        "Expert",
        "Expert+",
        "Master/TAS"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly int[] ReminderThresholdSeconds = [1800, 900, 600, 300, 120, 60, 30, 10];

    private readonly string _configPath;
    private readonly string _mapListPath;
    private readonly string _mapDataDirectory;
    private readonly string _bundledMapsPath;
    private readonly Dictionary<string, string> _nominationsBySteamId = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rtvSteamIds = new(StringComparer.Ordinal);
    private readonly HashSet<int> _sentReminderThresholds = [];
    private string[] _lastVoteMapOptionNames = [];

    private DateTime _mapStartUtc = DateTime.UtcNow;
    private string _currentMapName = "unknown";
    private double _extendedMinutes;
    private int _extendCount;
    private bool _endVoteStarted;
    private DateTime? _changeAtUtc;
    private MapChooserMapEntry? _nextMap;
    private MapChooserVote? _activeVote;

    public MapChooserService(string configPath, string mapListPath, string mapDataDirectory, string bundledMapsPath)
    {
        _configPath = configPath;
        _mapListPath = mapListPath;
        _mapDataDirectory = mapDataDirectory;
        _bundledMapsPath = bundledMapsPath;
    }

    public MapChooserConfig Config { get; private set; } = new();

    public bool Enabled => Config.Enabled;

    public MapChooserVote? ActiveVote => _activeVote;

    public MapChooserMapEntry? NextMap => _nextMap;

    public IReadOnlyList<MapChooserMapEntry> Maps => Config.Maps.Where(map => map.Enabled && !string.IsNullOrWhiteSpace(map.Name)).ToList();

    public void EnsureInitialized()
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_mapListPath))
        {
            SeedMapListFile();
        }

        if (!File.Exists(_configPath))
        {
            Config = new MapChooserConfig
            {
                Maps = ReadMapList().ToList()
            };
            SaveConfig();
            return;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            Config = JsonSerializer.Deserialize<MapChooserConfig>(json, JsonOptions) ?? new MapChooserConfig();
        }
        catch
        {
            Config = new MapChooserConfig();
        }

        NormalizeConfig();

        if (Config.Maps.Count == 0)
        {
            Config.Maps = ReadMapList().ToList();
            NormalizeConfig();
        }

        SaveConfig();
    }

    public void SaveConfig()
    {
        NormalizeConfig();

        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_configPath, JsonSerializer.Serialize(Config, JsonOptions) + Environment.NewLine);
    }

    public void BeginMap(string mapName, DateTime nowUtc)
    {
        _currentMapName = string.IsNullOrWhiteSpace(mapName) ? "unknown" : mapName;
        _mapStartUtc = nowUtc;
        _extendedMinutes = 0.0;
        _extendCount = 0;
        _endVoteStarted = false;
        _changeAtUtc = null;
        _nextMap = null;
        _activeVote = null;
        _nominationsBySteamId.Clear();
        _rtvSteamIds.Clear();
        _sentReminderThresholds.Clear();
    }

    public MapChooserAction Tick(DateTime nowUtc, int playerCount)
    {
        if (!Enabled)
        {
            return MapChooserAction.None;
        }

        if (_changeAtUtc.HasValue && nowUtc >= _changeAtUtc.Value && _nextMap is not null)
        {
            var map = _nextMap;
            _changeAtUtc = null;
            return MapChooserAction.Change(map, BuildChangeCommand(map));
        }

        if (_activeVote is not null && nowUtc >= _activeVote.EndsAtUtc)
        {
            return FinishVote(nowUtc);
        }

        if (_activeVote is not null || _changeAtUtc.HasValue)
        {
            return MapChooserAction.None;
        }

        var remaining = GetRemainingSeconds(nowUtc);
        if (remaining <= 0)
        {
            _endVoteStarted = true;
            return StartVote("time limit", nowUtc);
        }

        foreach (var threshold in ReminderThresholdSeconds)
        {
            if (remaining <= threshold && _sentReminderThresholds.Add(threshold))
            {
                return MapChooserAction.Reminder(threshold);
            }
        }

        if (!_endVoteStarted && remaining <= GetVoteStartBeforeEndSeconds())
        {
            _endVoteStarted = true;
            return StartVote("map end", nowUtc);
        }

        return MapChooserAction.None;
    }

    public MapChooserRtvResult AddRtv(string steamId, DateTime nowUtc, int playerCount)
    {
        if (!Enabled)
        {
            return new MapChooserRtvResult(false, 0, 0, MapChooserAction.None, "disabled");
        }

        if (_activeVote is not null)
        {
            return new MapChooserRtvResult(false, _rtvSteamIds.Count, GetRequiredRtvCount(playerCount), MapChooserAction.None, "vote-active");
        }

        if (!_rtvSteamIds.Add(steamId))
        {
            return new MapChooserRtvResult(false, _rtvSteamIds.Count, GetRequiredRtvCount(playerCount), MapChooserAction.None, "already");
        }

        var required = GetRequiredRtvCount(playerCount);
        if (_rtvSteamIds.Count >= required)
        {
            var action = StartVote("rtv", nowUtc);
            _rtvSteamIds.Clear();
            return new MapChooserRtvResult(true, required, required, action, "started");
        }

        return new MapChooserRtvResult(false, _rtvSteamIds.Count, required, MapChooserAction.None, "added");
    }

    public MapChooserAction StartVote(string reason, DateTime nowUtc)
    {
        if (!Enabled)
        {
            return MapChooserAction.None;
        }

        if (_activeVote is not null)
        {
            return MapChooserAction.None;
        }

        var options = BuildVoteOptions();
        if (options.Count == 0)
        {
            return MapChooserAction.None;
        }

        _activeVote = new MapChooserVote(reason, nowUtc.AddSeconds(Math.Max(5, Config.VoteDurationSeconds)), options);
        return MapChooserAction.VoteStarted(_activeVote);
    }

    public bool CastVote(string steamId, string optionId, out MapVoteOption? votedOption)
    {
        votedOption = null;
        if (_activeVote is null)
        {
            return false;
        }

        votedOption = _activeVote.Options.FirstOrDefault(option => string.Equals(option.Id, optionId, StringComparison.Ordinal));
        if (votedOption is null)
        {
            return false;
        }

        _activeVote.VotesBySteamId[steamId] = optionId;
        RecountVotes(_activeVote);
        return true;
    }

    public bool TryNominate(string steamId, string query, out MapChooserMapEntry? map)
    {
        map = FindMap(query);
        if (map is null)
        {
            return false;
        }

        _nominationsBySteamId[steamId] = map.Name;
        return true;
    }

    public MapChooserAction Extend(DateTime nowUtc, string reason)
    {
        if (!CanExtend)
        {
            return MapChooserAction.None;
        }

        _extendedMinutes += Math.Max(1.0, Config.ExtendMinutes);
        _extendCount++;
        _activeVote = null;
        _endVoteStarted = false;
        _rtvSteamIds.Clear();
        _sentReminderThresholds.Clear();
        return MapChooserAction.Extended(Math.Max(1.0, Config.ExtendMinutes), reason);
    }

    public MapChooserAction ChangeMapNow(MapChooserMapEntry map)
    {
        _nextMap = map;
        _changeAtUtc = null;
        _activeVote = null;
        return MapChooserAction.Change(map, BuildChangeCommand(map));
    }

    public MapChooserMapEntry? FindMap(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var maps = Maps;
        return maps.FirstOrDefault(map => string.Equals(map.Name, query, StringComparison.OrdinalIgnoreCase))
            ?? maps.FirstOrDefault(map => string.Equals(map.DisplayName, query, StringComparison.OrdinalIgnoreCase))
            ?? maps.FirstOrDefault(map => map.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            ?? maps.FirstOrDefault(map => map.DisplayName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            ?? maps.FirstOrDefault(map => map.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            ?? maps.FirstOrDefault(map => map.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    public bool TrySetMapTier(string query, int tier, out MapChooserMapEntry? map)
    {
        map = FindMap(query);
        if (map is null)
        {
            return false;
        }

        map.Tier = Math.Clamp(tier, 0, MaxTier);
        SaveConfig();
        return true;
    }

    public MapChooserMapEntry AddOrUpdateMap(string mapName, string workshopId, int tier, out bool added)
    {
        var normalizedName = mapName.Trim();
        var normalizedWorkshopId = workshopId.Trim();
        var existing = Config.Maps.FirstOrDefault(map => string.Equals(map.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
        added = existing is null;

        var map = existing ?? new MapChooserMapEntry
        {
            Name = normalizedName,
            DisplayName = normalizedName
        };

        if (string.IsNullOrWhiteSpace(map.DisplayName))
        {
            map.DisplayName = normalizedName;
        }

        map.WorkshopId = normalizedWorkshopId;
        map.Tier = Math.Clamp(tier, 0, MaxTier);
        map.Enabled = true;

        if (added)
        {
            Config.Maps.Add(map);
        }

        SaveConfig();
        return map;
    }

    public static string FormatTier(int tier)
    {
        if (tier <= 0)
        {
            return "Tier 0 (Unset)";
        }

        if (tier <= TierNames.Length)
        {
            return $"Tier {tier} ({TierNames[tier - 1]})";
        }

        return $"Tier {tier}";
    }

    public double GetRemainingSeconds(DateTime nowUtc)
    {
        var totalSeconds = (Math.Max(1.0, Config.TimeLimitMinutes) + _extendedMinutes) * 60.0;
        return Math.Max(0.0, totalSeconds - (nowUtc - _mapStartUtc).TotalSeconds);
    }

    public string FormatTimeLeft(DateTime nowUtc)
    {
        return FormatSeconds(GetRemainingSeconds(nowUtc));
    }

    public string FormatTotalTimeLimit()
    {
        return FormatSeconds((Math.Max(1.0, Config.TimeLimitMinutes) + _extendedMinutes) * 60.0);
    }

    public bool CanExtend => _extendCount < Math.Max(0, Config.MaxExtends);

    public double TotalTimeLimitMinutes => Math.Max(1.0, Config.TimeLimitMinutes) + _extendedMinutes;

    public double VoteStartBeforeEndMinutes => GetVoteStartBeforeEndSeconds() / 60.0;

    public string BuildChangeCommand(MapChooserMapEntry map)
    {
        var template = map.Command;

        if (string.IsNullOrWhiteSpace(template))
        {
            template = !string.IsNullOrWhiteSpace(map.WorkshopId)
                ? "host_workshop_map {workshopId}"
                : Config.ChangeCommandTemplate;
        }

        if (string.IsNullOrWhiteSpace(template))
        {
            template = "changelevel {map}";
        }

        return template
            .Replace("{map}", map.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{displayName}", map.DisplayName, StringComparison.OrdinalIgnoreCase)
            .Replace("{workshopId}", NormalizeWorkshopId(map.WorkshopId), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeWorkshopId(string workshopId)
    {
        var trimmed = workshopId.Trim();
        return trimmed.StartsWith("ws:", StringComparison.OrdinalIgnoreCase) ? trimmed[3..].Trim() : trimmed;
    }

    private MapChooserAction FinishVote(DateTime nowUtc)
    {
        if (_activeVote is null)
        {
            return MapChooserAction.None;
        }

        RecountVotes(_activeVote);
        var vote = _activeVote;
        _activeVote = null;

        var winner = vote.Options
            .OrderByDescending(option => option.Votes)
            .ThenBy(option => option.IsExtend ? 1 : 0)
            .FirstOrDefault();

        if (winner is null)
        {
            return MapChooserAction.None;
        }

        if (winner.IsExtend)
        {
            return Extend(nowUtc, "vote");
        }

        var map = FindMap(winner.MapName);
        if (map is null)
        {
            return MapChooserAction.None;
        }

        _nextMap = map;
        _changeAtUtc = nowUtc.AddSeconds(Math.Max(1, Config.ChangeDelaySeconds));
        return MapChooserAction.MapSelected(map, winner.Votes, Math.Max(1, Config.ChangeDelaySeconds));
    }

    private List<MapVoteOption> BuildVoteOptions()
    {
        var options = new List<MapVoteOption>();
        var addedMaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var maxMapOptions = Math.Max(1, Config.MaxVoteMapOptions);

        var nominatedMaps = _nominationsBySteamId.Values
            .Select(FindMap)
            .Where(map => map is not null)
            .Cast<MapChooserMapEntry>()
            .Where(map => !IsCurrentMap(map.Name))
            .DistinctBy(map => map.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        Shuffle(nominatedMaps);

        foreach (var nominated in nominatedMaps)
        {
            if (!addedMaps.Add(nominated.Name))
            {
                continue;
            }

            options.Add(MapVoteOption.ForMap(nominated));
            if (options.Count >= maxMapOptions)
            {
                break;
            }
        }

        var fillMaps = Maps
            .Where(map => !IsCurrentMap(map.Name) && !addedMaps.Contains(map.Name))
            .ToList();
        Shuffle(fillMaps);

        foreach (var map in fillMaps)
        {
            if (options.Count >= maxMapOptions)
            {
                break;
            }

            if (addedMaps.Add(map.Name))
            {
                options.Add(MapVoteOption.ForMap(map));
            }
        }

        AvoidRepeatingLastVoteOptions(options);

        if (CanExtend)
        {
            options.Add(MapVoteOption.ForExtend(Math.Max(1.0, Config.ExtendMinutes)));
        }

        return options;
    }

    private bool IsCurrentMap(string mapName)
    {
        return string.Equals(mapName, _currentMapName, StringComparison.OrdinalIgnoreCase);
    }

    private void AvoidRepeatingLastVoteOptions(List<MapVoteOption> options)
    {
        if (options.Count <= 1)
        {
            _lastVoteMapOptionNames = options.Select(option => option.MapName).ToArray();
            return;
        }

        for (var attempt = 0; attempt < 4 && IsSameVoteOptionOrder(options, _lastVoteMapOptionNames); attempt++)
        {
            Shuffle(options);
        }

        _lastVoteMapOptionNames = options.Select(option => option.MapName).ToArray();
    }

    private static bool IsSameVoteOptionOrder(IReadOnlyList<MapVoteOption> options, IReadOnlyList<string> lastOptionNames)
    {
        if (options.Count != lastOptionNames.Count)
        {
            return false;
        }

        for (var index = 0; index < options.Count; index++)
        {
            if (!string.Equals(options[index].MapName, lastOptionNames[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void Shuffle<T>(IList<T> values)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private int GetRequiredRtvCount(int playerCount)
    {
        var count = Math.Max(1, playerCount);
        var percentRequired = (int)Math.Ceiling(count * Math.Clamp(Config.RtvRequiredPercent, 0.01, 1.0));
        return Math.Clamp(Math.Max(Config.MinimumRtvPlayers, percentRequired), 1, count);
    }

    private static void RecountVotes(MapChooserVote vote)
    {
        foreach (var option in vote.Options)
        {
            option.Votes = vote.VotesBySteamId.Values.Count(value => string.Equals(value, option.Id, StringComparison.Ordinal));
        }
    }

    private IReadOnlyList<MapChooserMapEntry> ReadMapList()
    {
        if (!File.Exists(_mapListPath))
        {
            return ReadBundledJsonMaps();
        }

        var raw = File.ReadAllText(_mapListPath);
        var trimmed = raw.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            var jsonMaps = ParseMapChooserMapsJson(raw);
            if (jsonMaps.Count > 0)
            {
                return jsonMaps;
            }
        }

        var maps = new List<MapChooserMapEntry>();
        foreach (var rawLine in raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            var name = parts.ElementAtOrDefault(0) ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            maps.Add(new MapChooserMapEntry
            {
                Name = name,
                DisplayName = string.IsNullOrWhiteSpace(parts.ElementAtOrDefault(1)) ? name : parts[1],
                WorkshopId = parts.ElementAtOrDefault(2) ?? "",
                Command = parts.ElementAtOrDefault(3) ?? "",
                Enabled = true
            });
        }

        return maps.Count > 0 ? maps : DiscoverMapsFromMapData();
    }

    private void SeedMapListFile()
    {
        var directory = Path.GetDirectoryName(_mapListPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(_bundledMapsPath))
        {
            File.Copy(_bundledMapsPath, _mapListPath, overwrite: false);
            return;
        }

        const string embeddedName = "SwiftlyBhopTimer/MapChooser/maps.json";
        using var stream = typeof(MapChooserService).Assembly.GetManifestResourceStream(embeddedName);
        if (stream is not null)
        {
            using var destination = File.Create(_mapListPath);
            stream.CopyTo(destination);
            return;
        }

        var seededMaps = DiscoverMapsFromMapData();
        File.WriteAllLines(_mapListPath, seededMaps.Select(map => map.Name), System.Text.Encoding.UTF8);
    }

    private IReadOnlyList<MapChooserMapEntry> ReadBundledJsonMaps()
    {
        if (File.Exists(_bundledMapsPath))
        {
            var maps = ParseMapChooserMapsJson(File.ReadAllText(_bundledMapsPath));
            if (maps.Count > 0)
            {
                return maps;
            }
        }

        const string embeddedName = "SwiftlyBhopTimer/MapChooser/maps.json";
        using var stream = typeof(MapChooserService).Assembly.GetManifestResourceStream(embeddedName);
        if (stream is not null)
        {
            using var reader = new StreamReader(stream);
            var maps = ParseMapChooserMapsJson(reader.ReadToEnd());
            if (maps.Count > 0)
            {
                return maps;
            }
        }

        return DiscoverMapsFromMapData();
    }

    private static IReadOnlyList<MapChooserMapEntry> ParseMapChooserMapsJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("MapChooserMaps", out var wrapper) &&
                wrapper.ValueKind == JsonValueKind.Object)
            {
                root = wrapper;
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("Maps", out var mapsElement))
            {
                root = mapsElement;
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var maps = new List<MapChooserMapEntry>();
            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = ReadString(item, "Name", "name", "Map", "map");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var workshopId = ReadString(item, "WorkshopId", "workshopId", "Id", "id");
                var displayName = ReadString(item, "DisplayName", "displayName", "Title", "title");
                var command = ReadString(item, "Command", "command");
                var enabled = ReadBool(item, true, "Enabled", "enabled");
                var tier = ReadInt(item, 0, "Tier", "tier");

                maps.Add(new MapChooserMapEntry
                {
                    Name = name,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName,
                    WorkshopId = workshopId,
                    Command = command,
                    Tier = Math.Clamp(tier, 0, MaxTier),
                    Enabled = enabled
                });
            }

            return maps
                .GroupBy(map => map.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        }
        catch
        {
            return [];
        }
    }

    private void NormalizeConfig()
    {
        if (Config.VoteStartBeforeEndMinutes <= 0.0 && Config.VoteStartBeforeEndSeconds > 0)
        {
            Config.VoteStartBeforeEndMinutes = Config.VoteStartBeforeEndSeconds / 60.0;
        }

        if (Config.VoteStartBeforeEndMinutes <= 0.0)
        {
            Config.VoteStartBeforeEndMinutes = 5.0;
        }

        Config.VoteStartBeforeEndSeconds = Math.Max(1, (int)Math.Round(Config.VoteStartBeforeEndMinutes * 60.0));
        Config.ChatColors ??= new MapChooserChatColors();

        foreach (var map in Config.Maps)
        {
            map.Tier = Math.Clamp(map.Tier, 0, MaxTier);
        }
    }

    private int GetVoteStartBeforeEndSeconds()
    {
        var minutes = Config.VoteStartBeforeEndMinutes > 0.0
            ? Config.VoteStartBeforeEndMinutes
            : Config.VoteStartBeforeEndSeconds / 60.0;

        return Math.Max(1, (int)Math.Round(minutes * 60.0));
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString() ?? "",
                    JsonValueKind.Number => value.GetRawText(),
                    _ => ""
                };
            }
        }

        return "";
    }

    private static bool ReadBool(JsonElement element, bool fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value))
            {
                return value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
                    _ => fallback
                };
            }
        }

        return fallback;
    }

    private static int ReadInt(JsonElement element, int fallback, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsedNumber))
                {
                    return parsedNumber;
                }

                if (value.ValueKind == JsonValueKind.String &&
                    int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedString))
                {
                    return parsedString;
                }
            }
        }

        return fallback;
    }

    private IReadOnlyList<MapChooserMapEntry> DiscoverMapsFromMapData()
    {
        if (!Directory.Exists(_mapDataDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(_mapDataDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new MapChooserMapEntry { Name = name, DisplayName = name, Enabled = true })
            .ToList();
    }

    private static string FormatSeconds(double seconds)
    {
        var value = TimeSpan.FromSeconds(Math.Max(0.0, seconds));
        return value.TotalHours >= 1.0
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}";
    }
}

public sealed class MapChooserConfig
{
    public bool Enabled { get; set; } = true;

    public double TimeLimitMinutes { get; set; } = 60.0;

    public int VoteStartBeforeEndSeconds { get; set; } = 300;

    public double VoteStartBeforeEndMinutes { get; set; } = 5.0;

    public int VoteDurationSeconds { get; set; } = 20;

    public int ChangeDelaySeconds { get; set; } = 5;

    public double ExtendMinutes { get; set; } = 15.0;

    public int MaxExtends { get; set; } = 2;

    public int MaxVoteMapOptions { get; set; } = 5;

    public double RtvRequiredPercent { get; set; } = 0.6;

    public int MinimumRtvPlayers { get; set; } = 1;

    public string ChangeCommandTemplate { get; set; } = "changelevel {map}";

    public MapChooserChatColors ChatColors { get; set; } = new();

    public List<MapChooserMapEntry> Maps { get; set; } = [];
}

public sealed class MapChooserChatColors
{
    public string Label { get; set; } = "{lightblue}";

    public string Value { get; set; } = "{green}";

    public string Accent { get; set; } = "{gold}";

    public string Extend { get; set; } = "{gold}";

    public string Muted { get; set; } = "{gray}";

    public string Error { get; set; } = "{red}";
}

public sealed class MapChooserMapEntry
{
    public string Name { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string WorkshopId { get; set; } = "";

    public string Command { get; set; } = "";

    public int Tier { get; set; }

    public bool Enabled { get; set; } = true;
}

public sealed class MapChooserVote
{
    public MapChooserVote(string reason, DateTime endsAtUtc, List<MapVoteOption> options)
    {
        Reason = reason;
        EndsAtUtc = endsAtUtc;
        Options = options;
    }

    public string Reason { get; }

    public DateTime EndsAtUtc { get; }

    public List<MapVoteOption> Options { get; }

    public Dictionary<string, string> VotesBySteamId { get; } = new(StringComparer.Ordinal);
}

public sealed class MapVoteOption
{
    public string Id { get; set; } = "";

    public string Text { get; set; } = "";

    public string MapName { get; set; } = "";

    public bool IsExtend { get; set; }

    public int Votes { get; set; }

    public static MapVoteOption ForMap(MapChooserMapEntry map)
    {
        return new MapVoteOption
        {
            Id = $"map:{map.Name}",
            Text = string.IsNullOrWhiteSpace(map.DisplayName) ? map.Name : map.DisplayName,
            MapName = map.Name,
            IsExtend = false
        };
    }

    public static MapVoteOption ForExtend(double minutes)
    {
        return new MapVoteOption
        {
            Id = "extend",
            Text = $"Extend map +{minutes.ToString("0.#", CultureInfo.InvariantCulture)}m",
            IsExtend = true
        };
    }
}

public sealed record MapChooserRtvResult(bool StartedVote, int Current, int Required, MapChooserAction Action, string Reason);

public sealed record MapChooserAction(MapChooserActionKind Kind, string Message, MapChooserMapEntry? Map, string? Command, MapChooserVote? Vote, int Seconds, double Minutes)
{
    public static MapChooserAction None { get; } = new(MapChooserActionKind.None, "", null, null, null, 0, 0.0);

    public static MapChooserAction Reminder(int seconds) => new(MapChooserActionKind.Reminder, "", null, null, null, seconds, 0.0);

    public static MapChooserAction VoteStarted(MapChooserVote vote) => new(MapChooserActionKind.VoteStarted, "", null, null, vote, 0, 0.0);

    public static MapChooserAction Extended(double minutes, string reason) => new(MapChooserActionKind.Extended, reason, null, null, null, 0, minutes);

    public static MapChooserAction MapSelected(MapChooserMapEntry map, int votes, int seconds) => new(MapChooserActionKind.MapSelected, votes.ToString(CultureInfo.InvariantCulture), map, null, null, seconds, 0.0);

    public static MapChooserAction Change(MapChooserMapEntry map, string command) => new(MapChooserActionKind.ChangeMap, "", map, command, null, 0, 0.0);
}

public enum MapChooserActionKind
{
    None,
    Reminder,
    VoteStarted,
    Extended,
    MapSelected,
    ChangeMap
}
