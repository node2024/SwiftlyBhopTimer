using SwiftlyS2.Shared;

namespace SwiftlyBhopTimer.Services;

public sealed class MapExecService
{
    private readonly ISwiftlyCore _core;
    private readonly string _mapDataDirectory;
    private bool _executeWarningLogged;

    public MapExecService(ISwiftlyCore core, string mapDataDirectory)
    {
        _core = core;
        _mapDataDirectory = mapDataDirectory;
    }

    public string? ExecuteForMap(string mapName)
    {
        var cfgPath = FindMapExecPath(mapName);
        if (cfgPath is null)
        {
            return null;
        }

        ExecutePath(cfgPath);
        return cfgPath;
    }

    public string? ExecuteFile(string fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return null;
        }

        var cfgPath = Path.Combine(_mapDataDirectory, "MapExecs", $"{fileNameWithoutExtension}.cfg");
        if (!File.Exists(cfgPath))
        {
            return null;
        }

        ExecutePath(cfgPath);
        return cfgPath;
    }

    private void ExecutePath(string cfgPath)
    {
        var relative = Path.GetRelativePath(_core.CSGODirectory, cfgPath).Replace('\\', '/');
        if (relative.StartsWith("cfg/", StringComparison.OrdinalIgnoreCase))
        {
            relative = relative["cfg/".Length..];
        }

        var withoutExtension = relative.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase)
            ? relative[..^4]
            : relative;

        Execute($"exec {withoutExtension}");
    }

    private string? FindMapExecPath(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return null;
        }

        var mapExecDirectory = Path.Combine(_mapDataDirectory, "MapExecs");
        if (!Directory.Exists(mapExecDirectory))
        {
            return null;
        }

        var exact = Path.Combine(mapExecDirectory, $"{mapName}.cfg");
        if (File.Exists(exact))
        {
            return exact;
        }

        return Directory.EnumerateFiles(mapExecDirectory, "*.cfg")
            .Select(path => new
            {
                Path = path,
                Prefix = Path.GetFileNameWithoutExtension(path)
            })
            .Where(item => item.Prefix.EndsWith('_') &&
                           mapName.StartsWith(item.Prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Prefix.Length)
            .Select(item => item.Path)
            .FirstOrDefault();
    }

    private void Execute(string command)
    {
        try
        {
            _core.Engine.ExecuteCommand(command);
        }
        catch (Exception ex)
        {
            if (!_executeWarningLogged)
            {
                _executeWarningLogged = true;
                Console.WriteLine($"[SwiftlyBhopTimer] Failed to execute map cfg command '{command}': {ex.Message}");
            }
        }
    }
}
