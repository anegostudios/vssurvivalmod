using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockPlatePile : Block
    {
        Cuboidf[][] CollisionBoxesByFillLevel;

        public BlockPlatePile()
        {
            CollisionBoxesByFillLevel = new Cuboidf[17][];

            for (int i = 0; i <= 16; i++)
            {
                CollisionBoxesByFillLevel[i] = new Cuboidf[] { new Cuboidf(0.1875f, 0, 0.1875f, 0.8125f, i * 0.125f / 2, 0.8125f) };
            }
        }

        public int FillLevel(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityPlatePile)
            {
                return ((BlockEntityPlatePile)be).OwnStackSize;
            }

            return 1;
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return CollisionBoxesByFillLevel[FillLevel(blockAccessor, pos)];
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            return CollisionBoxesByFillLevel[FillLevel(blockAccessor, pos)];
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityPlatePile)
            {
                BlockEntityPlatePile pile = (BlockEntityPlatePile)be;
                return pile.OnPlayerInteract(byPlayer);
            }

            return false;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            BlockEntityItemPile be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityItemPile;
            be?.OnBlockBroken();
            base.OnBlockBroken(world, pos, byPlayer);
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            // Handled by BlockEntityItemPile
            return new ItemStack[0];
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityPlatePile)
            {
                BlockEntityPlatePile pile = (BlockEntityPlatePile)be;
                ItemStack stack = pile.inventory.GetSlot(0).Itemstack;
                if (stack != null)
                {
                    ItemStack pickstack = stack.Clone();
                    pickstack.StackSize = 1;
                    return pickstack;
                }
            }

            return new ItemStack(this);
        }


        internal bool Construct(IItemSlot slot, IWorldAccessor world, BlockPos pos, IPlayer player)
        {
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.SideSolid[BlockFacing.UP.Index] && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) != 16)) return false;

            if (!world.BlockAccessor.GetBlock(pos).IsReplacableBy(this)) return false;

            world.BlockAccessor.SetBlock(BlockId, pos);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityPlatePile)
            {
                BlockEntityPlatePile pile = (BlockEntityPlatePile)be;
                pile.inventory.GetSlot(0).Itemstack = (ItemStack)slot.Itemstack.Clone();
                pile.inventory.GetSlot(0).Itemstack.StackSize = 1;

                if (player.WorldData.CurrentGameMode != EnumGameMode.Creative) slot.TakeOut(1);
                
                pile.MarkDirty(true);
                
                world.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos.X, pos.Y, pos.Z, player, false);
            }

            return true;
        }


        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.SideSolid[BlockFacing.UP.Index] && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) < 8))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                //world.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos.X, pos.Y, pos.Z, null, false);
            }
        }


    }
}
