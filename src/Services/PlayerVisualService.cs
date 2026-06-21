using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyBhopTimer.Services;

public sealed class PlayerVisualService
{
    private static readonly Color NormalRenderColor = new(255, 255, 255, 255);
    private static readonly Color HiddenWeaponRenderColor = new(255, 255, 255, 0);
    private static readonly Color HideLegsRenderColor = new(254, 254, 254, 254);
    private const float HiddenViewmodelOffset = -1000.0f;
    private const float DefaultViewmodelOffsetX = 1.0f;
    private const float DefaultViewmodelOffsetY = 1.0f;
    private const float DefaultViewmodelOffsetZ = -1.0f;
    private const float DefaultViewmodelFov = 60.0f;
    private const int DefaultHideFpsRefreshTicks = 0;

    private bool _renderWarningLogged;
    private bool _transmitWarningLogged;
    private bool _viewModelWarningLogged;
    private bool _nativeHideFpsWarningLogged;

    private readonly ISwiftlyCore _core;

    public PlayerVisualService(ISwiftlyCore core)
    {
        _core = core;
    }

    public void ApplySelfVisuals(IPlayer player, PlayerTimerState state)
    {
        ApplyHideLegs(player, state.HideLegs);
    }

    public void ApplyPlayerHiding(IPlayer viewer, PlayerTimerState viewerState, IEnumerable<IPlayer> allPlayers)
    {
        foreach (var other in allPlayers)
        {
            if (other.Slot == viewer.Slot)
            {
                continue;
            }

            var applied = TryApplyTransmitBlock(viewer, other, viewerState.HidePlayers);
            if (!applied && !_transmitWarningLogged)
            {
                _transmitWarningLogged = true;
                Console.WriteLine("[SwiftlyBhopTimer] Could not apply per-player transmit block for !hide. Falling back is not possible without hiding players globally.");
            }
        }
    }

    public void ApplyHideFps(IPlayer player, PlayerTimerState state, int refreshTicks = DefaultHideFpsRefreshTicks)
    {
        ApplyHideFpsNow(player, state);
        if (state.HideFpsViewModel && refreshTicks > 0)
        {
            ScheduleHideFpsRefresh(player, state, refreshTicks);
        }
    }

    public void ResetHideFps(IPlayer player, PlayerTimerState? state = null)
    {
        if (state is not null)
        {
            state.HideFpsViewModel = false;
            ApplyNativeHideFps(player, hide: false, state);
            return;
        }

        ResetHideFpsSlot(player.Slot);
    }

    public void ResetHideFpsSlot(int slot)
    {
        try
        {
            _core.Engine.ExecuteCommand($"sbt_hidefps_toggle {slot} 0");
        }
        catch (Exception ex) when (!_nativeHideFpsWarningLogged)
        {
            _nativeHideFpsWarningLogged = true;
            Console.WriteLine($"[SwiftlyBhopTimer] Could not reset native hidefps helper for slot {slot}: {ex.Message}");
        }
    }

    private void ScheduleHideFpsRefresh(IPlayer player, PlayerTimerState state, int remainingTicks)
    {
        if (remainingTicks <= 0)
        {
            return;
        }

        _core.Scheduler.NextTick(() =>
        {
            ApplyHideFpsNow(player, state);
            ScheduleHideFpsRefresh(player, state, remainingTicks - 1);
        });
    }

    private void ApplyHideFpsNow(IPlayer player, PlayerTimerState state)
    {
        if (ApplyNativeHideFps(player, state.HideFpsViewModel, state))
        {
            return;
        }

        if (!_viewModelWarningLogged)
        {
            _viewModelWarningLogged = true;
            Console.WriteLine("[SwiftlyBhopTimer] Could not apply MetaMod-backed hidefps.");
        }
    }

    private void ApplyHideLegs(IPlayer player, bool hideLegs)
    {
        var pawn = player.PlayerPawn ?? player.Pawn;
        if (pawn is null)
        {
            return;
        }

        try
        {
            if (!SetModelRenderColor(pawn, hideLegs ? HideLegsRenderColor : NormalRenderColor))
            {
                SetRenderColor(pawn, hideLegs ? HideLegsRenderColor : NormalRenderColor);
            }
        }
        catch (Exception ex) when (!_renderWarningLogged)
        {
            _renderWarningLogged = true;
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to apply hidelegs render color: {ex.Message}");
        }
    }

    private static bool TryApplyTransmitBlock(IPlayer viewer, IPlayer other, bool block)
    {
        var applied = false;
        foreach (var entityIndex in GetEntityIndexes(other).Distinct())
        {
            applied |= TrySetViewerTransmitBlock(viewer, entityIndex, block);
        }

        applied |= TrySetEntityTransmitState(other.PlayerPawn ?? other.Pawn, viewer.Slot, !block);
        return applied;
    }

    private static IEnumerable<int> GetEntityIndexes(IPlayer player)
    {
        foreach (var entityIndex in GetDirectEntityIndexes(player))
        {
            yield return entityIndex;
        }

        foreach (var target in new object?[] { player.PlayerPawn, player.Pawn, player.ServerSideClient, player })
        {
            if (TryGetEntityIndex(target, out var entityIndex))
            {
                yield return entityIndex;
            }
        }

        foreach (var weapon in GetWeaponEntities(player))
        {
            if (TryGetEntityIndex(weapon, out var entityIndex))
            {
                yield return entityIndex;
            }
        }
    }

    private static IEnumerable<int> GetDirectEntityIndexes(IPlayer player)
    {
        foreach (var target in new object?[] { player.PlayerPawn, player.Pawn })
        {
            if (target is CEntityInstance entity && entity.Index > 0)
            {
                yield return checked((int)entity.Index);
            }
        }

        foreach (var weapon in GetWeaponModelEntities(player))
        {
            if (weapon.Index > 0)
            {
                yield return checked((int)weapon.Index);
            }
        }
    }

    private static bool TryGetEntityIndex(object? entity, out int entityIndex)
    {
        entityIndex = 0;
        if (entity is null)
        {
            return false;
        }

        foreach (var memberName in new[] { "Index", "EntityIndex", "EntIndex" })
        {
            if (TryGetIntMember(entity, memberName, out entityIndex) && entityIndex > 0)
            {
                return true;
            }
        }

        foreach (var handleName in new[] { "EntityHandle", "RefEHandle", "Handle", "EHandle" })
        {
            var handle = GetMember(entity, handleName);
            if (handle is not null && TryGetEntityIndex(handle, out entityIndex))
            {
                return true;
            }
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn" })
        {
            var nested = GetMember(entity, nestedName);
            if (nested is not null && !ReferenceEquals(nested, entity) && TryGetEntityIndex(nested, out entityIndex))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TrySetViewerTransmitBlock(IPlayer viewer, int entityIndex, bool block)
    {
        try
        {
            viewer.ShouldBlockTransmitEntity(entityIndex, block);
            return true;
        }
        catch
        {
            return TryInvokeMethod(viewer, "ShouldBlockTransmitEntity", [entityIndex, block]);
        }
    }

    private static bool TrySetEntityTransmitState(object? entity, int playerSlot, bool transmit)
    {
        if (entity is null)
        {
            return false;
        }

        if (TryInvokeMethod(entity, "SetTransmitState", [transmit, playerSlot]))
        {
            return true;
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn" })
        {
            var nested = GetMember(entity, nestedName);
            if (nested is not null && !ReferenceEquals(nested, entity) && TrySetEntityTransmitState(nested, playerSlot, transmit))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReplicateClientConvar(IPlayer player, string name, string value)
    {
        try
        {
            var convars = GetMember(player.ServerSideClient, "Convars");
            if (convars is not null && TryInvokeMethod(convars, "ReplicateToClient", [player.Slot, name, value]))
            {
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool ApplyNativeHideFps(IPlayer player, bool hide, PlayerTimerState? state)
    {
        try
        {
            _core.Engine.ExecuteCommand($"sbt_hidefps_toggle {player.Slot} {(hide ? 1 : 0)}");
            if (!hide && state is not null)
            {
                state.OriginalViewmodelOffsetX = null;
                state.OriginalViewmodelOffsetY = null;
                state.OriginalViewmodelOffsetZ = null;
                state.OriginalViewmodelFov = null;
            }

            return true;
        }
        catch (Exception ex) when (!_nativeHideFpsWarningLogged)
        {
            _nativeHideFpsWarningLogged = true;
            Console.WriteLine($"[SwiftlyBhopTimer] Could not call native hidefps helper toggle: {ex.Message}");
            return false;
        }
    }

    private static bool IsHiddenViewmodelValue(float value)
    {
        return value <= HiddenViewmodelOffset * 0.5f;
    }

    private bool ApplyPawnViewModelOffset(IPlayer player, PlayerTimerState state)
    {
        if (player.PlayerPawn is not CCSPlayerPawn pawn)
        {
            return false;
        }

        try
        {
            if (state.HideFpsViewModel)
            {
                state.OriginalViewmodelOffsetX ??= pawn.ViewmodelOffsetX;
                state.OriginalViewmodelOffsetY ??= pawn.ViewmodelOffsetY;
                state.OriginalViewmodelOffsetZ ??= pawn.ViewmodelOffsetZ;
                state.OriginalViewmodelFov ??= pawn.ViewmodelFOV;

                pawn.ViewmodelOffsetX = 0.0f;
                pawn.ViewmodelOffsetY = HiddenViewmodelOffset;
                pawn.ViewmodelOffsetZ = HiddenViewmodelOffset;
                pawn.ViewmodelFOV = 1.0f;
            }
            else
            {
                if (state.OriginalViewmodelOffsetX.HasValue)
                {
                    pawn.ViewmodelOffsetX = state.OriginalViewmodelOffsetX.Value;
                }

                if (state.OriginalViewmodelOffsetY.HasValue)
                {
                    pawn.ViewmodelOffsetY = state.OriginalViewmodelOffsetY.Value;
                }

                if (state.OriginalViewmodelOffsetZ.HasValue)
                {
                    pawn.ViewmodelOffsetZ = state.OriginalViewmodelOffsetZ.Value;
                }

                if (state.OriginalViewmodelFov.HasValue)
                {
                    pawn.ViewmodelFOV = state.OriginalViewmodelFov.Value;
                }

                state.OriginalViewmodelOffsetX = null;
                state.OriginalViewmodelOffsetY = null;
                state.OriginalViewmodelOffsetZ = null;
                state.OriginalViewmodelFov = null;
            }

            pawn.ViewmodelOffsetXUpdated();
            pawn.ViewmodelOffsetYUpdated();
            pawn.ViewmodelOffsetZUpdated();
            pawn.ViewmodelFOVUpdated();
            return true;
        }
        catch (Exception ex) when (!_viewModelWarningLogged)
        {
            _viewModelWarningLogged = true;
            Console.WriteLine($"[SwiftlyBhopTimer] Could not apply pawn viewmodel offset for hidefps: {ex.Message}");
            return false;
        }
    }

    private bool ApplyWeaponVisibility(IPlayer player, bool visible)
    {
        var color = visible ? NormalRenderColor : HiddenWeaponRenderColor;
        var shadowStrength = visible ? 1.0f : 0.0f;
        var applied = false;

        try
        {
            foreach (var weapon in GetWeaponModelEntities(player).DistinctBy(target => RuntimeHelpers.GetHashCode(target)))
            {
                SetModelRenderColor(weapon, color);
                weapon.ShadowStrength = shadowStrength;
                weapon.ShadowStrengthUpdated();
                applied = true;
            }

            foreach (var weapon in GetWeaponEntities(player).DistinctBy(target => RuntimeHelpers.GetHashCode(target)))
            {
                SetRenderColor(weapon, color);
                SetFloatMember(weapon, "ShadowStrength", shadowStrength);
                applied = true;
            }
        }
        catch (Exception ex) when (!_renderWarningLogged)
        {
            _renderWarningLogged = true;
            Console.WriteLine($"[SwiftlyBhopTimer] Failed to apply weapon visibility: {ex.Message}");
        }

        return applied;
    }

    private static IEnumerable<CBasePlayerWeapon> GetWeaponModelEntities(IPlayer player)
    {
        if (player.PlayerPawn is not CCSPlayerPawn pawn)
        {
            yield break;
        }

        var weaponServices = pawn.WeaponServices;
        if (weaponServices is null)
        {
            yield break;
        }

        foreach (var weapon in weaponServices.MyValidWeapons)
        {
            if (weapon is not null)
            {
                yield return weapon;
            }
        }

        var activeWeapon = weaponServices.ActiveWeapon;
        if (activeWeapon.IsValid && activeWeapon.Value is not null)
        {
            yield return activeWeapon.Value;
        }

        var lastWeapon = weaponServices.LastWeapon;
        if (lastWeapon.IsValid && lastWeapon.Value is not null)
        {
            yield return lastWeapon.Value;
        }

        foreach (var weaponHandle in weaponServices.MyWeapons)
        {
            if (weaponHandle.IsValid && weaponHandle.Value is not null)
            {
                yield return weaponHandle.Value;
            }
        }
    }

    private static IEnumerable<object> GetWeaponEntities(IPlayer player)
    {
        foreach (var target in new object?[] { player.PlayerPawn, player.Pawn })
        {
            foreach (var weapon in GetWeaponEntitiesFromPawn(target))
            {
                yield return weapon;
            }
        }
    }

    private static IEnumerable<object> GetWeaponEntitiesFromPawn(object? pawn)
    {
        if (pawn is null)
        {
            yield break;
        }

        var weaponServices = GetMember(pawn, "WeaponServices");
        if (weaponServices is null)
        {
            yield break;
        }

        foreach (var memberName in new[] { "ActiveWeapon", "LastWeapon" })
        {
            var weapon = UnwrapEntity(GetMember(weaponServices, memberName));
            if (weapon is not null)
            {
                yield return weapon;
            }
        }

        var weapons = GetMember(weaponServices, "MyWeapons");
        if (weapons is not System.Collections.IEnumerable enumerable)
        {
            yield break;
        }

        foreach (var item in enumerable)
        {
            var weapon = UnwrapEntity(item);
            if (weapon is not null)
            {
                yield return weapon;
            }
        }
    }

    private static object? UnwrapEntity(object? entity, int depth = 0)
    {
        if (entity is null || depth > 4)
        {
            return entity;
        }

        if (GetMember(entity, "Render") is not null)
        {
            return entity;
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Weapon", "Handle", "EntityHandle" })
        {
            var nested = GetMember(entity, nestedName);
            if (nested is null || ReferenceEquals(nested, entity))
            {
                continue;
            }

            var unwrapped = UnwrapEntity(nested, depth + 1);
            if (unwrapped is not null)
            {
                return unwrapped;
            }
        }

        return entity;
    }

    private static bool TryInvokeMethod(object target, string methodName, object?[] arguments)
    {
        try
        {
            var methods = target.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => string.Equals(method.Name, methodName, StringComparison.OrdinalIgnoreCase));

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length != arguments.Length)
                {
                    continue;
                }

                method.Invoke(target, arguments);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void SetRenderColor(object entity, Color color)
    {
        var applied = false;
        foreach (var target in FindRenderTargets(entity).DistinctBy(target => RuntimeHelpers.GetHashCode(target)))
        {
            if (SetMember(target, "Render", color))
            {
                InvokeNoArg(target, "RenderUpdated");
                InvokeNoArg(target, "ColorUpdated");
                InvokeNoArg(target, "ClrRenderUpdated");
                applied = true;
            }
        }

        if (!applied)
        {
            return;
        }
    }

    private static bool SetModelRenderColor(object entity, Color color)
    {
        if (entity is not CBaseModelEntity modelEntity)
        {
            return false;
        }

        modelEntity.Render = color;
        modelEntity.RenderUpdated();
        return true;
    }

    private static IEnumerable<object> FindRenderTargets(object? entity, int depth = 0)
    {
        if (entity is null || depth > 4)
        {
            yield break;
        }

        if (GetMember(entity, "Render") is not null)
        {
            yield return entity;
        }

        foreach (var nestedName in new[] { "Value", "Entity", "Pawn", "PlayerPawn", "RequiredPawn", "RequiredPlayerPawn", "BaseModelEntity" })
        {
            var nested = GetMember(entity, nestedName);
            if (nested is null || ReferenceEquals(nested, entity))
            {
                continue;
            }

            foreach (var target in FindRenderTargets(nested, depth + 1))
            {
                yield return target;
            }
        }
    }

    private static bool TryGetIntMember(object target, string name, out int result)
    {
        result = 0;
        var raw = GetMember(target, name);
        if (raw is null)
        {
            return false;
        }

        try
        {
            result = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
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

            field.SetValue(target, converted);
            return true;
        }

        return false;
    }

    private static bool SetFloatMember(object target, string name, float value)
    {
        if (!SetMember(target, name, value))
        {
            return false;
        }

        InvokeNoArg(target, $"{name}Updated");
        return true;
    }

    private static object? ConvertValue(object value, Type destinationType)
    {
        try
        {
            if (destinationType.IsEnum)
            {
                return Enum.ToObject(destinationType, Convert.ToInt32(value, CultureInfo.InvariantCulture));
            }

            if (destinationType.IsAssignableFrom(value.GetType()))
            {
                return value;
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
