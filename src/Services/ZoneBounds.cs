namespace SwiftlyBhopTimer.Services;

public sealed class ZoneBounds
{
    private const float DefaultHeight = 96.0f;

    private ZoneBounds(float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
        MinZ = minZ;
        MaxZ = maxZ;
    }

    public float MinX { get; }
    public float MaxX { get; }
    public float MinY { get; }
    public float MaxY { get; }
    public float MinZ { get; }
    public float MaxZ { get; }

    public Vector3Value Min => new(MinX, MinY, MinZ);
    public Vector3Value Max => new(MaxX, MaxY, MaxZ);

    public override string ToString()
    {
        return $"X={MinX:0.##}..{MaxX:0.##} Y={MinY:0.##}..{MaxY:0.##} Z={MinZ:0.##}..{MaxZ:0.##}";
    }

    public static ZoneBounds? FromCorners(string? first, string? second)
    {
        if (!Vector3Value.TryParse(first, out var a) || !Vector3Value.TryParse(second, out var b))
        {
            return null;
        }

        return FromPoints(a, b);
    }

    public static ZoneBounds FromPoints(Vector3Value a, Vector3Value b)
    {
        var minZ = MathF.Min(a.Z, b.Z);
        var maxZ = MathF.Max(a.Z, b.Z);
        if (MathF.Abs(maxZ - minZ) < 1.0f)
        {
            maxZ += DefaultHeight;
        }

        return new ZoneBounds(
            MathF.Min(a.X, b.X),
            MathF.Max(a.X, b.X),
            MathF.Min(a.Y, b.Y),
            MathF.Max(a.Y, b.Y),
            minZ,
            maxZ);
    }

    public bool Contains(Vector3Value point)
    {
        return point.X >= MinX && point.X <= MaxX &&
               point.Y >= MinY && point.Y <= MaxY &&
               point.Z >= MinZ && point.Z <= MaxZ;
    }
}
