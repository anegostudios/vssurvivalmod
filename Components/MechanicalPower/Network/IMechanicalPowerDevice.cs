using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    
    public interface IMechanicalPowerDevice
    {
        MechanicalNetwork Network { get; }
        float Angle { get; }

        Block Block { get; }
        BlockPos Position { get; }
        Vec4f LightRgba { get; }

        int[] AxisMapping { get; }
        int[] AxisSign { get; }
    }

    public interface IMechanicalPowerDeviceOld
    {
        bool hasConnectorAt(BlockFacing localFacing);
        bool isConnectedAt(BlockFacing localFacing);

        void trySetNetwork(int networkId, BlockFacing localFacing);
        MechanicalNetworkOld getNetwork(BlockFacing localFacing);
        BlockFacing getFacing(MechanicalNetworkOld network);

        void propagateNetworkToNeighbours(int propagationId, long networkId, BlockFacing remoteFacing);
        void propagateDirectionToNeightbours(int propagationId, BlockFacing remoteFacing, bool clockwise);

        bool isClockWiseDirection(BlockFacing localFacing);
        void setClockWiseDirection(long networkId, bool clockwise);

        BlockFacing getDirectionFromFacing();

        void onDevicePlaced(IWorldAccessor world, BlockPos pos, BlockFacing facing, BlockFacing ontoside);
        void onDeviceRemoved(IWorldAccessor world, BlockPos pos);



        bool exists();
        BlockPos getPosition();

        MechanicalNetworkOld[] getNetworks();
        void clearNetwork();
    }
}
