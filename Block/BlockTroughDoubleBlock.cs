using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockTroughDoubleBlock : Block
    {
        public BlockFacing OtherPartPos()
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (LastCodePart(1) == "feet") facing = facing.GetOpposite();
            return facing;
        }

        public BlockPos OtherPartPos(IWorldAccessor world, BlockPos pos)
        {
            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (LastCodePart(1) == "feet") facing = facing.GetOpposite();

            return pos.AddCopy(facing);
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
        {
            if (!world.TestPlayerAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            if (IsSuitablePosition(world, blockSel.Position))
            {
                BlockFacing[] horVer = SuggestedHVOrientation(byPlayer, blockSel);
                BlockPos secondPos = blockSel.Position.AddCopy(horVer[0]);

                if (!IsSuitablePosition(world, secondPos)) return false;

                string code = horVer[0].GetOpposite().Code;

                Block orientedBlock = world.BlockAccessor.GetBlock(CodeWithParts("head", code));
                orientedBlock.DoPlaceBlock(world, secondPos, blockSel.Face, itemstack);

                AssetLocation feetCode = CodeWithParts("feet", code);
                orientedBlock = world.BlockAccessor.GetBlock(feetCode);
                orientedBlock.DoPlaceBlock(world, blockSel.Position, blockSel.Face, itemstack);
                return true;
            }

            return false;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel != null) {
                BlockPos pos = blockSel.Position;

                if (LastCodePart(1) == "feet")
                {
                    BlockFacing facing = BlockFacing.FromCode(LastCodePart()).GetOpposite();
                    pos = pos.AddCopy(facing);
                }

                BlockEntityTrough betr = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityTrough;
                if (betr != null) return betr.OnInteract(byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
        {
            string headfoot = LastCodePart(1);

            BlockFacing facing = BlockFacing.FromCode(LastCodePart());
            if (LastCodePart(1) == "feet") facing = facing.GetOpposite();

            Block secondPlock = world.BlockAccessor.GetBlock(pos.AddCopy(facing));

            if (secondPlock is BlockTroughDoubleBlock && secondPlock.LastCodePart(1) != headfoot)
            {
                if (LastCodePart(1) == "feet")
                {
                    (world.BlockAccessor.GetBlockEntity(pos.AddCopy(facing)) as BlockEntityTrough)?.OnBlockBroken();
                }
                world.BlockAccessor.SetBlock(0, pos.AddCopy(facing));
            }

            base.OnBlockRemoved(world, pos);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            return new ItemStack[] { new ItemStack(world.BlockAccessor.GetBlock(CodeWithParts("head", "north"))) };
        }


        public override AssetLocation GetRotatedBlockCode(int angle)
        {
            BlockFacing beforeFacing = BlockFacing.FromCode(LastCodePart());
            int rotatedIndex = GameMath.Mod(beforeFacing.HorizontalAngleIndex - angle / 90, 4);
            BlockFacing nowFacing = BlockFacing.HORIZONTALS_ANGLEORDER[rotatedIndex];

            return CodeWithParts(nowFacing.Code);
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            return new ItemStack(world.BlockAccessor.GetBlock(CodeWithParts("head", "north")));
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

        public override void GetHeldItemInfo(ItemStack stack, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(stack, dsc, world, withDebugInfo);

            
        }

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            if (LastCodePart(1) == "feet")
            {
                BlockFacing facing = BlockFacing.FromCode(LastCodePart()).GetOpposite();
                pos = pos.AddCopy(facing);
            }

            BlockEntityTrough betr = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityTrough;
            if (betr != null) return betr.GetBlockInfo(forPlayer);


            return base.GetPlacedBlockInfo(world, pos, forPlayer);
        }
        
    }
}
