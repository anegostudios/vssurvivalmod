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

            if (this.Attributes?.IsTrue("noDownVariant") == true)
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

            AssetLocation blockCode = CodeWithVariants(new string[] { "verticalorientation", "horizontalorientation" }, new string[] { horVer[1].Code, horVer[0].Code });
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
            Block block = world.BlockAccessor.GetBlock(CodeWithVariants(new string[] { "verticalorientation", "horizontalorientation", "cover" }, new string[] { "up", "north", "free" }));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithVariants(new string[] { "verticalorientation", "horizontalorientation", "cover" }, new string[] { "up", "north", "free" }));
            return new ItemStack(block);
        }
        

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360-angle) / 90 + BlockFacing.FromCode(Variant["horizontalorientation"]).HorizontalAngleIndex) % 4];
            return CodeWithVariant("horizontalorientation", newFacing.Code);
        }

        public override AssetLocation GetVerticallyFlippedBlockCode()
        {
            return Variant["verticalorientation"] == "up" && hasDownVariant ? CodeWithVariant("verticalorientation", "down") : CodeWithVariant("verticalorientation", "up");
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis)
        {
            BlockFacing facing = BlockFacing.FromCode(Variant["horizontalorientation"]);
            if (facing.Axis == axis)
            {
                return CodeWithVariant("horizontalorientation", facing.Opposite.Code);
            }

            return Code;
        }
    }
}
