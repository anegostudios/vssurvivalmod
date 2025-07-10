using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#nullable disable

namespace VSSurvivalMod.Systems.ChiselModes
{
    public class FlipChiselMode : ChiselMode
    {
        public override DrawSkillIconDelegate DrawAction(ICoreClientAPI capi) => capi.Gui.Icons.Drawrepeat_svg;

        public override bool Apply(BlockEntityChisel chiselEntity, IPlayer byPlayer, Vec3i voxelPos, BlockFacing facing, bool isBreak, byte currentMaterialIndex)
        {
            var facings = Block.SuggestedHVOrientation(
                byPlayer,
                new BlockSelection() { Position = chiselEntity.Pos.Copy(), HitPosition = new Vec3d(voxelPos.X / 16.0, voxelPos.Y / 16.0, voxelPos.Z / 16.0) }
            );

            chiselEntity.FlipVoxels(facings[0]);

            return true;
        }
    }
}
