using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent.Mechanics
{
    public interface IMechanicalPowerBlock
    {
        MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos);
        bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face);
        void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face);
    }

}
