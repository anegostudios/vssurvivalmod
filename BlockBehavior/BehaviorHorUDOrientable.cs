using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorHorUDOrientable : BlockBehavior
    {
        public BlockBehaviorHorUDOrientable(Block block) : base(block)
        {
        }




        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            BlockFacing[] horVer = Block.SuggestedHVOrientation(byPlayer, blockSel);
            if (horVer[1] == null) horVer[1] = BlockFacing.UP;
            AssetLocation blockCode = block.CodeWithParts(horVer[1].Code, horVer[0].Code);
            Block orientedBlock = world.BlockAccessor.GetBlock(blockCode);

            if (orientedBlock.IsSuitablePosition(world, blockSel.Position))
            {
                orientedBlock.DoPlaceBlock(world, blockSel.Position, blockSel.Face, itemstack);
                return true;
            }


            return false;
        }



        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            return new ItemStack[] { new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts("up", "north"))) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;
            return new ItemStack(world.BlockAccessor.GetBlock(block.CodeWithParts("up", "north")));
        }



        public override AssetLocation GetRotatedBlockCode(int angle, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[(angle / 90 + BlockFacing.FromCode(block.LastCodePart()).HorizontalAngleIndex) % 4];
            return block.CodeWithParts(newFacing.Code);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockFacing facing = BlockFacing.FromCode(block.LastCodePart());
            if (facing.Axis == axis)
            {
                return block.CodeWithParts(facing.GetOpposite().Code);
            }
            return block.Code;
        }

        public override AssetLocation GetVerticallyFlippedBlockCode(ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            return block.LastCodePart(1) == "up" ? block.CodeWithParts("down", block.LastCodePart()) : block.CodeWithParts("up", block.LastCodePart());
        }
    }
}
