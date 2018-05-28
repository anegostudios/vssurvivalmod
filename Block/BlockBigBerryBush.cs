using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBerryBush : BlockPlant
    {
        internal override bool CanPlantStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            Block block = blockAccessor.GetBlock(pos.DownCopy());
            return block.Fertility > 0 || (Attributes?["stackable"]?.AsBool() == true && block.Attributes?["stackable"]?.AsBool() == true && block is BlockBerryBush);
        }


        public override int TextureSubIdForRandomBlockPixel(IWorldAccessor world, BlockPos pos, BlockFacing facing, ref int tintIndex)
        {
            tintIndex = 1;
            return base.TextureSubIdForRandomBlockPixel(world, pos, facing, ref tintIndex);
        }

    }
}
