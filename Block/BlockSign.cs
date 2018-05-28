using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSign : Block
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection bs)
        {
            if (bs.Face.IsHorizontal && world.BlockAccessor.GetBlock(bs.Position.AddCopy(bs.Face.GetOpposite())).CanAttachBlockAt(world.BlockAccessor, this, bs.Position, bs.Face))
            {
                Block block = world.BlockAccessor.GetBlock(CodeWithParts("wall", bs.Face.GetOpposite().Code));

                world.BlockAccessor.SetBlock(block.BlockId, bs.Position);
                return true;
            }

            if (world.BlockAccessor.GetBlock(bs.Position).IsReplacableBy(this))
            {
                BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, bs);

                AssetLocation blockCode = CodeWithParts(horVer[0].Code);
                Block block = world.BlockAccessor.GetBlock(blockCode);
                world.BlockAccessor.SetBlock(block.BlockId, bs.Position);
                return true;
            }


            return false;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("ground", "north"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("ground", "north"));
            return new ItemStack(block);
        }



        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighourBlockChange(world, pos, neibpos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity entity = world.BlockAccessor.GetBlockEntity(blockSel.Position);

            if (entity is BlockEntitySign)
            {
                BlockEntitySign besigh = (BlockEntitySign)entity;
                besigh.OpenDialog(byPlayer);
                return true;
            }

            return true;
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (facing.Axis == axis)
            {
                return CodeWithParts(facing.GetOpposite().Code);
            }
            return Code;
        }
    }
}
