using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent.Mechanics
{
    public enum EnumRotDirection
    {
        Clockwise = 0,
        Counterclockwise = 1
    }
    
    /// <summary>
    /// A mechanical power network component with axle connections
    /// </summary>
    public interface IMechanicalPowerNode : IMechanicalPowerRenderable
    {
        /// <summary>
        /// If set, then this node is the starting point for network discovery. In principal its fine for any single-connecter node to be starting point but it probably makes most sense if only power producers are
        /// </summary>
        BlockFacing OutFacingForNetworkDiscovery { get; }

        MechanicalNetwork Network { get; }

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

        TurnDirection GetTurnDirection(BlockFacing forFacing);

        TurnDirection GetInTurnDirection();
        void SetInTurnDirection(TurnDirection turnDir);

        bool JoinAndSpreadNetworkToNeighbours(ICoreAPI api, long propagationId, MechanicalNetwork network, TurnDirection turnDir, out Vec3i missingChunkPos);

        MechanicalNetwork CreateJoinAndDiscoverNetwork(BlockFacing powerOutFacing);

        void LeaveNetwork();
    }
}
