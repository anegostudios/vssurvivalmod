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
        MechanicalPowerMod mechPower;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            mechPower = api.ModLoader.GetModSystem<MechanicalPowerMod>();
        }

        public abstract void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face);
        public abstract bool HasConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face);

        public void WasPlaced(IWorldAccessor world, BlockPos ownPos, BlockFacing connectedOnFacing = null, IMechanicalPowerBlock connectedToBlock = null)
        {
            BEMPBase be = (world.BlockAccessor.GetBlockEntity(ownPos) as BEMPBase);

            MechanicalNetwork network;

            if (connectedOnFacing == null)
            {
                network = mechPower.CreateNetwork();
            } else
            {
                network = connectedToBlock.GetNetwork(world, ownPos.AddCopy(connectedOnFacing));                
            }

            be?.SetNetwork(network);
        }




        public MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
        {
            IMechanicalPowerDevice be = world.BlockAccessor.GetBlockEntity(pos) as IMechanicalPowerDevice;
            return be?.Network;
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            (world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEMPBase)?.OnBlockInteractStart(world, byPlayer);

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
    }
}
