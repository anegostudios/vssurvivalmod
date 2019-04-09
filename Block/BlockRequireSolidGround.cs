using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockRequireSolidGround : Block
    {
        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace)
        {
            if (!HasSolidGround(blockAccessor, pos))
            {
                return false;
            }

            return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace);
        }

        internal virtual bool HasSolidGround(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.DownCopy());
            return block.SideSolid[BlockFacing.UP.Index];
        }
    }
}
