using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public abstract class BlockMPBase : Block, IMechanicalPowerBlock
    {
        public abstract void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face);
        public abstract bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face);



        public virtual void WasPlaced(IWorldAccessor world, BlockPos ownPos, BlockFacing connectedOnFacing)
        {
            if (connectedOnFacing == null) return;
            BEBehaviorMPBase beMechBase = world.BlockAccessor.GetBlockEntity(ownPos)?.GetBehavior<BEBehaviorMPBase>();
            beMechBase?.tryConnect(connectedOnFacing);
        }


        public virtual bool tryConnect(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, BlockFacing face)
        {
            IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos.AddCopy(face)) as IMechanicalPowerBlock;
            if (block != null && block.HasMechPowerConnectorAt(world, pos, face.Opposite))
            {
                block.DidConnectAt(world, pos.AddCopy(face), face.Opposite);
                WasPlaced(world, pos, face);
                return true;
            }

            return false;
        }


        public virtual MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
        {
            IMechanicalPowerDevice be = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>() as IMechanicalPowerDevice;
            return be?.Network;
        }

        internal void ExchangeBlockAt(IWorldAccessor world, BlockPos pos)
        {
            world.BlockAccessor.ExchangeBlock(BlockId, pos);

            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            BEBehaviorMPBase bemp = be?.GetBehavior<BEBehaviorMPBase>();

            if (bemp != null)
            {
                bemp.SetOrientations();
                bemp.Shape = Shape;
                be.MarkDirty();
            }
        }
    }
}
