using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public interface IMechanicalPowerNetworkNode : IMechanicalPowerDevice
    {
        float getTorque(MechanicalNetwork network);
        float getResistance(MechanicalNetwork network);

        // Should return local facing
        BlockFacing getOutputSideForNetworkPropagation();

        void createMechanicalNetwork(MechanicalNetwork forkedFromNetwork, BlockFacing facing);
    }
}
