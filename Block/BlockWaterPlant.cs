using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
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

            if (blockToPlace != null && CanPlantStay(world.BlockAccessor, blockSel.Position))
            {
                world.BlockAccessor.SetBlock(blockToPlace.BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);

            //if (LastCodePart() != "free")
            //{
            //    world.BlockAccessor.SetBlock(world.GetBlock(new AssetLocation("water-still-7")).BlockId, pos);
            //    world.BlockAccessor.GetBlock(pos).OnNeighbourBlockChange(world, pos, pos);
            //}
        }


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldGenRand)
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

            if (belowBlock.LiquidCode == "water")   // This should be OK, as in worldgen blocks are either still water only, or not water at all
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
