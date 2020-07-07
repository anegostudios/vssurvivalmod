using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockPlankPile : Block
    {
        Cuboidf[][] CollisionBoxesByFillLevel;

        public BlockPlankPile()
        {
            CollisionBoxesByFillLevel = new Cuboidf[17][];

            for (int i = 0; i <= 16; i++)
            {
                CollisionBoxesByFillLevel[i] = new Cuboidf[] { new Cuboidf(0, 0, 0, 1, i * 0.125f / 2, 1) };
            }
        }

        public int FillLevel(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityPlankPile)
            {
                return ((BlockEntityPlankPile)be).OwnStackSize/3;
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
            if (be is BlockEntityPlankPile)
            {
                BlockEntityPlankPile pile = (BlockEntityPlankPile)be;
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

        public override BlockDropItemStack[] GetDropsForHandbook(ItemStack handbookStack, IPlayer forPlayer)
        {
            return new BlockDropItemStack[0];
        }
        

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            // Handled by BlockEntityItemPile
            return new ItemStack[0];
        }

        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityPlankPile)
            {
                BlockEntityPlankPile pile = (BlockEntityPlankPile)be;
                ItemStack stack = pile.inventory[0].Itemstack;
                if (stack != null)
                {
                    ItemStack pickstack = stack.Clone();
                    pickstack.StackSize = 1;
                    return pickstack;
                }
            }

            return new ItemStack(this);
        }


        internal bool Construct(ItemSlot slot, IWorldAccessor world, BlockPos pos, IPlayer player)
        {
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.SideSolid[BlockFacing.UP.Index] && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) != 16)) return false;

            if (!world.BlockAccessor.GetBlock(pos).IsReplacableBy(this)) return false;

            world.BlockAccessor.SetBlock(BlockId, pos);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityPlankPile)
            {
                BlockEntityPlankPile pile = (BlockEntityPlankPile)be;
                if (player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    pile.inventory[0].Itemstack = slot.Itemstack.Clone();
                    pile.inventory[0].Itemstack.StackSize = 1;
                }
                else
                {
                    pile.inventory[0].Itemstack = slot.TakeOut(player.Entity.Controls.Sprint ? pile.BulkTakeQuantity : pile.DefaultTakeQuantity);
                }
                
                pile.MarkDirty();
                world.BlockAccessor.MarkBlockDirty(pos);
                world.PlaySoundAt(new AssetLocation("sounds/block/planks"), pos.X, pos.Y, pos.Z, player, false);
            }

            return true;
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.SideSolid[BlockFacing.UP.Index])
            {
                int level = FillLevel(world.BlockAccessor, pos.DownCopy());
                if (belowBlock != this || level < 16)
                {
                    world.BlockAccessor.BreakBlock(pos, null);
                }
                //world.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos.X, pos.Y, pos.Z, null, false);
            }
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-plankpile-add",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sneak",
                    Itemstacks = new ItemStack[] { new ItemStack(this) },
                    GetMatchingStacks = (wi, bs, es) =>
                    {
                        BlockEntityPlankPile pile = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPlankPile;
                        ItemStack stack = pile?.inventory[0].Itemstack?.Clone();
                        if (stack == null) return null;

                        stack.StackSize = pile.BulkTakeQuantity;
                        return pile != null && pile.MaxStackSize > pile.inventory[0].StackSize ? new ItemStack[] { stack } : null;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-plankpile-remove",
                    MouseButton = EnumMouseButton.Right
                },

                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-plankpile-4add",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCodes = new string[] {"sprint", "sneak" },
                    Itemstacks = new ItemStack[] { new ItemStack(this) },
                    GetMatchingStacks = (wi, bs, es) =>
                    {
                        BlockEntityPlankPile pile = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPlankPile;
                        ItemStack stack = pile?.inventory[0].Itemstack?.Clone();
                        if (stack == null) return null;

                        stack.StackSize = pile.BulkTakeQuantity;

                        return pile != null && pile.MaxStackSize > pile.inventory[0].StackSize ? new ItemStack[] { stack } : null;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-plankpile-4remove",
                    HotKeyCode = "sprint",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }

    }
}
