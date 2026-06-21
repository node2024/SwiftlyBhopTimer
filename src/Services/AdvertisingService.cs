using System.Text.Json;

namespace SwiftlyBhopTimer.Services;

public sealed class AdvertisingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _configPath;
    private List<AdvertisingMessage> _messages = [];
    private DateTime _nextAdvertUtc = DateTime.MaxValue;
    private int _nextMessageIndex;
    private bool _readWarningLogged;

    public AdvertisingService(string configPath)
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
            File.WriteAllText(_configPath, JsonSerializer.Serialize(Array.Empty<AdvertisingMessage>(), JsonOptions) + Environment.NewLine);
        }

        Reload(force: true);
    }

    public int Reload()
    {
        return Reload(force: true);
    }

    public IReadOnlyList<string> CollectDueMessages(DateTime nowUtc, bool hasRecipients)
    {
        if (!hasRecipients || _messages.Count == 0)
        {
            return [];
        }

        if (_nextAdvertUtc == DateTime.MaxValue)
        {
            ScheduleNext(nowUtc);
            return [];
        }

        if (nowUtc < _nextAdvertUtc)
        {
            return [];
        }

        var message = _messages[_nextMessageIndex].Message.Trim();
        _nextMessageIndex = (_nextMessageIndex + 1) % _messages.Count;
        ScheduleNext(nowUtc);
        return [message];
    }

    private int Reload(bool force)
    {
        try
        {
            var raw = File.ReadAllText(_configPath);
            var loaded = ParseMessages(raw)
                .Where(message => !string.IsNullOrWhiteSpace(message.Message))
                .Select(message => message with { Sec = Math.Max(1, message.Sec) })
                .ToList();

            _messages = loaded;
            _nextMessageIndex = 0;
            _nextAdvertUtc = DateTime.MaxValue;
            if (_messages.Count > 0)
            {
                ScheduleNext(DateTime.UtcNow);
            }

            _readWarningLogged = false;
            return _messages.Count;
        }
        catch (Exception ex)
        {
            if (!_readWarningLogged || force)
            {
                _readWarningLogged = true;
                Console.WriteLine($"[SwiftlyBhopTimer] Failed to read advertising config '{_configPath}': {ex.Message}");
            }

            return _messages.Count;
        }
    }

    private static IReadOnlyList<AdvertisingMessage> ParseMessages(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        using var document = JsonDocument.Parse(raw);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            return document.RootElement.Deserialize<List<AdvertisingMessage>>(JsonOptions) ?? [];
        }

        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("messages", out var messagesElement) &&
            messagesElement.ValueKind == JsonValueKind.Array)
        {
            return messagesElement.Deserialize<List<AdvertisingMessage>>(JsonOptions) ?? [];
        }

        return [];
    }

    private void ScheduleNext(DateTime nowUtc)
    {
        if (_messages.Count == 0)
        {
            _nextAdvertUtc = DateTime.MaxValue;
            return;
        }

        var seconds = Math.Max(1, _messages[_nextMessageIndex].Sec);
        _nextAdvertUtc = nowUtc + TimeSpan.FromSeconds(seconds);
    }
}

public sealed record AdvertisingMessage(string Message, int Sec);
