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
    public interface IBlockItemFlow
    {
        bool HasItemFlowConnectorAt(BlockFacing facing);
    }

    public class BlockHopper : Block, IBlockItemFlow
    {
        public bool HasItemFlowConnectorAt(BlockFacing facing)
        {
            return facing == BlockFacing.DOWN;
        }

        //On contact with entity, if the entity is on the top and the entity is an item entity, pull the entity into the hopper's inventory.

        public override void OnEntityCollide(IWorldAccessor world, Entity entity, BlockPos pos, BlockFacing facing, Vec3d collideSpeed, bool isImpact)
        {
            base.OnEntityCollide(world, entity, pos, facing, collideSpeed, isImpact);

            // Don't suck up everything instantly
            if (world.Rand.NextDouble() < 0.9) return;

            if (facing == BlockFacing.UP && entity is EntityItem)
            {
                EntityItem inWorldItem = (EntityItem)entity;
                BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(pos);
                if (blockEntity is BlockEntityItemFlow)
                { 
                    BlockEntityItemFlow beItemFlow = (BlockEntityItemFlow)blockEntity;

                    WeightedSlot ws = beItemFlow.inventory.GetBestSuitedSlot(inWorldItem.Slot);

                    if (ws.slot != null) //we have determined there is room for this itemStack in this inventory.
                    {
                        inWorldItem.Slot.TryPutInto(api.World, ws.slot, 1);
                        if (inWorldItem.Slot.StackSize <= 0)
                        {
                            inWorldItem.Itemstack = null;
                        }
                    }
                }
            }
        }

    }
}
