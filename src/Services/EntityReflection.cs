using System.Reflection;
using SwiftlyS2.Shared.Players;

namespace SwiftlyBhopTimer.Services;

public static class EntityReflection
{
    public static string GetDesignerName(object entity)
    {
        return GetStringProperty(entity, "DesignerName");
    }

    public static string GetEntityName(object entity)
    {
        var directName = GetStringProperty(entity, "Name");
        if (!string.IsNullOrWhiteSpace(directName))
        {
            return directName;
        }

        var innerEntity = entity.GetType().GetProperty("Entity")?.GetValue(entity);
        if (innerEntity is not null)
        {
            var innerName = GetStringProperty(innerEntity, "Name");
            if (!string.IsNullOrWhiteSpace(innerName))
            {
                return innerName;
            }
        }

        return GetDesignerName(entity);
    }

    public static IPlayer? GetPlayerFromPawn(IPlayerManagerService playerManager, object pawn)
    {
        var method = playerManager.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(method => method.Name == "GetPlayerFromPawn" && method.GetParameters().Length == 1);

        return method?.Invoke(playerManager, [pawn]) as IPlayer;
    }

    public static bool TryGetEntityIndex(object? entity, out int entityIndex)
    {
        return TryGetEntityIndex(entity, out entityIndex, 0);
    }

    public static bool TryGetPosition(object? entity, out Vector3Value position)
    {
        position = default;
        return TryGetPosition(entity, out position, 0);
    }

    public static bool TryGetVelocity(object? entity, out Vector3Value velocity)
    {
        velocity = default;
        return TryGetVelocity(entity, out velocity, 0);
    }

    public static bool TryIsOnGround(object? entity, out bool onGround)
    {
        onGround = false;
        return TryIsOnGround(entity, out onGround, 0);
    }

    public static bool TryGetEyeAngles(object? entity, out Vector3Value angle)
    {
        angle = default;
        return TryGetEyeAngles(entity, out angle, 0);
    }

    private static bool TryGetPosition(object? entity, out Vector3Value position, int depth)
    {
        position = default;
        if (entity is null || depth > 4)
        {
            return false;
        }

        var rawPosition = GetPropertyOrFieldValue(entity, "AbsOrigin");
        if (TryConvertVector(rawPosition, out position))
        {
            return true;
        }

        var bodyComponent = GetPropertyOrFieldValue(entity, "CBodyComponent");
        var sceneNode = GetPropertyOrFieldValue(bodyComponent, "SceneNode");
        rawPosition = GetPropertyOrFieldValue(sceneNode, "AbsOrigin");
        if (TryConvertVector(rawPosition, out position))
        {
            return true;
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn" })
        {
            var nested = GetPropertyOrFieldValue(entity, nestedName);
            if (!ReferenceEquals(nested, entity) && TryGetPosition(nested, out position, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetEntityIndex(object? entity, out int entityIndex, int depth)
    {
        entityIndex = 0;
        if (entity is null || depth > 4)
        {
            return false;
        }

        if (TryConvertInt(entity, out entityIndex) && entityIndex > 0)
        {
            return true;
        }

        foreach (var memberName in new[] { "Index", "EntityIndex", "EntIndex" })
        {
            var rawIndex = GetPropertyOrFieldValue(entity, memberName);
            if (TryConvertInt(rawIndex, out entityIndex) && entityIndex > 0)
            {
                return true;
            }
        }

        foreach (var handleName in new[] { "EntityHandle", "RefEHandle", "Handle", "EHandle" })
        {
            var handle = GetPropertyOrFieldValue(entity, handleName);
            if (handle is not null && !ReferenceEquals(handle, entity) && TryGetEntityIndex(handle, out entityIndex, depth + 1))
            {
                return true;
            }
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn" })
        {
            var nested = GetPropertyOrFieldValue(entity, nestedName);
            if (nested is not null && !ReferenceEquals(nested, entity) && TryGetEntityIndex(nested, out entityIndex, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetVelocity(object? entity, out Vector3Value velocity, int depth)
    {
        velocity = default;
        if (entity is null || depth > 4)
        {
            return false;
        }

        foreach (var memberName in new[] { "AbsVelocity", "Velocity", "BaseVelocity" })
        {
            var rawVelocity = GetPropertyOrFieldValue(entity, memberName);
            if (TryConvertVector(rawVelocity, out velocity))
            {
                return true;
            }
        }

        var bodyComponent = GetPropertyOrFieldValue(entity, "CBodyComponent");
        var sceneNode = GetPropertyOrFieldValue(bodyComponent, "SceneNode");
        foreach (var memberName in new[] { "AbsVelocity", "Velocity" })
        {
            var rawVelocity = GetPropertyOrFieldValue(sceneNode, memberName);
            if (TryConvertVector(rawVelocity, out velocity))
            {
                return true;
            }
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn" })
        {
            var nested = GetPropertyOrFieldValue(entity, nestedName);
            if (!ReferenceEquals(nested, entity) && TryGetVelocity(nested, out velocity, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryIsOnGround(object? entity, out bool onGround, int depth)
    {
        onGround = false;
        if (entity is null || depth > 4)
        {
            return false;
        }

        foreach (var memberName in new[] { "IsOnGround", "OnGround" })
        {
            var rawGround = GetPropertyOrFieldValue(entity, memberName);
            if (TryConvertBool(rawGround, out onGround))
            {
                return true;
            }
        }

        foreach (var memberName in new[] { "Flags", "PlayerFlags", "GroundFlags", "m_fFlags" })
        {
            var rawFlags = GetPropertyOrFieldValue(entity, memberName);
            if (TryConvertInt(rawFlags, out var flags))
            {
                onGround = (flags & 1) != 0;
                return true;
            }
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn" })
        {
            var nested = GetPropertyOrFieldValue(entity, nestedName);
            if (!ReferenceEquals(nested, entity) && TryIsOnGround(nested, out onGround, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetEyeAngles(object? entity, out Vector3Value angle, int depth)
    {
        angle = default;
        if (entity is null || depth > 4)
        {
            return false;
        }

        foreach (var memberName in new[] { "EyeAngles", "AbsRotation" })
        {
            var rawAngle = GetPropertyOrFieldValue(entity, memberName);
            if (TryConvertVector(rawAngle, out angle))
            {
                return true;
            }
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn" })
        {
            var nested = GetPropertyOrFieldValue(entity, nestedName);
            if (!ReferenceEquals(nested, entity) && TryGetEyeAngles(nested, out angle, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetStringProperty(object value, string propertyName)
    {
        return GetPropertyOrFieldValue(value, propertyName)?.ToString() ?? "";
    }

    private static object? GetPropertyOrFieldValue(object? value, string memberName)
    {
        if (value is null)
        {
            return null;
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = value.GetType();
        try
        {
            var property = type.GetProperty(memberName, flags);
            if (property is not null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(value);
            }

            var field = type.GetField(memberName, flags);
            return field?.GetValue(value);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryConvertVector(object? value, out Vector3Value vector)
    {
        vector = default;
        if (value is null)
        {
            return false;
        }

        if (!TryGetFloatMember(value, "X", out var x) ||
            !TryGetFloatMember(value, "Y", out var y) ||
            !TryGetFloatMember(value, "Z", out var z))
        {
            return false;
        }

        vector = new Vector3Value(x, y, z);
        return true;
    }

    private static bool TryGetFloatMember(object value, string memberName, out float result)
    {
        result = 0;
        var raw = GetPropertyOrFieldValue(value, memberName);
        if (raw is null)
        {
            return false;
        }

        try
        {
            result = Convert.ToSingle(raw, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryConvertBool(object? value, out bool result)
    {
        result = false;
        if (value is null)
        {
            return false;
        }

        if (value is bool boolValue)
        {
            result = boolValue;
            return true;
        }

        try
        {
            result = Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryConvertInt(object? value, out int result)
    {
        result = 0;
        if (value is null)
        {
            return false;
        }

        try
        {
            result = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
