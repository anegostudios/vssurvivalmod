using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Vintagestory.GameContent

{
    public class BlockGroundStorage : Block
    {
        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                return beg.GetCollisionBoxes();
            }

            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                return beg.GetSelectionBoxes();
            }

            return base.GetSelectionBoxes(blockAccessor, pos);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);
            if (be is BlockEntityGroundStorage beg) 
            { 
                return beg.OnPlayerInteract(byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override EnumBlockMaterial GetBlockMaterial(IBlockAccessor blockAccessor, BlockPos pos, ItemStack stack = null)
        {
            return base.GetBlockMaterial(blockAccessor, pos, stack);
        }

        public float FillLevel(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity be = blockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                return (int)Math.Ceiling((float)beg.TotalStackSize / beg.Capacity);
            }

            return 1;
        }



        public bool CreateStorage(IWorldAccessor world, BlockSelection blockSel, IPlayer player)
        {
            BlockPos pos = blockSel.Position.AddCopy(blockSel.Face);
            Block belowBlock = world.BlockAccessor.GetBlock(pos.DownCopy());
            if (!belowBlock.CanAttachBlockAt(world.BlockAccessor, this, pos.DownCopy(), BlockFacing.UP) && (belowBlock != this || FillLevel(world.BlockAccessor, pos.DownCopy()) != 1)) return false;


            world.BlockAccessor.SetBlock(BlockId, pos);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                beg.OnPlayerInteract(player, blockSel);
                beg.MarkDirty(true);
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


        public override int GetColorWithoutTint(ICoreClientAPI capi, BlockPos pos)
        {
            BlockEntity be = capi.World.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                ItemSlot slot = beg.Inventory.FirstOrDefault(s => !s.Empty);
                if (slot != null)
                {
                    return slot.Itemstack.Collectible.GetRandomColor(capi, slot.Itemstack);
                }
            }

            return base.GetColorWithoutTint(capi, pos);
        }

        public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing)
        {
            BlockEntity be = capi.World.BlockAccessor.GetBlockEntity(pos);
            if (be is BlockEntityGroundStorage beg)
            {
                ItemSlot slot = beg.Inventory.FirstOrDefault(s => !s.Empty);
                if (slot != null)
                {
                    return slot.Itemstack.Collectible.GetRandomColor(capi, slot.Itemstack);
                }
            }

            return base.GetRandomColor(capi, pos, facing);
        }


        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        {
            return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer);
        }

    }
}
