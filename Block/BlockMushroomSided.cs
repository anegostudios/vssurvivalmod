using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockMushroomSided : Block
    {

        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace, LCGRandom worldgenRandom)
        {
            var block = blockAccessor.GetBlock(CodeWithVariant("horizontalorientation", onBlockFace.Code));

            blockAccessor.SetBlock(block.Id, pos);

            //return base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace, worldgenRandom);
            return true;
        }

    }
}
