using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorOmniAttachable : BlockBehavior
    {
        public int facingPos = 1;

        public BlockBehaviorOmniAttachable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            facingPos = properties["facingPos"].AsInt(1);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (!block.IsSuitablePosition(world, blockSel.Position))
            {
                return false;
            }

            // Prefer selected block face
            if (TryAttachTo(world, blockSel.Position, blockSel.Face, itemstack)) return true;


            // Otherwise attach to any possible face
            BlockFacing[] faces = BlockFacing.ALLFACES;
            for (int i = 0; i < faces.Length; i++)
            {
                if (faces[i] == BlockFacing.DOWN) continue;

                if (TryAttachTo(world, blockSel.Position, faces[i], itemstack)) return true;
            }

            return false;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            Block droppedblock = world.BlockAccessor.GetBlock(block.CodeWithPart("up", facingPos));
            return new ItemStack[] { new ItemStack(droppedblock) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            Block pickedblock = world.BlockAccessor.GetBlock(block.CodeWithPart("up", facingPos));
            return new ItemStack(pickedblock);
        }


        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (!CanStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

        bool TryAttachTo(IWorldAccessor world, BlockPos blockpos, BlockFacing onBlockFace, ItemStack itemstack)
        {
            BlockPos attachingBlockPos = blockpos.AddCopy(onBlockFace.GetOpposite());
            Block attachingBlock = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(attachingBlockPos));

            BlockFacing onFace = onBlockFace;
            ///if (onFace.IsHorizontal) onFace = onFace.GetOpposite(); - why is this here? Breaks attachment

            if (attachingBlock.CanAttachBlockAt(world.BlockAccessor, block, attachingBlockPos, onFace))
            {
                Block orientedBlock = world.BlockAccessor.GetBlock(block.CodeWithPart(onBlockFace.Code, facingPos));
                orientedBlock.DoPlaceBlock(world, blockpos, onBlockFace, itemstack);
                return true;
            }

            return false;
        }

        bool CanStay(IWorldAccessor world, BlockPos pos)
        {
            BlockFacing facing = BlockFacing.FromCode(block.FirstCodePart(facingPos));
            Block attachedblock = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(pos.AddCopy(facing.GetOpposite())));

            BlockFacing onFace = facing;
            //if (onFace.IsHorizontal) onFace = onFace.GetOpposite(); - why is this here? Breaks attachment

            return attachedblock.CanAttachBlockAt(world.BlockAccessor, block, pos, onFace);
        }

        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            return false;
        }

        public override AssetLocation GetRotatedBlockCode(int angle, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (block.FirstCodePart(facingPos) == "up" || block.FirstCodePart(facingPos) == "down") return block.Code;

            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360 - angle) / 90 + BlockFacing.FromCode(block.FirstCodePart(facingPos)).HorizontalAngleIndex) % 4];
            return block.CodeWithParts(newFacing.Code);
        }

        public override AssetLocation GetVerticallyFlippedBlockCode(ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            
            return block.FirstCodePart(facingPos) == "up" ? block.CodeWithPart("down", facingPos) : block.CodeWithPart("up", facingPos);
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockFacing facing = BlockFacing.FromCode(block.FirstCodePart(facingPos));
            if (facing.Axis == axis)
            {
                return block.CodeWithPart(facing.GetOpposite().Code, facingPos);
            }
            return block.Code;
        }
    }
}
