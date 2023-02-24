using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public interface IMicroblockBehavior
    {
        void RotateModel(int degrees, EnumAxis? flipAroundAxis);
        void RebuildCuboidList(bool[,,] voxels, byte[,,] voxelMaterial);
        void RegenMesh();
    }
}
