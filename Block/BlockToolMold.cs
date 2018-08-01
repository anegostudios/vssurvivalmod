using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class BlockToolMold : Block
    {
        public override bool OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(blockSel.Face.GetOpposite()));

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);

            if (byPlayer != null && be is BlockEntityToolMold)
            {
                BlockEntityToolMold beim = (BlockEntityToolMold)be;
                return beim.OnPlayerInteract(byPlayer, blockSel.Face, blockSel.HitPosition);
            }

            return false;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return false;

            BlockEntityToolMold be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityToolMold;
            bool handled = false;

            if (be != null)
            {
                handled = be.OnPlayerInteract(byPlayer, blockSel.Face, blockSel.HitPosition);
            }
            

            return handled;
        }


        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
        {
            if (!byPlayer.Entity.Controls.Sneak) return false;

            if (!world.TestPlayerAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            Block block = world.BlockAccessor.GetBlock(blockSel.Position);
            Block belowBlock = world.BlockAccessor.GetBlock(blockSel.Position.DownCopy());

            if (block.IsReplacableBy(this) && belowBlock.SideSolid[BlockFacing.UP.Index])
            {
                DoPlaceBlock(world, blockSel.Position, blockSel.Face, itemstack);
                return true;
            }

            return false;
        }


        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            List<ItemStack> stacks = new List<ItemStack>();

            stacks.Add(new ItemStack(this));

            BlockEntityToolMold bet = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityToolMold;
           
            if (bet != null)
            {
                ItemStack outstack = bet.GetReadyMoldedStack();
                if (outstack != null) {
                    stacks.Add(outstack);
                }
            }


            return stacks.ToArray();
        }

    }
}
