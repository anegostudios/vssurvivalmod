using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

#nullable disable

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
    public interface IMechanicalPowerDevice : IMechanicalPowerRenderable, IMechanicalPowerNode
    {
        /// <summary>
        /// If set, then this node is the starting point for network discovery. In principal its fine for any single-connecter node to be starting point but it probably makes most sense if only power producers are
        /// </summary>
        BlockFacing OutFacingForNetworkDiscovery { get; }

        MechanicalNetwork Network { get; }

        /// <summary>
        /// The positive propagation direction - equivalent to former InTurnDir.Facing
        /// </summary>
        BlockFacing GetPropagationDirection();

        /// <summary>
        /// The propagation direction from the input side for making connections - normally the same as GetPropagationDirection() except for Angled Gears
        /// </summary>
        BlockFacing GetPropagationDirectionInput();
        bool IsPropagationDirection(BlockPos fromPos, BlockFacing test);
        void SetPropagationDirection(MechPowerPath turnDir);

        bool JoinAndSpreadNetworkToNeighbours(ICoreAPI api, MechanicalNetwork network, MechPowerPath turnDir, out Vec3i missingChunkPos);

        MechanicalNetwork CreateJoinAndDiscoverNetwork(BlockFacing powerOutFacing);

        /// <summary>
        /// True for reverse rotation for rendering purposes (depends on the inTurnDir facing for 2-way blocks such as axles)
        /// </summary>
        bool IsRotationReversed();
        /// <summary>
        /// True if the network inTurnDir is coming into this block instead of going out (may or may not be paired with counter-clockwise rotation)
        /// </summary>
        bool IsInvertedNetworkFor(BlockPos pos);

        /// <summary>
        /// Only implemented on blocks which can support JoinPoints, such as Large Gear and Angled Gear
        /// </summary>
        void DestroyJoin(BlockPos pos);

        /// <summary>
        /// A side-sensitive version of GearedRatio property
        /// </summary>
        float GetGearedRatio(BlockFacing toFacing);

        /// <summary>
        /// Return null if this device does not change the direction
        /// </summary>
        /// <param name="toFacing"></param>
        /// <returns></returns>
        BlockFacing GetPropagatingTurnDir(BlockFacing toFacing);
    }
}
