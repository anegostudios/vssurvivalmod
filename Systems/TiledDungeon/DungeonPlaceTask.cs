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

    [ProtoMember(4)]
    public int StairsIndex;

    public List<GeneratedStructure> GeneratedStructures;

    public List<ConnectorMetaData> OpenSet { get; set; }

    public DungeonPlaceTask()
    {
    }

    public DungeonPlaceTask(string code, List<TilePlaceTask> tilePlaceTasks, List<GeneratedStructure> generatedStructures, List<ConnectorMetaData> openSet, int stairsIndex)
    {
        Code = code;
        TilePlaceTasks = tilePlaceTasks;
        GeneratedStructures = generatedStructures;
        OpenSet = openSet;
        StairsIndex = stairsIndex;
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

        return this;
    }
}
