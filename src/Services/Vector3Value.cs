namespace SwiftlyBhopTimer.Services;

public readonly record struct Vector3Value(float X, float Y, float Z)
{
    public override string ToString()
    {
        return $"{X:0.##} {Y:0.##} {Z:0.##}";
    }

    public static bool TryParse(string? value, out Vector3Value vector)
    {
        vector = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) ||
            !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
        {
            return false;
        }

        vector = new Vector3Value(x, y, z);
        return true;
    }
}
