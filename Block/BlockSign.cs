using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSign : Block
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection bs, ref string failureCode)
        {
            if (!world.Claims.TryAccess(byPlayer, bs.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                failureCode = "claimed";
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            BlockPos supportingPos = bs.Position.AddCopy(bs.Face.GetOpposite());
            Block supportingBlock = world.BlockAccessor.GetBlock(supportingPos);

            if (!world.BlockAccessor.GetBlock(bs.Position).IsReplacableBy(this))
            {
                failureCode = "notreplaceable";
                return false;
            }


            if (bs.Face.IsHorizontal && (supportingBlock.CanAttachBlockAt(world.BlockAccessor, this, bs.Position, bs.Face) || supportingBlock.Attributes?["partialAttachable"].AsBool() == true))
            {
                Block wallblock = world.BlockAccessor.GetBlock(CodeWithParts("wall", bs.Face.GetOpposite().Code));

                world.BlockAccessor.SetBlock(wallblock.BlockId, bs.Position);
                return true;
            }


            BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, bs);

            AssetLocation blockCode = CodeWithParts(horVer[0].Code);
            Block block = world.BlockAccessor.GetBlock(blockCode);
            world.BlockAccessor.SetBlock(block.BlockId, bs.Position);
            return true;
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
                besigh.OnRightClick(byPlayer);
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
