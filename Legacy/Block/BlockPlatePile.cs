using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
                if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
                {
                    byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                    return false;
                }

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
            if (be is BlockEntityPlatePile)
            {
                BlockEntityPlatePile pile = (BlockEntityPlatePile)be;
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
            if (!world.Claims.TryAccess(player, pos, EnumBlockAccessFlags.BuildOrBreak))
            {
                player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }


            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.CanAttachBlockAt(world.BlockAccessor, this, pos.DownCopy(), BlockFacing.UP) && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) != 16)) return false;

            if (!world.BlockAccessor.GetBlock(pos).IsReplacableBy(this)) return false;

            world.BlockAccessor.SetBlock(BlockId, pos);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityPlatePile)
            {
                BlockEntityPlatePile pile = (BlockEntityPlatePile)be;
                int q = player.Entity.Controls.CtrlKey ? pile.BulkTakeQuantity : pile.DefaultTakeQuantity;

                pile.inventory[0].Itemstack = (ItemStack)slot.Itemstack.Clone();
                pile.inventory[0].Itemstack.StackSize = Math.Min(slot.StackSize, q);

                if (player.WorldData.CurrentGameMode != EnumGameMode.Creative) slot.TakeOut(q);
                
                pile.MarkDirty(true);
                
                world.PlaySoundAt(new AssetLocation("sounds/block/plate"), pos.X, pos.Y, pos.Z, player, false);
            }

            return true;
        }


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.CanAttachBlockAt(world.BlockAccessor, this, pos.DownCopy(), BlockFacing.UP) && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) < 8))
            {
                world.BlockAccessor.BreakBlock(pos, null);
                //world.PlaySoundAt(new AssetLocation("sounds/block/plate"), pos.X, pos.Y, pos.Z, null, false);
            }
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-platepile-add",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "shift",
                    Itemstacks = new ItemStack[] { new ItemStack(this) },
                    GetMatchingStacks = (wi, bs, es) =>
                    {
                        BlockEntityPlatePile pile = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPlatePile;
                        if (pile != null && !pile.inventory[0].Empty && pile.MaxStackSize > pile.inventory[0].StackSize)
                        {
                            ItemStack stack = pile.inventory[0].Itemstack.Clone();
                            
                            stack.StackSize = 1;
                            return new ItemStack[] { stack };
                        }
                        return  null;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-platepile-remove",
                    MouseButton = EnumMouseButton.Right
                },

                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-platepile-4add",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCodes = new string[] {"ctrl", "shift" },
                    Itemstacks = new ItemStack[] { new ItemStack(this) },
                    GetMatchingStacks = (wi, bs, es) =>
                    {
                        BlockEntityPlatePile pile = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPlatePile;

                        if (pile != null && !pile.inventory[0].Empty && pile.MaxStackSize > pile.inventory[0].StackSize)
                        {
                            ItemStack stack = pile.inventory[0].Itemstack.Clone();
                            
                            stack.StackSize = pile.BulkTakeQuantity;
                            return new ItemStack[] { stack };
                        }
                        return  null;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-platepile-4remove",
                    HotKeyCode = "ctrl",
                    MouseButton = EnumMouseButton.Right
                }
            };
        }

    }
}
