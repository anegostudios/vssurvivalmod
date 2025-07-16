using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#nullable disable

namespace VSSurvivalMod.Systems.ChiselModes
{
    /// <summary>
    /// Extra behavior and data for chisel modes
    /// </summary>
    public abstract class ChiselMode
    {
        /// <summary>
        /// Total brush size of the mode, 1x1x1 is 1, 2x2x2 is 2, etc
        /// </summary>
        public virtual int ChiselSize => 1;

        /// <summary>
        /// Gives back the action needed to draw this mode's icon
        /// </summary>
        /// <param name="capi">Some icons are pulled from the client API so it is provided here.</param>
        /// <returns>An action that will draw the mode's icon wherever needed.</returns>
        public abstract DrawSkillIconDelegate DrawAction(ICoreClientAPI capi);

        /// <summary>
        /// Handles the behavior of the chisel mode when the chisel entity is activated.
        /// By default this handles simple modification but can be overriden for more complicated modes like rotation.
        /// </summary>
        /// <param name="chiselEntity">The entity activated.</param>
        /// <param name="byPlayer">The player using the chisel.</param>
        /// <param name="voxelPos">Where exactly on the block was activated.</param>
        /// <param name="facing">Which side the block was activated from.</param>
        /// <param name="isBreak">Normal or alt activation.</param>
        /// <param name="currentMaterialIndex">The bytes representing the material currently selected.</param>
        /// <returns>Whether this mode has modified the block, requiring a refresh</returns>
        public virtual bool Apply(BlockEntityChisel chiselEntity, IPlayer byPlayer, Vec3i voxelPos, BlockFacing facing, bool isBreak, byte currentMaterialIndex)
        {
            Vec3i addAtPos = voxelPos.Clone().Add(ChiselSize * facing.Normali.X, ChiselSize * facing.Normali.Y, ChiselSize * facing.Normali.Z);

            if (isBreak)
            {
                return chiselEntity.SetVoxel(voxelPos, false, byPlayer, currentMaterialIndex);
            }

            if (addAtPos.X >= 0 && addAtPos.X < 16 && addAtPos.Y >= 0 && addAtPos.Y < 16 && addAtPos.Z >= 0 && addAtPos.Z < 16)
            {
                return chiselEntity.SetVoxel(addAtPos, true, byPlayer, currentMaterialIndex);
            }

            return false;
        }
    }
}
