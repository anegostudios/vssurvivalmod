using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockFirewoodPile : Block
    {
        Cuboidf[][] CollisionBoxesByFillLevel;

        public BlockFirewoodPile()
        {
            CollisionBoxesByFillLevel = new Cuboidf[5][];

            for (int i = 0; i <= 4; i++)
            {
                CollisionBoxesByFillLevel[i] = new Cuboidf[] { new Cuboidf(0, 0, 0, 1, i * 0.25f, 1) };
            }
        }

        public int FillLevel(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityFirewoodPile)
            {
                return (int)Math.Ceiling(((BlockEntityFirewoodPile)be).OwnStackSize / 8.0);
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

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            // Handled by BlockEntityItemPile
            return new ItemStack[0];
        }


        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityFirewoodPile)
            {
                BlockEntityFirewoodPile pile = (BlockEntityFirewoodPile)be;
                return pile.OnPlayerInteract(byPlayer);
            }

            return false;
        }


        internal bool Construct(IItemSlot slot, IWorldAccessor world, BlockPos pos, IPlayer player)
        {
            Block block = world.BlockAccessor.GetBlock(pos);
            if (!block.IsReplacableBy(this)) return false;
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.SideSolid[BlockFacing.UP.Index] && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) != 4)) return false;

            world.BlockAccessor.SetBlock(BlockId, pos);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityFirewoodPile)
            {
                BlockEntityFirewoodPile pile = (BlockEntityFirewoodPile)be;
                if (player == null || player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                {
                    pile.inventory.GetSlot(0).Itemstack = (ItemStack)slot.TakeOut(2);
                    slot.MarkDirty();
                } else
                {
                    pile.inventory.GetSlot(0).Itemstack = (ItemStack)slot.Itemstack.Clone();
                }

                pile.MarkDirty();
                world.BlockAccessor.MarkBlockDirty(pos);
                world.PlaySoundAt(new AssetLocation("sounds/block/planks"), pos.X, pos.Y, pos.Z, player, false);
            }

            return true;
        }

        public override void OnNeighourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.SideSolid[BlockFacing.UP.Index] && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) < 4))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                //world.PlaySoundAt(new AssetLocation("sounds/block/planks"), pos.X, pos.Y, pos.Z, null, false);
            }
        }


        public override bool CanAttachBlockAt(IBlockAccessor blockAccessor, Block block, BlockPos pos, BlockFacing blockFace)
        {
            BlockEntityFirewoodPile be = blockAccessor.GetBlockEntity(pos) as BlockEntityFirewoodPile;
            if (be != null)
            {
                return be.OwnStackSize == be.MaxStackSize;
            }

            return base.CanAttachBlockAt(blockAccessor, block, pos, blockFace);
        }


    }
}
