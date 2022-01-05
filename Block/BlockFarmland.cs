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
                befarmland?.OnCreatedFromSoil(byItemStack.Block);
            }
        }


        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            if ((block is BlockCrop || block is BlockDeadCrop) && blockFace == BlockFacing.UP) return true;

            return base.CanAttachBlockAt(world, block, pos, blockFace, attachmentArea);
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityFarmland befarmland = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityFarmland;
            if (befarmland != null && befarmland.OnBlockInteract(byPlayer)) return true;

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }


        public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        {
            BlockEntityFarmland befarmland = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityFarmland;

            if (befarmland != null)
            {
                Block farmlandBlock = api.World.GetBlock(CodeWithVariant("state", befarmland.IsVisiblyMoist ? "moist" : "dry"));
                return new ItemStack(farmlandBlock).GetName();

            }
            return base.GetPlacedBlockName(world, pos);
        }


        public override int GetHeatRetention(BlockPos pos, BlockFacing facing)
        {
            return 3;
        }
    }
}
