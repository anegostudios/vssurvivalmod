using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public enum MechDeviceType
    {
        Gearbox = 0,
        Axle = 1,
        Windmillrotor = 2
    }


    public interface IMechanicalPowerDeviceVS
    {
        BlockPos Position { get; }
        Vec4f LightRgba { get; }
        MechDeviceType Type { get; }
    }

    public interface IMechanicalPowerDevice
    {
        bool hasConnectorAt(BlockFacing localFacing);
        bool isConnectedAt(BlockFacing localFacing);

        void trySetNetwork(int networkId, BlockFacing localFacing);
        MechanicalNetwork getNetwork(BlockFacing localFacing);
        BlockFacing getFacing(MechanicalNetwork network);

        void propagateNetworkToNeighbours(int propagationId, long networkId, BlockFacing remoteFacing);
        void propagateDirectionToNeightbours(int propagationId, BlockFacing remoteFacing, bool clockwise);

        bool isClockWiseDirection(BlockFacing localFacing);
        void setClockWiseDirection(long networkId, bool clockwise);

        BlockFacing getDirectionFromFacing();

        void onDevicePlaced(IWorldAccessor world, BlockPos pos, BlockFacing facing, BlockFacing ontoside);
        void onDeviceRemoved(IWorldAccessor world, BlockPos pos);



        bool exists();
        BlockPos getPosition();

        MechanicalNetwork[] getNetworks();
        void clearNetwork();
    }
}
