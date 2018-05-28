using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSeaweed : BlockWaterPlant
    {
        Random random = new Random();
        Block[] blocks;

        internal override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.X, pos.Y - 1, pos.Z);
            return (block.Fertility > 0) || (block is BlockSeaweed && block.LastCodePart() == "section");
        }

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace)
        {
            BlockPos belowPos = pos.DownCopy();

            Block block = blockAccessor.GetBlock(belowPos);
            if (!block.IsWater()) return false;

            int depth = 1;
            while (depth < 10)
            {
                belowPos.Down();
                block = blockAccessor.GetBlock(belowPos);

                if (block.Fertility > 0)
                {
                    belowPos.Up();
                    PlaceSeaweed(blockAccessor, belowPos, depth);
                    return true;
                } else
                {
                    if (!block.IsLiquid()) return false;
                }

                depth++;
            }

            return false;

        }


        private void PlaceSeaweed(IBlockAccessor blockAccessor, BlockPos pos, int depth)
        {
            int height = Math.Min(depth-1,  1 + random.Next(3) + random.Next(3));

            if (blocks == null)
            {
                blocks = new Block[]
                {
                    blockAccessor.GetBlock(CodeWithParts("section")),
                    blockAccessor.GetBlock(CodeWithParts("top")),
                };
            }

            while (height-- > 0)
            {
                blockAccessor.SetBlock(height == 0 ? blocks[1].BlockId : blocks[0].BlockId, pos);
                pos.Up();
            }
        }
    }
}
