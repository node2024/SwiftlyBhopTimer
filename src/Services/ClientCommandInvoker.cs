using System.Reflection;
using SwiftlyS2.Shared.Players;

namespace SwiftlyBhopTimer.Services;

public static class ClientCommandInvoker
{
    private static readonly string[] ClientCommandMethodNames =
    {
        "ExecuteClientCommand",
        "ClientCommand",
        "SendClientCommand",
        "SendCommand",
        "ExecuteCommand",
        "ProcessStringCmd",
        "SendStringCmd",
        "Command"
    };

    public static bool TryExecute(IPlayer player, string command)
    {
        foreach (var target in GetClientCommandTargets(player))
        {
            if (target is null)
            {
                continue;
            }

            if (TryInvokeClientCommand(target, command))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<object?> GetClientCommandTargets(IPlayer player)
    {
        yield return player;
        yield return player.ServerSideClient;
        yield return GetPropertyValue(player, "Controller");
        yield return GetPropertyValue(player, "PlayerController");
        yield return GetPropertyValue(player, "Pawn");
        yield return GetPropertyValue(player, "PlayerPawn");
    }

    private static object? GetPropertyValue(object target, string propertyName)
    {
        try
        {
            return target.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryInvokeClientCommand(object target, string command)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var methods = target.GetType().GetMethods(flags)
            .Where(method => ClientCommandMethodNames.Contains(method.Name, StringComparer.OrdinalIgnoreCase));

        foreach (var method in methods)
        {
            if (TryInvokeMethod(method, target, command))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryInvokeMethod(MethodInfo method, object target, string command)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
        {
            return TryInvoke(method, target, new object?[] { command });
        }

        if (parameters.Length == 2 &&
            parameters[0].ParameterType == typeof(string) &&
            parameters[1].ParameterType == typeof(bool))
        {
            return TryInvoke(method, target, new object?[] { command, false });
        }

        return false;
    }

    private static bool TryInvoke(MethodInfo method, object target, object?[] arguments)
    {
        try
        {
            var result = method.Invoke(target, arguments);
            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
