using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

#nullable disable

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
