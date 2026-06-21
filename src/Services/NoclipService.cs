using System.Reflection;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyBhopTimer.Services;

public sealed class NoclipService
{
    private static readonly string[] MoveTypeMemberNames =
    [
        "MoveType",
        "ActualMoveType",
        "MoveTypeOverride",
        "MoveCollide"
    ];

    public bool Apply(IPlayer player, bool enabled)
    {
        var applied = TryApplyMoveType(player.PlayerPawn ?? player.Pawn, enabled);

        if (!enabled)
        {
            player.Teleport(null, null, Vector.Zero);
        }

        return applied;
    }

    public bool? TryReadEnabled(IPlayer player)
    {
        return TryReadMoveType(player.PlayerPawn ?? player.Pawn);
    }

    private static bool TryApplyMoveType(object? pawn, bool enabled)
    {
        if (pawn is null)
        {
            return false;
        }

        if (pawn is CCSPlayerPawn typedPawn)
        {
            return TrySetTypedMoveType(typedPawn, enabled);
        }

        if (TrySetMoveType(pawn, enabled))
        {
            return true;
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn" })
        {
            var nested = GetMember(pawn, nestedName);
            if (nested is not null && !ReferenceEquals(nested, pawn) && TryApplyMoveType(nested, enabled))
            {
                return true;
            }
        }

        return false;
    }

    private static bool? TryReadMoveType(object? pawn)
    {
        if (pawn is null)
        {
            return null;
        }

        foreach (var memberName in MoveTypeMemberNames)
        {
            var value = GetMember(pawn, memberName);
            var enabled = TryConvertMoveTypeToNoclip(value);
            if (enabled.HasValue)
            {
                return enabled.Value;
            }
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn" })
        {
            var nested = GetMember(pawn, nestedName);
            if (nested is not null && !ReferenceEquals(nested, pawn))
            {
                var enabled = TryReadMoveType(nested);
                if (enabled.HasValue)
                {
                    return enabled.Value;
                }
            }
        }

        return null;
    }

    private static bool? TryConvertMoveTypeToNoclip(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var text = value.ToString();
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (text.Contains("noclip", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("no clip", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (text.Contains("walk", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("ladder", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("observer", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        try
        {
            var number = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            return number == 7;
        }
        catch
        {
            return null;
        }
    }

    private static bool TrySetMoveType(object target, bool enabled)
    {
        var moveTypeApplied = TrySetMoveTypeMember(target, "MoveType", enabled);
        var actualMoveTypeApplied = TrySetMoveTypeMember(target, "ActualMoveType", enabled);

        if (!moveTypeApplied && !actualMoveTypeApplied)
        {
            return false;
        }

        InvokeNoArg(target, "MoveTypeUpdated");
        InvokeNoArg(target, "ActualMoveTypeUpdated");
        InvokeNoArg(target, "MovementServicesUpdated");
        return true;
    }

    private static bool TrySetTypedMoveType(CCSPlayerPawn pawn, bool enabled)
    {
        var moveType = enabled ? MoveType_t.MOVETYPE_NOCLIP : MoveType_t.MOVETYPE_WALK;
        pawn.MoveType = moveType;
        pawn.ActualMoveType = moveType;
        pawn.MoveTypeUpdated();
        return true;
    }

    private static bool TrySetMoveTypeMember(object target, string memberName, bool enabled)
    {
        return TrySetMember(target, memberName, enabled ? "Noclip" : "Walk") ||
               TrySetMember(target, memberName, enabled ? "NoClip" : "Walk") ||
               TrySetMember(target, memberName, enabled ? "MOVETYPE_NOCLIP" : "MOVETYPE_WALK") ||
               TrySetMember(target, memberName, enabled ? 7 : 2);
    }

    private static object? GetMember(object target, string memberName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        try
        {
            var type = target.GetType();
            var property = type.GetProperty(memberName, flags);
            if (property is not null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(target);
            }

            return type.GetField(memberName, flags)?.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static bool TrySetMember(object target, string memberName, object value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        try
        {
            var type = target.GetType();
            var property = type.GetProperty(memberName, flags);
            if (property is not null && property.CanWrite && property.GetIndexParameters().Length == 0 &&
                TryConvertValue(value, property.PropertyType, out var convertedPropertyValue))
            {
                property.SetValue(target, convertedPropertyValue);
                return true;
            }

            var field = type.GetField(memberName, flags);
            if (field is not null && TryConvertValue(value, field.FieldType, out var convertedFieldValue))
            {
                field.SetValue(target, convertedFieldValue);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool TryConvertValue(object value, Type destinationType, out object converted)
    {
        converted = value;
        try
        {
            if (destinationType.IsInstanceOfType(value))
            {
                return true;
            }

            if (destinationType.IsEnum)
            {
                if (value is string text && Enum.TryParse(destinationType, text, ignoreCase: true, out var parsed))
                {
                    converted = parsed;
                    return true;
                }

                converted = Enum.ToObject(destinationType, Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }

            converted = Convert.ChangeType(value, destinationType, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            converted = value;
            return false;
        }
    }

    private static void InvokeNoArg(object target, string methodName)
    {
        try
        {
            target.GetType()
                .GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null)
                ?.Invoke(target, null);
        }
        catch
        {
        }
    }
}
