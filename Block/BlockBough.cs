using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public class BlockBough : Block
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                BlockFacing horVer = OrientForPlacement(world.BlockAccessor, byPlayer, blockSel).Opposite;
                AssetLocation newCode = CodeWithParts(horVer.Code.Substring(0, 1));
                world.BlockAccessor.SetBlock(world.GetBlock(newCode).BlockId, blockSel.Position);
                return true;
            }

            return false;
        }

        protected virtual BlockFacing OrientForPlacement(IBlockAccessor world, IPlayer player, BlockSelection bs)
        {
            BlockFacing[] facings = SuggestedHVOrientation(player, bs);
            return facings.Length > 0 ? facings[0] : BlockFacing.NORTH;
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.GetBlock(CodeWithParts("placed", Variant["wood"], "20", "n")));
        }
    }
}
