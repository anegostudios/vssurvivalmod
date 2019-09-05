using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public enum EnumTurnDirection
    {
        Clockwise,
        Counterclockwise
    }
    
    public interface IMechanicalPowerNode
    {
        MechanicalNetwork Network { get; }
        float Angle { get; }

        Block Block { get; }
        BlockPos Position { get; }
        Vec4f LightRgba { get; }

        int[] AxisMapping { get; }
        int[] AxisSign { get; }

        /// <summary>
        /// Amount of torque produced for given speed of the mechanical network it is in. 
        /// Return a positive number if the torque is in clockwise direction when looking north or east, and negative if counter clockwise
        /// </summary>
        /// <returns></returns>
        float GetTorque();

        /// <summary>
        /// If not a power producer but a consumer, return an above zero value here
        /// </summary>
        /// <returns></returns>
        float GetResistance();

        
        EnumTurnDirection GetTurnDirection(BlockFacing forFacing);

        EnumTurnDirection GetBaseTurnDirection();
        void SetBaseTurnDirection(EnumTurnDirection turnDir, BlockFacing fromDir);

        CompositeShape Shape { get; }
    }

    /*
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
    }*/
}
