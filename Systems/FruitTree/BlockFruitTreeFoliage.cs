using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFruitTreeFoliage : BlockFruitTreePart
    {
        Block branchBlock;

        public Dictionary<string, DynFoliageProperties> foliageProps = new Dictionary<string, DynFoliageProperties>();

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(this);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            branchBlock = api.World.GetBlock(AssetLocation.Create(Attributes["branchBlock"].AsString(), Code.Domain));

            var tmpProps = Attributes["foliageProperties"].AsObject<Dictionary<string, DynFoliageProperties>>();

            if (tmpProps.TryGetValue("base", out var baseProps))
            {
                foreach (var val in tmpProps)
                {
                    if (val.Key == "base") continue;
                    val.Value.Rebase(baseProps);
                    foliageProps[val.Key] = val.Value;

                    var texturesBasePath = new AssetLocation(val.Value.TexturesBasePath);

                    if (api is ICoreClientAPI capi)
                    {
                        foreach (var tex in val.Value.Textures.Values)
                        {
                            tex.Base.WithLocationPrefixOnce(texturesBasePath);
                            if (tex.Overlays != null)
                            {
                                foreach (var otex in tex.Overlays)
                                {
                                    otex.WithLocationPrefixOnce(texturesBasePath);
                                }
                            }
                        }

                        val.Value.LeafParticlesTexture?.Base.WithLocationPrefixOnce(texturesBasePath);
                        val.Value.BlossomParticlesTexture?.Base.WithLocationPrefixOnce(texturesBasePath);
                        val.Value.GetOrLoadTexture(capi, "largeleaves-plain");   // preload this so that off-thread building of ChunkMapLayer does not try to load a texture off-thread
                    }
                }
            } else
            {
                foliageProps = tmpProps;
            }
        }


        public override bool ShouldMergeFace(int facingIndex, Block neighbourBlock, int intraChunkIndex3d)
        {
            return (facingIndex == 1 || facingIndex == 2 || facingIndex == 4) && (neighbourBlock == this || neighbourBlock == branchBlock);
        }

        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            var bebranch = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreeFoliage;
            if (bebranch != null)
            {
                return Lang.Get("fruittree-foliage-" + bebranch.TreeType, Lang.Get("foliagestate-" + bebranch.FoliageState.ToString().ToLowerInvariant()));
            }
            return base.GetPlacedBlockName(world, pos);
        }


        public override int GetColor(ICoreClientAPI capi, BlockPos pos)
        {
            int color = 0x989898;   // a basic default color similar to oak leaves

            // Not all treetypes have the standard climatePlantTint, so take the tint from the foliageProps
            BlockEntityFruitTreeFoliage bef = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreeFoliage;
            string climateTint = null;
            string seasonTint = null;

            DynFoliageProperties props;
            if (bef != null && bef.TreeType?.Length > 0)
            {
                props = foliageProps[bef.TreeType];
                climateTint = props.ClimateColorMap;
                seasonTint = props.SeasonColorMap;
                TextureAtlasPosition texPos = bef["largeleaves-plain"];
                if (texPos != null) color = texPos.AvgColor;
            }
            if (climateTint == null) climateTint = "climatePlantTint";
            if (seasonTint == null) seasonTint = "seasonalFoliage";

            int newcol = capi.World.ApplyColorMapOnRgba(climateTint, seasonTint, color, pos.X, pos.Y, pos.Z);
            return newcol;
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
        {
            BlockEntityFruitTreeFoliage bef = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityFruitTreeFoliage;
            string climateTint = null;
            string seasonTint = null;
            int texSubId = 0;

            DynFoliageProperties props;
            if (bef != null && bef.TreeType?.Length > 0)
            {
                props = foliageProps[bef.TreeType];
                climateTint = props.ClimateColorMap;
                seasonTint = props.SeasonColorMap;
                if (props.Textures.TryGetValue("largeleaves-plain", out var ctex))
                {
                    texSubId = ctex.Baked.TextureSubId;
                }
            }
            if (climateTint == null) climateTint = "climatePlantTint";
            if (seasonTint == null) seasonTint = "seasonalFoliage";

            int color = capi.BlockTextureAtlas.GetRandomColor(texSubId, rndIndex);
            color = capi.World.ApplyColorMapOnRgba(climateTint, seasonTint, color, pos.X, pos.Y, pos.Z);

            return color;
        }
    }
}
