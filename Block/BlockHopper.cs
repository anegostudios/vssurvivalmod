using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

#nullable disable

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

            if (facing == BlockFacing.UP && entity is EntityItem inWorldItem && world.Side == EnumAppSide.Server)
            {
                // Don't suck up everything instantly
                if (world.Rand.NextDouble() < 0.9) return;

                var tmpPos = pos.Copy();
                api.Event.EnqueueMainThreadTask(() =>
                {
                    // Ensure these steps will occur on the main thread

                    BlockEntity blockEntity = world.BlockAccessor.GetBlockEntity(tmpPos);
                    if (inWorldItem.Alive && blockEntity is BlockEntityItemFlow beItemFlow)
                    {
                        WeightedSlot ws = beItemFlow.inventory.GetBestSuitedSlot(inWorldItem.Slot);

                        if (ws.slot != null) //we have determined there is room for this itemStack in this inventory.
                        {
                            inWorldItem.Slot.TryPutInto(api.World, ws.slot, 1);    // This can invoke CollectibleObject.UpdateAndGetTransitionStates() which can invoke the Room Registry(!!)
                            if (inWorldItem.Slot.StackSize <= 0)
                            {
                                inWorldItem.Itemstack = null;
                                inWorldItem.Alive = false;
                            }
                        }
                    }
                }, "hopperitempickup");
            }
        }

    }
}
