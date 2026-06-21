using SwiftlyS2.Shared;

namespace SwiftlyBhopTimer.Services;

public sealed record SwiftlyBhopTimerPaths(
    string DataDirectory,
    string PlayerRecordsDirectory,
    string PlayerStageDataDirectory,
    string MapDataDirectory,
    string BundledMapDataDirectory,
    string BundledMapChooserMapsPath,
    string GeneratedConfigPath,
    string MapChooserConfigPath,
    string MapChooserMapListPath,
    string ChatConfigPath,
    string AdvertisingConfigPath,
    string LegacyPlayerSettingsPath)
{
    public static SwiftlyBhopTimerPaths FromCore(ISwiftlyCore core)
    {
        var configRoot = Path.Combine(core.CSGODirectory, "cfg", "SwiftlyBhopTimer");
        var assemblyDirectory = Path.GetDirectoryName(typeof(SwiftlyBhopTimerPaths).Assembly.Location)
            ?? AppContext.BaseDirectory;
        var bundledRoot = Path.Combine(assemblyDirectory, "resources", "SwiftlyBhopTimer");

        return new SwiftlyBhopTimerPaths(
            configRoot,
            Path.Combine(configRoot, "PlayerRecords"),
            Path.Combine(configRoot, "PlayerStageData"),
            Path.Combine(configRoot, "MapData"),
            Path.Combine(bundledRoot, "MapData"),
            Path.Combine(bundledRoot, "MapChooser", "maps.json"),
            Path.Combine(configRoot, "SwiftlyBhopTimer.cfg"),
            Path.Combine(configRoot, "SwiftlyBhopTimer.MapChooser.json"),
            Path.Combine(configRoot, "SwiftlyBhopTimer.MapChooser.Maps.json"),
            Path.Combine(configRoot, "SwiftlyBhopTimer.Chat.json"),
            Path.Combine(configRoot, "SwiftlyBhopTimer.Advertising.json"),
            Path.Combine(configRoot, "SwiftlyBhopTimer.PlayerSettings.json"));
    }
}
