using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockStairs : Block
    {
        bool hasDownVariant = true;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            if (Attributes?["noDownVariant"].AsBool() == true)
            {
                hasDownVariant = false;
            }
        }

        public BlockFacing GetHorizontalFacing() {
            string[] split = Code.Path.Split('-');
            return BlockFacing.FromCode(split[split.Length - 1]);
        }

        public BlockFacing GetVerticalFacing()
        {
            string[] split = Code.Path.Split('-');
            return BlockFacing.FromCode(split[split.Length - 2]);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
        {
            if (!CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
            {
                return false;
            }

            BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);

            if (blockSel.Face.IsVertical)
            {
                horVer[1] = blockSel.Face;
            } else
            {
                horVer[1] = blockSel.HitPosition.Y < 0.5 || !hasDownVariant ? BlockFacing.UP : BlockFacing.DOWN;
            }

            AssetLocation blockCode = CodeWithParts(horVer[1].Code, horVer[0].Code);
            Block block = world.BlockAccessor.GetBlock(blockCode);
            if (block == null) return false;

            world.BlockAccessor.SetBlock(block.BlockId, blockSel.Position);

            return true;
        }


        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[] { new BlockDropItemStack(handbookStack) };
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
            return LastCodePart(1) == "up" && hasDownVariant ? CodeWithParts("down", LastCodePart()) : CodeWithParts("up", LastCodePart());
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
