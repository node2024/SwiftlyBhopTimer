using System.Globalization;
using System.Reflection;
using SwiftlyS2.Shared.Misc;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;

namespace SwiftlyBhopTimer.Services;

public sealed class PlayerProtectionService
{
    private const CollisionGroup PlayerNoCollisionGroup = CollisionGroup.Debris;
    private bool _collisionWarningLogged;

    public bool DisableCollision { get; set; } = true;
    public bool DisableDamage { get; set; } = true;

    public void ApplyCollision(IPlayer player)
    {
        if (!DisableCollision)
        {
            return;
        }

        var pawn = player.PlayerPawn ?? player.Pawn;
        if (pawn is null)
        {
            return;
        }

        try
        {
            var changed = SetCollisionGroup(pawn, PlayerNoCollisionGroup);
            if (changed)
            {
                InvokeNoArg(pawn, "CollisionUpdated");
                InvokeNoArg(pawn, "CollisionAttributeUpdated");
                InvokeNoArg(pawn, "CollisionRulesChanged");
                InvokeNoArg(pawn, "NetworkStateChanged");
                InvokeNoArg(pawn, "StateChanged");
            }
        }
        catch (Exception ex) when (!_collisionWarningLogged)
        {
            _collisionWarningLogged = true;
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to disable player collision: {ex.Message}");
        }
    }

    public HookResult HandleDamage(object? targetEntity)
    {
        if (!DisableDamage || targetEntity is null)
        {
            return HookResult.Continue;
        }

        return IsPlayerEntity(targetEntity) ? HookResult.CancelOriginal : HookResult.Continue;
    }

    private static bool SetCollisionGroup(object entity, CollisionGroup group)
    {
        var changed = false;
        var collision = GetMember(entity, "Collision");
        if (collision is not null)
        {
            var collisionChanged = SetMember(collision, "CollisionGroup", group);
            changed |= collisionChanged;
            if (collisionChanged)
            {
                InvokeNoArg(collision, "CollisionGroupUpdated");
                InvokeNoArg(collision, "CollisionRulesChanged");
                InvokeNoArg(collision, "NetworkStateChanged");
                InvokeNoArg(collision, "StateChanged");
            }

            var collisionAttribute = GetMember(collision, "CollisionAttribute");
            if (collisionAttribute is not null)
            {
                var attributeChanged = SetMember(collisionAttribute, "CollisionGroup", group);
                changed |= attributeChanged;
                if (attributeChanged)
                {
                    InvokeNoArg(collisionAttribute, "CollisionGroupUpdated");
                    InvokeNoArg(collisionAttribute, "CollisionRulesChanged");
                    InvokeNoArg(collisionAttribute, "NetworkStateChanged");
                    InvokeNoArg(collisionAttribute, "StateChanged");
                }
            }
        }

        return changed;
    }

    private static bool IsPlayerEntity(object entity)
    {
        var designerName = EntityReflection.GetDesignerName(entity);
        if (string.Equals(designerName, "player", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn" })
        {
            var nested = GetMember(entity, nestedName);
            if (nested is not null && !ReferenceEquals(nested, entity) && IsPlayerEntity(nested))
            {
                return true;
            }
        }

        return false;
    }

    private static object? GetMember(object target, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = target.GetType();
        try
        {
            var property = type.GetProperty(name, flags);
            if (property is not null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(target);
            }

            return type.GetField(name, flags)?.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static bool SetMember(object target, string name, object value)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = target.GetType();

        var property = type.GetProperty(name, flags);
        if (property is not null && property.CanWrite)
        {
            var converted = ConvertValue(value, property.PropertyType);
            if (converted is null)
            {
                return false;
            }

            if (ValuesEqual(property.GetValue(target), converted))
            {
                return false;
            }

            property.SetValue(target, converted);
            InvokeNoArg(target, $"{name}Updated");
            return true;
        }

        var field = type.GetField(name, flags);
        if (field is not null)
        {
            var converted = ConvertValue(value, field.FieldType);
            if (converted is null)
            {
                return false;
            }

            if (ValuesEqual(field.GetValue(target), converted))
            {
                return false;
            }

            field.SetValue(target, converted);
            return true;
        }

        return false;
    }

    private static bool ValuesEqual(object? current, object converted)
    {
        if (current is null)
        {
            return false;
        }

        if (Equals(current, converted))
        {
            return true;
        }

        try
        {
            return Convert.ToInt32(current, CultureInfo.InvariantCulture) ==
                   Convert.ToInt32(converted, CultureInfo.InvariantCulture);
        }
        catch
        {
            return false;
        }
    }

    private static object? ConvertValue(object value, Type destinationType)
    {
        try
        {
            if (destinationType.IsEnum)
            {
                return Enum.ToObject(destinationType, Convert.ToInt32(value, CultureInfo.InvariantCulture));
            }

            return Convert.ChangeType(value, destinationType, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static void InvokeNoArg(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, types: Type.EmptyTypes, modifiers: null)
            ?.Invoke(target, null);
    }
}
