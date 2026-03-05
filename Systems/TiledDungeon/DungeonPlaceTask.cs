using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common.Collectible.Block;

namespace Vintagestory.ServerMods;

[ProtoContract]
public class DungeonPlaceTask
{
    [ProtoMember(1)]
    public string Code;

    [ProtoMember(2)]
    public List<TilePlaceTask> TilePlaceTasks;

    [ProtoMember(3)]
    public Cuboidi DungeonBoundaries;

    [ProtoMember(5)]
    public ConnectorMetaData? StairsConnector { get; set; }

    [ProtoMember(6)]
    public List<TilePlaceTask>? SurfacePlaceTasks { get; set; }

    [ProtoMember(7)]
    public string? RockBlockCode { get; set; }

    public List<GeneratedStructure> GeneratedStructures;

    public List<ConnectorMetaData> OpenSet { get; set; }

    public bool IsDirty;

    public DungeonPlaceTask()
    {
    }

    public DungeonPlaceTask(string code, List<TilePlaceTask> tilePlaceTasks, List<GeneratedStructure> generatedStructures, List<ConnectorMetaData> openSet, ConnectorMetaData stairsCon)
    {
        Code = code;
        TilePlaceTasks = tilePlaceTasks;
        GeneratedStructures = generatedStructures;
        OpenSet = openSet;
        StairsConnector = stairsCon;
        IsDirty = true;
        GenBoundaries();
    }

    public DungeonPlaceTask GenBoundaries()
    {
        DungeonBoundaries = new Cuboidi(TilePlaceTasks[0].Pos, TilePlaceTasks[0].Pos);

        foreach (var task in TilePlaceTasks)
        {
            DungeonBoundaries.X1 = Math.Min(DungeonBoundaries.X1, task.Pos.X);
            DungeonBoundaries.Y1 = Math.Min(DungeonBoundaries.Y1, task.Pos.Y);
            DungeonBoundaries.Z1 = Math.Min(DungeonBoundaries.Z1, task.Pos.Z);

            DungeonBoundaries.X2 = Math.Max(DungeonBoundaries.X2, task.Pos.X + task.SizeX);
            DungeonBoundaries.Y2 = Math.Max(DungeonBoundaries.Y2, task.Pos.Y + task.SizeY);
            DungeonBoundaries.Z2 = Math.Max(DungeonBoundaries.Z2, task.Pos.Z + task.SizeZ);
        }

        // ensure the boundaries check for partial generation covers enough space for a decent sized surface room to generate
        var startTask = TilePlaceTasks[0];
        DungeonBoundaries.X1 = Math.Min(DungeonBoundaries.X1, startTask.Pos.X-32);
        DungeonBoundaries.Y1 = Math.Min(DungeonBoundaries.Y1, startTask.Pos.Y);
        DungeonBoundaries.Z1 = Math.Min(DungeonBoundaries.Z1, startTask.Pos.Z-32);

        DungeonBoundaries.X2 = Math.Max(DungeonBoundaries.X2, startTask.Pos.X + startTask.SizeX+32);
        DungeonBoundaries.Y2 = Math.Max(DungeonBoundaries.Y2, startTask.Pos.Y + startTask.SizeY);
        DungeonBoundaries.Z2 = Math.Max(DungeonBoundaries.Z2, startTask.Pos.Z + startTask.SizeZ+32);
        return this;
    }
}
