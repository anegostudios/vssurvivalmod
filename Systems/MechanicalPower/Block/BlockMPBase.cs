using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public abstract class BlockMPBase : Block, IMechanicalPowerBlock
    {
        public abstract void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face);
        public abstract bool HasConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face);



        public void WasPlaced(IWorldAccessor world, BlockPos ownPos, BlockFacing connectedOnFacing)
        {
            BEBehaviorMPBase beMechBase = world.BlockAccessor.GetBlockEntity(ownPos)?.GetBehavior<BEBehaviorMPBase>();
            beMechBase?.WasPlaced(connectedOnFacing);
        }


        public virtual bool tryConnect(IWorldAccessor world, IPlayer byPlayer, BlockPos pos, BlockFacing face)
        {
            IMechanicalPowerBlock block = world.BlockAccessor.GetBlock(pos.AddCopy(face)) as IMechanicalPowerBlock;
            if (block != null && block.HasConnectorAt(world, pos, face.GetOpposite()))
            {
                block.DidConnectAt(world, pos.AddCopy(face), face.GetOpposite());
                WasPlaced(world, pos, face);
                return true;
            }

            return false;
        }


        public MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
        {
            IMechanicalPowerNode be = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>() as IMechanicalPowerNode;
            return be?.Network;
        }

        internal void ExchangeBlockAt(IWorldAccessor world, BlockPos pos)
        {
            BlockEntity be = world.BlockAccessor.GetBlockEntity(pos);
            BEBehaviorMPBase bemp = be.GetBehavior<BEBehaviorMPBase>();
            bemp.Block = this;
            bemp.SetOrientations();
            bemp.Shape = Shape;

            world.BlockAccessor.ExchangeBlock(BlockId, pos);

            
        }
    }
}
