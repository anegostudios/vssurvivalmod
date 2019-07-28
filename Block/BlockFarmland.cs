using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFarmland : Block
    {

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            base.OnBlockPlaced(world, blockPos, byItemStack);

            if (byItemStack != null)
            {
                BlockEntityFarmland befarmland = world.BlockAccessor.GetBlockEntity(blockPos) as BlockEntityFarmland;
                befarmland?.CreatedFromSoil(byItemStack.Block);
            }
        }


        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace)
        {
            if (block is BlockCrop && blockFace == BlockFacing.UP) return true;

            return base.CanAttachBlockAt(world, block, pos, blockFace);
        }

    }
}
