using ProtoBuf;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.ServerMods;

[ProtoContract]
public class TilePlaceTask
{
    [ProtoMember(1)]
    public string TileCode;

    [ProtoMember(2)]
    public int Rotation;

    [ProtoMember(3)]
    public BlockPos Pos;

    public int SizeX;
    public int SizeY;
    public int SizeZ;
}
