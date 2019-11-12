using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockBehaviorOmniAttachable : BlockBehavior
    {
        public string facingCode = "orientation";

        public BlockBehaviorOmniAttachable(Block block) : base(block)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            facingCode = properties["facingCode"].AsString("orientation");
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref EnumHandling handling, ref string failureCode)
        {
            handling = EnumHandling.PreventDefault;

            // Prefer selected block face
            if (TryAttachTo(world, byPlayer, blockSel.Position, blockSel.Face, itemstack)) return true;


            // Otherwise attach to any possible face
            BlockFacing[] faces = BlockFacing.ALLFACES;
            for (int i = 0; i < faces.Length; i++)
            {
                //if (faces[i] == BlockFacing.DOWN) continue; - what for? o.O

                if (TryAttachTo(world, byPlayer, blockSel.Position, faces[i], itemstack)) return true;
            }

            failureCode = "requireattachable";

            return false;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            Block droppedblock = world.BlockAccessor.GetBlock(block.CodeWithVariant(facingCode, "up"));
            return new ItemStack[] { new ItemStack(droppedblock) };
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            Block pickedblock = world.BlockAccessor.GetBlock(block.CodeWithVariant(facingCode, "up"));
            return new ItemStack(pickedblock);
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos, ref EnumHandling handled)
        {
            handled = EnumHandling.PreventDefault;

            if (!CanStay(world, pos))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }

        bool TryAttachTo(IWorldAccessor world, IPlayer byPlayer, BlockPos blockpos, BlockFacing onBlockFace, ItemStack itemstack)
        {
            BlockPos attachingBlockPos = blockpos.AddCopy(onBlockFace.GetOpposite());
            Block attachingBlock = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(attachingBlockPos));

            BlockFacing onFace = onBlockFace;

            if (attachingBlock.CanAttachBlockAt(world.BlockAccessor, block, attachingBlockPos, onFace))
            {
                Block orientedBlock = world.BlockAccessor.GetBlock(block.CodeWithVariant(facingCode, onBlockFace.Code));
                orientedBlock.DoPlaceBlock(world, byPlayer, new BlockSelection() { Position = blockpos, Face = onFace }, itemstack);
                return true;
            }

            return false;
        }

        bool CanStay(IWorldAccessor world, BlockPos pos)
        {
            BlockFacing facing = BlockFacing.FromCode(block.Variant[facingCode]);
            Block attachedblock = world.BlockAccessor.GetBlock(world.BlockAccessor.GetBlockId(pos.AddCopy(facing.GetOpposite())));

            BlockFacing onFace = facing;

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

            if (block.Variant[facingCode] == "up" || block.Variant[facingCode] == "down") return block.Code;

            BlockFacing newFacing = BlockFacing.HORIZONTALS_ANGLEORDER[((360 - angle) / 90 + BlockFacing.FromCode(block.Variant[facingCode]).HorizontalAngleIndex) % 4];
            return block.CodeWithParts(newFacing.Code);
        }

        public override AssetLocation GetVerticallyFlippedBlockCode(ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;
            
            return block.Variant[facingCode] == "up" ? block.CodeWithVariant(facingCode, "down") : block.CodeWithVariant(facingCode, "up");
        }

        public override AssetLocation GetHorizontallyFlippedBlockCode(EnumAxis axis, ref EnumHandling handling)
        {
            handling = EnumHandling.PreventDefault;

            BlockFacing facing = BlockFacing.FromCode(block.Variant[facingCode]);
            if (facing.Axis == axis)
            {
                return block.CodeWithVariant(facingCode, facing.GetOpposite().Code);
            }
            return block.Code;
        }
    }
}
