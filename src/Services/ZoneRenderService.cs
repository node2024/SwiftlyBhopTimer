using System.Numerics;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using NativeColor = SwiftlyS2.Shared.Natives.Color;
using NativeVector = SwiftlyS2.Shared.Natives.Vector;

namespace SwiftlyBhopTimer.Services;

public sealed class ZoneRenderService
{
    private const float BeamWidth = 1.5f;
    private const float BeamFadeMinDistance = 9999.0f;
    private const string StartColor = "#00ff00";
    private const string EndColor = "#ff0000";

    private readonly ISwiftlyCore _core;
    private readonly List<CBeam> _beams = [];
    private readonly Dictionary<int, List<CBeam>> _previewBeams = [];

    public ZoneRenderService(ISwiftlyCore core)
    {
        _core = core;
    }

    public void Clear()
    {
        ClearBeamList(_beams);
    }

    public void ClearPreview(int playerSlot)
    {
        if (_previewBeams.Remove(playerSlot, out var beams))
        {
            ClearBeamList(beams);
        }
    }

    public void ClearPreviews()
    {
        foreach (var beams in _previewBeams.Values)
        {
            ClearBeamList(beams);
        }

        _previewBeams.Clear();
    }

    public void DrawZones(ActiveMapInfo activeMap)
    {
        Clear();

        if (activeMap.StartZone is not null)
        {
            DrawWireframe(activeMap.StartZone, StartColor, _beams);
        }

        if (activeMap.EndZone is not null)
        {
            DrawWireframe(activeMap.EndZone, EndColor, _beams);
        }

        foreach (var bonus in activeMap.Bonuses.Values.OrderBy(bonus => bonus.Number))
        {
            if (bonus.StartZone is not null)
            {
                DrawWireframe(bonus.StartZone, StartColor, _beams);
            }

            if (bonus.EndZone is not null)
            {
                DrawWireframe(bonus.EndZone, EndColor, _beams);
            }
        }

        Console.WriteLine($"[SwiftlyBhopTimer] Zone rendering complete. Beams={_beams.Count}; Map={activeMap.MapName}");
    }

    public bool HasInvalidBeams()
    {
        return _beams.Count > 0 && _beams.Any(beam => beam is not { IsValid: true });
    }

    public void DrawPreviewZone(int playerSlot, ZoneBounds zone, bool startZone)
    {
        ClearPreview(playerSlot);

        var beams = new List<CBeam>();
        DrawWireframe(zone, startZone ? StartColor : EndColor, beams);
        _previewBeams[playerSlot] = beams;
    }

    private void DrawWireframe(ZoneBounds zone, string hexColor, List<CBeam> target)
    {
        var min = zone.Min;
        var max = zone.Max;

        var c1 = new Vector3(min.X, min.Y, min.Z);
        var c2 = new Vector3(min.X, max.Y, min.Z);
        var c3 = new Vector3(max.X, max.Y, min.Z);
        var c4 = new Vector3(max.X, min.Y, min.Z);
        var c5 = new Vector3(max.X, min.Y, max.Z);
        var c6 = new Vector3(min.X, min.Y, max.Z);
        var c7 = new Vector3(min.X, max.Y, max.Z);
        var c8 = new Vector3(max.X, max.Y, max.Z);

        CreateBeam(c1, c2, hexColor, target);
        CreateBeam(c2, c3, hexColor, target);
        CreateBeam(c3, c4, hexColor, target);
        CreateBeam(c4, c1, hexColor, target);

        CreateBeam(c5, c6, hexColor, target);
        CreateBeam(c6, c7, hexColor, target);
        CreateBeam(c7, c8, hexColor, target);
        CreateBeam(c8, c5, hexColor, target);

        CreateBeam(c1, c6, hexColor, target);
        CreateBeam(c2, c7, hexColor, target);
        CreateBeam(c3, c8, hexColor, target);
        CreateBeam(c4, c5, hexColor, target);
    }

    private void CreateBeam(Vector3 start, Vector3 end, string hexColor, List<CBeam> target)
    {
        var beam = _core.EntitySystem.CreateEntityByDesignerName<CBeam>("beam");
        if (beam is null)
        {
            Console.WriteLine("[SwiftlyBhopTimer] Failed to create beam entity.");
            return;
        }

        beam.DispatchSpawn();

        beam.Render = NativeColor.FromHex(hexColor);
        beam.Width = BeamWidth;
        beam.EndWidth = BeamWidth;
        beam.FadeMinDist = BeamFadeMinDistance;
        beam.TurnedOff = false;

        beam.Teleport(NativeVector.FromBuiltin(start), new QAngle(), NativeVector.Zero);

        var endNative = NativeVector.FromBuiltin(end);
        beam.EndPos.X = endNative.X;
        beam.EndPos.Y = endNative.Y;
        beam.EndPos.Z = endNative.Z;

        beam.RenderUpdated();
        beam.WidthUpdated();
        beam.EndWidthUpdated();
        beam.FadeMinDistUpdated();
        beam.TurnedOffUpdated();
        beam.EndPosUpdated();

        target.Add(beam);
    }

    private static void ClearBeamList(List<CBeam> beams)
    {
        foreach (var beam in beams)
        {
            if (beam is { IsValid: true })
            {
                beam.Despawn();
            }
        }

        beams.Clear();
    }
}
