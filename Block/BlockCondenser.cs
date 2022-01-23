using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockCondenser : BlockLiquidContainerTopOpened
    {
        public override bool AllowHeldLiquidTransfer => false;


        public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        {
            return true;
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            var stack = base.OnPickBlock(world, pos);

            SetContents(stack, null);

            return stack;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntityCondenser be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityCondenser;

            if (be?.OnBlockInteractStart(byPlayer, blockSel) == true) return true;

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            StringBuilder dsc = new StringBuilder();
            dsc.AppendLine(base.GetPlacedBlockInfo(world, pos, forPlayer));

            BlockEntityCondenser be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityCondenser;
            if (be?.Inventory[1].Empty == false)
            {
                BlockLiquidContainerBase block = be.Inventory[1].Itemstack.Collectible as BlockLiquidContainerBase;
                dsc.Append("Bucket: ");
                block.GetContentInfo(be.Inventory[1], dsc, world);
            }

            return dsc.ToString();
        }
    }
}
