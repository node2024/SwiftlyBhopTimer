namespace SwiftlyBhopTimer.Services;

public enum TimerRunMode
{
    Standard,
    Classic
}

public static class TimerRunModes
{
    public static readonly bool ClassicEnabled = true;
    public const string StandardKey = "standard";
    public const string ClassicKey = "classic";

    public static string ToStorageValue(TimerRunMode mode)
    {
        mode = Normalize(mode);
        return mode == TimerRunMode.Classic ? ClassicKey : StandardKey;
    }

    public static string ToDisplayName(TimerRunMode mode)
    {
        mode = Normalize(mode);
        return mode == TimerRunMode.Classic ? "Classic" : "Standard";
    }

    public static TimerRunMode Normalize(TimerRunMode mode)
    {
        return IsEnabled(mode) ? mode : TimerRunMode.Standard;
    }

    public static bool IsEnabled(TimerRunMode mode)
    {
        return mode != TimerRunMode.Classic || ClassicEnabled;
    }

    public static bool TryParse(string? value, out TimerRunMode mode)
    {
        mode = TimerRunMode.Standard;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.Equals(StandardKey, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("std", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            mode = TimerRunMode.Standard;
            return true;
        }

        if (normalized.Equals(ClassicKey, StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("css", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("cs:s", StringComparison.OrdinalIgnoreCase))
        {
            mode = TimerRunMode.Classic;
            return true;
        }

        return false;
    }

    public static TimerRunMode ParseOrDefault(string? value)
    {
        return TryParse(value, out var mode) ? Normalize(mode) : TimerRunMode.Standard;
    }
}
