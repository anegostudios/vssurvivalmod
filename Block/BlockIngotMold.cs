
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
    public class BlockIngotMold : Block
    {
        public override bool OnHeldInteractStart(IItemSlot itemslot, IEntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            if (blockSel == null) return false;

            BlockEntity be = byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position.AddCopy(blockSel.Face.GetOpposite()));

            IPlayer byPlayer = null;
            if (byEntity is IEntityPlayer) byPlayer = byEntity.World.PlayerByUid(((IEntityPlayer)byEntity).PlayerUID);

            if (byPlayer != null && be is BlockEntityIngotMold)
            {
                BlockEntityIngotMold beim = (BlockEntityIngotMold)be;
                return beim.OnPlayerInteract(byPlayer, blockSel.Face, blockSel.HitPosition);
            }

            return false;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (blockSel == null) return false;

            BlockEntity be = world.BlockAccessor.GetBlockEntity(blockSel.Position);

            if (be is BlockEntityIngotMold)
            {
                BlockEntityIngotMold beim = (BlockEntityIngotMold)be;
                return beim.OnPlayerInteract(byPlayer, blockSel.Face, blockSel.HitPosition);
            }

            return false;
        }

        
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel)
        {
            if (!world.TestPlayerAccessBlock(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                return false;
            }

            if (!byPlayer.Entity.Controls.Sneak) return false;

            if (IsSuitablePosition(world, blockSel.Position) && world.BlockAccessor.GetBlock(blockSel.Position.DownCopy()).SideSolid[BlockFacing.UP.Index])
            {
                DoPlaceBlock(world, blockSel.Position, blockSel.Face, itemstack);
                return true;
            }

            return false;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            List<ItemStack> stacks = new List<ItemStack>();

            BlockEntityIngotMold bei = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityIngotMold;
            if (bei != null)
            {
                stacks.Add(new ItemStack(this, bei.quantityMolds));

                ItemStack stackl = bei.GetLeftContents();
                if (stackl != null)
                {
                    stacks.Add(stackl);
                }
                ItemStack stackr = bei.GetRightContents();
                if (stackr != null)
                {
                    stacks.Add(stackr);
                }
            } else
            {
                stacks.Add(new ItemStack(this, 1));
            }

            return stacks.ToArray();
        }
        

    }
}
