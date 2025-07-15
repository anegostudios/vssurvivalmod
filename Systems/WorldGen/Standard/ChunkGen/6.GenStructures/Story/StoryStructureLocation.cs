using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.MathTools;

#nullable disable

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
    [ProtoMember(11)]
    public bool DidGenerateAdditional;    // Used for any additional hard-coded generation, e.g. for the Devastation Area, the past (intact) version of the tower

    [ProtoMember(12)]
    public Dictionary<string, int> SchematicsSpawned;
}
