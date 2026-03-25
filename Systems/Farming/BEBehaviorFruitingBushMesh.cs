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
            textureMapping.TryGetValue(textureCode, out var compositeTexture);
            capi.BlockTextureAtlas.GetOrInsertTexture(compositeTexture.Base, out _, out var texPos);
            return texPos;
        }
    }

    public FruitingBushState BState => Blockentity.GetBehavior<BEBehaviorFruitingBush>().BState;
    protected string randomVariant;
    protected string healthAndBerryState;

    public BEBehaviorFruitingBushMesh(BlockEntity blockentity) : base(blockentity)
    {

    }

    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        capi = api as ICoreClientAPI;
    }

    MeshData? bushMesh;
    Dictionary<string, CompositeTexture>? textureMapping;

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator)
    {
        ensureMeshExists();
        mesher.AddMeshData(bushMesh);
        return true;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
    {
        base.FromTreeAttributes(tree, worldAccessForResolve);

        if (BState.MeshDirty)
        {
            BState.MeshDirty = false;
            bushMesh = null;
            Blockentity.MarkDirty(true);
        }
    }

    protected virtual string meshCacheKey => Block.Code + "-" + healthAndBerryState + "-" + randomVariant;
    protected void ensureMeshExists()
    {
        if (this.bushMesh != null) return;

        bool hideBerries = false;
        bool isYoung = false;
        string berryStage = "";
        string healthState = healthAndBerryState = Blockentity.GetBehavior<BEBehaviorFruitingBush>().GetHealthState().ToString().ToLowerInvariant();
        if (healthState == "barren")
        {
            hideBerries = true;
            healthAndBerryState = "empty";
        }

        switch (BState.Growthstate)
        {
            case EnumFruitingBushGrowthState.Flowering:
                berryStage = "flowering";
                if (healthAndBerryState != "empty") healthAndBerryState += "-" + berryStage;
                break;
            case EnumFruitingBushGrowthState.Ripening:
                berryStage = "unripe";
                if (healthAndBerryState != "empty") healthAndBerryState += "-" + berryStage;
                break;
            case EnumFruitingBushGrowthState.Ripe:
                berryStage = "ripe";
                if (healthAndBerryState != "empty") healthAndBerryState += "-" + berryStage;
                break;
            case EnumFruitingBushGrowthState.Mature:
                hideBerries = true;
                healthAndBerryState = "empty";
                break;
            case EnumFruitingBushGrowthState.Dormant:
                hideBerries = true;
                healthAndBerryState = "dormant";
                break;
            case EnumFruitingBushGrowthState.Young:
                hideBerries = true;
                isYoung = true;
                healthAndBerryState = "young"; break;
        }

        int posHash = GameMath.MurmurHash3(Pos.X, Pos.Y, Pos.Z);
        int textureVariant = GameMath.Mod(posHash, Block.Attributes[isYoung ? "youngTextureVariants" : "textureVariants"].AsInt(1));
        randomVariant = "" + textureVariant;

        var cshape = Block.Shape;
        if (isYoung)
        {
            cshape = Block.Attributes["youngShape"].AsObject<CompositeShape>();
            cshape.LoadAlternates(capi.Assets, capi.Logger);
        }

        if (cshape.BakedAlternates != null)
        {
            int variant = GameMath.Mod(posHash, cshape.BakedAlternates.Length);
            randomVariant += "-" + variant;
            cshape = cshape.BakedAlternates[variant];
        }

        bushMesh = ObjectCacheUtil.GetOrCreate(capi, meshCacheKey, () =>
        {
            string[]? ignoreElements = null;

            if (hideBerries) ignoreElements = ["Berries/*"];

            textureMapping = Block.Attributes[isYoung ? "youngTextureMapping" : "textureMapping"].AsObject<Dictionary<string, CompositeTexture>>();

            foreach ((string key, var compositeTexture) in textureMapping)
            {
                compositeTexture.Base.Path = compositeTexture.Base.Path
                        .Replace("{berrystage}", berryStage)
                        .Replace("{healthstate}", healthState)
                    ;

                if (compositeTexture.Alternates != null)
                {
                    foreach (CompositeTexture alternate in compositeTexture.Alternates)
                    {
                        alternate.Base.Path = alternate.Base.Path
                                .Replace("{berrystage}", berryStage)
                                .Replace("{healthstate}", healthState)
                            ;
                    }
                }

                CompositeTexture.Bake(capi.Assets, compositeTexture);
                int alternatesLength = compositeTexture.Alternates?.Length ?? 0;
                if (alternatesLength > 0)
                {
                    int id = GameMath.Mod(textureVariant, alternatesLength + 1);
                    if (id > 0)
                    {
                        textureMapping[key] = compositeTexture.Alternates[id - 1];
                    }
                }
            }

            var loc = cshape.Base.Clone();
            var shape = capi.Assets.Get<Shape>(loc.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));

            if (BState.Growthstate == EnumFruitingBushGrowthState.Dormant && Block.Variant["type"] != "strawberry") ignoreElements = ignoreElements.Append(["Leaves/*"]);

            capi.Tesselator.TesselateShape(new TesselationMetaData()
            {
                TexSource = this,
                UsesColorMap = true,
                IgnoreElements = ignoreElements,
                Rotation = cshape.RotateXYZCopy
            }, shape, out var bushMesh);

            for (int i = 0; i < bushMesh.ColorMapIdsCount; i++)
            {
                if (bushMesh.ClimateColorMapIds[i] > 0) bushMesh.ClimateColorMapIds[i] = (byte)(Block.ClimateColorMapResolved.RectIndex + 1);
                if (bushMesh.SeasonColorMapIds[i] > 0) bushMesh.SeasonColorMapIds[i] = (byte)(Block.SeasonColorMapResolved.RectIndex + 1);
            }

            return bushMesh;
        });
    }
}
