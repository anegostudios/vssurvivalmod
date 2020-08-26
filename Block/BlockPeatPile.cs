using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent
{
    public class BlockPeatPile : Block
    {
        public Cuboidf[][] CollisionBoxesByFillLevel;

        public BlockPeatPile()
        {
            CollisionBoxesByFillLevel = new Cuboidf[9][];

            for (int i = 0; i <= 8; i++)
            {
                CollisionBoxesByFillLevel[i] = new Cuboidf[] { new Cuboidf(0, 0, 0, 1, i * 0.125f, 1) };
            }
        }

        public int FillLevel(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityPeatPile)
            {
                return (int)Math.Ceiling(((BlockEntityPeatPile)be).OwnStackSize / 4.0);
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
            if (be is BlockEntityPeatPile)
            {
                BlockEntityPeatPile pile = (BlockEntityPeatPile)be;
                return pile.OnPlayerInteract(byPlayer);
            }

            return false;
        }
        


        internal bool Construct(ItemSlot slot, IWorldAccessor world, BlockPos pos, IPlayer player)
        {
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.CanAttachBlockAt(world.BlockAccessor, this, pos.DownCopy(), BlockFacing.UP) && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) != 8)) return false;


            world.BlockAccessor.SetBlock(BlockId, pos);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityPeatPile)
            {
                BlockEntityPeatPile pile = (BlockEntityPeatPile)be;
                if (player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    pile.inventory[0].Itemstack = slot.Itemstack.Clone();
                    pile.inventory[0].Itemstack.StackSize = 1;
                } else
                {
                    pile.inventory[0].Itemstack = slot.TakeOut(player.Entity.Controls.Sprint ? pile.BulkTakeQuantity : pile.DefaultTakeQuantity);
                }
                
                pile.MarkDirty();
                world.BlockAccessor.MarkBlockDirty(pos);
                world.PlaySoundAt(new AssetLocation("sounds/block/dirt"), pos.X, pos.Y, pos.Z, player, false);
            }


            if (CollisionTester.AabbIntersect(
                GetCollisionBoxes(world.BlockAccessor, pos)[0],
                pos.X, pos.Y, pos.Z,
                player.Entity.CollisionBox,
                player.Entity.SidedPos.XYZ
            ))
            {
                player.Entity.SidedPos.Y += GetCollisionBoxes(world.BlockAccessor, pos)[0].Y2;
            }

            (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            return true;
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


        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.CanAttachBlockAt(world.BlockAccessor, this, pos.DownCopy(), BlockFacing.UP) && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) < 8))
            {
                world.BlockAccessor.BreakBlock(pos, null);
            }
        }


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            return base.GetRandomColor(capi, pos, facing);
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityPeatPile)
            {
                BlockEntityPeatPile pile = (BlockEntityPeatPile)be;
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

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return new WorldInteraction[]
            {
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-peatpile-add",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCode = "sneak",
                    Itemstacks = new ItemStack[] { new ItemStack(this) },
                    GetMatchingStacks = (wi, bs, es) =>
                    {
                        BlockEntityPeatPile pile = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPeatPile;
                        if (pile != null && pile.MaxStackSize > pile.inventory[0].StackSize && pile.inventory[0].Itemstack != null)
                        {
                            ItemStack displaystack = pile.inventory[0].Itemstack.Clone();
                            displaystack.StackSize = pile.DefaultTakeQuantity;
                            return new  ItemStack[] { displaystack };
                        }
                        return null;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-peatpile-remove",
                    MouseButton = EnumMouseButton.Right
                },


                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-peatpile-4add",
                    MouseButton = EnumMouseButton.Right,
                    HotKeyCodes = new string[] {"sprint", "sneak" },
                    Itemstacks = new ItemStack[] { new ItemStack(this) },
                    GetMatchingStacks = (wi, bs, es) =>
                    {
                        BlockEntityPeatPile pile = world.BlockAccessor.GetBlockEntity(bs.Position) as BlockEntityPeatPile;
                        if (pile != null && pile.MaxStackSize > pile.inventory[0].StackSize && pile.inventory[0].Itemstack != null)
                        {
                            ItemStack displaystack = pile.inventory[0].Itemstack.Clone();
                            displaystack.StackSize = pile.BulkTakeQuantity;
                            return new  ItemStack[] { displaystack };
                        }
                        return null;
                    }
                },
                new WorldInteraction()
                {
                    ActionLangCode = "blockhelp-peatpile-4remove",
                    HotKeyCode = "sprint",
                    MouseButton = EnumMouseButton.Right
                },


            }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }

    }
}
