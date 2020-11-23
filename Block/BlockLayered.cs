using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockLayered : Block
    {
        public Block GetNextLayer(IWorldAccessor world)
        {
            int layer;
            int.TryParse(Code.Path.Split('-')[1], out layer);

            string basecode = CodeWithoutParts(1);

            if (layer < 7) return world.BlockAccessor.GetBlock(CodeWithPath(basecode + "-" + (layer + 1)));
            return world.BlockAccessor.GetBlock(CodeWithPath(basecode.Replace("layer", "block")));
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                failureCode = "claimed";
                return false;
            }

            Block block = world.BlockAccessor.GetBlock(blockSel.Position.AddCopy(blockSel.Face.Opposite));

            if (block is BlockLayered)
            {
                Block nextBlock = ((BlockLayered)block).GetNextLayer(world);
                world.BlockAccessor.SetBlock(nextBlock.BlockId, blockSel.Position.AddCopy(blockSel.Face.Opposite));

                return true;
            }

            if (!CanLayerStay(world, blockSel.Position))
            {
                failureCode = "belowblockcannotsupport";
                return false;
            }

            return base.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
        }

   
        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("1"));
            return new ItemStack(block);
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (GetBehavior<BlockBehaviorUnstableFalling>() != null)
            {
                base.OnNeighbourBlockChange(world, pos, neibpos);
                return;
            }

            if (!CanLayerStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

        bool CanLayerStay(IWorldAccessor world, BlockPos pos)
        {
            BlockPos belowPos = pos.DownCopy();
            Block block = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(belowPos));

            return block.CanAttachBlockAt(world.BlockAccessor, this, belowPos, BlockFacing.UP);
        }

        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace, Cuboidi attachmentArea = null)
        {
            return false;
        }
    }
}
