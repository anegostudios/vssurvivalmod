using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSlab : Block
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            /*if ((blockSel.HitPosition.Y > 0.5 && blockSel.Face.IsHorizontal) || blockSel.Face == BlockFacing.DOWN)
            {
                Block block = world.BlockAccessor.GetBlock(CodeWithParts("up"));

                return block.TryPlaceBlock(block);

                if (!CanPlaceBlock(world, blockSel.Position, ref failureCode))
                {
                    block.DoPlaceBlock(world, blockSel.Position, blockSel.Face, itemstack);
                    return true;
                }

                return false;
            }*/

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode); 
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[] { new BlockDropItemStack(handbookStack) };
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("down"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("down"));
            return new ItemStack(block);
        }

        public override AssetLocation GetVerticallyFlippedBlockCode()
        {
            return LastCodePart() == "up" ? CodeWithParts("down") : CodeWithParts("up");
        }

    }
}
