using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockStairs : Block
    {
        public BlockFacing GetHorizontalFacing() {
            string[] split = Code.Path.Split('-');
            return BlockFacing.FromCode(split[split.Length - 1]);
        }

        public BlockFacing GetVerticalFacing()
        {
            string[] split = Code.Path.Split('-');
            return BlockFacing.FromCode(split[split.Length - 2]);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
        {
            if (!world.TestPlayerAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            if (!IsSuitablePosition(world, blockSel.Position))
            {
                return false;
            }

            BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);

            if (blockSel.Face.IsVertical)
            {
                horVer[1] = blockSel.Face;
            } else
            {
                horVer[1] = blockSel.HitPosition.Y < 0.5 ? BlockFacing.UP : BlockFacing.DOWN;
            }

            AssetLocation blockCode = CodeWithParts(horVer[1].Code, horVer[0].Code);

            world.BlockAccessor.SetBlock(world.BlockAccessor.GetBlock(blockCode).BlockId, blockSel.Position);

            return true;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("up", "north"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("up", "north"));
            return new ItemStack(block);
        }
        

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360-angle) / 90 + BlockFacing.FromCode(LastCodePart()).HorizontalAngleIndex) % 4];
            return CodeWithParts(newFacing.Code);
        }

        public override AssetLocation GetVerticallyFlippedBlockCode()
        {
            return LastCodePart(1) == "up" ? CodeWithParts("down", LastCodePart()) : CodeWithParts("up", LastCodePart());
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
