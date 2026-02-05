using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods;

#nullable disable

namespace Vintagestory.GameContent;

[ProtoContract]
public class StoryStructureLocation : IStructureLocation, IWorldGenArea
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

    // IStructureLocation implementation
    string IStructureLocation.Code => Code;
    Cuboidi IStructureLocation.Location => Location;

    // IWorldGenArea implementation
    string IWorldGenArea.Code => Code;
    Cuboidi IWorldGenArea.Location => Location;
    BlockPos IWorldGenArea.CenterPos => CenterPos;
    int IWorldGenArea.LandformRadius => LandformRadius;
    int IWorldGenArea.GenerationRadius => GenerationRadius;
    Dictionary<int, int> IWorldGenArea.SkipGenerationFlags => SkipGenerationFlags;
    public int MaxSkipGenerationRadiusSq { get; set; }


    public StoryStructureLocation Clone()
    {
        return new StoryStructureLocation()
        {
            Code = Code,
            CenterPos = CenterPos,
            DidGenerate = DidGenerate,
            Location = Location,
            LandformRadius = LandformRadius,
            GenerationRadius = GenerationRadius,
            DirX = DirX,
            WorldgenHeight = WorldgenHeight,
            RockBlockCode = RockBlockCode,
            SkipGenerationFlags = SkipGenerationFlags,
            DidGenerateAdditional = DidGenerateAdditional,
            SchematicsSpawned = SchematicsSpawned
        };
    }
}
