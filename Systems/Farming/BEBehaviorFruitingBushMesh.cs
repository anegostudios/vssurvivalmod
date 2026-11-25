using System;
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
            if (stemOnly && textureCode.StartsWith("flowerberrysteminside"))
            {
                string suffix = textureCode.Substring("flowerberrysteminside".Length);
                string insideCode = "steminside" + suffix;
                textureCode = textureMapping.ContainsKey(insideCode) ? insideCode : "stemcenter" + suffix;
            }
            if (stemOnly && textureCode.StartsWith("flowerberrystemcenter"))
            {
                textureCode = "stemcenter" + textureCode.Substring("flowerberrystemcenter".Length);
            }

            textureMapping.TryGetValue(textureCode, out var compositeTexture);
            if (compositeTexture == null)
            {
                capi.Logger.Warning("Texture not found: {0} in bush: {1}", textureCode, Block.Code);
                return capi.BlockTextureAtlas.UnknownTexturePosition;
            }
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
        ensureMeshExists(this);
        if (Block.RandomizeRotations)
        {
            int randomSelector = GameMath.MurmurHash3Mod(Pos.X, Block.RandomizeAxes == EnumRandomizeAxes.XYZ ? Pos.Y : 0, Pos.Z, TesselationMetaData.randomRotations.Length);
            mesher.AddMeshData(bushMesh, TesselationMetaData.randomRotMatrices[randomSelector]);
        }
        else
        {
            mesher.AddMeshData(bushMesh);
        }
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

    public void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
    {
        ensureMeshExists(this);
        blockModelData = getOrCreateMesh(this, false);
        decalModelData = getOrCreateMesh(decalTexSource, false, "decal-");
    }

    protected virtual string meshCacheKey => Block.Code + "-" + healthAndBerryState + "-" + randomVariant;

    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(bushMesh))]
    protected void ensureMeshExists(ITexPositionSource texSource)
    {
        if (this.bushMesh != null) return;
        this.bushMesh = getOrCreateMesh(texSource, true);
    }

    bool stemOnly;
    private MeshData getOrCreateMesh(ITexPositionSource texSource, bool cache, string cacheKeyPrefix = "")
    {
        bool hasBerriesOrFlowers = false;
        bool isYoung = false;
        string berryStage = "stem";
        string healthState = healthAndBerryState = Blockentity.GetBehavior<BEBehaviorFruitingBush>().GetHealthState().ToString().ToLowerInvariant();
        if (healthState == "barren")
        {
            hasBerriesOrFlowers = true;
            healthAndBerryState = "empty";
        }

        switch (BState.Growthstate)
        {
            case EnumFruitingBushGrowthState.Flowering:
                hasBerriesOrFlowers = true;
                berryStage = "flowering";
                if (healthAndBerryState != "empty") healthAndBerryState += "-" + berryStage;
                break;
            case EnumFruitingBushGrowthState.Ripening:
                hasBerriesOrFlowers = true;
                berryStage = "unripe";
                if (healthAndBerryState != "empty") healthAndBerryState += "-" + berryStage;
                break;
            case EnumFruitingBushGrowthState.Ripe:
                hasBerriesOrFlowers = true;
                berryStage = "ripe";
                if (healthAndBerryState != "empty") healthAndBerryState += "-" + berryStage;
                break;
            case EnumFruitingBushGrowthState.Mature:
                healthAndBerryState = "empty";
                break;
            case EnumFruitingBushGrowthState.Dormant:
                healthAndBerryState = "dormant";
                break;
            case EnumFruitingBushGrowthState.Young:
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
        stemOnly = berryStage == "stem";
        if (!cache)
        {
            return createMesh(texSource, hasBerriesOrFlowers, isYoung, berryStage, healthState, textureVariant, cshape);
        }

        string cacheKey = cacheKeyPrefix + meshCacheKey + "-" + BState.Growthstate + "-" + healthState;
        return ObjectCacheUtil.GetOrCreate(capi, cacheKey, () =>
        {
            return createMesh(texSource, hasBerriesOrFlowers, isYoung, berryStage, healthState, textureVariant, cshape);
        });
    }

    public static MeshData CreateRipeMeshForInventory(ICoreClientAPI capi, Block block)
    {
        var cshape = block.Shape;
        var shape = capi.Assets.Get<Shape>(cshape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));
        var texSource = new ShapeTextureSource(capi, shape, block.Code.ToString())
        {
            textures = createTextureMapping(capi, block, false, "ripe", "healthy", true, 0)
        };

        capi.Tesselator.TesselateShape("ripe mesh", shape, out var mesh, texSource, cshape.RotateXYZCopy);

        if (block.ClimateColorMap != null)
        {
            int tint = capi.World.ApplyColorMapOnRgba(block.ClimateColorMap, null, ColorUtil.WhiteArgb, 180, 138, false);
            byte[] tintBytes = ColorUtil.ToBGRABytes(tint);
            for (int face = 0; face < mesh.ColorMapIdsCount; face++)
            {
                if (mesh.ClimateColorMapIds[face] == 0) continue;

                int faceIndex = face * 4 * 4;
                for (int v = 0; v < 4; v++)
                {
                    int colorIndex = faceIndex + v * 4;
                    for (int channel = 0; channel < 3; channel++)
                    {
                        mesh.Rgba[colorIndex + channel] = (byte)(mesh.Rgba[colorIndex + channel] * tintBytes[channel] / 255);
                    }
                }
            }
        }

        return mesh;
    }


    private MeshData createMesh(ITexPositionSource texSource, bool hasBerriesOrFlowers, bool isYoung, string berryStage, string healthState, int textureVariant, CompositeShape cshape)
    {
        string[]? ignoreElements = null;
        textureMapping = createTextureMapping(capi, Block, isYoung, berryStage, healthState, hasBerriesOrFlowers, textureVariant);

        var loc = cshape.Base.Clone();
        var shape = capi.Assets.Get<Shape>(loc.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json"));

        if (BState.Growthstate == EnumFruitingBushGrowthState.Dormant && Block.Variant["type"] != "strawberry") ignoreElements = ignoreElements.Append(["Leaves/*"]);

        capi.Tesselator.TesselateShape(new TesselationMetaData()
        {
            TexSource = texSource,
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
    }

    protected static Dictionary<string, CompositeTexture> resolveTextureMapping(JsonObject mapping, JsonObject templates)
    {
        var textures = new Dictionary<string, CompositeTexture>();
        foreach (var entry in mapping)
        {
            string key = entry.AsString();
            var texture = mapping[key];
            string templateName = texture["template"].AsString();
            if (templateName != null)
            {
                if (!templates[templateName].Exists)
                {
                    throw new ArgumentException($"Unknown texture template '{templateName}' for texture '{key}'.");
                }
                string path = texture["path"].AsString();
                if (path == null)
                {
                    throw new ArgumentException($"Missing path for texture '{key}' using template '{templateName}'.");
                }
                texture = templates[templateName].Clone();
                texture.FillPlaceHolder("path", path);
            }
            textures[key] = texture.AsObject<CompositeTexture>();
        }
        return textures;
    }


    private static Dictionary<string, CompositeTexture> createTextureMapping(ICoreClientAPI capi, Block block, bool isYoung, string berryStage, string healthState, bool hasBerriesOrFlowers, int textureVariant)
    {
        var textures = resolveTextureMapping(block.Attributes[isYoung ? "youngTextureMapping" : "textureMapping"], block.Attributes["textureTemplates"]);

        foreach ((string key, CompositeTexture ct) in textures)
        {
            var compositeTexture = ct;

            compositeTexture.Base.Path = compositeTexture.Base.Path
                    .Replace("{berrystage}", berryStage)
                    .Replace("{healthstate}", healthState)
                    .Replace("{stemorberries}", hasBerriesOrFlowers ? "berries" : "stem")
                ;

            if (compositeTexture.Alternates != null)
            {
                foreach (CompositeTexture alternate in compositeTexture.Alternates)
                {
                    if (berryStage == "stem")
                    {
                        alternate.Base.Path = alternate.Base.Path.Replace("{healthstate}/", "");
                    }
                    alternate.Base.Path = alternate.Base.Path
                            .Replace("{berrystage}", berryStage)
                            .Replace("{healthstate}", healthState)
                            .Replace("{stemorberries}", hasBerriesOrFlowers ? "berries" : "stem")
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
                    textures[key] = compositeTexture.Alternates[id - 1];
                }
            }

        }

        return textures;
    }
}
