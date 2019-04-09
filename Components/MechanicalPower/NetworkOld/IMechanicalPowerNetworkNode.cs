using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public interface IMechanicalPowerNetworkNode : IMechanicalPowerDeviceOld
    {
        float getTorque(MechanicalNetworkOld network);
        float getResistance(MechanicalNetworkOld network);

        // Should return local facing
        BlockFacing getOutputSideForNetworkPropagation();

        void createMechanicalNetwork(MechanicalNetworkOld forkedFromNetwork, BlockFacing facing);
    }
}
