using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockSimpleCoating : Block
    {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
        {
            if (!world.TryAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            if (!IsSuitablePosition(world, blockSel.Position))
            {
                return false;
            }

            // Prefer selected block face
            if (TryAttachTo(world, blockSel.Position, blockSel.Face)) return true;

            // Otherwise attach to any possible face
            BlockFacing[] faces = BlockFacing.ALLFACES;
            for (int i = 0; i < faces.Length; i++)
            {
                if (TryAttachTo(world, blockSel.Position, faces[i])) return true;
            }

            return false;
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

        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!CanBlockStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

        bool TryAttachTo(IWorldAccessor world, BlockPos blockpos, BlockFacing onBlockFace)
        {
            BlockFacing oppositeFace = onBlockFace.GetOpposite();

            BlockPos attachingBlockPos = blockpos.AddCopy(oppositeFace);
            Block block = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(attachingBlockPos));

            if (block.CanAttachBlockAt(world.BlockAccessor, this, attachingBlockPos, onBlockFace))
            {
                ushort blockId = world.BlockAccessor.GetBlock(CodeWithParts(oppositeFace.Code)).BlockId;
                world.BlockAccessor.SetBlock(blockId, blockpos);
                return true;
            }

            return false;
        }

        bool CanBlockStay(IWorldAccessor world, BlockPos pos)
        {
            string[] parts = Code.Path.Split('-');
            BlockFacing facing = BlockFacing.FromCode(parts[parts.Length - 1]);
            ushort blockId = world.BlockAccessor.GetBlockId(pos.AddCopy(facing));

            Block block = world.BlockAccessor.GetBlock(blockId);

            return block.CanAttachBlockAt(world.BlockAccessor, this, pos, facing);
        }

        public override bool CanAttachBlockAt(IBlockAccessor world, Block block, BlockPos pos, BlockFacing blockFace)
        {
            return false;
        }



        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            if (LastCodePart() == "up" || LastCodePart() == "down") return Code;

            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360 - angle) / 90 + BlockFacing.FromCode(LastCodePart()).HorizontalAngleIndex) % 4];
            return CodeWithParts(newFacing.Code);
        }

        public override AssetLocation GetVerticallyFlippedBlockCode()
        {
            return LastCodePart() == "up" ? CodeWithParts("down") : CodeWithParts("up");
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
