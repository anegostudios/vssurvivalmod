using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockIngotPile : Block
    {
        public Cuboidf[][] CollisionBoxesByFillLevel;

        public BlockIngotPile()
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
            if (be is BlockEntityIngotPile)
            {
                return (int)Math.Ceiling(((BlockEntityIngotPile)be).OwnStackSize / 8.0);
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
            if (be is BlockEntityIngotPile)
            {
                BlockEntityIngotPile pile = (BlockEntityIngotPile)be;
                return pile.OnPlayerInteract(byPlayer);
            }

            return false;
        }
        


        internal bool Construct(IItemSlot slot, IWorldAccessor world, BlockPos pos, IPlayer player)
        {
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.SideSolid[BlockFacing.UP.Index] && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) != 8)) return false;


            world.BlockAccessor.SetBlock(BlockId, pos);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityIngotPile)
            {
                BlockEntityIngotPile pile = (BlockEntityIngotPile)be;
                if (player.WorldData.CurrentGameMode == EnumGameMode.Creative)
                {
                    pile.inventory[0].Itemstack = slot.Itemstack.Clone();
                    pile.inventory[0].Itemstack.StackSize = 1;
                } else
                {
                    pile.inventory[0].Itemstack = slot.TakeOut(1);
                }
                
                pile.MarkDirty();
                world.BlockAccessor.MarkBlockDirty(pos);
                world.PlaySoundAt(new AssetLocation("sounds/block/ingot"), pos.X, pos.Y, pos.Z, player, false);
            }


            if (CollisionTester.AabbIntersect(
                GetCollisionBoxes(world.BlockAccessor, pos)[0],
                pos.X, pos.Y, pos.Z,
                player.Entity.CollisionBox,
                player.Entity.LocalPos.XYZ
            ))
            {
                player.Entity.LocalPos.Y += GetCollisionBoxes(world.BlockAccessor, pos)[0].Y2;
            }


            return true;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
        {
            // Handled by BlockEntityItemPile
            return new ItemStack[0];
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


        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            BlockEntityIngotPile be = capi.World.BlockAccessor.GetBlockEntity(pos) as BlockEntityIngotPile;
            if (be == null) return base.GetRandomColor(capi, pos, facing);
            string metalType = be.MetalType;
            if (metalType == null) return base.GetRandomColor(capi, pos, facing);

            return capi.BlockTextureAtlas.GetRandomPixel(Textures[be.MetalType].Baked.TextureSubId);
        }


        public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityIngotPile)
            {
                BlockEntityIngotPile pile = (BlockEntityIngotPile)be;
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
    }
}
