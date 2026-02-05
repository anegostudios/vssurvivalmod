using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent;

public class BEBehaviorFruitingBushMesh : BlockEntityBehavior, ITexPositionSource
{
    protected ICoreClientAPI capi;
    public Size2i AtlasSize => capi.BlockTextureAtlas.Size;
    public TextureAtlasPosition this[string textureCode]
    {
        get
        {
            var texturePath = textureMapping[textureCode]
                .Replace("{type}", Block.Variant["type"])
                .Replace("{variant}", textureVariant)
                .Replace("{berrystage}", berryStage)
                .Replace("{healthstate}", healthState)
            ;
            var loc = AssetLocation.Create(texturePath, Block.Code.Domain);
            capi.BlockTextureAtlas.GetOrInsertTexture(loc, out _, out var texPos);
            return texPos;
        }
    }

    public BerrybushState BState => Blockentity.GetBehavior<BEBehaviorFruitingBush>().BState;
    protected string textureVariant;
    protected string berryStage;
    protected string healthState;

    public BEBehaviorFruitingBushMesh(BlockEntity blockentity) : base(blockentity)
    {
        
    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        capi = api as ICoreClientAPI;
    }

    MeshData? bushMesh;
    Dictionary<string, string>? textureMapping;

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        ensureMeshExists();
        mesher.AddMeshData(bushMesh);
        return true;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        BState.Growthstate = (EnumBushGrowthState)(1 + GameMath.MurmurHash3Mod(Pos.X, Pos.Y+1, Pos.Z, 5));
        BState.WildBushState = (EnumBushHealthState)(GameMath.MurmurHash3Mod(Pos.X, Pos.Y + 1, Pos.Z, 4));

        bushMesh = null;
        Blockentity.MarkDirty(true);
    }

    protected virtual string meshCacheKey => Block.Code + "-" + BState.Growthstate + "-" + textureVariant;
    protected void ensureMeshExists()
    {
        if (bushMesh != null) return;
        textureVariant = "" + (1+GameMath.MurmurHash3Mod(Pos.X, Pos.Y, Pos.Z, Block.Attributes["textureVariants"].AsInt()));
        healthState = Blockentity.GetBehavior< BEBehaviorFruitingBush>().GetHealthState().ToString().ToLowerInvariant();

        bushMesh = ObjectCacheUtil.GetOrCreate(capi, meshCacheKey, () =>
        {
            string[]? ignoreElements = null;

            switch (BState.Growthstate)
            {
                case EnumBushGrowthState.Flowering: berryStage = "flowering"; break;
                case EnumBushGrowthState.Ripening: berryStage = "unripe"; break;
                case EnumBushGrowthState.Ripe: berryStage = "ripe"; break;
                default: ignoreElements = ["Berries/*"]; break;
            }
            textureMapping = Block.Attributes["textureMapping"].AsObject<Dictionary<string, string>>();

            var loc = Block.Shape.Base;
            var shape = capi.Assets.Get<Shape>(loc.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));

            
            if (BState.Growthstate == EnumBushGrowthState.Dormant) ignoreElements = ignoreElements.Append(["Leaves/*"]);

            capi.Tesselator.TesselateShape(new TesselationMetaData()
            {
                TexSource = this,
                UsesColorMap = true,
                IgnoreElements = ignoreElements
            }, shape, out var bushMesh);

            return bushMesh;
        });
    }




}
