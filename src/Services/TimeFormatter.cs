namespace SwiftlyBhopTimer.Services;

public static class TimeFormatter
{
    private const double TickRate = 64.0;

    public static string FormatTicks(int ticks)
    {
        var time = TimeSpan.FromSeconds(ticks / TickRate);
        return time.Hours > 0
            ? $"{time.Hours:00}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}"
            : $"{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    public static string FormatDeltaTicks(int ticks)
    {
        return FormatTicks(Math.Abs(ticks));
    }
}
