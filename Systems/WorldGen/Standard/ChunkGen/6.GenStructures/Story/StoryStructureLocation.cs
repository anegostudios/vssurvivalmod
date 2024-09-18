using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent;

[ProtoContract]
public class StoryStructureLocation
{
    [ProtoMember(1)]
    public string Code;
    [ProtoMember(2)]
    public BlockPos CenterPos;
    [ProtoMember(3)]
    public bool DidGenerate;
    [ProtoMember(4)]
    public Cuboidi Location;
    [ProtoMember(5)]
    public int LandformRadius;
    [ProtoMember(6)]
    public int GenerationRadius;
    [ProtoMember(7)]
    public int DirX;
    [ProtoMember(8)]
    public int WorldgenHeight = -1;
    [ProtoMember(9)]
    public string RockBlockCode;
    [ProtoMember(10)]
    public Dictionary<int, int> SkipGenerationFlags;
}
