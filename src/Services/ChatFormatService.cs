using System.Text.Json;

namespace SwiftlyBhopTimer.Services;

public sealed class ChatFormatService
{
    internal const string DefaultPrefix = "{green}[SwiftlyBhopTimer]{default}";
    private static readonly string[] BuiltInPrefixes = ["[SwiftlyBhopTimer]"];
    private static readonly Dictionary<string, string> ColorTags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = "\x01",
        ["white"] = "\x01",
        ["darkred"] = "\x02",
        ["purple"] = "\x03",
        ["olive"] = "\x04",
        ["lightgreen"] = "\x05",
        ["lime"] = "\x05",
        ["green"] = "\x06",
        ["red"] = "\x07",
        ["grey"] = "\x08",
        ["gray"] = "\x08",
        ["lightgray"] = "\x08",
        ["yellow"] = "\x09",
        ["gold"] = "\x10",
        ["lightblue"] = "\x0B",
        ["blue"] = "\x0C",
        ["magenta"] = "\x0E",
        ["lightpurple"] = "\x0E",
        ["lightred"] = "\x0F",
        ["orange"] = "\x10"
    };

    private readonly string _configPath;
    private ChatFormatOptions _options = new();

    public ChatFormatService(string configPath)
    {
        _configPath = configPath;
    }

    public string ConfigPath => _configPath;

    public void EnsureConfigAndLoad()
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_configPath))
        {
            var defaults = new ChatFormatOptions();
            var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }

        Reload();
    }

    public void Reload()
    {
        try
        {
            var json = File.ReadAllText(_configPath);
            _options = JsonSerializer.Deserialize<ChatFormatOptions>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new ChatFormatOptions();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to read chat config '{_configPath}': {ex.Message}");
            _options = new ChatFormatOptions();
        }
    }

    public string Format(string message)
    {
        var normalized = StripBuiltInPrefix(message);
        var prefix = TranslateColorTags(_options.Prefix);
        var messageColor = TranslateColorTags(_options.MessageColor);
        var formattedMessage = TranslateColorTags(normalized);
        var reset = TranslateColorTags("{default}");

        return string.IsNullOrWhiteSpace(prefix)
            ? $" {messageColor}{formattedMessage}{reset}"
            : $" {prefix} {messageColor}{formattedMessage}{reset}";
    }

    public static string TranslateColorTags(string value)
    {
        var result = value;
        foreach (var (tag, code) in ColorTags)
        {
            result = result.Replace($"{{{tag}}}", code, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static string StripBuiltInPrefix(string message)
    {
        foreach (var builtInPrefix in BuiltInPrefixes)
        {
            if (message.StartsWith(builtInPrefix, StringComparison.Ordinal))
            {
                return message[builtInPrefix.Length..].TrimStart();
            }
        }

        return message;
    }
}

public sealed class ChatFormatOptions
{
    public string Prefix { get; set; } = ChatFormatService.DefaultPrefix;
    public string MessageColor { get; set; } = "{default}";
}
