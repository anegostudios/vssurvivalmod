using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockWaterPlant : BlockPlant
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            Block blockToPlace = this;
            Block block = world.BlockAccessor.GetBlock(blockSel.Position, BlockLayersAccess.Fluid);
            bool inWater = block.IsLiquid() && block.LiquidLevel == 7 && block.LiquidCode.Contains("water");

            if (inWater)
            {
                blockToPlace = world.GetBlock(CodeWithParts("water"));    // may be unnecessary, but let's not change this, to minimise changes to existing world saves
                if (blockToPlace == null) blockToPlace = this;
            } else
            {
                if (LastCodePart() != "free")
                {
                    failureCode = "requirefullwater";
                    return false;
                }
            }

            if (blockToPlace != null && skipPlantCheck || CanPlantStay(world.BlockAccessor, blockSel.Position))
            {
                world.BlockAccessor.SetBlock(blockToPlace.BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        public override bool TryPlaceBlockForWorldGenUnderwater(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, IRandom worldGenRand, int minWaterDepth, int maxWaterDepth, BlockPatchAttributes attributes = null)
        {
            BlockPos belowPos = pos.DownCopy();

            Block block;

            int depth = 1;
            while (depth < maxWaterDepth)
            {
                belowPos.Down();
                block = blockAccessor.GetBlock(belowPos);
                if (block is BlockWaterPlant) return false;
                if (block.Fertility > 0)
                {
                    blockAccessor.SetBlock(BlockId, belowPos.Up());
                    return true;
                }
                if (!block.IsLiquid()) return false;

                depth++;
            }

            return false;
        }
    }
}
