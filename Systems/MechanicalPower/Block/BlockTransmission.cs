using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public class BlockTransmission : BlockMPBase
    {
        public bool IsOrientedTo(BlockFacing facing)
        {
            string dirs = LastCodePart();

            return dirs[0] == facing.Code[0] || (dirs.Length > 1 && dirs[1] == facing.Code[0]);
        }

        public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {
            return IsOrientedTo(face);
        }

        public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
        {

        }

        public override MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
        {
            BEBehaviorMPTransmission be = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPTransmission>();
            if (be == null || !be.engaged) return null;
            return be.Network;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            BEBehaviorMPTransmission be = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPTransmission>();
            be?.CheckEngaged(world.BlockAccessor, true);
        }
    }
}
