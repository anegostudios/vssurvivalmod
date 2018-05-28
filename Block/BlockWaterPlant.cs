using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockWaterPlant : BlockPlant
    {

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
        {
            Block block = world.BlockAccessor.GetBlock(blockSel.Position);

            Block blockToPlace = this;

            if (block.IsLiquid())
            {
                if (block.LiquidLevel == 7) blockToPlace = world.GetBlock(CodeWithParts("water"));
                else return false;

                if (blockToPlace == null) blockToPlace = this;
            } else
            {
                if (LastCodePart() != "free") return false;
            }

            if (blockToPlace != null && CanPlantStay(world.BlockAccessor, blockSel.Position) && blockToPlace.IsSuitablePosition(world, blockSel.Position))
            {
                world.BlockAccessor.SetBlock(blockToPlace.BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);

            if (LastCodePart() != "free")
            {
                world.BlockAccessor.SetBlock(world.GetBlock(new AssetLocation("water-7")).BlockId, pos);
            }
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace)
        {
            Block block = blockAccessor.GetBlock(pos);

            if (!block.IsReplacableBy(this))
            {
                return false;
            }

            Block belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            if (belowBlock.Fertility > 0)
            {
                Block placingBlock = blockAccessor.GetBlock(CodeWithParts("free"));
                if (placingBlock == null) return false;
                blockAccessor.SetBlock(placingBlock.BlockId, pos);
                return true;
            }

            if (belowBlock.IsWater())
            {
                return TryPlaceBlockInWater(blockAccessor, pos);
            }

            return false;
        }

        protected virtual bool TryPlaceBlockInWater(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block belowBlock = blockAccessor.GetBlock(pos.X, pos.Y - 2, pos.Z);
            if (belowBlock.Fertility > 0)
            {
                blockAccessor.SetBlock(blockAccessor.GetBlock(CodeWithParts("water")).BlockId, pos.AddCopy(0, -1, 0));
                return true;
            }
            return false;
        }

    }
}
