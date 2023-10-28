using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorSlab : BlockBehavior
    {
        public BlockBehaviorSlab(Block block) : base(block)
        {
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventDefault;

            if (!world.BlockAccessor.GetBlock(blockSel.Position).IsReplacableBy(block)) return false;

            Block orientedBlock = world.BlockAccessor.GetBlock(block.CodeWithParts("down"));

            if ((blockSel.HitPosition.Y > 0.5 && blockSel.Face.IsHorizontal) || blockSel.Face == BlockFacing.DOWN)
            {
                orientedBlock = world.BlockAccessor.GetBlock(block.CodeWithParts("up"));
            }

            if (!orientedBlock.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode)) return false;

            orientedBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
            return true;
        }



        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, ref float dropQuantityMultiplier, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            return new ItemStack[] { new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts("down"))) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            return new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts("down")));
        }


        public override AssetLocation GetVerticallyFlippedBlockCode(ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            return block.LastCodePart() == "up" ? block.CodeWithParts("down") : block.CodeWithParts("up");
        }
    }
}
