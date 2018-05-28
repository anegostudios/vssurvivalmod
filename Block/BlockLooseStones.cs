using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLooseStones : Block
    {
        Random rand = new Random();

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace)
        {
            if (!HasSolidGround(blockAccessor, pos))
            {
                return false;
            }

            ushort blockId = BlockId;

            if (rand.NextDouble() <= 0.20)
            {
                blockId = blockAccessor.GetBlock(CodeWithPath("looseflints-" + LastCodePart())).BlockId;
            }

            if (blockAccessor.GetBlock(pos).IsReplacableBy(this))
            {
                blockAccessor.SetBlock(blockId, pos);
                return true;
            }

            return false;
        }

        internal virtual bool HasSolidGround(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.DownCopy());
            return block.SideSolid[BlockFacing.UP.Index];
        }
    }
}
