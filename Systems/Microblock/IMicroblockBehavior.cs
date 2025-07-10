using Vintagestory.API.MathTools;

#nullable disable

namespace Vintagestory.GameContent
{
    public interface IMicroblockBehavior
    {
        void RotateModel(int degrees, EnumAxis? flipAroundAxis);
        void RebuildCuboidList(BoolArray16x16x16 voxels, byte[,,] voxelMaterial);
        void RegenMesh();
    }
}
