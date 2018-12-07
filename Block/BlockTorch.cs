using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockTorch : Block
    {


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
        {
            if (byPlayer.Entity.Controls.Sneak) return false;

            if (!IsSuitablePosition(world, blockSel.Position))
            {
                return false;
            }

            if (!world.TryAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            // Prefer selected block face
            if (blockSel.Face.IsHorizontal || blockSel.Face == BlockFacing.UP)
            {
                if (TryAttachTo(world, blockSel.Position, blockSel.Face)) return true;
            }

            // Otherwise attach to any possible face

            BlockFacing[] faces = BlockFacing.ALLFACES;
            for (int i = 0; i < faces.Length; i++)
            {
                if (faces[i] == BlockFacing.DOWN) continue;

                if (TryAttachTo(world, blockSel.Position, faces[i])) return true;
            }

            return false;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            if (FirstCodePart(1) == "extinct") return new ItemStack[0];

            Block block = world.BlockAccessor.GetBlock(CodeWithParts("up"));
            return new ItemStack[] { new ItemStack(block) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            Block block = world.BlockAccessor.GetBlock(CodeWithParts("up"));
            return new ItemStack(block);
        }


        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            if (!CanTorchStay(world.BlockAccessor, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

        bool TryAttachTo(IWorldAccessor world, BlockPos blockpos, BlockFacing onBlockFace)
        {
            BlockFacing onFace = onBlockFace;
            //if (onFace.IsHorizontal) onFace = onFace.GetOpposite(); - why is this here? Breaks attachment

            BlockPos attachingBlockPos = blockpos.AddCopy(onBlockFace.GetOpposite());
            Block block = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(attachingBlockPos));

            if (block.CanAttachBlockAt(world.BlockAccessor, this, attachingBlockPos, onFace))
            {
                ushort blockId = world.BlockAccessor.GetBlock(CodeWithParts(onBlockFace.Code)).BlockId;
                world.BlockAccessor.SetBlock(blockId, blockpos);
                return true;
            }

            return false;
        }

        bool CanTorchStay(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            Block block = blockAccessor.GetBlock(blockAccessor.GetBlockId(pos.AddCopy(facing.GetOpposite())));
            return block.CanAttachBlockAt(blockAccessor, this, pos, facing);
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace)
        {
            return false;
        }

        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            if (LastCodePart() == "up") return Code;

            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360 - angle) / 90 + BlockFacing.FromCode(LastCodePart()).HorizontalAngleIndex) % 4];
            return CodeWithParts(newFacing.Code);
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


        public override bool TryPlaceBlockForWorldGen(IBlockAccessor blockAccessor, BlockPos pos, BlockFacing onBlockFace)
        {
            return CanTorchStay(blockAccessor, pos) && base.TryPlaceBlockForWorldGen(blockAccessor, pos, onBlockFace);
        }
    }
}
