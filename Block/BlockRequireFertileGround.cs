using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockRequireFertileGround : Block
    {
        public virtual bool skipPlantCheck { get; set; } = false;
        protected bool disappearOnSoilRemoved = false;

        protected bool isWaterPlant = false;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            disappearOnSoilRemoved = Attributes?["disappearOnSoilRemoved"].AsBool(false) ?? false;
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (skipPlantCheck)
            {
                return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            }

            if (Variant.ContainsKey("side"))
            {
                return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            }

            if (CanPlantStay(world.BlockAccessor, blockSel.Position))
            {
                return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            }

            failureCode = "requirefertileground";

            return false;
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!skipPlantCheck && !CanPlantStay(world.BlockAccessor, pos))
            {
                if (world.BlockAccessor.GetBlock(pos.DownCopy()).Id == 0 && disappearOnSoilRemoved) world.BlockAccessor.SetBlock(0, pos);
                else world.BlockAccessor.BreakBlock(pos, null);
            }
            base.OnNeighbourBlockChange(world, pos, neibpos);
        }

        public virtual bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (Variant.ContainsKey("side"))
            {
                var facing = BlockFacing.FromCode(Variant["side"]);

                var npos = pos.AddCopy(facing);
                var block = blockAccessor.GetBlock(npos);
                return block.CanAttachBlockAt(blockAccessor, this, npos, facing.Opposite);
            }
            else
            {
                Block blockBelow = blockAccessor.GetBlockBelow(pos);
                if (blockBelow.Fertility <= 0) return false;
                return true;
            }
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, BlockPatchAttributes attributes = null)
        {
            if (!CanPlantStay(blockAccessor, pos)) return false;

            if (isWaterPlant)
            {
                var canPlace = true;
                var tmpPos = pos.Copy();
                for (int x = -1; x < 2; x++)
                {
                    for (int z = -1; z < 2; z++)
                    {
                        tmpPos.Set(pos.X + x, pos.Y, pos.Z + z);
                        var block = blockAccessor.GetBlock(tmpPos, BlockLayersAccess.Solid);
                        if (block is BlockWaterLilyGiant)
                        {
                            canPlace = false;
                        }
                    }
                }
                if (!canPlace) return false;
            } else
            {
                if (blockAccessor.GetBlock(pos, BlockLayersAccess.Fluid).Id != 0) return false;
            }

            return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldGenRand, attributes);
        }

    }
}
