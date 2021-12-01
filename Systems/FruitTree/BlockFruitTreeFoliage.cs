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

                    foreach (var tex in val.Value.Textures.Values)
                    {
                        tex.Base.WithPathPrefixOnce(val.Value.TexturesBasePath);
                        if (tex.Overlays != null)
                        {
                            foreach (var otex in tex.Overlays)
                            {
                                otex.WithPathPrefixOnce(val.Value.TexturesBasePath);
                            }
                        }
                    }

                    val.Value.LeafParticlesTexture?.Base.WithPathPrefixOnce(val.Value.TexturesBasePath);
                    val.Value.BlossomParticlesTexture?.Base.WithPathPrefixOnce(val.Value.TexturesBasePath);
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


    }
}
